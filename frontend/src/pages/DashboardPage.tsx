import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '../components/ui/Alert'
import { Button, LinkButton } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { Spinner } from '../components/ui/Spinner'
import { FormError } from '../components/form/FormError'
import { authApi } from '../features/auth/authApi'
import { useAuth } from '../features/auth/authContext'
import type { CurrentUser } from '../features/auth/types'
import type { ProfilesSummary } from '../features/profiles/types'
import { useProfilesSummary } from '../features/profiles/useProfilesSummary'

/** Profile completeness: what is still missing for the categories the user has chosen. */
function completion(user: CurrentUser, s: ProfilesSummary) {
  const checks: [string, boolean][] = [
    ['email', user.emailConfirmed],
    ['category', s.picker.status.active || s.driver.status.active || s.orchardOwner.status.active],
  ]
  if (s.picker.status.active || s.driver.status.active) checks.push(['personal', s.personalDetails !== null])
  if (s.driver.status.active) checks.push(['vehicle', s.driver.vehicles.length > 0])
  if (s.orchardOwner.status.active) checks.push(['orchard', s.orchardOwner.orchards.length > 0])
  const done = checks.filter(([, ok]) => ok).length
  return { percent: Math.round((done / checks.length) * 100), missing: checks.filter(([, ok]) => !ok).map(([k]) => k) }
}

export function DashboardPage() {
  const { t, i18n } = useTranslation()
  const { user } = useAuth()
  const { summary, error } = useProfilesSummary()
  const [resend, setResend] = useState<'idle' | 'sending' | 'sent' | 'error'>('idle')

  if (!user) return null
  if (error) {
    return (
      <div className="mx-auto max-w-5xl px-4 py-10">
        <FormError code={error} />
      </div>
    )
  }
  if (!summary) return <Spinner />

  const resendEmail = async () => {
    setResend('sending')
    try {
      await authApi.resendVerification()
      setResend('sent')
    } catch {
      setResend('error')
    }
  }

  const name = summary.personalDetails ? `${summary.personalDetails.firstName} ${summary.personalDetails.lastName}` : null
  const { percent, missing } = completion(user, summary)
  const anyMissingCategory = !summary.picker.status.active || !summary.driver.status.active || !summary.orchardOwner.status.active
  const numberFormat = new Intl.NumberFormat(i18n.language === 'en' ? 'en-GB' : 'ka-GE', { maximumFractionDigits: 4 })

  return (
    <div className="mx-auto max-w-5xl space-y-6 px-4 py-10">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div className="flex items-center gap-4">
          <span className="flex size-14 shrink-0 items-center justify-center rounded-full bg-brand-100 text-xl font-semibold text-brand-800" aria-hidden>
            {(name ?? user.email).charAt(0).toUpperCase()}
          </span>
          <div>
            <p className="text-sm text-stone-500">{t('dashboard.title')}</p>
            <h1 className="text-2xl font-semibold">{name ?? user.email}</h1>
          </div>
        </div>
        {anyMissingCategory && (
          <LinkButton to="/register/categories" variant="secondary">
            + {t('dashboard.add_category')}
          </LinkButton>
        )}
      </div>

      {!user.emailConfirmed && (
        <Alert tone="warning">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <span>{t('dashboard.verify_notice', { email: user.email })}</span>
            {resend === 'sent' ? (
              <span className="font-medium text-brand-800">{t('dashboard.resent')}</span>
            ) : (
              <Button variant="secondary" className="shrink-0 whitespace-nowrap" onClick={() => void resendEmail()} disabled={resend === 'sending'}>
                {t('dashboard.resend')}
              </Button>
            )}
          </div>
          {resend === 'error' && <p className="mt-2 text-red-700">{t('errors.unknown')}</p>}
        </Alert>
      )}

      <Card>
        <div className="flex items-center justify-between gap-4">
          <h2 className="font-semibold">{t('dashboard.completion')}</h2>
          <span className="text-sm font-semibold text-brand-800">{percent}%</span>
        </div>
        <div className="mt-3 h-2.5 overflow-hidden rounded-full bg-stone-200" role="progressbar" aria-valuenow={percent} aria-valuemin={0} aria-valuemax={100}>
          <div className="h-full rounded-full bg-brand-600 transition-all" style={{ width: `${percent}%` }} />
        </div>
        {missing.length > 0 && (
          <ul className="mt-3 space-y-1 text-sm text-stone-600">
            {missing.map((m) => (
              <li key={m}>○ {t(`dashboard.missing.${m}`)}</li>
            ))}
          </ul>
        )}
      </Card>

      <div className="grid gap-6 lg:grid-cols-3">
        <CategoryCard icon="🧺" title={t('categories.picker')} status={summary.picker.status} registerText={t('categories.picker_text')}>
          {summary.personalDetails && (
            <>
              <Facts
                rows={[
                  [t('personal.personal_id'), `•••• ${summary.personalDetails.personalIdNumberLast4}`],
                  [
                    t('personal.iban'),
                    `•••• ${summary.personalDetails.bankAccountLast4}` +
                      (summary.personalDetails.bankAccountOwnedByOther
                        ? ` (${summary.personalDetails.accountHolderFirstName} ${summary.personalDetails.accountHolderLastName})`
                        : ''),
                  ],
                  [t('personal.alternative_phone'), summary.personalDetails.alternativePhoneNumber ?? '—'],
                  [t('personal.actual_address'), summary.personalDetails.actualAddress],
                ]}
              />
              <LinkButton to="/profile" variant="secondary" className="mt-4">
                {t('profile.edit_personal')}
              </LinkButton>
            </>
          )}
        </CategoryCard>

        <CategoryCard icon="🚐" title={t('categories.driver')} status={summary.driver.status} registerText={t('categories.driver_text')}>
          {summary.driver.vehicles.length === 0 ? (
            <p className="text-sm text-stone-600">{t('dashboard.no_vehicles')}</p>
          ) : (
            <ul className="space-y-2 text-sm">
              {summary.driver.vehicles.map((v) => (
                <li key={v.id} className="rounded-[var(--radius-control)] bg-stone-50 px-3 py-2">
                  <span className="font-semibold tracking-wide">{v.licensePlateNumber}</span>
                  <span className="block text-stone-600">
                    {v.model} · {t('review.seats', { count: v.passengerCapacity })}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </CategoryCard>

        <CategoryCard icon="🌳" title={t('categories.orchard')} status={summary.orchardOwner.status} registerText={t('categories.orchard_text')}>
          {summary.orchardOwner.orchards.length === 0 ? (
            <p className="text-sm text-stone-600">{t('dashboard.no_orchards')}</p>
          ) : (
            <ul className="space-y-2 text-sm">
              {summary.orchardOwner.orchards.map((o) => (
                <li key={o.id} className="rounded-[var(--radius-control)] bg-stone-50 px-3 py-2">
                  <span className="font-semibold">{o.name}</span>
                  <span className="block text-stone-600">
                    {o.address} · {t('review.hectares', { value: numberFormat.format(o.areaHectares) })}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </CategoryCard>
      </div>

      <Card>
        <h2 className="text-lg font-semibold text-brand-800">{t('dashboard.account_title')}</h2>
        <dl className="mt-3 grid gap-4 text-sm sm:grid-cols-2">
          <div>
            <dt className="text-stone-500">{t('fields.email')}</dt>
            <dd className="font-medium break-all">{user.email}</dd>
            <dd className="mt-1">
              <span className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-medium ${user.emailConfirmed ? 'bg-brand-50 text-brand-800' : 'bg-accent-100 text-accent-600'}`}>
                {user.emailConfirmed ? t('dashboard.verified') : t('dashboard.not_verified')}
              </span>
            </dd>
          </div>
          <div>
            <dt className="text-stone-500">{t('fields.phone')}</dt>
            <dd className="font-medium">{user.phoneNumber ?? '—'}</dd>
          </div>
        </dl>
      </Card>
    </div>
  )
}

function CategoryCard({ icon, title, status, registerText, children }: {
  icon: string
  title: string
  status: { registered: boolean; active: boolean }
  registerText: string
  children: ReactNode
}) {
  const { t } = useTranslation()
  return (
    <Card className={status.active ? '' : 'border-dashed bg-stone-50/50'}>
      <div className="flex items-start justify-between gap-3">
        <h2 className="flex items-center gap-2 text-lg font-semibold text-brand-800">
          <span aria-hidden>{icon}</span> {title}
        </h2>
        {status.active ? (
          <span className="rounded-full bg-brand-50 px-2.5 py-0.5 text-xs font-medium text-brand-800">{t('dashboard.status_active')}</span>
        ) : status.registered ? (
          <span className="rounded-full bg-stone-100 px-2.5 py-0.5 text-xs font-medium text-stone-600">{t('dashboard.status_inactive')}</span>
        ) : null}
      </div>
      <div className="mt-4">
        {status.active ? (
          children
        ) : (
          <>
            <p className="text-sm text-stone-600">{registerText}</p>
            <LinkButton to="/register/categories" className="mt-4">
              {status.registered ? t('dashboard.reactivate') : t('dashboard.register_as', { category: title })}
            </LinkButton>
          </>
        )}
      </div>
    </Card>
  )
}

function Facts({ rows }: { rows: [string, string][] }) {
  return (
    <dl className="space-y-2 text-sm">
      {rows.map(([label, value]) => (
        <div key={label}>
          <dt className="text-stone-500">{label}</dt>
          <dd className="font-medium break-words">{value}</dd>
        </div>
      ))}
    </dl>
  )
}
