import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { CheckboxField } from '../../components/form/CheckboxField'
import { FormError } from '../../components/form/FormError'
import { TextField } from '../../components/form/TextField'
import { Alert } from '../../components/ui/Alert'
import { AuthLayout } from '../../components/ui/Card'
import { Button } from '../../components/ui/Button'
import { authApi } from '../../features/auth/authApi'
import { useAuth } from '../../features/auth/authContext'
import { loginSchema, type LoginValues } from '../../features/auth/validation'
import { applyServerErrors } from '../../lib/formErrors'
import { safeReturnTo } from '../../lib/safeReturnTo'

export function LoginPage() {
  const { t } = useTranslation()
  const { setUser } = useAuth()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const [formError, setFormError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '', rememberMe: true },
  })

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      const user = await authApi.login(values.email, values.password, values.rememberMe)
      setUser(user)
      void navigate(safeReturnTo(params.get('returnTo')) ?? '/dashboard', { replace: true })
    } catch (error) {
      setFormError(applyServerErrors(error, setError, ['email', 'password']))
    }
  })

  return (
    <AuthLayout title={t('login.title')} subtitle={t('login.subtitle')}>
      {params.get('reset') === '1' && (
        <div className="mb-5">
          <Alert tone="success">{t('reset.success')}</Alert>
        </div>
      )}
      <form onSubmit={onSubmit} noValidate className="space-y-5">
        <FormError code={formError} />
        <TextField label={t('fields.email')} type="email" autoComplete="email" inputMode="email" error={errors.email?.message} {...register('email')} />
        <TextField label={t('fields.password')} type="password" autoComplete="current-password" error={errors.password?.message} {...register('password')} />
        <div className="flex items-center justify-between gap-4">
          <CheckboxField label={t('login.remember_me')} {...register('rememberMe')} />
          <Link to="/forgot-password" className="text-sm font-medium text-brand-700 hover:underline">
            {t('login.forgot')}
          </Link>
        </div>
        <Button type="submit" size="lg" className="w-full" disabled={isSubmitting}>
          {isSubmitting ? t('form.please_wait') : t('login.submit')}
        </Button>
      </form>
      <p className="mt-6 text-center text-sm text-stone-600">
        {t('login.no_account')}{' '}
        <Link to="/register" className="font-medium text-brand-700 hover:underline">
          {t('nav.register')}
        </Link>
      </p>
    </AuthLayout>
  )
}
