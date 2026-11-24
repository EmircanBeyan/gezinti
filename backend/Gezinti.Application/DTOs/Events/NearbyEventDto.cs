namespace Gezinti.Application.DTOs.Events;

public sealed class NearbyEventDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public string VenueName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double DistanceMeters { get; set; }
    public string TicketUrl { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string Provider { get; set; } = "Ticketmaster";
}
