using System.Text.Json.Serialization;

namespace Gezinti.Infrastructure.Providers.OpenStreetMap;

public class OverpassResponse
{
    [JsonPropertyName("elements")]
    public List<OverpassElement> Elements { get; set; } = [];
}

public class OverpassElement
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("lat")]
    public double? Latitude { get; set; }

    [JsonPropertyName("lon")]
    public double? Longitude { get; set; }

    [JsonPropertyName("center")]
    public OverpassCenter? Center { get; set; }

    [JsonPropertyName("tags")]
    public Dictionary<string, string>? Tags { get; set; }
}

public class OverpassCenter
{
    [JsonPropertyName("lat")]
    public double Latitude { get; set; }

    [JsonPropertyName("lon")]
    public double Longitude { get; set; }
}