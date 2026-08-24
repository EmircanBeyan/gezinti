using Gezinti.Application.DTOs.Places;
using Gezinti.Application.Interfaces;
using Gezinti.Domain.Entities;

namespace Gezinti.Application.Services;

public class PlaceImportService
{
    private readonly IPlaceRepository _placeRepository;

    public PlaceImportService(IPlaceRepository placeRepository)
    {
        _placeRepository = placeRepository;
    }

    public async Task<List<Place>> ImportManyAsync(
        IEnumerable<ExternalPlaceDto> externalPlaces)
    {
        var places = externalPlaces
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.Provider) &&
                !string.IsNullOrWhiteSpace(x.ExternalId))
            .ToList();

        if (places.Count == 0)
        {
            return [];
        }

        var providers = places
            .Select(x => x.Provider)
            .Distinct()
            .ToList();

        if (providers.Count != 1)
        {
            throw new InvalidOperationException(
                "ImportManyAsync tek bir provider ile çalışmalıdır.");
        }

        var provider = providers[0];

        var externalIds = places
            .Select(x => x.ExternalId!)
            .Distinct()
            .ToList();

        var existingPlaces =
            await _placeRepository.GetByExternalIdsAsync(
                provider,
                externalIds);

        var existingByExternalId = existingPlaces
            .Where(x => !string.IsNullOrWhiteSpace(x.ExternalId))
            .ToDictionary(x => x.ExternalId!);

        var importedPlaces = new List<Place>();
        var newPlaces = new List<Place>();
        var updatedPlaces = new List<Place>();

        var now = DateTime.UtcNow;

        foreach (var externalPlace in places)
        {
            if (existingByExternalId.TryGetValue(
                externalPlace.ExternalId!,
                out var existingPlace))
            {
                existingPlace.Name = externalPlace.Name;
                existingPlace.Address = externalPlace.Address;
                existingPlace.Category = externalPlace.Category;
                existingPlace.Latitude = externalPlace.Latitude;
                existingPlace.Longitude = externalPlace.Longitude;
                existingPlace.Phone = externalPlace.Phone;
                existingPlace.Website = externalPlace.Website;
                existingPlace.LastSeenAt = now;

                updatedPlaces.Add(existingPlace);
                importedPlaces.Add(existingPlace);

                continue;
            }

            var newPlace = new Place
            {
                Name = externalPlace.Name,
                Description = string.Empty,

                Latitude = externalPlace.Latitude,
                Longitude = externalPlace.Longitude,

                Address = externalPlace.Address,
                Category = externalPlace.Category,

                Phone = externalPlace.Phone,
                Website = externalPlace.Website,

                Provider = externalPlace.Provider,
                ExternalId = externalPlace.ExternalId,

                LastSeenAt = now
            };

            newPlaces.Add(newPlace);
            importedPlaces.Add(newPlace);
        }

        if (newPlaces.Count > 0)
        {
            await _placeRepository.AddRangeAsync(newPlaces);
        }

        if (updatedPlaces.Count > 0)
        {
            await _placeRepository.UpdateRangeAsync(updatedPlaces);
        }

        return importedPlaces;
    }
}