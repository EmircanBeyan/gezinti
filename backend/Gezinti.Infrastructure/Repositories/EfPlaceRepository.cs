using Gezinti.Application.Interfaces;
using Gezinti.Domain.Entities;
using Gezinti.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Gezinti.Infrastructure.Repositories;

public class EfPlaceRepository : IPlaceRepository
{
    private readonly GezintiDbContext _context;

    public EfPlaceRepository(GezintiDbContext context)
    {
        _context = context;
    }

    public async Task<List<Place>> GetAllAsync()
    {
        return await _context.Places.ToListAsync();
    }

    public async Task<Place?> GetByIdAsync(int id)
    {
        return await _context.Places
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<Place> AddAsync(Place place)
    {
        _context.Places.Add(place);
        await _context.SaveChangesAsync();

        return place;
    }

    public async Task<Place?> UpdateAsync(
        int id,
        Place place)
    {
        var existingPlace = await _context.Places
            .FirstOrDefaultAsync(x => x.Id == id);

        if (existingPlace is null)
        {
            return null;
        }

        existingPlace.Name = place.Name;
        existingPlace.Description = place.Description;
        existingPlace.Latitude = place.Latitude;
        existingPlace.Longitude = place.Longitude;

        await _context.SaveChangesAsync();

        return existingPlace;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var place = await _context.Places
            .FirstOrDefaultAsync(x => x.Id == id);

        if (place is null)
        {
            return false;
        }

        _context.Places.Remove(place);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<Place?> GetByExternalIdAsync(
        string provider,
        string externalId)
    {
        return await _context.Places
            .FirstOrDefaultAsync(x =>
                x.Provider == provider &&
                x.ExternalId == externalId);
    }

    public async Task<List<Place>> GetByExternalIdsAsync(
        string provider,
        IEnumerable<string> externalIds)
    {
        var ids = externalIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        return await _context.Places
            .Where(x =>
                x.Provider == provider &&
                x.ExternalId != null &&
                ids.Contains(x.ExternalId))
            .ToListAsync();
    }

    public async Task AddRangeAsync(
        IEnumerable<Place> places)
    {
        _context.Places.AddRange(places);

        await _context.SaveChangesAsync();
    }

    public async Task UpdateRangeAsync(
        IEnumerable<Place> places)
    {
        _context.Places.UpdateRange(places);

        await _context.SaveChangesAsync();
    }

    public async Task<List<Place>> GetNearbyFreshAsync(
        double latitude,
        double longitude,
        double radiusMeters,
        string? category,
        DateTime minimumLastSeenAt)
    {
        const double latitudeKmPerDegree = 111.0;

        var latitudeDelta =
            radiusMeters / 1000.0 / latitudeKmPerDegree;

        var longitudeKmPerDegree =
            111.0 *
            Math.Cos(latitude * Math.PI / 180.0);

        var longitudeDelta =
            radiusMeters /
            1000.0 /
            longitudeKmPerDegree;

        var minLatitude =
            latitude - latitudeDelta;

        var maxLatitude =
            latitude + latitudeDelta;

        var minLongitude =
            longitude - longitudeDelta;

        var maxLongitude =
            longitude + longitudeDelta;

        var query = _context.Places
            .Where(x =>
                x.LastSeenAt >= minimumLastSeenAt &&
                x.Latitude >= minLatitude &&
                x.Latitude <= maxLatitude &&
                x.Longitude >= minLongitude &&
                x.Longitude <= maxLongitude);

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(x =>
                x.Category == category);
        }

        return await query.ToListAsync();
    }
}