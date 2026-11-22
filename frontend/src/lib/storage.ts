/**
 * Tiny wrapper around localStorage for UI preferences only (never tokens or personal data).
 * Storage can be unavailable (private mode, embedded views), so every access is guarded.
 */
export function readPreference(key: string): string | null {
  try {
    return window.localStorage.getItem(key)
  } catch {
    return null
  }
}

export function writePreference(key: string, value: string): void {
  try {
    window.localStorage.setItem(key, value)
  } catch {
    // Preference simply won't persist.
  }
}
