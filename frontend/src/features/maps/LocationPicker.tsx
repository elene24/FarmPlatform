import { useEffect, useId, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { config } from '../../config'
import type { GeoPoint } from '../profiles/types'
import { loadGoogleMaps, onMapsAuthFailure } from './googleMaps'

interface Props {
  label: string
  value: GeoPoint | null
  onChange: (value: GeoPoint) => void
  /** Error code (translated here). */
  error?: string
}

const GEORGIA_CENTER = { lat: 42.15, lng: 43.6 }
const round6 = (n: number) => Math.round(n * 1e6) / 1e6

/**
 * Location field: Google Map with address search, click-to-place and a draggable marker. Without an API key
 * (or if Google rejects it) it falls back to "use my current location" plus manual coordinates, so the form
 * always stays usable.
 */
export function LocationPicker({ label, value, onChange, error }: Props) {
  const { t } = useTranslation()
  const [mapsFailed, setMapsFailed] = useState(!config.googleMapsApiKey)
  const errorId = useId()

  return (
    <fieldset aria-describedby={error ? errorId : undefined}>
      <legend className="mb-1.5 block text-sm font-medium text-stone-800">{label}</legend>
      {mapsFailed ? (
        <ManualLocation value={value} onChange={onChange} />
      ) : (
        <GoogleMapPicker value={value} onChange={onChange} onFailure={() => setMapsFailed(true)} />
      )}
      {value && (
        <p className="mt-2 text-sm text-stone-600">
          {t('location.selected')}: {value.latitude.toFixed(6)}, {value.longitude.toFixed(6)}
        </p>
      )}
      {error && (
        <p id={errorId} className="mt-1.5 text-sm text-red-700">
          {t(`errors.${error}`, { defaultValue: t('errors.unknown') })}
        </p>
      )}
    </fieldset>
  )
}

function GoogleMapPicker({ value, onChange, onFailure }: Omit<Props, 'label' | 'error'> & { onFailure: () => void }) {
  const { t, i18n } = useTranslation()
  const mapRef = useRef<HTMLDivElement>(null)
  const searchRef = useRef<HTMLDivElement>(null)
  const markerRef = useRef<google.maps.marker.AdvancedMarkerElement | null>(null)
  const mapInstance = useRef<google.maps.Map | null>(null)
  const onChangeRef = useRef(onChange)
  const [address, setAddress] = useState<string | null>(null)
  const [ready, setReady] = useState(false)

  useEffect(() => {
    onChangeRef.current = onChange
  }, [onChange])

  // Create the map, marker, search box and listeners once.
  useEffect(() => {
    let disposed = false
    const listeners: google.maps.MapsEventListener[] = []
    let searchElement: HTMLElement | null = null
    const stopAuth = onMapsAuthFailure(onFailure)

    const init = async () => {
      await loadGoogleMaps(config.googleMapsApiKey, i18n.language)
      const [{ Map }, { AdvancedMarkerElement }, { PlaceAutocompleteElement }, { Geocoder }] = await Promise.all([
        google.maps.importLibrary('maps') as Promise<google.maps.MapsLibrary>,
        google.maps.importLibrary('marker') as Promise<google.maps.MarkerLibrary>,
        google.maps.importLibrary('places') as Promise<google.maps.PlacesLibrary>,
        google.maps.importLibrary('geocoding') as Promise<google.maps.GeocodingLibrary>,
      ])
      if (disposed || !mapRef.current) return

      const initial = value ? { lat: value.latitude, lng: value.longitude } : null
      const map = new Map(mapRef.current, {
        center: initial ?? GEORGIA_CENTER,
        zoom: initial ? 15 : 7,
        mapId: config.googleMapsMapId,
        clickableIcons: false,
        streetViewControl: false,
        mapTypeControl: true,
        fullscreenControl: false,
        gestureHandling: 'cooperative',
      })
      mapInstance.current = map
      const geocoder = new Geocoder()

      const place = (lat: number, lng: number, knownAddress?: string) => {
        const position = { lat: round6(lat), lng: round6(lng) }
        if (markerRef.current) {
          markerRef.current.position = position
        } else {
          const marker = new AdvancedMarkerElement({ map, position, gmpDraggable: true, title: t('location.drag_hint') })
          listeners.push(
            marker.addListener('dragend', () => {
              const p = marker.position
              if (!p) return
              const latLng = p instanceof google.maps.LatLng ? p.toJSON() : { lat: p.lat as number, lng: p.lng as number }
              place(latLng.lat, latLng.lng)
            }),
          )
          markerRef.current = marker
        }
        onChangeRef.current({ latitude: position.lat, longitude: position.lng })
        if (knownAddress) {
          setAddress(knownAddress)
        } else {
          geocoder
            .geocode({ location: position })
            .then(({ results }) => setAddress(results[0]?.formatted_address ?? null))
            .catch(() => setAddress(null))
        }
      }

      if (initial) place(initial.lat, initial.lng)

      listeners.push(
        map.addListener('click', (event: google.maps.MapMouseEvent) => {
          if (event.latLng) place(event.latLng.lat(), event.latLng.lng())
        }),
      )

      const search = new PlaceAutocompleteElement({ includedRegionCodes: ['ge'] })
      search.style.width = '100%'
      search.addEventListener('gmp-select', async (event) => {
        const selected = event.placePrediction.toPlace()
        await selected.fetchFields({ fields: ['location', 'formattedAddress'] })
        if (!selected.location) return
        map.setCenter(selected.location)
        map.setZoom(16)
        place(selected.location.lat(), selected.location.lng(), selected.formattedAddress ?? undefined)
      })
      searchRef.current?.append(search)
      searchElement = search
      setReady(true)
    }

    init().catch(onFailure)

    return () => {
      disposed = true
      stopAuth()
      listeners.forEach((l) => l.remove())
      searchElement?.remove()
      if (markerRef.current) markerRef.current.map = null
      markerRef.current = null
      mapInstance.current = null
    }
    // The map is created once; later value changes come from the map itself.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const useMyLocation = () =>
    navigator.geolocation?.getCurrentPosition((pos) => {
      const { latitude, longitude } = pos.coords
      mapInstance.current?.setCenter({ lat: latitude, lng: longitude })
      mapInstance.current?.setZoom(16)
      google.maps.event.trigger(mapInstance.current!, 'click', { latLng: new google.maps.LatLng(latitude, longitude) })
    })

  return (
    <div className="space-y-2">
      <div ref={searchRef} aria-label={t('location.search')} />
      <div ref={mapRef} className="h-72 w-full overflow-hidden rounded-[var(--radius-control)] border border-stone-300 bg-stone-100" />
      <div className="flex flex-wrap items-center justify-between gap-2 text-sm">
        <span className="text-stone-500">{ready ? t('location.map_hint') : t('location.loading')}</span>
        <button type="button" onClick={useMyLocation} className="font-medium text-brand-700 hover:underline">
          {t('location.use_mine')}
        </button>
      </div>
      {address && <p className="text-sm text-stone-700">📍 {address}</p>}
    </div>
  )
}

/** Fallback when Google Maps is not configured or unavailable. */
function ManualLocation({ value, onChange }: Omit<Props, 'label' | 'error'>) {
  const { t } = useTranslation()
  const [lat, setLat] = useState(value ? String(value.latitude) : '')
  const [lng, setLng] = useState(value ? String(value.longitude) : '')
  const [geoError, setGeoError] = useState(false)
  const latId = useId()
  const lngId = useId()

  const commit = (latText: string, lngText: string) => {
    const la = Number(latText.replace(',', '.'))
    const lo = Number(lngText.replace(',', '.'))
    if (latText.trim() && lngText.trim() && Number.isFinite(la) && Number.isFinite(lo)) {
      onChange({ latitude: round6(la), longitude: round6(lo) })
    }
  }

  const useMyLocation = () => {
    setGeoError(false)
    if (!navigator.geolocation) {
      setGeoError(true)
      return
    }
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        const la = String(round6(pos.coords.latitude))
        const lo = String(round6(pos.coords.longitude))
        setLat(la)
        setLng(lo)
        commit(la, lo)
      },
      () => setGeoError(true),
      { enableHighAccuracy: true, timeout: 15000 },
    )
  }

  const inputClass =
    'block h-12 w-full rounded-[var(--radius-control)] border border-stone-300 bg-white px-4 text-base focus:border-brand-600 focus:outline-none focus:ring-2 focus:ring-brand-100'

  return (
    <div className="space-y-3 rounded-[var(--radius-control)] border border-dashed border-stone-300 bg-stone-50 p-4">
      <p className="text-sm text-stone-600">{t('location.manual_notice')}</p>
      <button
        type="button"
        onClick={useMyLocation}
        className="inline-flex h-11 items-center rounded-[var(--radius-control)] bg-brand-700 px-4 text-sm font-medium text-white hover:bg-brand-800"
      >
        📍 {t('location.use_mine')}
      </button>
      {geoError && <p className="text-sm text-red-700">{t('location.geo_failed')}</p>}
      <div className="grid gap-3 sm:grid-cols-2">
        <div>
          <label htmlFor={latId} className="mb-1 block text-xs text-stone-500">{t('location.latitude')}</label>
          <input id={latId} inputMode="decimal" placeholder="41.715137" value={lat} className={inputClass}
            onChange={(e) => { setLat(e.target.value); commit(e.target.value, lng) }} />
        </div>
        <div>
          <label htmlFor={lngId} className="mb-1 block text-xs text-stone-500">{t('location.longitude')}</label>
          <input id={lngId} inputMode="decimal" placeholder="44.827096" value={lng} className={inputClass}
            onChange={(e) => { setLng(e.target.value); commit(lat, e.target.value) }} />
        </div>
      </div>
    </div>
  )
}
