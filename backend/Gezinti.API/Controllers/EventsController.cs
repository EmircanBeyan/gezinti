using Gezinti.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Gezinti.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class EventsController : ControllerBase
{
    [HttpGet("nearby")]
    public async Task<IActionResult> GetNearby(
        [FromServices] IEventProvider eventProvider,
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radius = 5000,
        CancellationToken cancellationToken = default)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            return BadRequest("Enlem veya boylam geçerli aralıkta değil.");
        if (radius <= 0)
            return BadRequest("Yarıçap 0'dan büyük olmalı.");
        if (!eventProvider.IsConfigured)
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Etkinlik servisi yapılandırılmamış.",
                detail: "Yakındaki konserleri göstermek için ücretsiz Ticketmaster API anahtarını Ticketmaster:ApiKey ayarına ekle.");

        try
        {
            var events = await eventProvider.GetUpcomingNearbyAsync(
                latitude, longitude, radius, cancellationToken);
            return Ok(events);
        }
        catch (HttpRequestException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Etkinlik servisi yanıt vermedi.",
                detail: exception.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Problem(
                statusCode: StatusCodes.Status504GatewayTimeout,
                title: "Etkinlik araması zaman aşımına uğradı.",
                detail: "Ticketmaster 8 saniye içinde yanıt vermedi. Biraz sonra yeniden dene.");
        }
    }
}
