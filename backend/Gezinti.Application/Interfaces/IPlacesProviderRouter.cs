using Gezinti.Application.DTOs.Places;

namespace Gezinti.Application.Interfaces;

public interface IPlacesProviderRouter
{
    Task<List<ExternalPlaceDto>> GetNearbyAsync(
        double latitude,
        double longitude,
        double radius,
        string? category = null,
        CancellationToken cancellationToken = default);
}