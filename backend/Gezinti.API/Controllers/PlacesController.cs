using Gezinti.Application.DTOs.Places;
using Gezinti.Application.Interfaces;
using Gezinti.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

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
}