using Gezinti.Domain.Entities;

namespace Gezinti.Application.Interfaces;

public interface IPlaceRepository
{
    Task<List<Place>> GetAllAsync();

    Task<Place?> GetByIdAsync(int id);

    Task<Place> AddAsync(Place place);

    Task<Place?> UpdateAsync(int id, Place place);

    Task<bool> DeleteAsync(int id);

    Task<Place?> GetByExternalIdAsync(
        string provider,
        string externalId);

    Task<List<Place>> GetByExternalIdsAsync(
        string provider,
        IEnumerable<string> externalIds);

    Task AddRangeAsync(
        IEnumerable<Place> places);

    Task<List<Place>> GetNearbyFreshAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        string? category,
        DateTime minimumLastSeenAt);

    Task UpdateRangeAsync(
        IEnumerable<Place> places);
}