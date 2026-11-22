import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router'
import { Alert } from '../../components/ui/Alert'
import { LinkButton } from '../../components/ui/Button'
import { AuthLayout } from '../../components/ui/Card'
import { Spinner } from '../../components/ui/Spinner'
import { authApi } from '../../features/auth/authApi'
import { useAuth } from '../../features/auth/authContext'

type State = 'verifying' | 'done' | 'failed'

/** Opened from the verification email: /verify-email?userId=...&token=... */
export function VerifyEmailPage() {
  const { t } = useTranslation()
  const { status, refresh } = useAuth()
  const [params] = useSearchParams()
  const userId = params.get('userId')
  const token = params.get('token')
  const [state, setState] = useState<State>(userId && token ? 'verifying' : 'failed')
  const started = useRef(false) // StrictMode runs effects twice in development

  useEffect(() => {
    if (started.current || !userId || !token) return
    started.current = true
    authApi
      .verifyEmail(userId, token)
      .then(async () => {
        await refresh()
        setState('done')
      })
      .catch(() => setState('failed'))
  }, [userId, token, refresh])

  return (
    <AuthLayout title={t('verify.title')}>
      {state === 'verifying' && <Spinner />}
      {state === 'done' && <Alert tone="success">{t('verify.success')}</Alert>}
      {state === 'failed' && <Alert tone="error">{t('verify.failed')}</Alert>}
      {state !== 'verifying' && (
        <LinkButton to={status === 'authenticated' ? '/dashboard' : '/login'} size="lg" className="mt-6 w-full">
          {status === 'authenticated' ? t('verify.to_dashboard') : t('nav.login')}
        </LinkButton>
      )}
    </AuthLayout>
  )
}
