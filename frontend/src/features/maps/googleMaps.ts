/**
 * Loads the Google Maps JavaScript API once, on demand (only pages with a map pay for it).
 * The key comes from VITE_GOOGLE_MAPS_API_KEY and must be restricted to your domains in Google Cloud.
 */
let loading: Promise<void> | null = null
let authFailed = false
const authListeners = new Set<() => void>()

declare global {
  interface Window {
    __fpGoogleMapsReady?: () => void
    gm_authFailure?: () => void
  }
}

export function loadGoogleMaps(apiKey: string, language: string): Promise<void> {
  if (typeof google !== 'undefined' && typeof google.maps?.importLibrary === 'function') return Promise.resolve()

  loading ??= new Promise<void>((resolve, reject) => {
    window.__fpGoogleMapsReady = () => resolve()
    // Called by Google when the key is invalid, restricted or billing is off.
    window.gm_authFailure = () => {
      authFailed = true
      authListeners.forEach((listener) => listener())
    }

    const params = new URLSearchParams({
      key: apiKey,
      v: 'weekly',
      loading: 'async',
      language,
      region: 'GE',
      callback: '__fpGoogleMapsReady',
    })
    const script = document.createElement('script')
    script.src = `https://maps.googleapis.com/maps/api/js?${params}`
    script.async = true
    script.onerror = () => {
      loading = null
      reject(new Error('maps_load_failed'))
    }
    document.head.append(script)
  })

  return loading
}

/** Subscribe to "the API key was rejected". Returns an unsubscribe function. */
export function onMapsAuthFailure(listener: () => void): () => void {
  if (authFailed) listener()
  authListeners.add(listener)
  return () => authListeners.delete(listener)
}
