using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Gezinti.Application.DTOs.Places;
using Gezinti.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Gezinti.Infrastructure.Providers.TomTom;

public sealed class TomTomPlacesProvider : IPlacesProvider
{
    private static readonly Dictionary<string, string> PoiTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cafe"] = "cafe",
        ["restaurant"] = "restaurant",
        ["bar"] = "bar",
        ["pub"] = "pub",
        ["museum"] = "museum",
        ["park"] = "park_and_recreation_area",
        ["pharmacy"] = "pharmacy",
        ["bank"] = "bank",
    };

    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public TomTomPlacesProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["TomTom:ApiKey"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<List<ExternalPlaceDto>> GetNearbyAsync(
        double latitude,
        double longitude,
        double radius,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("TomTom API anahtarı yapılandırılmamış.");

        var filters = new Dictionary<string, object>
        {
            ["types"] = new[] { "poi" },
            ["countryCodesIso2"] = new[] { "TR" },
            ["geometry"] = new
            {
                type = "circle",
                center = new { type = "point", coordinates = new[] { longitude, latitude } },
                radiusInMeters = Math.Min(radius, 5000)
            }
        };

        if (!string.IsNullOrWhiteSpace(category) && PoiTypes.TryGetValue(category, out var poiType))
            filters["poiTypes"] = new[] { poiType };

        var requestBody = new Dictionary<string, object>
        {
            ["origin"] = new { type = "point", coordinates = new[] { longitude, latitude } },
            ["maxResults"] = 50,
            ["filters"] = filters
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "places/discover")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("TomTom-Api-Key", _apiKey);
        request.Headers.Add("TomTom-Api-Version", "3");
        request.Headers.Add("Attributes", "results(id,type,title,position,distanceInMeters,address,poi(poiTypes))");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.AcceptLanguage.ParseAdd("tr-TR");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"TomTom Places API hata döndürdü. Status: {(int)response.StatusCode} {response.ReasonPhrase}.",
                null,
                response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<TomTomResponse>(cancellationToken);
        return payload?.Results?
            .Where(result => string.Equals(result.Type, "poi", StringComparison.OrdinalIgnoreCase))
            .Where(result => result.Position?.Coordinates is { Length: >= 2 })
            .Where(result => !string.IsNullOrWhiteSpace(result.Title))
            .Select(result => new ExternalPlaceDto
            {
                Name = result.Title!,
                Address = result.Address?.FreeformAddress
                    ?? string.Join(", ", result.Subtitles ?? []),
                Category = category ?? result.Poi?.PoiTypes?.FirstOrDefault()?.Id ?? string.Empty,
                Latitude = result.Position!.Coordinates[1],
                Longitude = result.Position.Coordinates[0],
                Provider = "TomTom",
                ExternalId = result.Id
            })
            .DistinctBy(place => place.ExternalId)
            .Take(50)
            .ToList() ?? [];
    }

    private sealed class TomTomResponse
    {
        [JsonPropertyName("results")]
        public List<TomTomPlace> Results { get; set; } = [];
    }

    private sealed class TomTomPlace
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("subtitles")]
        public List<string>? Subtitles { get; set; }

        [JsonPropertyName("position")]
        public TomTomPosition? Position { get; set; }

        [JsonPropertyName("address")]
        public TomTomAddress? Address { get; set; }

        [JsonPropertyName("poi")]
        public TomTomPoi? Poi { get; set; }
    }

    private sealed class TomTomPosition
    {
        [JsonPropertyName("coordinates")]
        public double[] Coordinates { get; set; } = [];
    }

    private sealed class TomTomAddress
    {
        [JsonPropertyName("freeformAddress")]
        public string? FreeformAddress { get; set; }
    }

    private sealed class TomTomPoi
    {
        [JsonPropertyName("poiTypes")]
        public List<TomTomPoiType>? PoiTypes { get; set; }
    }

    private sealed class TomTomPoiType
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
