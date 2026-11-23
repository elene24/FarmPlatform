import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router'
import { FormError } from '../components/form/FormError'
import { Alert } from '../components/ui/Alert'
import { Button } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { Spinner } from '../components/ui/Spinner'
import { consentApi } from '../features/linked/linkedApi'
import type { ConsentRequest } from '../features/linked/types'
import { ApiError } from '../lib/api'

/**
 * Opened from the consent SMS (/c/:token) by a picker who has no account. Big buttons, plain words:
 * agree, decline, or withdraw consent given earlier.
 */
export function ConsentPage() {
  const { t } = useTranslation()
  const { token = '' } = useParams()
  const [request, setRequest] = useState<ConsentRequest | null | undefined>(undefined)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    consentApi
      .get(token)
      .then((r) => !cancelled && setRequest(r))
      .catch((e: unknown) => !cancelled && setError(e instanceof ApiError ? (e.problem.code ?? 'unknown') : 'unknown'))
    return () => {
      cancelled = true
    }
  }, [token])

  const respond = async (action: 'accept' | 'decline' | 'withdraw') => {
    setBusy(true)
    setError(null)
    try {
      setRequest(await consentApi.respond(token, action))
    } catch (e) {
      setError(e instanceof ApiError ? (e.problem.code ?? 'unknown') : 'unknown')
    } finally {
      setBusy(false)
    }
  }

  if (error && request === undefined) return <Wrapper><FormError code={error} /></Wrapper>
  if (request === undefined) return <Spinner />
  if (request === null) {
    return (
      <Wrapper>
        <Card>
          <h1 className="text-xl font-semibold">{t('consent.not_found_title')}</h1>
          <p className="mt-2 text-stone-600">{t('consent.not_found_text')}</p>
        </Card>
      </Wrapper>
    )
  }

  const r = request
  return (
    <Wrapper>
      <Card>
        <h1 className="text-2xl font-semibold">{t('consent.hello', { name: r.personFirstName })}</h1>
        <p className="mt-4 text-lg leading-relaxed text-stone-800">{t('consent.request', { rep: r.representativeName })}</p>
        <ul className="mt-4 space-y-2 text-base text-stone-700">
          <li>✓ {t('consent.scope_availability')}</li>
          {r.payToRepresentative ? (
            <li>✓ {t('consent.scope_pay_rep', { rep: r.representativeName, last4: r.bankAccountLast4 ?? '' })}</li>
          ) : (
            <li>✓ {t('consent.scope_pay_own', { last4: r.bankAccountLast4 ?? '' })}</li>
          )}
          <li>✓ {t('consent.scope_withdraw')}</li>
        </ul>

        <div className="mt-6 space-y-4">
          <FormError code={error} />
          {r.consentStatus === 'confirmed' ? (
            <>
              <Alert tone="success">{t('consent.agreed')}</Alert>
              <Button variant="secondary" size="lg" className="w-full" disabled={busy} onClick={() => void respond('withdraw')}>
                {t('consent.withdraw')}
              </Button>
            </>
          ) : (
            <>
              {r.consentStatus === 'declined' && <Alert tone="info">{t('consent.declined')}</Alert>}
              {r.consentStatus === 'revoked' && <Alert tone="info">{t('consent.withdrawn')}</Alert>}
              <Button size="lg" className="h-14 w-full text-lg" disabled={busy} onClick={() => void respond('accept')}>
                {t('consent.agree')}
              </Button>
              {r.consentStatus === 'pending' && (
                <Button variant="secondary" size="lg" className="h-14 w-full text-lg" disabled={busy} onClick={() => void respond('decline')}>
                  {t('consent.decline')}
                </Button>
              )}
            </>
          )}
        </div>
      </Card>
      <p className="mt-4 text-center text-sm text-stone-500">{t('consent.footer')}</p>
    </Wrapper>
  )
}

function Wrapper({ children }: { children: React.ReactNode }) {
  return <div className="mx-auto max-w-lg px-4 py-10">{children}</div>
}
