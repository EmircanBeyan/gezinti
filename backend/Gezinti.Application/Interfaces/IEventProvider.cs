using Gezinti.Application.DTOs.Events;

namespace Gezinti.Application.Interfaces;

public interface IEventProvider
{
    bool IsConfigured { get; }

    Task<List<NearbyEventDto>> GetUpcomingNearbyAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        CancellationToken cancellationToken = default);
}
