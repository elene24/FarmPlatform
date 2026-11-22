import { useCallback, useEffect, useState } from 'react'
import { ApiError } from '../../lib/api'
import { profilesApi } from './profilesApi'
import type { ProfilesSummary } from './types'

/** Loads GET /api/v1/profiles. `error` holds an error code for display. */
export function useProfilesSummary() {
  const [summary, setSummary] = useState<ProfilesSummary | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [version, setVersion] = useState(0)

  useEffect(() => {
    let cancelled = false
    profilesApi
      .summary()
      .then((data) => {
        if (!cancelled) {
          setSummary(data)
          setError(null)
        }
      })
      .catch((e: unknown) => {
        if (!cancelled) setError(e instanceof ApiError ? (e.problem.code ?? 'unknown') : 'unknown')
      })
    return () => {
      cancelled = true
    }
  }, [version])

  const reload = useCallback(() => setVersion((v) => v + 1), [])
  return { summary, error, reload, setSummary }
}
