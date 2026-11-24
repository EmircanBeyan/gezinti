using Gezinti.Application.DTOs.Places;
using Gezinti.Application.Interfaces;
using Gezinti.Infrastructure.Providers.OpenStreetMap;
using Gezinti.Infrastructure.Providers.TomTom;
using Microsoft.Extensions.Logging;

namespace Gezinti.Infrastructure.Providers;

public sealed class FallbackPlacesProvider : IPlacesProvider
{
    private static readonly TimeSpan TotalBudget = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan OpenStreetMapBudget = TimeSpan.FromSeconds(4);
    private readonly OsmPlacesProvider _openStreetMap;
    private readonly TomTomPlacesProvider _tomTom;
    private readonly ILogger<FallbackPlacesProvider> _logger;

    public FallbackPlacesProvider(
        OsmPlacesProvider openStreetMap,
        TomTomPlacesProvider tomTom,
        ILogger<FallbackPlacesProvider> logger)
    {
        _openStreetMap = openStreetMap;
        _tomTom = tomTom;
        _logger = logger;
    }

    public async Task<List<ExternalPlaceDto>> GetNearbyAsync(
        double latitude,
        double longitude,
        double radius,
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        using var totalTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        totalTimeout.CancelAfter(TotalBudget);
        var failures = new List<Exception>();

        using (var openStreetMapTimeout = CancellationTokenSource.CreateLinkedTokenSource(totalTimeout.Token))
        {
            openStreetMapTimeout.CancelAfter(OpenStreetMapBudget);
            try
            {
                var results = await _openStreetMap.GetNearbyAsync(
                    latitude, longitude, radius, category, openStreetMapTimeout.Token);
                if (results.Count > 0)
                    return results;

                _logger.LogInformation("OpenStreetMap sonuç döndürmedi; TomTom yedeği deneniyor.");
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                failures.Add(exception);
                _logger.LogWarning("OpenStreetMap yanıt süresini aştı; TomTom yedeği deneniyor.");
            }
            catch (HttpRequestException exception)
            {
                failures.Add(exception);
                _logger.LogWarning("OpenStreetMap isteği başarısız; TomTom yedeği deneniyor.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (totalTimeout.IsCancellationRequested)
            throw new TimeoutException("Konum sağlayıcıları toplam 10 saniyelik yanıt süresini aştı.");

        if (!_tomTom.IsConfigured)
        {
            if (failures.Count > 0)
                throw new HttpRequestException("OpenStreetMap yanıt vermedi ve TomTom API anahtarı yapılandırılmamış.", failures[^1]);

            return [];
        }

        try
        {
            var results = await _tomTom.GetNearbyAsync(
                latitude, longitude, radius, category, totalTimeout.Token);
            if (results.Count > 0)
                return results;

            if (failures.Count > 0)
                throw new HttpRequestException("Her iki konum sağlayıcısı da sonuç döndürmedi.", failures[^1]);

            return [];
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Konum sağlayıcıları toplam 10 saniyelik yanıt süresini aştı.");
        }
        catch (HttpRequestException exception) when (failures.Count > 0)
        {
            throw new HttpRequestException("OpenStreetMap ve TomTom konum servisleri yanıt vermedi.", exception);
        }
    }
}
