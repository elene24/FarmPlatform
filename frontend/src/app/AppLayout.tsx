import { useTranslation } from 'react-i18next'
import { Outlet, useNavigate } from 'react-router'
import { ApiStatus } from '../components/ApiStatus'
import { LanguageSwitcher } from '../components/LanguageSwitcher'
import { Logo } from '../components/Logo'
import { Button, LinkButton } from '../components/ui/Button'
import { useAuth } from '../features/auth/authContext'

const currentYear = new Date().getFullYear()

export function AppLayout() {
  const { t } = useTranslation()
  const { status, logout } = useAuth()
  const navigate = useNavigate()

  // Leave protected pages first; otherwise their guard would redirect to /login when the session clears.
  const signOut = async () => {
    await navigate('/', { replace: true })
    await logout()
  }

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="sticky top-0 z-10 border-b border-stone-200 bg-white/90 backdrop-blur">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-3 px-4">
          <Logo />
          <nav className="flex items-center gap-2">
            <LanguageSwitcher />
            {status === 'authenticated' ? (
              <>
                <LinkButton to="/dashboard" variant="ghost">
                  {t('nav.dashboard')}
                </LinkButton>
                <Button variant="secondary" onClick={() => void signOut()}>
                  {t('nav.logout')}
                </Button>
              </>
            ) : status === 'anonymous' ? (
              <>
                <span className="hidden sm:block">
                  <LinkButton to="/login" variant="ghost">
                    {t('nav.login')}
                  </LinkButton>
                </span>
                <LinkButton to="/register">{t('nav.register')}</LinkButton>
              </>
            ) : null}
          </nav>
        </div>
      </header>

      <main className="flex-1">
        <Outlet />
      </main>

      <footer className="border-t border-stone-200 bg-white">
        <div className="mx-auto flex max-w-6xl flex-col gap-2 px-4 py-6 text-sm text-stone-500 sm:flex-row sm:items-center sm:justify-between">
          <span>
            © {currentYear} {t('app.name')}. {t('footer.rights')}
          </span>
          {import.meta.env.DEV && <ApiStatus />}
        </div>
      </footer>
    </div>
  )
}
