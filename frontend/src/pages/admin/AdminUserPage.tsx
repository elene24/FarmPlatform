import { useEffect, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { FormError } from '../../components/form/FormError'
import { LinkButton } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { Spinner } from '../../components/ui/Spinner'
import { adminApi, formatDate } from '../../features/admin/adminApi'
import type { AdminUserDetail } from '../../features/admin/types'
import { ApiError } from '../../lib/api'
import { Badge } from './AdminPage'

/** One account as the admin sees it. ID numbers and IBANs are only ever shown as their last 4 digits. */
export function AdminUserPage() {
  const { t, i18n } = useTranslation()
  const { id = '' } = useParams()
  const [detail, setDetail] = useState<AdminUserDetail | null | undefined>(undefined)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    adminApi
      .user(id)
      .then((d) => !cancelled && setDetail(d))
      .catch((e: unknown) => !cancelled && setError(e instanceof ApiError ? (e.problem.code ?? 'unknown') : 'unknown'))
    return () => {
      cancelled = true
    }
  }, [id])

  const back = (
    <LinkButton to="/admin" variant="ghost">
      ← {t('admin.title')}
    </LinkButton>
  )
  if (error) return <div className="mx-auto max-w-4xl px-4 py-10">{back}<FormError code={error} /></div>
  if (detail === undefined) return <Spinner />
  if (detail === null) return <div className="mx-auto max-w-4xl space-y-4 px-4 py-10">{back}<p>{t('admin.user_not_found')}</p></div>

  const { account: a, profiles: p, linkedPickers } = detail
  const pd = p.personalDetails
  const date = (iso: string | null | undefined) => formatDate(iso, i18n.language)

  return (
    <div className="mx-auto max-w-4xl space-y-6 px-4 py-10">
      {back}
      <div>
        <h1 className="text-2xl font-semibold">{a.firstName ? `${a.firstName} ${a.lastName}` : a.email}</h1>
        <div className="mt-2">
          {a.picker && <Badge>{t('categories.picker')}</Badge>}
          {a.driver && <Badge>{t('categories.driver')}</Badge>}
          {a.orchardOwner && <Badge>{t('categories.orchard')}</Badge>}
          {a.isAdmin && <Badge tone="dark">{t('admin.badge.admin')}</Badge>}
          {!a.isActive && <Badge tone="red">{t('admin.badge.disabled')}</Badge>}
        </div>
      </div>

      <Section title={t('dashboard.account_title')}>
        <Facts
          rows={[
            [t('fields.email'), `${a.email} (${a.emailConfirmed ? t('dashboard.verified') : t('dashboard.not_verified')})`],
            [t('fields.phone'), a.phoneNumber ?? '—'],
            [t('admin.col.registered'), date(a.createdAt)],
            [t('admin.terms_accepted'), date(detail.termsAcceptedAt)],
            [t('admin.language'), detail.preferredLanguage === 'en' ? 'English' : 'ქართული'],
          ]}
        />
      </Section>

      <Section title={t('personal.title')}>
        {pd ? (
          <Facts
            rows={[
              [t('review.name'), `${pd.firstName} ${pd.lastName}`],
              [t('personal.personal_id'), `•••• ${pd.personalIdNumberLast4}`],
              [t('personal.alternative_phone'), pd.alternativePhoneNumber ?? '—'],
              [t('personal.iban'), `•••• ${pd.bankAccountLast4}`],
              [
                t('profile.account_owner'),
                pd.bankAccountOwnedByOther
                  ? `${pd.accountHolderFirstName} ${pd.accountHolderLastName} (•••• ${pd.accountHolderPersonalIdLast4})`
                  : t('profile.owner_self'),
              ],
              [t('personal.legal_address'), pd.legalAddress],
              [t('personal.actual_address'), pd.actualAddress],
              [t('personal.location'), `${pd.latitude.toFixed(5)}, ${pd.longitude.toFixed(5)}`],
            ]}
          />
        ) : (
          <p className="text-sm text-stone-500">{t('admin.no_personal')}</p>
        )}
      </Section>

      {p.driver.status.registered && (
        <Section title={`${t('categories.driver')} · ${t('admin.vehicles')}`}>
          {p.driver.vehicles.length === 0 ? (
            <p className="text-sm text-stone-500">{t('dashboard.no_vehicles')}</p>
          ) : (
            <ul className="space-y-2 text-sm">
              {p.driver.vehicles.map((v) => (
                <li key={v.id} className="rounded-[var(--radius-control)] bg-stone-50 px-3 py-2">
                  <span className="font-semibold tracking-wide">{v.licensePlateNumber}</span> · {v.model} · {t('review.seats', { count: v.passengerCapacity })}
                </li>
              ))}
            </ul>
          )}
        </Section>
      )}

      {p.orchardOwner.status.registered && (
        <Section title={`${t('categories.orchard')} · ${t('admin.orchards')}`}>
          {p.orchardOwner.orchards.length === 0 ? (
            <p className="text-sm text-stone-500">{t('dashboard.no_orchards')}</p>
          ) : (
            <ul className="space-y-2 text-sm">
              {p.orchardOwner.orchards.map((o) => (
                <li key={o.id} className="rounded-[var(--radius-control)] bg-stone-50 px-3 py-2">
                  <span className="font-semibold">{o.name}</span> · {o.identificationNumber} · {o.areaHectares} {t('admin.ha')}
                  <span className="block text-stone-600">{o.address}</span>
                </li>
              ))}
            </ul>
          )}
        </Section>
      )}

      <Section title={t('linked.title')}>
        {linkedPickers.length === 0 ? (
          <p className="text-sm text-stone-500">{t('admin.no_linked')}</p>
        ) : (
          <ul className="space-y-2 text-sm">
            {linkedPickers.map((l) => (
              <li key={l.id} className="flex flex-col gap-1 rounded-[var(--radius-control)] bg-stone-50 px-3 py-2 sm:flex-row sm:items-center sm:justify-between">
                <span>
                  <span className="font-semibold">{l.firstName} {l.lastName}</span> · {l.phoneNumber} · •••• {l.personalIdNumberLast4}
                  <span className="block text-stone-600">
                    {l.payToRepresentative ? t('admin.pay_rep') : t('admin.pay_own')} · {l.actualAddress}
                  </span>
                </span>
                <Badge tone={l.consentStatus === 'confirmed' ? 'green' : l.consentStatus === 'pending' ? 'amber' : 'stone'}>
                  {t(`linked.status.${l.consentStatus}`)}
                </Badge>
              </li>
            ))}
          </ul>
        )}
        <Link to="/admin?view=linked" className="mt-3 inline-block text-sm text-brand-800 hover:underline">
          {t('admin.all_linked')} →
        </Link>
      </Section>
    </div>
  )
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Card>
      <h2 className="mb-4 text-lg font-semibold text-brand-800">{title}</h2>
      {children}
    </Card>
  )
}

function Facts({ rows }: { rows: [string, string][] }) {
  return (
    <dl className="grid gap-x-6 gap-y-2 text-sm sm:grid-cols-[auto_1fr]">
      {rows.map(([label, value], i) => (
        <div key={`${i}-${label}`} className="contents">
          <dt className="text-stone-500">{label}</dt>
          <dd className="font-medium break-words">{value}</dd>
        </div>
      ))}
    </dl>
  )
}
