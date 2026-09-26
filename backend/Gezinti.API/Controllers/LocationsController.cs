using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Gezinti.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocationsController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, LocationSearchResult[]> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim RequestGate = new(1, 1);
    private static readonly HashSet<string> StreetLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "sokak", "sokağı", "sok", "sk", "street", "cadde", "caddesi", "cd", "bulvar", "bulvarı", "blv"
    };
    private static readonly HashSet<string> AdministrativeLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        "mahalle", "mahallesi", "mah", "district", "ilçe", "ilcesi"
    };
    private static DateTime _lastRequestAt = DateTime.MinValue;
    private readonly HttpClient _httpClient;

    public LocationsController(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("Nominatim");
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        var query = q?.Trim();
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return BadRequest("En az iki karakterlik bir şehir veya semt adı yaz.");

        if (Cache.TryGetValue(query, out var cached))
            return Ok(KeepRequestedStreet(query, cached));

        await RequestGate.WaitAsync(cancellationToken);
        try
        {
            if (Cache.TryGetValue(query, out cached))
                return Ok(KeepRequestedStreet(query, cached));

            var results = Array.Empty<LocationSearchResult>();
            foreach (var candidate in BuildQueryCandidates(query))
            {
                if (Cache.TryGetValue(candidate, out var candidateCache))
                {
                    results = KeepRequestedStreet(query, candidateCache);
                    if (results.Length > 0)
                        break;
                    continue;
                }

                var waitForRateLimit = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - _lastRequestAt);
                if (waitForRateLimit > TimeSpan.Zero)
                    await Task.Delay(waitForRateLimit, cancellationToken);

                var url = "search?" + string.Join("&", new Dictionary<string, string>
                {
                    ["q"] = candidate + ", Türkiye",
                    ["format"] = "jsonv2",
                    ["limit"] = "5",
                    ["addressdetails"] = "0",
                    ["accept-language"] = "tr",
                    ["countrycodes"] = "tr",
                    // Location searches should resolve to streets, neighborhoods and
                    // administrative areas, not a similarly named shop or office.
                    ["layer"] = "address",
                }.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

                _lastRequestAt = DateTime.UtcNow;
                var externalResults = await _httpClient.GetFromJsonAsync<List<NominatimResult>>(url, cancellationToken);
                results = externalResults?
                    .Select(MapLocation)
                    .Where(result => result is not null)
                    .Cast<LocationSearchResult>()
                    .Where(result => !ContainsStreetName(query) || MatchesRequestedStreet(query, result.Name))
                    .ToArray() ?? [];
                Cache[candidate] = results;

                if (results.Length > 0)
                    break;
            }

            Cache[query] = results;
            return Ok(results);
        }
        catch (HttpRequestException)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Konum araması şu anda kullanılamıyor.",
                detail: "Biraz sonra yeniden dene.");
        }
        finally
        {
            RequestGate.Release();
        }
    }

    private static IEnumerable<string> BuildQueryCandidates(string query)
    {
        var tokens = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.Trim(',', '.', ';', ':'))
            .ToList();

        var candidates = new List<string>();
        for (var index = 0; index < tokens.Count; index++)
        {
            if (!StreetLabels.Contains(tokens[index]) || index == 0)
                continue;

            var street = string.Join(' ', tokens.Skip(index - 1).Take(2));
            var area = tokens.Take(index - 1).ToList();
            var neighborhoodIndex = area.FindLastIndex(token => AdministrativeLabels.Contains(token));
            var addressParts = new List<string> { street };

            if (neighborhoodIndex > 0)
            {
                addressParts.Add(ExpandAdministrativeLabels(string.Join(' ', area.Skip(neighborhoodIndex - 1).Take(2))));
                area = area.Take(neighborhoodIndex - 1).ToList();
            }

            area.Reverse();
            addressParts.AddRange(area);

            candidates.Add(string.Join(", ", addressParts));
            break;
        }

        candidates.Add(query);
        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            yield return candidate;

        // If a street was requested, never silently replace it with a nearby
        // neighborhood center. Nearby-place search must use the street result.
        if (tokens.Any(StreetLabels.Contains))
            yield break;

        tokens.RemoveAll(token => AdministrativeLabels.Contains(token));
        var broaderQuery = string.Join(' ', tokens);
        if (broaderQuery.Length >= 2 && !string.Equals(query, broaderQuery, StringComparison.OrdinalIgnoreCase))
            yield return broaderQuery;
    }

    private static string ExpandAdministrativeLabels(string value) => string.Join(' ', value
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(token => token.Equals("mah", StringComparison.OrdinalIgnoreCase) ? "Mahallesi" : token));

    private static bool ContainsStreetName(string query) => query
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Any(StreetLabels.Contains);

    private static LocationSearchResult[] KeepRequestedStreet(string query, LocationSearchResult[] results) =>
        ContainsStreetName(query) ? results.Where(result => MatchesRequestedStreet(query, result.Name)).ToArray() : results;

    private static bool MatchesRequestedStreet(string query, string resultName)
    {
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var streetLabelIndex = Array.FindIndex(tokens, StreetLabels.Contains);
        if (streetLabelIndex <= 0)
            return false;

        var requestedStreetName = tokens[streetLabelIndex - 1].Trim(',', '.', ';', ':');
        return resultName.Contains(requestedStreetName, StringComparison.OrdinalIgnoreCase);
    }

    private static LocationSearchResult? MapLocation(NominatimResult result)
    {
        var hasLatitude = double.TryParse(result.Latitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude);
        var hasLongitude = double.TryParse(result.Longitude, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude);
        return hasLatitude && hasLongitude
            ? new LocationSearchResult(result.DisplayName, latitude, longitude)
            : null;
    }

    private sealed class NominatimResult
    {
        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("lat")]
        public string Latitude { get; set; } = string.Empty;

        [JsonPropertyName("lon")]
        public string Longitude { get; set; } = string.Empty;
    }

    public sealed record LocationSearchResult(string Name, double Latitude, double Longitude);
}
