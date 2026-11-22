import { Navigate, Outlet, useLocation } from 'react-router'
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

/** Login / registration pages: signed-in users go straight to their dashboard. */
export function GuestOnly() {
  const { status } = useAuth()

  if (status === 'loading') return <Spinner />
  if (status === 'authenticated') return <Navigate to="/dashboard" replace />
  return <Outlet />
}
