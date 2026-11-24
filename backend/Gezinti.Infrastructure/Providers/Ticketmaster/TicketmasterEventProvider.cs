using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Gezinti.Application.DTOs.Events;
using Gezinti.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Gezinti.Infrastructure.Providers.Ticketmaster;

public sealed class TicketmasterEventProvider : IEventProvider
{
    private const string GeohashAlphabet = "0123456789bcdefghjkmnpqrstuvwxyz";
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public TicketmasterEventProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Ticketmaster:ApiKey"];
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<List<NearbyEventDto>> GetUpcomingNearbyAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Ticketmaster API anahtarı yapılandırılmamış.");

        var now = DateTimeOffset.UtcNow;
        var parameters = new Dictionary<string, string>
        {
            ["apikey"] = _apiKey!,
            ["geoPoint"] = EncodeGeohash(latitude, longitude, 8),
            ["radius"] = Math.Clamp(radiusMeters / 1000, 1, 100).ToString("0.##", CultureInfo.InvariantCulture),
            ["unit"] = "km",
            ["countryCode"] = "TR",
            ["classificationName"] = "music",
            ["startDateTime"] = now.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["endDateTime"] = now.AddDays(60).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["sort"] = "date,asc",
            ["size"] = "50",
            ["locale"] = "tr-TR"
        };

        var url = "events.json?" + string.Join("&", parameters.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Ticketmaster Discovery API hata döndürdü. Status: {(int)response.StatusCode} {response.ReasonPhrase}.",
                null,
                response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<TicketmasterResponse>(cancellationToken);
        return payload?.Embedded?.Events?
            .Select(eventItem => ToNearbyEvent(eventItem, latitude, longitude))
            .Where(eventItem => eventItem is not null)
            .Cast<NearbyEventDto>()
            .Where(eventItem => eventItem.DistanceMeters <= radiusMeters)
            .OrderBy(eventItem => eventItem.StartsAt)
            .Take(50)
            .ToList() ?? [];
    }

    private static NearbyEventDto? ToNearbyEvent(
        TicketmasterEvent eventItem,
        double latitude,
        double longitude)
    {
        var venue = eventItem.Embedded?.Venues?.FirstOrDefault();
        if (venue?.Location is null ||
            !double.TryParse(venue.Location.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var eventLatitude) ||
            !double.TryParse(venue.Location.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var eventLongitude))
            return null;

        var startValue = eventItem.Dates?.Start?.DateTime;
        if (!DateTimeOffset.TryParse(startValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var startsAt) ||
            startsAt <= DateTimeOffset.UtcNow)
            return null;

        var addressParts = new[]
        {
            venue.Address?.Line1,
            venue.City?.Name,
            venue.State?.Name
        }.Where(part => !string.IsNullOrWhiteSpace(part));

        var image = eventItem.Images?
            .OrderByDescending(item => string.Equals(item.Ratio, "16_9", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault()?.Url;

        return new NearbyEventDto
        {
            Id = eventItem.Id ?? string.Empty,
            Name = eventItem.Name ?? "Etkinlik",
            StartsAt = startsAt,
            VenueName = venue.Name ?? "Mekân bilgisi yok",
            Address = string.Join(", ", addressParts),
            Latitude = eventLatitude,
            Longitude = eventLongitude,
            DistanceMeters = DistanceMeters(latitude, longitude, eventLatitude, eventLongitude),
            TicketUrl = eventItem.Url ?? string.Empty,
            ImageUrl = image
        };
    }

    private static double DistanceMeters(double latitude, double longitude, double otherLatitude, double otherLongitude)
    {
        const double earthRadiusMeters = 6_371_000;
        var latitudeDelta = DegreesToRadians(otherLatitude - latitude);
        var longitudeDelta = DegreesToRadians(otherLongitude - longitude);
        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2) +
                        Math.Cos(DegreesToRadians(latitude)) * Math.Cos(DegreesToRadians(otherLatitude)) *
                        Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        return earthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private static string EncodeGeohash(double latitude, double longitude, int precision)
    {
        var latitudeRange = new[] { -90d, 90d };
        var longitudeRange = new[] { -180d, 180d };
        var isLongitude = true;
        var bit = 0;
        var character = 0;
        var hash = new System.Text.StringBuilder(precision);

        while (hash.Length < precision)
        {
            var range = isLongitude ? longitudeRange : latitudeRange;
            var coordinate = isLongitude ? longitude : latitude;
            var middle = (range[0] + range[1]) / 2;
            if (coordinate >= middle)
            {
                character |= 1 << (4 - bit);
                range[0] = middle;
            }
            else
            {
                range[1] = middle;
            }

            isLongitude = !isLongitude;
            if (bit < 4)
            {
                bit++;
            }
            else
            {
                hash.Append(GeohashAlphabet[character]);
                bit = 0;
                character = 0;
            }
        }

        return hash.ToString();
    }

    private sealed class TicketmasterResponse
    {
        [JsonPropertyName("_embedded")]
        public TicketmasterEmbedded? Embedded { get; set; }
    }

    private sealed class TicketmasterEmbedded
    {
        [JsonPropertyName("events")]
        public List<TicketmasterEvent> Events { get; set; } = [];
    }

    private sealed class TicketmasterEvent
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("images")]
        public List<TicketmasterImage>? Images { get; set; }

        [JsonPropertyName("dates")]
        public TicketmasterDates? Dates { get; set; }

        [JsonPropertyName("_embedded")]
        public TicketmasterEmbeddedVenues? Embedded { get; set; }
    }

    private sealed class TicketmasterImage
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("ratio")]
        public string? Ratio { get; set; }
    }

    private sealed class TicketmasterDates
    {
        [JsonPropertyName("start")]
        public TicketmasterStart? Start { get; set; }
    }

    private sealed class TicketmasterStart
    {
        [JsonPropertyName("dateTime")]
        public string? DateTime { get; set; }
    }

    private sealed class TicketmasterEmbeddedVenues
    {
        [JsonPropertyName("venues")]
        public List<TicketmasterVenue>? Venues { get; set; }
    }

    private sealed class TicketmasterVenue
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("address")]
        public TicketmasterAddress? Address { get; set; }

        [JsonPropertyName("city")]
        public TicketmasterNamedPlace? City { get; set; }

        [JsonPropertyName("state")]
        public TicketmasterNamedPlace? State { get; set; }

        [JsonPropertyName("location")]
        public TicketmasterCoordinates? Location { get; set; }
    }

    private sealed class TicketmasterAddress
    {
        [JsonPropertyName("line1")]
        public string? Line1 { get; set; }
    }

    private sealed class TicketmasterNamedPlace
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class TicketmasterCoordinates
    {
        [JsonPropertyName("latitude")]
        public string? Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public string? Longitude { get; set; }
    }
}
