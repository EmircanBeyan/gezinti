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
                    DistanceMeters = Math.Round(
                        place.DistanceMeters,
                        2)
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
        catch (HttpRequestException)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "OpenStreetMap mekân servisi yanıt vermedi.",
                detail: "Gezinti API'si çalışıyor; dış mekân servisi geçici olarak yanıt vermedi. Biraz sonra yeniden dene.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Problem(
                statusCode: StatusCodes.Status504GatewayTimeout,
                title: "Mekân sağlayıcısının yanıtı zaman aşımına uğradı.",
                detail: "Gezinti API'si çalışıyor; dış mekân servisi geç yanıt verdi. Biraz sonra yeniden dene.");
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
                DistanceMeters = Math.Round(
                    place.DistanceMeters,
                    2)
            });

        return Ok(response);
    }
}
