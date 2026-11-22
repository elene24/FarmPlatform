/** Runtime configuration from Vite env vars (see .env.example). */
export const config = {
  /** '' in development (same-origin via Vite proxy); e.g. https://api.example.ge in production. */
  apiBaseUrl: (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, ''),
  googleMapsApiKey: import.meta.env.VITE_GOOGLE_MAPS_API_KEY ?? '',
  /** Map ID from Google Cloud (needed for draggable "advanced" markers). DEMO_MAP_ID works for development. */
  googleMapsMapId: import.meta.env.VITE_GOOGLE_MAPS_MAP_ID || 'DEMO_MAP_ID',
} as const
