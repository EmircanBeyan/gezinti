import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react'
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
}

type LocationResult = { name: string; latitude: number; longitude: number }
type Category = { label: string; value: string; icon: string }

const categories: Category[] = [
  { label: 'Tümü', value: '', icon: '✳' },
  { label: 'Kafe', value: 'cafe', icon: '☕' },
  { label: 'Restoran', value: 'restaurant', icon: '♨' },
  { label: 'Müze', value: 'museum', icon: '▧' },
  { label: 'Park', value: 'park', icon: '♧' },
  { label: 'Eczane', value: 'pharmacy', icon: '✚' },
]

const initialPosition: [number, number] = [41.0082, 28.9784]
const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5056'

function formatDistance(meters: number) {
  return meters < 1000 ? `${Math.round(meters)} m` : `${(meters / 1000).toFixed(1)} km`
}

function App() {
  const mapElement = useRef<HTMLDivElement>(null)
  const map = useRef<L.Map | null>(null)
  const markerLayer = useRef<L.LayerGroup | null>(null)
  const userMarker = useRef<L.CircleMarker | null>(null)
  const markers = useRef<Map<number, Marker>>(new Map())
  const searchedCenter = useRef<[number, number]>(initialPosition)
  const initialSearchStarted = useRef(false)
  const [places, setPlaces] = useState<Place[]>([])
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
  const [error, setError] = useState('')
  const [errorTitle, setErrorTitle] = useState('Arama tamamlanamadı')
  const [hasSearched, setHasSearched] = useState(false)

  const searchPlaces = useCallback(async (coords = position, selectedCategory = category, radius = searchRadius) => {
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
      const response = await fetch(`${apiBaseUrl}/api/places/nearby?${query}`)
      if (!response.ok) {
        const problem = await response.json().catch(() => null) as { detail?: string; title?: string } | null
        if (problem?.title) setErrorTitle(problem.title)
        throw new Error(problem?.detail ?? problem?.title ?? `Sunucu ${response.status} yanıtı verdi.`)
      }
      const results = await response.json() as Place[]
      setPlaces(results)
      setActivePlace(null)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Mekânlar yüklenemedi. Lütfen yeniden dene.')
      setPlaces([])
    } finally {
      setLoading(false)
    }
  }, [category, position, searchRadius])

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
        void searchPlaces(nextPosition, category)
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
    void searchPlaces(nextPosition, category)
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
    if (initialSearchStarted.current) return
    initialSearchStarted.current = true
    if (!navigator.geolocation) {
      void searchPlaces(initialPosition, '', searchRadius)
      return
    }

    setLocating(true)
    navigator.geolocation.getCurrentPosition(
      ({ coords }) => {
        const nextPosition: [number, number] = [coords.latitude, coords.longitude]
        setPosition(nextPosition)
        setLocationLabel('Mevcut konum')
        setLocating(false)
        void searchPlaces(nextPosition, '', searchRadius)
      },
      () => {
        setLocating(false)
        void searchPlaces(initialPosition, '', searchRadius)
      },
      { enableHighAccuracy: true, timeout: 10000 },
    )
  }, [searchPlaces])

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
    void searchPlaces(nextPosition, category, searchRadius)
  }

  const changeSearchRadius = (value: number) => {
    setSearchRadius(value)
    void searchPlaces(position, category, value)
  }

  const useMyLocation = () => {
    if (!navigator.geolocation) {
      setError('Bu tarayıcı konum özelliğini desteklemiyor.')
      return
    }
    setLocating(true)
    setError('')
    navigator.geolocation.getCurrentPosition(
      ({ coords }) => {
        const nextPosition: [number, number] = [coords.latitude, coords.longitude]
        setPosition(nextPosition)
        setLocationLabel('Mevcut konum')
        setLocating(false)
        void searchPlaces(nextPosition, category, searchRadius)
      },
      () => {
        setLocating(false)
        setError('Konum alınamadı. İzinleri kontrol et veya haritada bir noktaya dokun.')
      },
      { enableHighAccuracy: true, timeout: 10000 },
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
        <a className="brand" href="/" aria-label="Gezinti ana sayfa"><span className="brand-mark">g</span><span>gezinti<span className="brand-dot">.</span></span></a>
        <form className="city-search" onSubmit={searchLocations}>
          <span className="city-search-icon">⌕</span>
          <input value={cityQuery} onChange={(event) => setCityQuery(event.target.value)} onFocus={() => setLocationResults([])} placeholder="Şehir veya semt ara" aria-label="Şehir veya semt ara" />
          <button type="submit" disabled={locationLoading}>{locationLoading ? 'Aranıyor…' : 'Ara'}</button>
          {locationResults.length > 0 && <div className="location-results">{locationResults.map((result) => <button type="button" key={`${result.latitude}-${result.longitude}`} onClick={() => chooseLocation(result)}><span className="result-pin">⌖</span><span><strong>{result.name.split(',').slice(0, 2).join(', ')}</strong><small>{result.name}</small></span><span className="result-arrow">↗</span></button>)}</div>}
        </form>
        <button className="my-location-button" onClick={useMyLocation} disabled={locating}><span>⌖</span>{locating ? 'Konum aranıyor' : 'Konumum'}</button>
      </header>

      <section className="map-panel" aria-label="Yakındaki yerlerin haritası">
        <div ref={mapElement} className="map-canvas" />
        <div className="map-shade" />
        <aside className="explore-card">
          <div className="card-kicker"><span className="live-dot" /> GEZİNTİ / YAKININDA</div>
          <div className="card-title-row"><div><h1>Şehrini keşfet.</h1><p><span className="tiny-pin">⌖</span>{locationLabel} <span className="middot">·</span> {formatDistance(searchRadius)} çevre</p></div><button className="refresh-button" aria-label="Tekrar ara" onClick={() => void searchPlaces(position, category, searchRadius)} disabled={loading}>↻</button></div>

          <label className="radius-control"><span>ARAMA MESAFESİ</span><select value={searchRadius} onChange={(event) => changeSearchRadius(Number(event.target.value))}><option value={1000}>1 km</option><option value={1500}>1.5 km</option><option value={3000}>3 km</option><option value={5000}>5 km</option></select></label>

          <div className="category-strip" aria-label="Mekân kategorisi">
            {categories.map((item) => <button key={item.value} className={`category-chip ${category === item.value ? 'selected' : ''}`} onClick={() => selectCategory(item.value)}><span>{item.icon}</span>{item.label}</button>)}
          </div>

          <div className="places-heading"><div><h2>{loading ? 'Yakınındaki yerler aranıyor' : 'Yakınındaki yerler'}</h2><span>{places.length ? `${places.length} keşif noktası` : 'Yeni bir yer keşfet'}</span></div><span className="result-count">{places.length.toString().padStart(2, '0')}</span></div>
          <div className="results-list">
            {loading && <div className="message-card"><span className="spinner" /> Yerleri senin için buluyoruz…</div>}
            {!loading && error && <div className="message-card error-card"><strong>{errorTitle}</strong><span>{error}</span></div>}
            {!loading && !error && hasSearched && places.length === 0 && <div className="message-card"><strong>Bu çevrede yer bulamadık.</strong><span>Başka bir kategori veya harita konumu deneyebilirsin.</span></div>}
            {!loading && !error && !hasSearched && <div className="message-card"><strong>Keşif seni bekliyor.</strong><span>Bir şehir ara veya haritada istediğin noktaya dokun.</span></div>}
            {!loading && places.map((place, index) => <button key={place.id} className={`place-card ${activePlace === place.id ? 'active' : ''}`} onClick={() => selectPlace(place)}>
              <span className={`place-number tone-${index % 4}`}>{String(index + 1).padStart(2, '0')}</span>
              <span className="place-info"><strong>{place.name}</strong><small>{place.description || categories.find((item) => item.value === category)?.label || 'Çevrende keşfedilecek'}</small></span>
              <span className="place-distance">{formatDistance(place.distanceMeters)}</span>
            </button>)}
          </div>
          <div className="card-footnote"><span>↖</span> Haritada başka bir noktaya dokunarak ara</div>
        </aside>

        <div className="map-status"><span className="live-dot" /> {locationLabel} <span className="status-divider">/</span> OpenStreetMap</div>
        {mapMoved && <button className="search-this-area" onClick={searchThisArea} disabled={loading}><span>⌖</span>{loading ? 'Aranıyor…' : 'Bu bölgede ara'}</button>}
      </section>
    </main>
  )
}

export default App
