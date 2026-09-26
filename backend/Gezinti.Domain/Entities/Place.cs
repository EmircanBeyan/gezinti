using NetTopologySuite.Geometries;

namespace Gezinti.Domain.Entities;

public class Place
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public string Address { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Website { get; set; }

    public string Provider { get; set; } = string.Empty;

    public string? ExternalId { get; set; }

    public DateTime LastSeenAt { get; set; }

    public Point Location { get; set; } = default!;
}