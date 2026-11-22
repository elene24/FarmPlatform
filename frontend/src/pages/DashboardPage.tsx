import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert } from '../components/ui/Alert'
import { Button, LinkButton } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { authApi } from '../features/auth/authApi'
import { useAuth } from '../features/auth/authContext'

/** Phase 2 dashboard: account info and email verification. Profile cards arrive in Phase 3. */
export function DashboardPage() {
  const { t } = useTranslation()
  const { user } = useAuth()
  const [resend, setResend] = useState<'idle' | 'sending' | 'sent' | 'error'>('idle')

  if (!user) return null

  const resendEmail = async () => {
    setResend('sending')
    try {
      await authApi.resendVerification()
      setResend('sent')
    } catch {
      setResend('error')
    }
  }

  return (
    <div className="mx-auto max-w-4xl space-y-6 px-4 py-10">
      <h1 className="text-2xl font-semibold">{t('dashboard.title')}</h1>

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

      <div className="grid gap-6 md:grid-cols-2">
        <Card>
          <h2 className="text-lg font-semibold text-brand-800">{t('dashboard.categories_title')}</h2>
          <p className="mt-2 text-stone-600">{t('dashboard.categories_empty')}</p>
          <LinkButton to="/register/categories" className="mt-5">
            {t('dashboard.add_category')}
          </LinkButton>
        </Card>

        <Card>
          <h2 className="text-lg font-semibold text-brand-800">{t('dashboard.account_title')}</h2>
          <dl className="mt-3 space-y-3 text-sm">
            <div>
              <dt className="text-stone-500">{t('fields.email')}</dt>
              <dd className="font-medium break-all">{user.email}</dd>
              <dd className="mt-1">
                <span
                  className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-medium ${
                    user.emailConfirmed ? 'bg-brand-50 text-brand-800' : 'bg-accent-100 text-accent-600'
                  }`}
                >
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
    </div>
  )
}
