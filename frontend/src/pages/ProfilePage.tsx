import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { FormError } from '../components/form/FormError'
import { Alert } from '../components/ui/Alert'
import { Button, LinkButton } from '../components/ui/Button'
import { Card } from '../components/ui/Card'
import { Spinner } from '../components/ui/Spinner'
import { useAuth } from '../features/auth/authContext'
import { PersonalDetailsFields } from '../features/profiles/PersonalDetailsFields'
import { personalFormFrom, toPersonalInput } from '../features/profiles/personalForm'
import { profilesApi } from '../features/profiles/profilesApi'
import type { PersonalDetailsDto } from '../features/profiles/types'
import { personalSchema, type PersonalForm } from '../features/profiles/validation'
import { ApiError } from '../lib/api'
import { applyServerErrors } from '../lib/formErrors'

const FIELDS = [
  'firstName', 'lastName', 'personalIdNumber', 'bankAccountNumber', 'alternativePhoneNumber',
  'accountHolderFirstName', 'accountHolderLastName', 'accountHolderPersonalIdNumber',
  'legalAddress', 'actualAddress', 'location',
] as const

/**
 * Profile management: personal information shared by the Picker and Driver profiles.
 * (Vehicles, orchards, photo and category management are added in Phase 4.)
 */
export function ProfilePage() {
  const { t } = useTranslation()
  const [details, setDetails] = useState<PersonalDetailsDto | null | undefined>(undefined)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [editing, setEditing] = useState(false)
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    let cancelled = false
    profilesApi
      .personalDetails()
      .then((d) => !cancelled && setDetails(d))
      .catch((e: unknown) => !cancelled && setLoadError(e instanceof ApiError ? (e.problem.code ?? 'unknown') : 'unknown'))
    return () => {
      cancelled = true
    }
  }, [])

  if (loadError) {
    return (
      <div className="mx-auto max-w-2xl px-4 py-10">
        <FormError code={loadError} />
      </div>
    )
  }
  if (details === undefined) return <Spinner />

  return (
    <div className="mx-auto max-w-2xl space-y-6 px-4 py-10">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-semibold">{t('profile.title')}</h1>
        <LinkButton to="/dashboard" variant="ghost">
          ← {t('nav.dashboard')}
        </LinkButton>
      </div>

      {details === null ? (
        <Card>
          <p className="text-stone-600">{t('profile.no_personal')}</p>
          <LinkButton to="/register/categories" className="mt-4">
            {t('dashboard.add_category')}
          </LinkButton>
        </Card>
      ) : editing ? (
        <EditPersonal
          details={details}
          onCancel={() => setEditing(false)}
          onSaved={(d) => {
            setDetails(d)
            setEditing(false)
            setSaved(true)
          }}
        />
      ) : (
        <>
          {saved && <Alert tone="success">{t('profile.saved')}</Alert>}
          <ViewPersonal
            details={details}
            onEdit={() => {
              setSaved(false)
              setEditing(true)
            }}
          />
        </>
      )}
    </div>
  )
}

function ViewPersonal({ details: d, onEdit }: { details: PersonalDetailsDto; onEdit: () => void }) {
  const { t } = useTranslation()
  const rows: [string, string][] = [
    [t('review.name'), `${d.firstName} ${d.lastName}`],
    [t('personal.personal_id'), `•••• ${d.personalIdNumberLast4}`],
    [t('personal.alternative_phone'), d.alternativePhoneNumber ?? '—'],
    [t('personal.iban'), `•••• ${d.bankAccountLast4}`],
    [t('profile.account_owner'), d.bankAccountOwnedByOther ? t('profile.owner_other') : t('profile.owner_self')],
  ]
  if (d.bankAccountOwnedByOther) {
    rows.push(
      [t('review.account_holder'), `${d.accountHolderFirstName} ${d.accountHolderLastName}`],
      [t('personal.holder_personal_id'), `•••• ${d.accountHolderPersonalIdLast4}`],
    )
  }
  rows.push(
    [t('personal.legal_address'), d.legalAddress],
    [t('personal.actual_address'), d.actualAddress],
    [t('personal.location'), `${d.latitude.toFixed(5)}, ${d.longitude.toFixed(5)}`],
  )

  return (
    <Card>
      <div className="flex items-start justify-between gap-4">
        <div>
          <h2 className="text-lg font-semibold text-brand-800">{t('personal.title')}</h2>
          <p className="mt-1 text-sm text-stone-500">{t('profile.shared_note')}</p>
        </div>
        <Button variant="secondary" onClick={onEdit}>
          {t('review.edit')}
        </Button>
      </div>
      <dl className="mt-5 grid gap-x-6 gap-y-3 text-sm sm:grid-cols-[auto_1fr]">
        {rows.map(([label, value], i) => (
          <div key={`${i}-${label}`} className="contents">
            <dt className="text-stone-500">{label}</dt>
            <dd className="font-medium break-words">{value}</dd>
          </div>
        ))}
      </dl>
    </Card>
  )
}

function EditPersonal({ details, onCancel, onSaved }: {
  details: PersonalDetailsDto
  onCancel: () => void
  onSaved: (d: PersonalDetailsDto) => void
}) {
  const { t } = useTranslation()
  const { user } = useAuth()
  const [formError, setFormError] = useState<string | null>(null)
  const schema = useMemo(
    () => personalSchema({ editing: true, hasStoredHolderId: details.accountHolderPersonalIdLast4 !== null, primaryPhone: user?.phoneNumber }),
    [details.accountHolderPersonalIdLast4, user?.phoneNumber],
  )

  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<PersonalForm>({ resolver: zodResolver(schema), defaultValues: personalFormFrom(details) })

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      onSaved(await profilesApi.savePersonalDetails(toPersonalInput(values)))
    } catch (error) {
      setFormError(applyServerErrors(error, setError, FIELDS))
    }
  })

  return (
    <Card>
      <h2 className="text-lg font-semibold text-brand-800">{t('personal.title')}</h2>
      <p className="mt-1 text-sm text-stone-500">{t('profile.edit_note')}</p>
      <form onSubmit={onSubmit} noValidate className="mt-6">
        <div className="mb-5">
          <FormError code={formError} />
        </div>
        <PersonalDetailsFields register={register} control={control} errors={errors} saved={details} />
        <div className="mt-8 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <Button variant="secondary" size="lg" onClick={onCancel} disabled={isSubmitting}>
            {t('profile.cancel')}
          </Button>
          <Button type="submit" size="lg" disabled={isSubmitting}>
            {isSubmitting ? t('form.please_wait') : t('profile.save')}
          </Button>
        </div>
      </form>
    </Card>
  )
}
