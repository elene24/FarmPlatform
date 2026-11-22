import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect } from 'react'
import { Controller, useFieldArray, useForm, useWatch, type Control, type FieldErrors, type UseFormRegister } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Navigate, useNavigate } from 'react-router'
import { FormError } from '../../components/form/FormError'
import { SelectField } from '../../components/form/SelectField'
import { TextField } from '../../components/form/TextField'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { LocationPicker } from '../maps/LocationPicker'
import type { LegalEntityStatus } from '../profiles/types'
import { useLegalStatuses } from '../profiles/useLegalStatuses'
import { orchardsFormSchema, type OrchardForm } from '../profiles/validation'
import { WizardNav } from './WizardNav'
import { STEP_PATHS, errorsFor, useWizard } from './wizardContext'

type FormValues = { orchards: OrchardForm[] }
const emptyOrchard: OrchardForm = {
  legalEntityStatusId: '',
  statusCode: undefined,
  name: '',
  identificationNumber: '',
  address: '',
  areaHectares: '',
  location: null,
}

/** Step 3c (Orchard): one or more orchards, each with its own location. */
export function OrchardsStep() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { data, update, steps, next, previous } = useWizard()
  const statuses = useLegalStatuses()

  const {
    register,
    control,
    handleSubmit,
    setError,
    setValue,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(orchardsFormSchema),
    defaultValues: { orchards: data.orchards.length ? data.orchards : [emptyOrchard] },
  })
  const { fields, append, remove } = useFieldArray({ control, name: 'orchards' })

  useEffect(() => {
    for (const [field, code] of errorsFor(data.serverErrors, 'orchards.')) {
      setError(`orchards.${field}` as `orchards.${number}.name`, { type: 'server', message: code })
    }
  }, [data.serverErrors, setError])

  if (!steps.includes('orchards')) return <Navigate to={STEP_PATHS.categories} replace />

  const onSubmit = handleSubmit((values) => {
    update({ orchards: values.orchards })
    void navigate(next('orchards'))
  })

  return (
    <Card>
      <h1 className="text-2xl font-semibold">{t('orchards.title')}</h1>
      <p className="mt-2 text-stone-600">{t('orchards.subtitle')}</p>

      <form onSubmit={onSubmit} noValidate className="mt-6 space-y-6">
        <FormError code={errors.orchards?.root?.message ?? errors.orchards?.message ?? null} />
        {fields.map((field, index) => (
          <OrchardFields
            key={field.id}
            index={index}
            canRemove={fields.length > 1}
            onRemove={() => remove(index)}
            statuses={statuses}
            register={register}
            control={control}
            errors={errors}
            onStatusChange={(code) => setValue(`orchards.${index}.statusCode`, code)}
          />
        ))}
        <Button variant="secondary" onClick={() => append(emptyOrchard)}>
          + {t('orchards.add')}
        </Button>
        <WizardNav back={previous('orchards')} />
      </form>
    </Card>
  )
}

interface OrchardFieldsProps {
  index: number
  canRemove: boolean
  onRemove: () => void
  statuses: LegalEntityStatus[]
  register: UseFormRegister<FormValues>
  control: Control<FormValues>
  errors: FieldErrors<FormValues>
  onStatusChange: (code: string | undefined) => void
}

function OrchardFields({ index, canRemove, onRemove, statuses, register, control, errors, onStatusChange }: OrchardFieldsProps) {
  const { t, i18n } = useTranslation()
  const statusCode = useWatch({ control, name: `orchards.${index}.statusCode` })
  const e = errors.orchards?.[index]
  const statusField = register(`orchards.${index}.legalEntityStatusId`)

  const idHint =
    statusCode === 'llc' ? t('orchards.id_hint_company') : statusCode ? t('orchards.id_hint_personal') : t('orchards.id_hint')

  return (
    <section className="rounded-[var(--radius-card)] border border-stone-200 p-4 sm:p-5">
      <div className="mb-4 flex items-center justify-between">
        <h2 className="font-semibold text-brand-800">{t('orchards.orchard_n', { n: index + 1 })}</h2>
        {canRemove && (
          <button type="button" onClick={onRemove} className="text-sm font-medium text-red-700 hover:underline">
            {t('orchards.remove')}
          </button>
        )}
      </div>
      <div className="space-y-5">
        <SelectField
          label={t('orchards.legal_status')}
          error={e?.legalEntityStatusId?.message}
          {...statusField}
          onChange={(ev) => {
            void statusField.onChange(ev)
            onStatusChange(statuses.find((s) => String(s.id) === ev.target.value)?.code)
          }}
        >
          <option value="">{t('orchards.choose')}</option>
          {statuses.map((s) => (
            <option key={s.id} value={s.id}>
              {i18n.language === 'en' ? s.nameEn : s.nameKa}
            </option>
          ))}
        </SelectField>
        <TextField label={t('orchards.name')} error={e?.name?.message} {...register(`orchards.${index}.name`)} />
        <TextField label={t('orchards.id_number')} inputMode="numeric" hint={idHint} error={e?.identificationNumber?.message}
          {...register(`orchards.${index}.identificationNumber`)} />
        <div className="grid gap-5 sm:grid-cols-[2fr_1fr]">
          <TextField label={t('orchards.address')} error={e?.address?.message} {...register(`orchards.${index}.address`)} />
          <TextField label={t('orchards.area')} inputMode="decimal" placeholder="12.5" hint={t('orchards.area_hint')}
            error={e?.areaHectares?.message} {...register(`orchards.${index}.areaHectares`)} />
        </div>
        <Controller
          control={control}
          name={`orchards.${index}.location`}
          render={({ field, fieldState }) => (
            <LocationPicker label={t('orchards.location')} value={field.value} onChange={field.onChange} error={fieldState.error?.message} />
          )}
        />
      </div>
    </section>
  )
}
