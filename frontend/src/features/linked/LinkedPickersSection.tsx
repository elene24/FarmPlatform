import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useLocation } from 'react-router'
import { FormError } from '../../components/form/FormError'
import { Alert } from '../../components/ui/Alert'
import { Button, LinkButton } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { Spinner } from '../../components/ui/Spinner'
import { ApiError } from '../../lib/api'
import type { PersonalDetailsDto } from '../profiles/types'
import { linkedApi } from './linkedApi'
import type { LinkedPerson } from './types'

const codeOf = (e: unknown) => (e instanceof ApiError ? (e.problem.code ?? 'unknown') : 'unknown')

/**
 * "Linked pickers" section of the profile page: pickers without an account whose availability the
 * signed-in user answers for, after they agree by SMS. Shown once the user's own personal details exist.
 */
export function LinkedPickersSection({ me }: { me: PersonalDetailsDto }) {
  const { t } = useTranslation()
  const { hash } = useLocation()
  const ref = useRef<HTMLElement>(null)
  const [people, setPeople] = useState<LinkedPerson[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    linkedApi
      .list()
      .then((list) => !cancelled && setPeople(list))
      .catch((e: unknown) => !cancelled && setError(codeOf(e)))
    return () => {
      cancelled = true
    }
  }, [])

  // /profile#linked (from the dashboard or after saving a linked picker) scrolls here once loaded.
  useEffect(() => {
    if (people && hash === '#linked') ref.current?.scrollIntoView({ block: 'start' })
  }, [people, hash])

  const replace = (p: LinkedPerson) => setPeople((list) => list?.map((x) => (x.id === p.id ? p : x)) ?? null)
  const removeFromList = (id: string) => setPeople((list) => list?.filter((x) => x.id !== id) ?? null)

  return (
    <section id="linked" ref={ref} className="scroll-mt-24 space-y-4">
      <div className="flex flex-col gap-3 pt-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h2 className="text-xl font-semibold">{t('linked.title')}</h2>
          <p className="mt-1 text-sm text-stone-600">{t('linked.subtitle')}</p>
        </div>
        <LinkButton to="/linked/new" className="shrink-0 whitespace-nowrap">
          + {t('linked.add')}
        </LinkButton>
      </div>

      {error ? (
        <FormError code={error} />
      ) : !people ? (
        <Spinner />
      ) : people.length === 0 ? (
        <Card>
          <p className="text-stone-600">{t('linked.empty')}</p>
          <ul className="mt-4 list-disc space-y-1 pl-5 text-sm text-stone-600">
            <li>{t('linked.how_1')}</li>
            <li>{t('linked.how_2')}</li>
            <li>{t('linked.how_3')}</li>
          </ul>
        </Card>
      ) : (
        people.map((p) => (
          <PersonCard key={p.id} person={p} myIbanLast4={me.bankAccountLast4} onChange={replace} onRemoved={() => removeFromList(p.id)} />
        ))
      )}
    </section>
  )
}

const badge: Record<LinkedPerson['consentStatus'], string> = {
  pending: 'bg-accent-100 text-accent-600',
  confirmed: 'bg-brand-50 text-brand-800',
  declined: 'bg-red-50 text-red-700',
  revoked: 'bg-stone-100 text-stone-700',
}

function PersonCard({ person: p, myIbanLast4, onChange, onRemoved }: {
  person: LinkedPerson
  myIbanLast4: string
  onChange: (p: LinkedPerson) => void
  onRemoved: () => void
}) {
  const { t, i18n } = useTranslation()
  const [code, setCode] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [confirmRemove, setConfirmRemove] = useState(false)
  const date = (iso: string | null) => {
    if (!iso) return ''
    const d = new Date(iso)
    // Browsers often lack Georgian locale data, so the Georgian format (DD.MM.YYYY) is built by hand.
    if (i18n.language === 'en') return d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })
    return `${String(d.getDate()).padStart(2, '0')}.${String(d.getMonth() + 1).padStart(2, '0')}.${d.getFullYear()}`
  }

  const run = async (action: () => Promise<void>) => {
    setBusy(true)
    setError(null)
    setNotice(null)
    try {
      await action()
    } catch (e) {
      setError(codeOf(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Card>
      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h2 className="text-lg font-semibold text-brand-800">{p.firstName} {p.lastName}</h2>
          <p className="mt-1 text-sm text-stone-600">
            {p.phoneNumber} · {t('personal.personal_id')} •••• {p.personalIdNumberLast4}
          </p>
          <p className="mt-1 text-sm text-stone-600">
            {t('linked.payment')}:{' '}
            {p.payToRepresentative ? t('linked.pay_to_me_short', { last4: myIbanLast4 }) : t('linked.pay_to_own_short', { last4: p.bankAccountLast4 })}
          </p>
        </div>
        <span className={`self-start rounded-full px-3 py-1 text-xs font-medium ${badge[p.consentStatus]}`}>{t(`linked.status.${p.consentStatus}`)}</span>
      </div>

      <div className="mt-4 space-y-3">
        {p.consentStatus === 'confirmed' && (
          <p className="text-sm text-stone-600">
            {t('linked.confirmed_on', { date: date(p.consentedAt), method: t(`linked.method.${p.consentMethod ?? 'link'}`) })}
          </p>
        )}

        {p.consentStatus === 'pending' && (
          <div className="rounded-[var(--radius-control)] bg-stone-50 p-4">
            <p className="text-sm text-stone-700">{t('linked.pending_help', { phone: p.phoneNumber })}</p>
            <form
              className="mt-3 flex flex-col gap-2 sm:flex-row"
              onSubmit={(e) => {
                e.preventDefault()
                void run(async () => onChange(await linkedApi.confirm(p.id, code)))
              }}
            >
              <input
                aria-label={t('linked.code_label')}
                inputMode="numeric"
                autoComplete="one-time-code"
                maxLength={6}
                placeholder="000000"
                value={code}
                onChange={(e) => setCode(e.target.value.replace(/\D/g, ''))}
                className="h-11 w-full rounded-[var(--radius-control)] border border-stone-300 bg-white px-4 text-lg tracking-[0.3em] sm:w-40"
              />
              <Button type="submit" disabled={busy || code.length !== 6}>
                {t('linked.confirm_code')}
              </Button>
            </form>
          </div>
        )}

        {(p.consentStatus === 'declined' || p.consentStatus === 'revoked') && (
          <p className="text-sm text-stone-600">{t(`linked.${p.consentStatus}_help`, { date: date(p.consentWithdrawnAt) })}</p>
        )}

        <FormError code={error} />
        {notice && <Alert tone="success">{notice}</Alert>}

        {confirmRemove ? (
          <Alert tone="error">
            <p>{t('linked.remove_confirm', { name: `${p.firstName} ${p.lastName}` })}</p>
            <div className="mt-3 flex gap-2">
              <Button
                className="bg-red-700 text-white hover:bg-red-800"
                disabled={busy}
                onClick={() => void run(async () => { await linkedApi.remove(p.id); onRemoved() })}
              >
                {t('linked.remove_yes')}
              </Button>
              <Button variant="secondary" onClick={() => setConfirmRemove(false)}>
                {t('profile.cancel')}
              </Button>
            </div>
          </Alert>
        ) : (
          <div className="flex flex-wrap gap-2 pt-1">
            {p.consentStatus !== 'confirmed' && (
              <Button
                variant="secondary"
                disabled={busy || !p.canResendSms}
                title={p.canResendSms ? undefined : t('linked.resend_wait')}
                onClick={() =>
                  void run(async () => {
                    onChange(await linkedApi.resendConsent(p.id))
                    setNotice(t('linked.sms_sent', { phone: p.phoneNumber }))
                  })
                }
              >
                {p.consentStatus === 'pending' ? t('linked.resend') : t('linked.ask_again')}
              </Button>
            )}
            <LinkButton to={`/linked/${p.id}/edit`} variant="secondary">
              {t('review.edit')}
            </LinkButton>
            <Button variant="ghost" className="text-red-700 hover:bg-red-50" onClick={() => setConfirmRemove(true)}>
              {t('linked.remove')}
            </Button>
          </div>
        )}
      </div>
    </Card>
  )
}
