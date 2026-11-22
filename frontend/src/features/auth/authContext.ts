import { createContext, useContext } from 'react'
import type { CurrentUser } from './types'

export type AuthStatus = 'loading' | 'authenticated' | 'anonymous'

export interface AuthContextValue {
  status: AuthStatus
  user: CurrentUser | null
  /** Store the user returned by login/register (or null after logout). */
  setUser: (user: CurrentUser | null) => void
  /** Re-read the session from the server (e.g. after verifying the email). */
  refresh: () => Promise<void>
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext)
  if (!value) throw new Error('useAuth must be used inside <AuthProvider>')
  return value
}
