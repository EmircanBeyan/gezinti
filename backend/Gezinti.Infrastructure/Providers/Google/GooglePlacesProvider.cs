using System.Net.Http.Json;
using Gezinti.Application.DTOs.Places;
using Gezinti.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Gezinti.Infrastructure.Providers.Google;

public class GooglePlacesProvider : IPlacesProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GooglePlacesProvider(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<List<ExternalPlaceDto>> GetNearbyAsync(
        double latitude,
        double longitude,
        double radius,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["GooglePlaces:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "Google Places API key bulunamadı.");
        }

        var requestBody = new
        {
            includedTypes = string.IsNullOrWhiteSpace(category)
                ? null
                : new[] { category },

            maxResultCount = 20,

            locationRestriction = new
            {
                circle = new
                {
                    center = new
                    {
                        latitude,
                        longitude
                    },
                    radius
                }
            },

            rankPreference = "DISTANCE"
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://places.googleapis.com/v1/places:searchNearby");

        request.Headers.Add("X-Goog-Api-Key", apiKey);

        request.Headers.Add(
            "X-Goog-FieldMask",
            "places.id,places.displayName,places.formattedAddress,places.location,places.types");

        request.Content = JsonContent.Create(requestBody);

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<GoogleNearbySearchResponse>(
                cancellationToken);

        if (result?.Places is null)
        {
            return [];
        }

        return result.Places
            .Select(place => new ExternalPlaceDto
            {
                Name = place.DisplayName?.Text ?? string.Empty,
                Address = place.FormattedAddress ?? string.Empty,
                Category = place.Types?.FirstOrDefault() ?? string.Empty,
                Latitude = place.Location?.Latitude ?? 0,
                Longitude = place.Location?.Longitude ?? 0,
                Provider = "Google",
                ExternalId = place.Id
            })
            .ToList();
    }
}