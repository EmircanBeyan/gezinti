using Gezinti.Domain.Entities;

namespace Gezinti.Application.Interfaces;

public interface IPlaceRepository
{
    Task<List<Place>> GetAllAsync();
}