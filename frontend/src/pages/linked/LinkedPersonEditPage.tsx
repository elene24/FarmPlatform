import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useMemo, useState } from 'react'
import { Controller, useForm, useWatch } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Navigate, useNavigate, useParams } from 'react-router'
import { CheckboxField } from '../../components/form/CheckboxField'
import { FormError } from '../../components/form/FormError'
import { TextField } from '../../components/form/TextField'
import { Alert } from '../../components/ui/Alert'
import { Button, LinkButton } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { Spinner } from '../../components/ui/Spinner'
import { useAuth } from '../../features/auth/authContext'
import { linkedApi } from '../../features/linked/linkedApi'
import type { LinkedPerson } from '../../features/linked/types'
import { emptyLinkedForm, linkedFormFrom, linkedPersonSchema, toLinkedInput, type LinkedPersonForm } from '../../features/linked/validation'
import { LocationPicker } from '../../features/maps/LocationPicker'
import { profilesApi } from '../../features/profiles/profilesApi'
import type { PersonalDetailsDto } from '../../features/profiles/types'
import { applyServerErrors } from '../../lib/formErrors'

const FIELDS = ['firstName', 'lastName', 'personalIdNumber', 'phoneNumber', 'legalAddress', 'actualAddress', 'location', 'bankAccountNumber'] as const

/** Add (/linked/new) or edit (/linked/:id/edit) a person the user represents. */
export function LinkedPersonEditPage() {
  const { id } = useParams()
  const [existing, setExisting] = useState<LinkedPerson | null | undefined>(id ? undefined : null)
  const [me, setMe] = useState<PersonalDetailsDto | null | undefined>(undefined)

  useEffect(() => {
    let cancelled = false
    Promise.all([id ? linkedApi.get(id) : Promise.resolve(null), profilesApi.personalDetails()])
      .then(([person, details]) => {
        if (cancelled) return
        setExisting(person)
        setMe(details)
      })
      .catch(() => !cancelled && setMe(null))
    return () => {
      cancelled = true
    }
  }, [id])

  if (existing === undefined || me === undefined) return <Spinner />
  if (!me) return <Navigate to="/profile" replace />
  if (id && !existing) return <Navigate to="/profile#linked" replace />
  return <LinkedPersonForm existing={existing} me={me} />
}

function LinkedPersonForm({ existing, me }: { existing: LinkedPerson | null; me: PersonalDetailsDto }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { user } = useAuth()
  const [formError, setFormError] = useState<string | null>(null)
  const editing = existing !== null
  const schema = useMemo(
    () =>
      linkedPersonSchema({
        editing,
        hasStoredBankAccount: existing?.bankAccountLast4 != null,
        representativePhones: [user?.phoneNumber, me.alternativePhoneNumber],
      }),
    [editing, existing?.bankAccountLast4, user?.phoneNumber, me.alternativePhoneNumber],
  )

  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LinkedPersonForm>({ resolver: zodResolver(schema), defaultValues: existing ? linkedFormFrom(existing) : emptyLinkedForm })
  const sameAddress = useWatch({ control, name: 'sameAddress' })
  const payTo = useWatch({ control, name: 'payToRepresentative' })
  const firstName = useWatch({ control, name: 'firstName' })

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      const input = toLinkedInput(values)
      await (existing ? linkedApi.update(existing.id, input) : linkedApi.create(input))
      void navigate('/profile#linked')
    } catch (error) {
      setFormError(applyServerErrors(error, setError, FIELDS))
    }
  })

  const keep = (last4: string | null | undefined) => (last4 ? t('profile.keep_hint', { last4 }) : undefined)
  const repName = `${me.firstName} ${me.lastName}`

  return (
    <div className="mx-auto max-w-2xl px-4 py-10">
      <Card>
        <h1 className="text-2xl font-semibold">{editing ? t('linked.edit_title') : t('linked.add_title')}</h1>
        <p className="mt-2 text-stone-600">{t('linked.form_subtitle')}</p>

        <form onSubmit={onSubmit} noValidate className="mt-6 space-y-5">
          <FormError code={formError} />
          <div className="grid gap-5 sm:grid-cols-2">
            <TextField label={t('personal.first_name')} error={errors.firstName?.message} {...register('firstName')} />
            <TextField label={t('personal.last_name')} error={errors.lastName?.message} {...register('lastName')} />
          </div>
          <TextField label={t('personal.personal_id')} inputMode="numeric" maxLength={14}
            hint={keep(existing?.personalIdNumberLast4) ?? t('personal.personal_id_hint')}
            error={errors.personalIdNumber?.message} {...register('personalIdNumber')} />
          <TextField label={t('linked.phone')} type="tel" inputMode="tel" placeholder="5XX XX XX XX" hint={t('linked.phone_hint')}
            error={errors.phoneNumber?.message} {...register('phoneNumber')} />

          <fieldset className="space-y-3">
            <legend className="mb-1.5 text-sm font-medium text-stone-800">{t('linked.payment')}</legend>
            {(['own', 'representative'] as const).map((value) => (
              <label key={value} className={`flex cursor-pointer items-start gap-3 rounded-[var(--radius-control)] border-2 p-3 ${payTo === value ? 'border-brand-600 bg-brand-50' : 'border-stone-200'}`}>
                <input type="radio" value={value} className="mt-1 size-4 accent-brand-700" {...register('payToRepresentative')} />
                <span className="text-sm">
                  <span className="block font-medium text-stone-900">{value === 'own' ? t('linked.pay_own') : t('linked.pay_me')}</span>
                  <span className="block text-stone-600">
                    {value === 'own' ? t('linked.pay_own_text') : t('linked.pay_me_text', { last4: me.bankAccountLast4 })}
                  </span>
                </span>
              </label>
            ))}
            {payTo === 'own' && (
              <TextField label={t('personal.iban')} autoCapitalize="characters" placeholder="GE00 XX00 0000 0000 0000 00"
                hint={keep(existing?.bankAccountLast4) ?? t('linked.iban_hint')}
                error={errors.bankAccountNumber?.message} {...register('bankAccountNumber')} />
            )}
          </fieldset>

          <TextField label={t('personal.legal_address')} error={errors.legalAddress?.message} {...register('legalAddress')} />
          <CheckboxField label={t('personal.same_address')} {...register('sameAddress')} />
          {!sameAddress && <TextField label={t('personal.actual_address')} error={errors.actualAddress?.message} {...register('actualAddress')} />}
          <Controller
            control={control}
            name="location"
            render={({ field, fieldState }) => (
              <LocationPicker label={t('linked.location')} value={field.value} onChange={field.onChange} error={fieldState.error?.message} />
            )}
          />

          <Alert tone="info">
            <p className="font-medium">{t('linked.consent_box_title')}</p>
            <p className="mt-1">
              {t(payTo === 'representative' ? 'linked.consent_box_pay' : 'linked.consent_box', { name: firstName || '…', rep: repName })}
            </p>
            {editing && <p className="mt-1">{t('linked.consent_box_edit')}</p>}
          </Alert>

          <div className="flex flex-col-reverse gap-3 pt-2 sm:flex-row sm:justify-end">
            <LinkButton to="/profile#linked" variant="secondary" size="lg">
              {t('profile.cancel')}
            </LinkButton>
            <Button type="submit" size="lg" disabled={isSubmitting}>
              {isSubmitting ? t('form.please_wait') : editing ? t('profile.save') : t('linked.save_and_send')}
            </Button>
          </div>
        </form>
      </Card>
    </div>
  )
}
