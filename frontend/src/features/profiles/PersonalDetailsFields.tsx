import { Controller, useWatch, type Control, type FieldErrors, type UseFormRegister } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { CheckboxField } from '../../components/form/CheckboxField'
import { TextField } from '../../components/form/TextField'
import { LocationPicker } from '../maps/LocationPicker'
import type { PersonalDetailsDto } from './types'
import type { PersonalForm } from './validation'

interface Props {
  register: UseFormRegister<PersonalForm>
  control: Control<PersonalForm>
  errors: FieldErrors<PersonalForm>
  /** When editing, the saved (masked) values are shown and sensitive numbers may be left empty to keep them. */
  saved?: PersonalDetailsDto | null
}

/** The personal-details inputs shared by the Picker/Driver registration step and the profile page. */
export function PersonalDetailsFields({ register, control, errors, saved }: Props) {
  const { t } = useTranslation()
  const sameAddress = useWatch({ control, name: 'sameAddress' })
  const ownedByOther = useWatch({ control, name: 'bankAccountOwnedByOther' })
  const keepHint = (last4: string | null | undefined) => (last4 ? t('profile.keep_hint', { last4 }) : undefined)

  return (
    <div className="space-y-5">
      <div className="grid gap-5 sm:grid-cols-2">
        <TextField label={t('personal.first_name')} autoComplete="given-name" error={errors.firstName?.message} {...register('firstName')} />
        <TextField label={t('personal.last_name')} autoComplete="family-name" error={errors.lastName?.message} {...register('lastName')} />
      </div>
      <TextField
        label={t('personal.personal_id')}
        inputMode="numeric"
        maxLength={14}
        hint={keepHint(saved?.personalIdNumberLast4) ?? t('personal.personal_id_hint')}
        error={errors.personalIdNumber?.message}
        {...register('personalIdNumber')}
      />
      <TextField
        label={t('personal.alternative_phone')}
        type="tel"
        inputMode="tel"
        autoComplete="tel"
        placeholder="5XX XX XX XX"
        hint={t('personal.alternative_phone_hint')}
        error={errors.alternativePhoneNumber?.message}
        {...register('alternativePhoneNumber')}
      />

      <div className="space-y-3">
        <TextField
          label={t('personal.iban')}
          autoCapitalize="characters"
          placeholder="GE00 XX00 0000 0000 0000 00"
          hint={keepHint(saved?.bankAccountLast4) ?? t('personal.iban_hint')}
          error={errors.bankAccountNumber?.message}
          {...register('bankAccountNumber')}
        />
        <CheckboxField label={t('personal.not_my_account')} {...register('bankAccountOwnedByOther')} />
        {ownedByOther && (
          <div className="space-y-5 rounded-[var(--radius-card)] border border-stone-200 bg-stone-50 p-4">
            <p className="text-sm text-stone-600">{t('personal.holder_intro')}</p>
            <div className="grid gap-5 sm:grid-cols-2">
              <TextField label={t('personal.holder_first_name')} error={errors.accountHolderFirstName?.message} {...register('accountHolderFirstName')} />
              <TextField label={t('personal.holder_last_name')} error={errors.accountHolderLastName?.message} {...register('accountHolderLastName')} />
            </div>
            <TextField
              label={t('personal.holder_personal_id')}
              inputMode="numeric"
              maxLength={14}
              hint={keepHint(saved?.accountHolderPersonalIdLast4) ?? t('personal.personal_id_hint')}
              error={errors.accountHolderPersonalIdNumber?.message}
              {...register('accountHolderPersonalIdNumber')}
            />
          </div>
        )}
      </div>

      <TextField label={t('personal.legal_address')} autoComplete="street-address" hint={t('personal.legal_address_hint')}
        error={errors.legalAddress?.message} {...register('legalAddress')} />
      <CheckboxField label={t('personal.same_address')} {...register('sameAddress')} />
      {!sameAddress && (
        <TextField label={t('personal.actual_address')} error={errors.actualAddress?.message} {...register('actualAddress')} />
      )}
      <Controller
        control={control}
        name="location"
        render={({ field, fieldState }) => (
          <LocationPicker label={t('personal.location')} value={field.value} onChange={field.onChange} error={fieldState.error?.message} />
        )}
      />
    </div>
  )
}
