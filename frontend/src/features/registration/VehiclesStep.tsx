import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect } from 'react'
import { useFieldArray, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Navigate, useNavigate } from 'react-router'
import { FormError } from '../../components/form/FormError'
import { TextField } from '../../components/form/TextField'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { vehiclesFormSchema, type VehicleForm } from '../profiles/validation'
import { WizardNav } from './WizardNav'
import { STEP_PATHS, errorsFor, useWizard } from './wizardContext'

const MAX_VEHICLES = 10
const emptyVehicle: VehicleForm = { licensePlateNumber: '', model: '', passengerCapacity: '' }

/** Step 3b (Driver): one or more vehicles, stored separately from personal information. */
export function VehiclesStep() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { data, update, steps, next, previous } = useWizard()

  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<{ vehicles: VehicleForm[] }>({
    resolver: zodResolver(vehiclesFormSchema),
    defaultValues: { vehicles: data.vehicles.length ? data.vehicles : [emptyVehicle] },
  })
  const { fields, append, remove } = useFieldArray({ control, name: 'vehicles' })

  useEffect(() => {
    for (const [field, code] of errorsFor(data.serverErrors, 'vehicles.')) {
      setError(`vehicles.${field}` as `vehicles.${number}.model`, { type: 'server', message: code })
    }
  }, [data.serverErrors, setError])

  if (!steps.includes('vehicles')) return <Navigate to={STEP_PATHS.categories} replace />

  const onSubmit = handleSubmit((values) => {
    update({ vehicles: values.vehicles })
    void navigate(next('vehicles'))
  })

  return (
    <Card>
      <h1 className="text-2xl font-semibold">{t('vehicles.title')}</h1>
      <p className="mt-2 text-stone-600">{t('vehicles.subtitle')}</p>

      <form onSubmit={onSubmit} noValidate className="mt-6 space-y-6">
        <FormError code={errors.vehicles?.root?.message ?? errors.vehicles?.message ?? null} />
        {fields.map((field, index) => (
          <section key={field.id} className="rounded-[var(--radius-card)] border border-stone-200 p-4 sm:p-5">
            <div className="mb-4 flex items-center justify-between">
              <h2 className="font-semibold text-brand-800">{t('vehicles.vehicle_n', { n: index + 1 })}</h2>
              {fields.length > 1 && (
                <button type="button" onClick={() => remove(index)} className="text-sm font-medium text-red-700 hover:underline">
                  {t('vehicles.remove')}
                </button>
              )}
            </div>
            <div className="grid gap-5 sm:grid-cols-2">
              <TextField label={t('vehicles.plate')} placeholder="AA-123-BB" autoCapitalize="characters"
                error={errors.vehicles?.[index]?.licensePlateNumber?.message} {...register(`vehicles.${index}.licensePlateNumber`)} />
              <TextField label={t('vehicles.model')} placeholder="Ford Transit"
                error={errors.vehicles?.[index]?.model?.message} {...register(`vehicles.${index}.model`)} />
              <TextField label={t('vehicles.capacity')} inputMode="numeric" hint={t('vehicles.capacity_hint')}
                error={errors.vehicles?.[index]?.passengerCapacity?.message} {...register(`vehicles.${index}.passengerCapacity`)} />
            </div>
          </section>
        ))}
        {fields.length < MAX_VEHICLES && (
          <Button variant="secondary" onClick={() => append(emptyVehicle)}>
            + {t('vehicles.add')}
          </Button>
        )}
        <WizardNav back={previous('vehicles')} />
      </form>
    </Card>
  )
}
