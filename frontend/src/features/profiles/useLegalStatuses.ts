import { useEffect, useState } from 'react'
import { profilesApi } from './profilesApi'
import type { LegalEntityStatus } from './types'

let cache: LegalEntityStatus[] | null = null

/** Legal entity options for orchards (configurable on the server). Cached for the session. */
export function useLegalStatuses(): LegalEntityStatus[] {
  const [statuses, setStatuses] = useState<LegalEntityStatus[]>(cache ?? [])

  useEffect(() => {
    if (cache) return
    let cancelled = false
    profilesApi
      .legalStatuses()
      .then((data) => {
        cache = data
        if (!cancelled) setStatuses(data)
      })
      .catch(() => undefined)
    return () => {
      cancelled = true
    }
  }, [])

  return statuses
}
