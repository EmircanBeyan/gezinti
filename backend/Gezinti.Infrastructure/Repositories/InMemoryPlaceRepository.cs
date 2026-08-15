using Gezinti.Application.Interfaces;
using Gezinti.Domain.Entities;

namespace Gezinti.Infrastructure.Repositories;

public class InMemoryPlaceRepository : IPlaceRepository
{
    private readonly List<Place> _places =
    [
        new Place
        {
            Id = Guid.NewGuid(),
            Name = "Kadıköy Kahve Evi",
            Description = "Deneme mekanımız.",
            Latitude = 40.9923,
            Longitude = 29.0301
        },
        new Place
        {
            Id = Guid.NewGuid(),
            Name = "Şehir Müzesi",
            Description = "Deneme müzemiz.",
            Latitude = 41.0082,
            Longitude = 28.9784
        }
    ];

    public Task<List<Place>> GetAllAsync()
    {
        return Task.FromResult(_places);
    }
}