import { Navigate, Outlet, useLocation, useSearchParams } from 'react-router'
import { safeReturnTo } from '../../lib/safeReturnTo'
import { Spinner } from '../../components/ui/Spinner'
import { useAuth } from './authContext'

/** Only for signed-in users; others are sent to login and brought back afterwards. */
export function RequireAuth() {
  const { status } = useAuth()
  const location = useLocation()

  if (status === 'loading') return <Spinner />
  if (status === 'anonymous') {
    const returnTo = encodeURIComponent(location.pathname + location.search)
    return <Navigate to={`/login?returnTo=${returnTo}`} replace />
  }
  return <Outlet />
}

/**
 * Login / registration pages. When the user becomes signed in (including right after submitting one of these
 * forms) this is the single place that decides where they go next: a new account continues with registration
 * step 2, a login returns to the page that asked for it, everything else goes to the dashboard.
 */
export function GuestOnly() {
  const { status } = useAuth()
  const location = useLocation()
  const [params] = useSearchParams()

  if (status === 'loading') return <Spinner />
  if (status === 'authenticated') {
    const target =
      location.pathname === '/register' ? '/register/categories' : (safeReturnTo(params.get('returnTo')) ?? '/dashboard')
    return <Navigate to={target} replace />
  }
  return <Outlet />
}
