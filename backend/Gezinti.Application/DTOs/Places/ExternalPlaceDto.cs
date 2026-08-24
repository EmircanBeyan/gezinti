namespace Gezinti.Application.DTOs.Places;

public class ExternalPlaceDto
{
    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double? Rating { get; set; }

    public string? Phone { get; set; }

    public string? Website { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string? ExternalId { get; set; }
}