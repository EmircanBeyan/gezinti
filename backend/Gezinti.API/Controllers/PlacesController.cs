using Gezinti.Application.Interfaces;
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

        return Ok(places);
    }
}