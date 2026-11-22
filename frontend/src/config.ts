/** Runtime configuration from Vite env vars (see .env.example). */
export const config = {
  /** '' in development (same-origin via Vite proxy); e.g. https://api.example.ge in production. */
  apiBaseUrl: (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, ''),
  googleMapsApiKey: import.meta.env.VITE_GOOGLE_MAPS_API_KEY ?? '',
} as const
