using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gezinti.Application.DTOs.Places;
using Gezinti.Application.Interfaces;

namespace Gezinti.Infrastructure.Providers.OpenStreetMap;

public class OsmPlacesProvider : IPlacesProvider
{
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

        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("data", query)
        ]);

        content.Headers.ContentType =
            new MediaTypeHeaderValue("application/x-www-form-urlencoded");

        using var response = await _httpClient.PostAsync(
            "https://maps.mail.ru/osm/tools/overpass/api/interpreter",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<OverpassResponse>(
                cancellationToken);

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

        return $"""
        [out:json][timeout:20];
        (
          node{categoryFilter}(around:{radius},{latitude},{longitude});
          way{categoryFilter}(around:{radius},{latitude},{longitude});
        );
        out center tags;
        """;
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