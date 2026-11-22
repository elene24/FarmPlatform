import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, Navigate, useNavigate } from 'react-router'
import { FormError } from '../../components/form/FormError'
import { Alert } from '../../components/ui/Alert'
import { Button, LinkButton } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { ApiError } from '../../lib/api'
import { profilesApi } from '../profiles/profilesApi'
import type { CategoryRegistrationInput } from '../profiles/types'
import { useLegalStatuses } from '../profiles/useLegalStatuses'
import { toPersonalInput } from '../profiles/personalForm'
import { digitsOnly, formatIban, parseDecimal } from '../profiles/validation'
import { STEP_PATHS, useWizard, type WizardData, type WizardStep } from './wizardContext'

function toRequest(data: WizardData): CategoryRegistrationInput {
  const p = data.personal
  return {
    picker: data.categories.picker,
    driver: data.categories.driver,
    orchardOwner: data.categories.orchard,
    personalDetails: p && toPersonalInput(p),
    vehicles: data.vehicles.map((v) => ({
      licensePlateNumber: v.licensePlateNumber.trim(),
      model: v.model.trim(),
      passengerCapacity: Number(v.passengerCapacity),
    })),
    orchards: data.orchards.map((o) => ({
      legalEntityStatusId: Number(o.legalEntityStatusId),
      name: o.name.trim(),
      identificationNumber: digitsOnly(o.identificationNumber),
      address: o.address.trim(),
      latitude: o.location?.latitude ?? null,
      longitude: o.location?.longitude ?? null,
      areaHectares: parseDecimal(o.areaHectares),
    })),
  }
}

/** Which step a server error field belongs to. */
function stepOf(field: string): WizardStep {
  if (field.startsWith('personalDetails')) return 'personal'
  if (field.startsWith('vehicles')) return 'vehicles'
  if (field.startsWith('orchards')) return 'orchards'
  return 'categories'
}

/** Steps 4-5: review everything, then submit it in one request. */
export function ReviewStep() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const { data, update, steps, summary, previous } = useWizard()
  const statuses = useLegalStatuses()
  const [busy, setBusy] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  const { categories } = data
  if (!categories.picker && !categories.driver && !categories.orchard) return <Navigate to={STEP_PATHS.categories} replace />
  // A required step was skipped (e.g. page reload lost the in-memory data): send the user back to it.
  const missing = steps.find(
    (s) => (s === 'personal' && !data.personal) || (s === 'vehicles' && !data.vehicles.length) || (s === 'orchards' && !data.orchards.length),
  )
  if (missing) return <Navigate to={STEP_PATHS[missing]} replace />

  const submit = async () => {
    setBusy(true)
    setFormError(null)
    try {
      await profilesApi.register(toRequest(data))
      // The done page clears the wizard data once it is shown (clearing here would bounce back to step 2).
      void navigate('/register/done', { replace: true })
    } catch (error) {
      if (error instanceof ApiError && error.problem.errors && Object.keys(error.problem.errors).length > 0) {
        const serverErrors = error.problem.errors
        update({ serverErrors })
        const firstStep = stepOf(Object.keys(serverErrors)[0]!)
        if (firstStep !== 'categories' && steps.includes(firstStep)) {
          void navigate(STEP_PATHS[firstStep])
          return
        }
      }
      setFormError(error instanceof ApiError ? (error.problem.code ?? 'unknown') : 'unknown')
    } finally {
      setBusy(false)
    }
  }

  const statusName = (id: string) => {
    const s = statuses.find((x) => String(x.id) === id)
    return s ? (i18n.language === 'en' ? s.nameEn : s.nameKa) : '—'
  }
  const chosen = (['picker', 'driver', 'orchard'] as const).filter((k) => categories[k]).map((k) => t(`categories.${k}`))
  const p = data.personal

  return (
    <Card>
      <h1 className="text-2xl font-semibold">{t('review.title')}</h1>
      <p className="mt-2 text-stone-600">{t('review.subtitle')}</p>

      <div className="mt-6 space-y-4">
        <FormError code={formError} />

        <ReviewSection title={t('review.categories')} edit={STEP_PATHS.categories}>
          <p>{chosen.join(', ')}</p>
        </ReviewSection>

        {(categories.picker || categories.driver) && (
          <ReviewSection title={t('personal.title')} edit={p ? STEP_PATHS.personal : undefined}>
            {p ? (
              <Rows
                rows={[
                  [t('review.name'), `${p.firstName} ${p.lastName}`],
                  [t('personal.personal_id'), digitsOnly(p.personalIdNumber)],
                  [t('personal.alternative_phone'), p.alternativePhoneNumber.trim() || '—'],
                  [t('personal.iban'), formatIban(p.bankAccountNumber)],
                  ...(p.bankAccountOwnedByOther
                    ? ([
                        [t('review.account_holder'), `${p.accountHolderFirstName} ${p.accountHolderLastName}`],
                        [t('personal.holder_personal_id'), digitsOnly(p.accountHolderPersonalIdNumber)],
                      ] as [string, string][])
                    : []),
                  [t('personal.legal_address'), p.legalAddress],
                  [t('personal.actual_address'), p.sameAddress ? p.legalAddress : p.actualAddress],
                  [t('personal.location'), p.location ? `${p.location.latitude.toFixed(5)}, ${p.location.longitude.toFixed(5)}` : '—'],
                ]}
              />
            ) : (
              <p className="text-stone-600">
                {t('review.existing_personal', { name: `${summary.personalDetails?.firstName} ${summary.personalDetails?.lastName}` })}
              </p>
            )}
          </ReviewSection>
        )}

        {categories.driver && (
          <ReviewSection title={t('vehicles.title')} edit={data.vehicles.length ? STEP_PATHS.vehicles : undefined}>
            {data.vehicles.length ? (
              <ul className="space-y-1">
                {data.vehicles.map((v, i) => (
                  <li key={i}>
                    <span className="font-medium">{v.licensePlateNumber.toUpperCase()}</span> · {v.model} ·{' '}
                    {t('review.seats', { count: Number(v.passengerCapacity) })}
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-stone-600">{t('review.existing_vehicles', { count: summary.driver.vehicles.length })}</p>
            )}
          </ReviewSection>
        )}

        {categories.orchard && (
          <ReviewSection title={t('orchards.title')} edit={data.orchards.length ? STEP_PATHS.orchards : undefined}>
            {data.orchards.length ? (
              <ul className="space-y-3">
                {data.orchards.map((o, i) => (
                  <li key={i}>
                    <p className="font-medium">{o.name}</p>
                    <p className="text-sm text-stone-600">
                      {statusName(o.legalEntityStatusId)} · {digitsOnly(o.identificationNumber)} · {o.address} ·{' '}
                      {t('review.hectares', { value: o.areaHectares.replace(',', '.') })}
                    </p>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-stone-600">{t('review.existing_orchards', { count: summary.orchardOwner.orchards.length })}</p>
            )}
          </ReviewSection>
        )}

        <Alert tone="info">{t('review.privacy')}</Alert>
      </div>

      <div className="mt-8 flex flex-col-reverse gap-3 sm:flex-row sm:justify-between">
        <LinkButton to={previous('review')} variant="secondary" size="lg">
          ← {t('wizard.back')}
        </LinkButton>
        <Button size="lg" onClick={() => void submit()} disabled={busy}>
          {busy ? t('form.please_wait') : t('review.submit')}
        </Button>
      </div>
    </Card>
  )
}

function ReviewSection({ title, edit, children }: { title: string; edit?: string; children: ReactNode }) {
  const { t } = useTranslation()
  return (
    <section className="rounded-[var(--radius-card)] border border-stone-200 p-4">
      <div className="mb-2 flex items-center justify-between">
        <h2 className="font-semibold text-brand-800">{title}</h2>
        {edit && (
          <Link to={edit} className="text-sm font-medium text-brand-700 hover:underline">
            {t('review.edit')}
          </Link>
        )}
      </div>
      <div className="text-sm text-stone-800">{children}</div>
    </section>
  )
}

function Rows({ rows }: { rows: [string, string][] }) {
  return (
    <dl className="grid gap-x-4 gap-y-1.5 sm:grid-cols-[auto_1fr]">
      {rows.map(([label, value]) => (
        <div key={label} className="contents">
          <dt className="text-stone-500">{label}</dt>
          <dd className="break-words">{value}</dd>
        </div>
      ))}
    </dl>
  )
}
