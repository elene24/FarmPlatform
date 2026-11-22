import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useMemo } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Navigate, useNavigate } from 'react-router'
import { Card } from '../../components/ui/Card'
import { useAuth } from '../auth/authContext'
import { PersonalDetailsFields } from '../profiles/PersonalDetailsFields'
import { emptyPersonalForm } from '../profiles/personalForm'
import { personalSchema, type PersonalForm } from '../profiles/validation'
import { WizardNav } from './WizardNav'
import { STEP_PATHS, errorsFor, useWizard } from './wizardContext'

/** Step 3a: personal details, shared by the Picker and Driver categories (entered once). */
export function PersonalStep() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { user } = useAuth()
  const { data, update, steps, next, previous } = useWizard()
  const schema = useMemo(() => personalSchema({ primaryPhone: user?.phoneNumber }), [user?.phoneNumber])

  const {
    register,
    handleSubmit,
    control,
    setError,
    formState: { errors },
  } = useForm<PersonalForm>({ resolver: zodResolver(schema), defaultValues: data.personal ?? emptyPersonalForm })

  useEffect(() => {
    for (const [field, code] of errorsFor(data.serverErrors, 'personalDetails.')) {
      setError(field as keyof PersonalForm, { type: 'server', message: code })
    }
  }, [data.serverErrors, setError])

  if (!steps.includes('personal')) return <Navigate to={STEP_PATHS.categories} replace />

  const onSubmit = handleSubmit((values) => {
    update({ personal: values })
    void navigate(next('personal'))
  })

  return (
    <Card>
      <h1 className="text-2xl font-semibold">{t('personal.title')}</h1>
      <p className="mt-2 text-stone-600">{t('personal.subtitle')}</p>

      <form onSubmit={onSubmit} noValidate className="mt-6">
        <PersonalDetailsFields register={register} control={control} errors={errors} />
        <WizardNav back={previous('personal')} />
      </form>
    </Card>
  )
}
