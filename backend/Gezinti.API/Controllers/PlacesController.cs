using Gezinti.Application.DTOs.Places;
using Gezinti.Application.Interfaces;
using Gezinti.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Gezinti.Application.Services;

namespace Gezinti.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlacesController : ControllerBase
{
    private readonly IPlaceRepository _placeRepository;

    public PlacesController(IPlaceRepository placeRepository)
    {
        _placeRepository = placeRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var places = await _placeRepository.GetAllAsync();

        var response = places.Select(place => new PlaceResponseDto
        {
            Id = place.Id,
            Name = place.Name,
            Description = place.Description,
            Latitude = place.Latitude,
            Longitude = place.Longitude
        });

        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreatePlaceDto dto)
    {
        var place = new Place
        {
            Name = dto.Name,
            Description = dto.Description,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude
        };

        var createdPlace = await _placeRepository.AddAsync(place);

        var response = new PlaceResponseDto
        {
            Id = createdPlace.Id,
            Name = createdPlace.Name,
            Description = createdPlace.Description,
            Latitude = createdPlace.Latitude,
            Longitude = createdPlace.Longitude
        };

        return CreatedAtAction(
            nameof(GetById),
            new { id = response.Id },
            response);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var place = await _placeRepository.GetByIdAsync(id);

        if (place is null)
            return NotFound();

        var response = new PlaceResponseDto
        {
            Id = place.Id,
            Name = place.Name,
            Description = place.Description,
            Latitude = place.Latitude,
            Longitude = place.Longitude
        };

        return Ok(response);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreatePlaceDto dto)
    {
        var place = new Place
        {
            Name = dto.Name,
            Description = dto.Description,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude
        };

        var updatedPlace = await _placeRepository.UpdateAsync(id, place);

        if (updatedPlace is null)
            return NotFound();

        var response = new PlaceResponseDto
        {
            Id = updatedPlace.Id,
            Name = updatedPlace.Name,
            Description = updatedPlace.Description,
            Latitude = updatedPlace.Latitude,
            Longitude = updatedPlace.Longitude
        };

        return Ok(response);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _placeRepository.DeleteAsync(id);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
    [HttpGet("nearby")]
    public async Task<IActionResult> GetNearby(
    [FromServices] IPlacesProvider placesProvider,
    [FromServices] PlaceImportService placeImportService,
    [FromQuery] double latitude,
    [FromQuery] double longitude,
    [FromQuery] double radius = 1000,
    [FromQuery] string? category = null,
    CancellationToken cancellationToken = default)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return BadRequest("Enlem veya boylam geçerli aralıkta değil.");

        if (radius <= 0)
            return BadRequest("Yarıçap 0'dan büyük olmalı.");

        var freshnessWindow = TimeSpan.FromMinutes(30);

        var minimumLastSeenAt =
            DateTime.UtcNow - freshnessWindow;

        var existingPlaces =
            await _placeRepository.GetNearbyFreshAsync(
                latitude,
                longitude,
                radius,
                category,
                minimumLastSeenAt);

        if (existingPlaces.Count > 0)
        {
            var existingResponse = existingPlaces.Select(place =>
                new PlaceResponseDto
                {
                    Id = place.Id,
                    Name = place.Name,
                    Description = place.Description,
                    Latitude = place.Latitude,
                    Longitude = place.Longitude,
                    DistanceMeters = Math.Round(place.DistanceMeters, 2),
                    Provider = place.Provider,
                    IsStale = place.IsStale
                });

            return Ok(existingResponse);
        }

        List<ExternalPlaceDto> externalPlaces;
        try
        {
            externalPlaces = await placesProvider.GetNearbyAsync(
                latitude,
                longitude,
                radius,
                category,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or TimeoutException ||
            exception is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            var cachedPlaces = await _placeRepository.GetNearbyCachedAsync(
                latitude,
                longitude,
                radius,
                category,
                DateTime.UtcNow.AddDays(-90),
                minimumLastSeenAt);

            if (cachedPlaces.Count > 0)
                return Ok(cachedPlaces.Select(ToResponse));

            return Problem(
                statusCode: exception is TimeoutException or TaskCanceledException
                    ? StatusCodes.Status504GatewayTimeout
                    : StatusCodes.Status502BadGateway,
                title: "Mekân servisleri yanıt vermedi.",
                detail: "OpenStreetMap yanıt vermedi ve TomTom yedeği sonuç getiremedi. TomTom:ApiKey ayarı yoksa yedek servis etkin değildir. Biraz sonra yeniden dene.");
        }

        await placeImportService.ImportManyAsync(
            externalPlaces);

        // Import işleminden sonra mesafeleri tekrar
        // PostGIS'e hesaplattırıyoruz.
        var importedPlaces =
            await _placeRepository.GetNearbyFreshAsync(
                latitude,
                longitude,
                radius,
                category,
                minimumLastSeenAt);

        var response = importedPlaces.Select(place =>
            new PlaceResponseDto
            {
                Id = place.Id,
                Name = place.Name,
                Description = place.Description,
                Latitude = place.Latitude,
                Longitude = place.Longitude,
                DistanceMeters = Math.Round(place.DistanceMeters, 2),
                Provider = place.Provider,
                IsStale = place.IsStale
            });

        return Ok(response);
    }

    private static PlaceResponseDto ToResponse(NearbyPlaceDto place) => new()
    {
        Id = place.Id,
        Name = place.Name,
        Description = place.Description,
        Latitude = place.Latitude,
        Longitude = place.Longitude,
        DistanceMeters = Math.Round(place.DistanceMeters, 2),
        Provider = place.Provider,
        IsStale = place.IsStale
    };
}
