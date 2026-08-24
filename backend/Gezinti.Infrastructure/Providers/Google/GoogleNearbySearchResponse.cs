using System.Text.Json.Serialization;

namespace Gezinti.Infrastructure.Providers.Google;

public class GoogleNearbySearchResponse
{
    [JsonPropertyName("places")]
    public List<GooglePlace> Places { get; set; } = [];
}

public class GooglePlace
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public GoogleDisplayName? DisplayName { get; set; }

    [JsonPropertyName("formattedAddress")]
    public string? FormattedAddress { get; set; }

    [JsonPropertyName("location")]
    public GoogleLocation? Location { get; set; }

    [JsonPropertyName("types")]
    public List<string>? Types { get; set; }
}

public class GoogleDisplayName
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class GoogleLocation
{
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }
}