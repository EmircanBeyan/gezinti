using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
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
        "sokak", "sokağı", "sok", "sk", "sokakta", "sokağında", "sokaktaki", "sokağındaki",
        "street", "cadde", "caddesi", "cd", "caddede", "caddesinde", "caddesindeki", "caddeden",
        "bulvar", "bulvarı", "blv", "bulvarda", "bulvarında", "bulvarındaki"
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
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        CancellationToken cancellationToken)
    {
        var query = q?.Trim();
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return BadRequest("En az iki karakterlik bir şehir veya semt adı yaz.");

        var hasStreet = ContainsStreetName(query);
        if (latitude.HasValue != longitude.HasValue ||
            (latitude is < -90 or > 90) || (longitude is < -180 or > 180))
            return BadRequest("Enlem ve boylam birlikte, geçerli koordinatlar olarak gönderilmeli.");

        var locationBias = hasStreet && latitude.HasValue
            ? $":{latitude.Value.ToString("F2", CultureInfo.InvariantCulture)}:{longitude!.Value.ToString("F2", CultureInfo.InvariantCulture)}"
            : string.Empty;
        var queryCacheKey = $"{(hasStreet ? "street" : "settlement")}:{query}{locationBias}";
        if (Cache.TryGetValue(queryCacheKey, out var cached))
            return Ok(KeepRequestedStreet(query, cached));

        await RequestGate.WaitAsync(cancellationToken);
        try
        {
            if (Cache.TryGetValue(queryCacheKey, out cached))
                return Ok(KeepRequestedStreet(query, cached));

            var results = Array.Empty<LocationSearchResult>();
            foreach (var candidate in BuildQueryCandidates(query))
            {
                var candidateCacheKey = $"{(hasStreet ? "street" : "settlement")}:{candidate}{locationBias}";
                if (Cache.TryGetValue(candidateCacheKey, out var candidateCache))
                {
                    results = KeepRequestedStreet(query, candidateCache);
                    if (results.Length > 0)
                        break;
                    continue;
                }

                var waitForRateLimit = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - _lastRequestAt);
                if (waitForRateLimit > TimeSpan.Zero)
                    await Task.Delay(waitForRateLimit, cancellationToken);

                var parameters = new Dictionary<string, string>
                {
                    ["q"] = candidate + ", Türkiye",
                    ["format"] = "jsonv2",
                    ["limit"] = hasStreet ? "10" : "5",
                    ["addressdetails"] = "0",
                    ["accept-language"] = "tr",
                    ["countrycodes"] = "tr",
                };
                if (hasStreet && latitude.HasValue && longitude.HasValue)
                {
                    // Nominatim treats viewbox as a ranking hint unless bounded=1.
                    // Keep bounded unset so explicit addresses outside the hint still match.
                    var latDelta = 0.25;
                    var lonDelta = 0.35;
                    parameters["viewbox"] = string.Join(',',
                        (longitude.Value - lonDelta).ToString(CultureInfo.InvariantCulture),
                        (latitude.Value + latDelta).ToString(CultureInfo.InvariantCulture),
                        (longitude.Value + lonDelta).ToString(CultureInfo.InvariantCulture),
                        (latitude.Value - latDelta).ToString(CultureInfo.InvariantCulture));
                }
                // Neighborhood/district searches should return actual settlements,
                // not a similarly named POI such as a bank or road junction.
                parameters[hasStreet ? "layer" : "featureType"] = hasStreet ? "address" : "settlement";
                var url = "search?" + string.Join("&", parameters
                    .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

                _lastRequestAt = DateTime.UtcNow;
                var externalResults = await _httpClient.GetFromJsonAsync<List<NominatimResult>>(url, cancellationToken);
                results = externalResults?
                    .Select(MapLocation)
                    .Where(result => result is not null)
                    .Cast<LocationSearchResult>()
                    .Where(result => !ContainsStreetName(query) || MatchesRequestedStreet(query, result.Name))
                    .OrderBy(result => IsBareStreetQuery(query) && latitude.HasValue && longitude.HasValue
                        ? DistanceSquared(latitude.Value, longitude.Value, result.Latitude, result.Longitude)
                        : 0)
                    .ToArray() ?? [];
                Cache[candidateCacheKey] = results;

                if (results.Length > 0)
                    break;
            }

            Cache[queryCacheKey] = results;
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
        var tokens = Tokenize(query);

        var candidates = new List<string>();
        for (var index = 0; index < tokens.Count; index++)
        {
            if (!StreetLabels.Contains(tokens[index]) || index == 0)
                continue;

            var precedingTokens = tokens.Take(index).ToList();
            var neighborhoodIndex = precedingTokens.FindLastIndex(token => AdministrativeLabels.Contains(token));
            var streetNameStart = neighborhoodIndex >= 0 && neighborhoodIndex + 1 < index
                ? neighborhoodIndex + 1
                : index - 1;
            var street = $"{string.Join(' ', tokens.Skip(streetNameStart).Take(index - streetNameStart))} {CanonicalStreetLabel(tokens[index])}";
            var area = neighborhoodIndex > 0
                ? tokens.Take(neighborhoodIndex - 1).ToList()
                : tokens.Take(streetNameStart).ToList();
            var addressParts = new List<string> { street };

            if (neighborhoodIndex > 0)
            {
                addressParts.Add(ExpandAdministrativeLabels(string.Join(' ', tokens.Skip(neighborhoodIndex - 1).Take(2))));
            }

            area.Reverse();
            addressParts.AddRange(area);

            candidates.Add(string.Join(", ", addressParts));
            break;
        }

        candidates.Add(query);

        if (!tokens.Any(StreetLabels.Contains))
        {
            var neighborhoodIndex = tokens.FindLastIndex(token => AdministrativeLabels.Contains(token));
            if (neighborhoodIndex > 0)
            {
                var neighborhood = ExpandAdministrativeLabels(string.Join(' ', tokens.Skip(neighborhoodIndex - 1).Take(2)));
                var surroundingTokens = tokens.Take(neighborhoodIndex - 1)
                    .Concat(tokens.Skip(neighborhoodIndex + 1))
                    .ToList();
                surroundingTokens.Reverse();

                if (surroundingTokens.Count > 0)
                    candidates.Insert(0, $"{neighborhood}, {string.Join(", ", surroundingTokens)}");
            }
            else if (tokens.Count is > 1 and <= 4)
            {
                // Short city/district queries are often typed province first
                // ("İstanbul Güngören"). Nominatim ranks comma-separated address
                // components more reliably in street/district-to-province order.
                candidates.Insert(0, string.Join(", ", tokens.AsEnumerable().Reverse()));
            }
        }

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
        .Select(token => token.Trim(',', '.', ';', ':'))
        .Any(StreetLabels.Contains);

    private static bool IsBareStreetQuery(string query)
    {
        var tokens = Tokenize(query);
        var streetLabelIndex = tokens.FindIndex(StreetLabels.Contains);
        return streetLabelIndex > 0 && streetLabelIndex == tokens.Count - 1 &&
               !tokens.Take(streetLabelIndex).Any(AdministrativeLabels.Contains);
    }

    private static double DistanceSquared(double latitude, double longitude, double resultLatitude, double resultLongitude)
    {
        var latitudeDelta = latitude - resultLatitude;
        var longitudeDelta = (longitude - resultLongitude) * Math.Cos(latitude * Math.PI / 180);
        return latitudeDelta * latitudeDelta + longitudeDelta * longitudeDelta;
    }

    private static LocationSearchResult[] KeepRequestedStreet(string query, LocationSearchResult[] results) =>
        ContainsStreetName(query) ? results.Where(result => MatchesRequestedStreet(query, result.Name)).ToArray() : results;

    private static bool MatchesRequestedStreet(string query, string resultName)
    {
        var tokens = Tokenize(query);
        var streetLabelIndex = tokens.FindIndex(StreetLabels.Contains);
        if (streetLabelIndex <= 0)
            return false;

        var requestedStreetName = tokens[streetLabelIndex - 1].Trim(',', '.', ';', ':');
        return NormalizeName(resultName).Contains(NormalizeName(requestedStreetName), StringComparison.Ordinal);
    }

    private static string NormalizeName(string value)
    {
        var decomposed = value.Replace('ı', 'i').Replace('İ', 'I').Normalize(NormalizationForm.FormD);
        return string.Concat(decomposed
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
            .ToLowerInvariant();
    }

    private static List<string> Tokenize(string query) => query
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(token => token.Trim(',', '.', ';', ':'))
        .Where(token => token.Length > 0)
        .ToList();

    private static string CanonicalStreetLabel(string label) => label.ToLowerInvariant() switch
    {
        "sok" or "sk" or "sokağı" or "sokakta" or "sokağında" or "sokaktaki" or "sokağındaki" => "Sokak",
        "cd" or "cadde" or "caddede" or "caddesinde" or "caddesindeki" or "caddeden" => "Caddesi",
        "blv" or "bulvar" or "bulvarı" or "bulvarda" or "bulvarında" or "bulvarındaki" => "Bulvar",
        _ => label
    };

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
