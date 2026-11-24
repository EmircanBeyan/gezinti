import { useCallback, useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react'
import L from 'leaflet'
import type { Marker } from 'leaflet'
import 'leaflet/dist/leaflet.css'

type Place = {
  id: number
  name: string
  description: string
  latitude: number
  longitude: number
  distanceMeters: number
  provider?: string
  isStale?: boolean
}
type NearbyEvent = {
  id: string
  name: string
  startsAt: string
  venueName: string
  address: string
  distanceMeters: number
  ticketUrl: string
  imageUrl?: string | null
  provider: string
}

type LocationResult = { name: string; latitude: number; longitude: number }
type Category = { label: string; value: string; icon: string }

const categories: Category[] = [
  { label: 'Tümü', value: '', icon: 'spark' },
  { label: 'Kafe', value: 'cafe', icon: 'coffee' },
  { label: 'Restoran', value: 'restaurant', icon: 'fork' },
  { label: 'Müze', value: 'museum', icon: 'museum' },
  { label: 'Park', value: 'park', icon: 'tree' },
  { label: 'Eczane', value: 'pharmacy', icon: 'plus' },
]

const initialPosition: [number, number] = [41.0082, 28.9784]
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5056'

function formatDistance(meters: number) {
  return meters < 1000 ? `${Math.round(meters)} m` : `${(meters / 1000).toFixed(1)} km`
}

function Icon({ name, size = 18 }: { name: string; size?: number }) {
  const paths: Record<string, ReactNode> = {
    pin: <><path d="M20 10c0 5-8 11-8 11S4 15 4 10a8 8 0 1 1 16 0Z" /><circle cx="12" cy="10" r="2.5" /></>,
    search: <><circle cx="10.8" cy="10.8" r="6.3" /><path d="m16 16 4 4" /></>,
    crosshair: <><circle cx="12" cy="12" r="8" /><circle cx="12" cy="12" r="2" /><path d="M12 2v2m0 16v2M2 12h2m16 0h2" /></>,
    compass: <><circle cx="12" cy="12" r="9" /><path d="m15.5 8.5-2.2 4.8-4.8 2.2 2.2-4.8 4.8-2.2Z" /></>,
    spark: <><path d="m12 3 1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8L12 3Z" /><path d="m19 15 .8 2.2L22 18l-2.2.8L19 21l-.8-2.2L16 18l2.2-.8L19 15Z" /></>,
    coffee: <><path d="M5 8h12v7a4 4 0 0 1-4 4H9a4 4 0 0 1-4-4V8Zm12 2h2a2 2 0 0 1 0 4h-2M8 3v2m4-2v2m4-2v2" /></>,
    fork: <><path d="M7 3v6m4-6v6M7 6h4m-2 3v12M17 3v18m0-18c2 2 2 7 0 9" /></>,
    museum: <><path d="m3 9 9-6 9 6M5 10h14M6 10v9m4-9v9m4-9v9m4-9v9M4 21h16" /></>,
    tree: <><path d="M12 21v-7m0 0 5-5h-4l3-4-4-3-4 3 3 4H7l5 5Z" /><path d="M6 20h12" /></>,
    plus: <><path d="M12 4v16M4 12h16" /></>,
    arrow: <><path d="M7 17 17 7M7 7h10v10" /></>,
    clock: <><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" /></>,
    chevron: <path d="m9 18 6-6-6-6" />,
    refresh: <><path d="M20 7v5h-5M4 17v-5h5" /><path d="M5.5 9A7 7 0 0 1 18 6l2 6M4 12l2 6a7 7 0 0 0 12.5-3" /></>,
  }
  return <svg aria-hidden="true" width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">{paths[name] ?? paths.spark}</svg>
}

function App() {
  const mapElement = useRef<HTMLDivElement>(null)
  const map = useRef<L.Map | null>(null)
  const markerLayer = useRef<L.LayerGroup | null>(null)
  const userMarker = useRef<L.CircleMarker | null>(null)
  const markers = useRef<Map<number, Marker>>(new Map())
  const searchedCenter = useRef<[number, number]>(initialPosition)
  const activePlacesRequest = useRef<AbortController | null>(null)
  const [places, setPlaces] = useState<Place[]>([])
  const [events, setEvents] = useState<NearbyEvent[]>([])
  const [activeTab, setActiveTab] = useState<'places' | 'events'>('places')
  const [eventsLoading, setEventsLoading] = useState(false)
  const [eventsError, setEventsError] = useState('')
  const [activePlace, setActivePlace] = useState<number | null>(null)
  const [category, setCategory] = useState('')
  const [position, setPosition] = useState<[number, number]>(initialPosition)
  const [locationLabel, setLocationLabel] = useState('İstanbul, Türkiye')
  const [searchRadius, setSearchRadius] = useState(1500)
  const [mapMoved, setMapMoved] = useState(false)
  const [cityQuery, setCityQuery] = useState('')
  const [locationResults, setLocationResults] = useState<LocationResult[]>([])
  const [locationLoading, setLocationLoading] = useState(false)
  const [loading, setLoading] = useState(false)
  const [locating, setLocating] = useState(false)
  const [locationMessage, setLocationMessage] = useState('')
  const [error, setError] = useState('')
  const [errorTitle, setErrorTitle] = useState('Arama tamamlanamadı')
  const [hasSearched, setHasSearched] = useState(false)

  const searchPlaces = useCallback(async (coords = position, selectedCategory = category, radius = searchRadius) => {
    activePlacesRequest.current?.abort()
    const requestController = new AbortController()
    activePlacesRequest.current = requestController
    setLoading(true)
    setError('')
    setErrorTitle('Arama tamamlanamadı')
    setHasSearched(true)
    searchedCenter.current = coords
    setMapMoved(false)
    map.current?.flyTo(coords, 15, { duration: 0.7 })
    const query = new URLSearchParams({ latitude: String(coords[0]), longitude: String(coords[1]), radius: String(radius) })
    if (selectedCategory) query.set('category', selectedCategory)

    try {
      const response = await fetch(`${apiBaseUrl}/api/places/nearby?${query}`, { signal: requestController.signal })
      if (!response.ok) {
        const problem = await response.json().catch(() => null) as { detail?: string; title?: string } | null
        if (problem?.title) setErrorTitle(problem.title)
        throw new Error(problem?.detail ?? problem?.title ?? `Sunucu ${response.status} yanıtı verdi.`)
      }
      const results = await response.json() as Place[]
      setPlaces(results)
      setActivePlace(null)
    } catch (cause) {
      if (requestController.signal.aborted) return
      setError(cause instanceof Error ? cause.message : 'Mekânlar yüklenemedi. Lütfen yeniden dene.')
      setPlaces([])
    } finally {
      if (activePlacesRequest.current === requestController) {
        activePlacesRequest.current = null
        setLoading(false)
      }
    }
  }, [category, position, searchRadius])

  const searchEvents = useCallback(async (coords = position, radius = searchRadius) => {
    setActiveTab('events')
    setEventsLoading(true)
    setEventsError('')
    const query = new URLSearchParams({ latitude: String(coords[0]), longitude: String(coords[1]), radius: String(radius) })
    try {
      const response = await fetch(`${apiBaseUrl}/api/events/nearby?${query}`)
      const payload = await response.json().catch(() => null) as NearbyEvent[] | { detail?: string; title?: string } | null
      if (!response.ok) {
        const problem = payload as { detail?: string; title?: string } | null
        throw new Error(problem?.detail ?? problem?.title ?? `Sunucu ${response.status} yanıtı verdi.`)
      }
      setEvents(payload as NearbyEvent[])
    } catch (cause) {
      setEvents([])
      setEventsError(cause instanceof Error ? cause.message : 'Etkinlikler yüklenemedi. Lütfen yeniden dene.')
    } finally {
      setEventsLoading(false)
    }
  }, [position, searchRadius])

  const searchLocations = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const query = cityQuery.trim()
    if (query.length < 2) return

    setLocationLoading(true)
    setLocationResults([])
    setError('')
    try {
      const params = new URLSearchParams({
        q: query,
        latitude: String(position[0]),
        longitude: String(position[1]),
      })
      const response = await fetch(`${apiBaseUrl}/api/locations/search?${params}`)
      if (!response.ok) {
        const problem = await response.json().catch(() => null) as { detail?: string; title?: string } | null
        throw new Error(problem?.detail ?? problem?.title ?? 'Konum aranamadı.')
      }
      const results = await response.json() as LocationResult[]
      setLocationResults(results)
      if (results.length === 0) {
        setError(/\b(sokak|sokağı|sok|sk|cadde|caddesi|cd|bulvar|bulvarı|blv)\b/i.test(query)
          ? 'Bu sokak için harita koordinatı bulunamadı; çevresindeki yerleri aramadım.'
          : 'Bu adla bir şehir veya semt bulamadık. Başka bir ad deneyebilirsin.')
      } else {
        // Like a map search, submitting a query immediately takes the user to
        // the best match. Keep alternatives open so the user can refine it.
        const bestMatch = results[0]
        const nextPosition: [number, number] = [bestMatch.latitude, bestMatch.longitude]
        setPosition(nextPosition)
        setLocationLabel(bestMatch.name.split(',').slice(0, 2).join(', '))
        if (activeTab === 'events') void searchEvents(nextPosition)
        else void searchPlaces(nextPosition, category)
      }
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Konum aranamadı. Lütfen yeniden dene.')
    } finally {
      setLocationLoading(false)
    }
  }

  const chooseLocation = (result: LocationResult) => {
    const nextPosition: [number, number] = [result.latitude, result.longitude]
    setPosition(nextPosition)
    setLocationLabel(result.name.split(',').slice(0, 2).join(', '))
    setCityQuery('')
    setLocationResults([])
    if (activeTab === 'events') void searchEvents(nextPosition)
    else void searchPlaces(nextPosition, category)
  }

  useEffect(() => {
    if (!mapElement.current || map.current) return

    map.current = L.map(mapElement.current, { zoomControl: false, scrollWheelZoom: true }).setView(initialPosition, 14)
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      maxZoom: 19,
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
    }).addTo(map.current)
    L.control.zoom({ position: 'bottomright' }).addTo(map.current)
    markerLayer.current = L.layerGroup().addTo(map.current)

    return () => {
      map.current?.remove()
      map.current = null
    }
  }, [])

  useEffect(() => {
    // Start with the default city. Ask for device location only after the
    // user explicitly chooses the current-location button.
    void searchPlaces(initialPosition, '', searchRadius)
  }, [searchPlaces])

  useEffect(() => () => activePlacesRequest.current?.abort(), [])

  useEffect(() => {
    if (!map.current) return
    userMarker.current?.remove()
    userMarker.current = L.circleMarker(position, {
      radius: 7,
      color: '#ffffff',
      weight: 3,
      fillColor: '#35664b',
      fillOpacity: 1,
    }).addTo(map.current)
    userMarker.current.bindTooltip('Arama merkezi', { direction: 'top', offset: [0, -8] })
  }, [position])

  useEffect(() => {
    if (!map.current) return
    const checkMapCenter = () => {
      const center = map.current?.getCenter()
      if (!center) return
      const distance = center.distanceTo(L.latLng(searchedCenter.current))
      setMapMoved(distance > 150)
    }
    map.current.on('moveend', checkMapCenter)
    return () => { map.current?.off('moveend', checkMapCenter) }
  }, [])

  useEffect(() => {
    if (!markerLayer.current) return
    markerLayer.current.clearLayers()
    markers.current.clear()
    for (const place of places) {
      const marker = L.marker([place.latitude, place.longitude], {
        icon: L.divIcon({
          className: 'place-marker-wrap',
          html: `<span class="place-marker ${activePlace === place.id ? 'is-active' : ''}"><span>●</span></span>`,
          iconSize: [38, 46],
          iconAnchor: [19, 42],
        }),
      }).addTo(markerLayer.current)
      marker.bindPopup(`<strong>${place.name.replace(/[&<>"']/g, '')}</strong><br>${formatDistance(place.distanceMeters)} uzaklıkta`)
      marker.on('click', () => setActivePlace(place.id))
      markers.current.set(place.id, marker)
      if (activePlace === place.id) marker.openPopup()
    }
  }, [places, activePlace])

  const selectCategory = (value: string) => {
    setCategory(value)
    void searchPlaces(position, value, searchRadius)
  }

  const searchThisArea = () => {
    const center = map.current?.getCenter()
    if (!center) return
    const nextPosition: [number, number] = [center.lat, center.lng]
    setPosition(nextPosition)
    setLocationLabel('Harita merkezi')
    if (activeTab === 'events') void searchEvents(nextPosition, searchRadius)
    else void searchPlaces(nextPosition, category, searchRadius)
  }

  const changeSearchRadius = (value: number) => {
    setSearchRadius(value)
    if (activeTab === 'events') void searchEvents(position, value)
    else void searchPlaces(position, category, value)
  }

  const useMyLocation = () => {
    if (!navigator.geolocation) {
      setLocationMessage('Bu tarayıcı konum özelliğini desteklemiyor.')
      return
    }
    setLocating(true)
    setError('')
    setLocationMessage('Konumun alınıyor…')
    navigator.geolocation.getCurrentPosition(
      ({ coords }) => {
        const nextPosition: [number, number] = [coords.latitude, coords.longitude]
        setPosition(nextPosition)
        setLocationLabel('Mevcut konum')
        setLocating(false)
        setLocationMessage(`Konum bulundu · yaklaşık ${Math.round(coords.accuracy)} m hassasiyet`)
        if (activeTab === 'events') void searchEvents(nextPosition, searchRadius)
        else void searchPlaces(nextPosition, category, searchRadius)
      },
      (cause) => {
        setLocating(false)
        const message = cause.code === cause.PERMISSION_DENIED
          ? 'Konum izni kapalı. Tarayıcı ayarlarından Gezinti için konum erişimini açıp yeniden dene.'
          : cause.code === cause.POSITION_UNAVAILABLE
            ? 'Cihaz konumu bulamadı. Wi‑Fi veya konum servislerini açıp tekrar dene.'
            : 'Konum isteği zaman aşımına uğradı. Biraz bekleyip yeniden deneyebilirsin.'
        setLocationMessage(message)
      },
      { enableHighAccuracy: false, timeout: 18000, maximumAge: 60000 },
    )
  }

  const selectPlace = (place: Place) => {
    setActivePlace(place.id)
    searchedCenter.current = [place.latitude, place.longitude]
    setMapMoved(false)
    map.current?.flyTo([place.latitude, place.longitude], 17, { duration: 0.6 })
  }

  return (
    <main className="app-shell">
      <header className="topbar">
        <a className="brand" href="/" aria-label="Gezinti ana sayfa"><span className="brand-mark"><Icon name="pin" size={20} /></span><span>gezinti<span className="brand-period">.</span></span></a>
        <form className="city-search" onSubmit={searchLocations}>
          <Icon name="search" size={19} />
          <input value={cityQuery} onChange={(event) => setCityQuery(event.target.value)} onFocus={() => setLocationResults([])} placeholder="Bir mahalle, sokak ya da şehir ara" aria-label="Konum ara" />
          {cityQuery && <kbd>↵</kbd>}
          <button className="search-submit" type="submit" disabled={locationLoading}>{locationLoading ? <span className="spinner" /> : 'Ara'}</button>
          {locationResults.length > 0 && <div className="location-results">{locationResults.map((result) => <button type="button" key={`${result.latitude}-${result.longitude}`} onClick={() => chooseLocation(result)}><span className="result-pin"><Icon name="pin" size={17} /></span><span><strong>{result.name.split(',').slice(0, 2).join(', ')}</strong><small>{result.name}</small></span><Icon name="chevron" size={16} /></button>)}</div>}
        </form>
        <button className="my-location-button" onClick={useMyLocation} disabled={locating} aria-label={locating ? 'Konum alınıyor' : 'Mevcut konumumu kullan'} title="Mevcut konumumu kullan"><Icon name="crosshair" size={17} /><span>{locating ? 'Konum aranıyor' : 'Konumum'}</span></button>
      </header>

      <section className="workspace" aria-label="Gezinti keşif alanı">
        <aside className="explore-sidebar">
          <div className="sidebar-intro">
            <div className="eyebrow"><span className="eyebrow-line" /> YAKININDA NE VAR?</div>
            <div className="sidebar-title-row"><div><h1>Şehrin sana<br /><em>iyi gelsin.</em></h1><p><Icon name="pin" size={15} />{locationLabel}</p></div><button className="refresh-button" aria-label="Tekrar ara" onClick={() => activeTab === 'places' ? void searchPlaces(position, category, searchRadius) : void searchEvents(position, searchRadius)} disabled={loading || eventsLoading}><Icon name="refresh" size={17} /></button></div>
          </div>

          <div className="explore-tabs" role="tablist" aria-label="Keşif türü">
            <button role="tab" aria-selected={activeTab === 'places'} className={activeTab === 'places' ? 'selected' : ''} onClick={() => setActiveTab('places')}>Mekânlar <span>{places.length || ''}</span></button>
            <button role="tab" aria-selected={activeTab === 'events'} className={activeTab === 'events' ? 'selected' : ''} onClick={() => void searchEvents(position, searchRadius)}>Etkinlikler <span>{events.length || ''}</span></button>
          </div>

          <div className="radius-card">
            <span className="radius-symbol"><Icon name="compass" size={17} /></span>
            <span className="radius-copy"><strong>Arama çevresi</strong><small>Yakınındaki mesafeyi seç</small></span>
            <label className="radius-control"><select aria-label="Arama yarıçapı" value={searchRadius} onChange={(event) => changeSearchRadius(Number(event.target.value))}><option value={1000}>1 km</option><option value={1500}>1.5 km</option><option value={3000}>3 km</option><option value={5000}>5 km</option></select><span>⌄</span></label>
          </div>

          {activeTab === 'places' && <>
            <div className="category-strip" aria-label="Mekân kategorisi">{categories.map((item) => <button key={item.value} className={`category-chip ${category === item.value ? 'selected' : ''}`} onClick={() => selectCategory(item.value)}><Icon name={item.icon} size={15} /><span>{item.label}</span></button>)}</div>
            <div className="list-heading"><div><h2>{loading ? 'Aranıyor' : 'Keşfedilecek yerler'}</h2><p>{places.length ? `${places.length} mekân bu civarda` : 'Yol üstünde güzel bir mola.'}</p></div><span className="result-count">{places.length.toString().padStart(2, '0')}</span></div>
            {places.some((place) => place.isStale) && <div className="notice-card"><span className="notice-mark">i</span><span>Bağlantı kurulamadı; kayıtlı sonuçlar gösteriliyor. Bilgiler güncel olmayabilir.</span></div>}
            <div className="results-list">
              {loading && <div className="empty-state"><span className="spinner" /><strong>Çevrene göz atıyoruz</strong><p>Yakındaki yerler birazdan burada.</p></div>}
              {!loading && error && <div className="empty-state error-card"><span className="empty-icon">!</span><strong>{errorTitle}</strong><p>{error}</p></div>}
              {!loading && !error && hasSearched && places.length === 0 && <div className="empty-state"><span className="empty-icon"><Icon name="pin" size={21} /></span><strong>Şimdilik sessiz görünüyor.</strong><p>Biraz daha uzağa bakmayı ya da haritada başka bir alanı dene.</p></div>}
              {!loading && !error && !hasSearched && <div className="empty-state"><span className="empty-icon"><Icon name="spark" size={21} /></span><strong>Bir yer seç, keşfe başlayalım.</strong><p>Şehir, semt veya sokak adı yazabilirsin.</p></div>}
              {!loading && places.map((place, index) => <button key={place.id} className={`place-card ${activePlace === place.id ? 'active' : ''}`} onClick={() => selectPlace(place)}>
                <span className="place-index">{String(index + 1).padStart(2, '0')}</span>
                <span className="place-info"><strong>{place.name}</strong><small>{place.isStale ? 'Kayıtlı sonuç · güncel olmayabilir' : place.description || categories.find((item) => item.value === category)?.label || place.provider || 'Yakınında'}</small></span>
                <span className="place-meta"><span>{formatDistance(place.distanceMeters)}</span><Icon name="chevron" size={15} /></span>
              </button>)}
            </div>
          </>}

          {activeTab === 'events' && <>
            <div className="list-heading"><div><h2>Takvimde yakında</h2><p>{events.length ? `${events.length} etkinlik · 60 gün içinde` : 'Canlı müzik ve buluşmalar'}</p></div><span className="result-count">{events.length.toString().padStart(2, '0')}</span></div>
            <div className="results-list event-list">
              {eventsLoading && <div className="empty-state"><span className="spinner" /><strong>Takvime bakıyoruz</strong><p>Yakındaki etkinlikleri arıyoruz.</p></div>}
              {!eventsLoading && eventsError && <div className="empty-state error-card"><span className="empty-icon">!</span><strong>Etkinliklere ulaşılamadı</strong><p>{eventsError}</p></div>}
              {!eventsLoading && !eventsError && events.length === 0 && <div className="empty-state"><span className="empty-icon"><Icon name="clock" size={21} /></span><strong>Şimdilik yeni bir etkinlik yok.</strong><p>Arama alanını genişletip tekrar bakabilirsin.</p></div>}
              {!eventsLoading && events.map((eventItem) => <article className="event-card" key={eventItem.id}>
                {eventItem.imageUrl && <img className="event-image" src={eventItem.imageUrl} alt="" loading="lazy" />}
                <div className="event-card-content"><time><Icon name="clock" size={13} />{new Intl.DateTimeFormat('tr-TR', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(eventItem.startsAt))}</time><strong>{eventItem.name}</strong><span>{eventItem.venueName}{eventItem.address ? ` · ${eventItem.address}` : ''}</span><div className="event-card-footer"><small>{formatDistance(eventItem.distanceMeters)} uzakta</small><a href={eventItem.ticketUrl} target="_blank" rel="noreferrer">Detaylar <Icon name="arrow" size={13} /></a></div></div>
              </article>)}
            </div>
          </>}
          <div className="sidebar-foot"><span className="live-dot" /> Harita üzerinde serbestçe dolaş</div>
        </aside>

        <div className="map-panel" aria-label="Yakındaki yerlerin haritası">
          <div ref={mapElement} className="map-canvas" />
          <div className="map-toolbar"><span className="map-live"><span className="live-dot" />{locationLabel}</span><span className="map-credit">© OpenStreetMap</span></div>
          {mapMoved && <button className="search-this-area" onClick={searchThisArea} disabled={loading}><Icon name="search" size={15} />{loading ? 'Aranıyor…' : 'Bu alanda ara'}</button>}
          <div className="map-caption"><span className="caption-mark"><Icon name="pin" size={17} /></span><span><strong>Yakın çevren</strong><small>{formatDistance(searchRadius)} içinde keşfedilecek yerler</small></span></div>
        </div>
      </section>
      {locationMessage && <div className={`location-toast ${locating ? 'is-loading' : ''}`} role="status"><span className="toast-indicator">{locating ? <span className="spinner" /> : <Icon name={locationMessage.startsWith('Konum bulundu') ? 'pin' : 'crosshair'} size={16} />}</span><span>{locationMessage}</span><button onClick={() => setLocationMessage('')} aria-label="Bildirimi kapat">×</button></div>}
    </main>
  )
}

export default App
