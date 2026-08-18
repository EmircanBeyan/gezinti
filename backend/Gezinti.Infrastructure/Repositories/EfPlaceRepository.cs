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

    public async Task<Place> AddAsync(Place place)
    {
        _context.Places.Add(place);
        await _context.SaveChangesAsync();

        return place;
    }
    public async Task<Place?> GetByIdAsync(int id)
    {
        return await _context.Places
            .FirstOrDefaultAsync(x => x.Id == id);
    }
    public async Task<Place?> UpdateAsync(int id, Place place)
    {
        var existingPlace = await _context.Places
            .FirstOrDefaultAsync(x => x.Id == id);

        if (existingPlace is null)
            return null;

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
        return false;

    _context.Places.Remove(place);
    await _context.SaveChangesAsync();

    return true;
    }
}