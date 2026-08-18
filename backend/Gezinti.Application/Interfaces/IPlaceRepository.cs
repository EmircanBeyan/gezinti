using Gezinti.Domain.Entities;

namespace Gezinti.Application.Interfaces;

public interface IPlaceRepository
{
    Task<List<Place>> GetAllAsync();
    Task<Place> AddAsync(Place place);
    Task<Place?> GetByIdAsync(int id);
    Task<Place?> UpdateAsync(int id, Place place);
    Task<bool> DeleteAsync(int id);
}