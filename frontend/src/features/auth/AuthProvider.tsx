import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { authApi } from './authApi'
import { AuthContext, type AuthContextValue } from './authContext'
import type { CurrentUser } from './types'

/** Loads the current session once on startup and shares it with the whole app. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUserState] = useState<CurrentUser | null>(null)
  const [loaded, setLoaded] = useState(false)

  const refresh = useCallback(async () => {
    try {
      setUserState(await authApi.me())
    } catch {
      setUserState(null) // server unreachable: treat as signed out, pages show their own errors
    } finally {
      setLoaded(true)
    }
  }, [])

  useEffect(() => {
    let cancelled = false
    authApi
      .me()
      .catch(() => null) // server unreachable: treat as signed out, pages show their own errors
      .then((me) => {
        if (cancelled) return
        setUserState(me)
        setLoaded(true)
      })
    return () => {
      cancelled = true
    }
  }, [])

  const setUser = useCallback((next: CurrentUser | null) => {
    setUserState(next)
    setLoaded(true)
  }, [])

  const logout = useCallback(async () => {
    try {
      await authApi.logout()
    } finally {
      setUserState(null)
    }
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      status: !loaded ? 'loading' : user ? 'authenticated' : 'anonymous',
      user,
      setUser,
      refresh,
      logout,
    }),
    [loaded, user, setUser, refresh, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
