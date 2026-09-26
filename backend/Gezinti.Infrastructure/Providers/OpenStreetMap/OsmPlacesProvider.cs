using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gezinti.Application.DTOs.Places;
using Gezinti.Application.Interfaces;
using System.Globalization;
using System.Text.Json;

namespace Gezinti.Infrastructure.Providers.OpenStreetMap;

public class OsmPlacesProvider : IPlacesProvider
{
    private static readonly string[] OverpassEndpoints =
    [
        "https://overpass.private.coffee/api/interpreter",
        "https://overpass-api.de/api/interpreter",
        "https://maps.mail.ru/osm/tools/overpass/api/interpreter",
    ];

    private readonly HttpClient _httpClient;

    public OsmPlacesProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;

        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Gezinti/1.0 (development)");

        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<List<ExternalPlaceDto>> GetNearbyAsync(
    double latitude,
    double longitude,
    double radius,
    string? category = null,
    CancellationToken cancellationToken = default)
    {
        if (radius <= 0)
        {
            throw new ArgumentException("Radius 0'dan büyük olmalı.");
        }

        radius = Math.Min(radius, 5000);

        var query = BuildQuery(
            latitude,
            longitude,
            radius,
            category);

        string? responseBody = null;

        for (var endpointIndex = 0; endpointIndex < OverpassEndpoints.Length; endpointIndex++)
        {
            using var content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("data", query)
            ]);

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                OverpassEndpoints[endpointIndex])
            {
                Content = content
            };

            request.Headers.UserAgent.Clear();
            request.Headers.UserAgent.ParseAdd(
                "Gezinti/1.0 (+https://github.com/EmircanBeyan/gezinti)");

            request.Headers.Accept.Clear();
            request.Headers.Accept.ParseAdd("application/json");

            using var endpointTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            endpointTimeout.CancelAfter(TimeSpan.FromSeconds(8));

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    endpointTimeout.Token);
            }
            catch (OperationCanceledException) when (
                !cancellationToken.IsCancellationRequested &&
                endpointIndex < OverpassEndpoints.Length - 1)
            {
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    break;
                }

                // Keep 429 responses on hold instead of immediately retrying another public server.
                if ((int)response.StatusCode >= 500 && endpointIndex < OverpassEndpoints.Length - 1)
                    continue;

                throw new HttpRequestException(
                    $"Overpass API hata döndürdü. Status: {(int)response.StatusCode} {response.ReasonPhrase}.",
                    null,
                    response.StatusCode);
            }
        }

        if (responseBody is null)
        {
            throw new HttpRequestException("Hiçbir Overpass sunucusundan yanıt alınamadı.");
        }

        var result =
            JsonSerializer.Deserialize<OverpassResponse>(
                responseBody,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (result?.Elements is null)
        {
            return [];
        }

        return result.Elements
            .Select(MapToDto)
            .Where(x => x is not null)
            .Cast<ExternalPlaceDto>()
            .Take(50)
            .ToList();
    }

    private static string BuildQuery(
    double latitude,
    double longitude,
    double radius,
    string? category)
    {
        var categoryFilter = BuildCategoryFilter(category);

        return string.Create(CultureInfo.InvariantCulture, $"""
        [out:json][timeout:20];

        nwr{categoryFilter}(around:{radius},{latitude},{longitude});

        out center tags 50;
        """);
    }
    private static string BuildCategoryFilter(string? category)
    {
        return category?.Trim().ToLowerInvariant() switch
        {
            "cafe" =>
                "[amenity=cafe]",

            "restaurant" =>
                "[amenity=restaurant]",

            "bar" =>
                "[amenity=bar]",

            "pub" =>
                "[amenity=pub]",

            "museum" =>
                "[tourism=museum]",

            "park" =>
                "[leisure=park]",

            "pharmacy" =>
                "[amenity=pharmacy]",

            "bank" =>
                "[amenity=bank]",

            _ =>
                "[name]"
        };
    }

    private static ExternalPlaceDto? MapToDto(
        OverpassElement element)
    {
        if (element.Tags is null)
        {
            return null;
        }

        var latitude =
            element.Latitude ?? element.Center?.Latitude;

        var longitude =
            element.Longitude ?? element.Center?.Longitude;

        if (latitude is null || longitude is null)
        {
            return null;
        }

        var name =
            element.Tags.GetValueOrDefault("name");

        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var category =
            element.Tags.GetValueOrDefault("amenity")
            ?? element.Tags.GetValueOrDefault("tourism")
            ?? element.Tags.GetValueOrDefault("leisure")
            ?? element.Tags.GetValueOrDefault("shop")
            ?? string.Empty;

        var address = BuildAddress(element.Tags);

        var osmId = $"{element.Type}/{element.Id}";

        return new ExternalPlaceDto
        {
            Name = name,
            Address = address,
            Category = category,
            Latitude = latitude.Value,
            Longitude = longitude.Value,
            Phone = element.Tags.GetValueOrDefault("phone"),
            Website = element.Tags.GetValueOrDefault("website"),
            Provider = "OpenStreetMap",
            ExternalId = osmId
        };
    }

    private static string BuildAddress(
        Dictionary<string, string> tags)
    {
        var street =
            tags.GetValueOrDefault("addr:street");

        var houseNumber =
            tags.GetValueOrDefault("addr:housenumber");

        var city =
            tags.GetValueOrDefault("addr:city");

        var parts = new[]
        {
            street,
            houseNumber,
            city
        };

        return string.Join(
            ", ",
            parts.Where(x =>
                !string.IsNullOrWhiteSpace(x)));
    }
}
