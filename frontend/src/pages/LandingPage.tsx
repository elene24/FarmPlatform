import { useTranslation } from 'react-i18next'
import { LinkButton } from '../components/ui/Button'

/**
 * Phase 1 landing page. The hero uses a CSS placeholder; the real orchard photograph
 * is served from Cloudflare R2 once file storage is set up.
 */
export function LandingPage() {
  const { t } = useTranslation()
  const roles = ['picker', 'driver', 'orchard'] as const

  return (
    <>
      <section className="relative overflow-hidden bg-gradient-to-br from-brand-800 via-brand-700 to-brand-500 text-white">
        <div aria-hidden className="absolute -right-24 -top-24 size-96 rounded-full bg-accent-500/30 blur-3xl" />
        <div className="relative mx-auto max-w-6xl px-4 py-20 sm:py-28">
          <p className="text-sm font-medium uppercase tracking-wide text-brand-100">{t('app.tagline')}</p>
          <h1 className="mt-3 max-w-2xl text-3xl font-bold leading-tight sm:text-5xl">{t('landing.title')}</h1>
          <p className="mt-5 max-w-xl text-lg text-brand-50/90">{t('landing.description')}</p>
          <div className="mt-8 flex flex-col gap-3 sm:flex-row">
            <LinkButton to="/register" size="lg" variant="accent">
              {t('landing.cta_register')}
            </LinkButton>
            <LinkButton to="/login" size="lg" variant="ghostOnDark">
              {t('landing.cta_login')}
            </LinkButton>
          </div>
        </div>
      </section>

      <section className="mx-auto grid max-w-6xl gap-4 px-4 py-12 sm:grid-cols-3">
        {roles.map((role) => (
          <article key={role} className="rounded-[var(--radius-card)] border border-stone-200 bg-white p-6">
            <h2 className="text-lg font-semibold text-brand-800">{t(`landing.roles.${role}`)}</h2>
            <p className="mt-2 text-stone-600">{t(`landing.roles.${role}_text`)}</p>
          </article>
        ))}
      </section>
    </>
  )
}
