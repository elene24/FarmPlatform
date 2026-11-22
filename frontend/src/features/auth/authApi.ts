import { ApiError, api, resetCsrfToken } from '../../lib/api'
import type { CurrentUser, RegisterInput } from './types'

/**
 * Auth endpoints. The CSRF token is bound to the signed-in user, so it is dropped whenever the session
 * changes (register, login, logout) and fetched again on the next unsafe request.
 */
async function changingSession<T>(call: () => Promise<T>): Promise<T> {
  try {
    return await call()
  } finally {
    resetCsrfToken()
  }
}

export const authApi = {
  /** Returns null when nobody is signed in. */
  async me(): Promise<CurrentUser | null> {
    try {
      return await api.get<CurrentUser>('/api/v1/auth/me')
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) return null
      throw error
    }
  },
  register: (input: RegisterInput) => changingSession(() => api.post<CurrentUser>('/api/v1/auth/register', input)),
  login: (email: string, password: string, rememberMe: boolean) =>
    changingSession(() => api.post<CurrentUser>('/api/v1/auth/login', { email, password, rememberMe })),
  logout: () => changingSession(() => api.post<void>('/api/v1/auth/logout')),
  verifyEmail: (userId: string, token: string) => api.post<void>('/api/v1/auth/verify-email', { userId, token }),
  resendVerification: () => api.post<void>('/api/v1/auth/resend-verification'),
  forgotPassword: (email: string) => api.post<void>('/api/v1/auth/forgot-password', { email }),
  resetPassword: (email: string, token: string, newPassword: string, confirmPassword: string) =>
    api.post<void>('/api/v1/auth/reset-password', { email, token, newPassword, confirmPassword }),
}
