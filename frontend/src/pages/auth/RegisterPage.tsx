import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { CheckboxField } from '../../components/form/CheckboxField'
import { FormError } from '../../components/form/FormError'
import { TextField } from '../../components/form/TextField'
import { RegistrationSteps } from '../../components/RegistrationSteps'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { authApi } from '../../features/auth/authApi'
import { useAuth } from '../../features/auth/authContext'
import { PASSWORD_MIN, registerSchema, type RegisterValues } from '../../features/auth/validation'
import { applyServerErrors } from '../../lib/formErrors'

/** Step 1 of registration: create the account. Categories and profiles follow (Phase 3). */
export function RegisterPage() {
  const { t, i18n } = useTranslation()
  const { setUser } = useAuth()
  const [formError, setFormError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { email: '', phoneNumber: '', password: '', confirmPassword: '', acceptTerms: false },
  })

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      // GuestOnly redirects to step 2 as soon as the user is signed in.
      setUser(await authApi.register({ ...values, preferredLanguage: i18n.language }))
    } catch (error) {
      setFormError(
        applyServerErrors(error, setError, ['email', 'phoneNumber', 'password', 'confirmPassword', 'acceptTerms']),
      )
    }
  })

  return (
    <div className="mx-auto w-full max-w-lg px-4 py-10 sm:py-14">
      <RegistrationSteps current={1} />
      <Card className="mt-6">
        <h1 className="text-2xl font-semibold text-stone-900">{t('register.title')}</h1>
        <p className="mt-2 text-stone-600">{t('register.subtitle')}</p>

        <form onSubmit={onSubmit} noValidate className="mt-6 space-y-5">
          <FormError code={formError} />
          <TextField label={t('fields.email')} type="email" autoComplete="email" inputMode="email" error={errors.email?.message} {...register('email')} />
          <TextField
            label={t('fields.phone')}
            type="tel"
            autoComplete="tel"
            inputMode="tel"
            placeholder="5XX XX XX XX"
            hint={t('register.phone_hint')}
            error={errors.phoneNumber?.message}
            {...register('phoneNumber')}
          />
          <TextField
            label={t('fields.password')}
            type="password"
            autoComplete="new-password"
            hint={t('register.password_hint', { min: PASSWORD_MIN })}
            error={errors.password?.message}
            {...register('password')}
          />
          <TextField
            label={t('fields.confirm_password')}
            type="password"
            autoComplete="new-password"
            error={errors.confirmPassword?.message}
            {...register('confirmPassword')}
          />
          <CheckboxField
            error={errors.acceptTerms?.message}
            label={
              <>
                {t('register.terms_prefix')}
                <Link to="/terms" target="_blank" className="font-medium text-brand-700 underline">
                  {t('register.terms_link')}
                </Link>
              </>
            }
            {...register('acceptTerms')}
          />
          <Button type="submit" size="lg" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? t('form.please_wait') : t('register.submit')}
          </Button>
        </form>

        <p className="mt-6 text-center text-sm text-stone-600">
          {t('register.have_account')}{' '}
          <Link to="/login" className="font-medium text-brand-700 hover:underline">
            {t('nav.login')}
          </Link>
        </p>
      </Card>
    </div>
  )
}
