import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useSearchParams } from 'react-router'
import { FormError } from '../../components/form/FormError'
import { TextField } from '../../components/form/TextField'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { AuthLayout } from '../../components/ui/Card'
import { authApi } from '../../features/auth/authApi'
import { PASSWORD_MIN, resetPasswordSchema, type ResetPasswordValues } from '../../features/auth/validation'
import { applyServerErrors } from '../../lib/formErrors'

/** Opened from the password-reset email: /reset-password?email=...&token=... */
export function ResetPasswordPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const email = params.get('email') ?? ''
  const token = params.get('token') ?? ''
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ResetPasswordValues>({
    resolver: zodResolver(resetPasswordSchema),
    defaultValues: { newPassword: '', confirmPassword: '' },
  })

  if (!email || !token) {
    return (
      <AuthLayout title={t('reset.title')}>
        <Alert tone="error">{t('errors.auth.invalid_token')}</Alert>
        <LinkToForgot />
      </AuthLayout>
    )
  }

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      await authApi.resetPassword(email, token, values.newPassword, values.confirmPassword)
      void navigate('/login?reset=1', { replace: true })
    } catch (error) {
      setFormError(applyServerErrors(error, setError, ['newPassword', 'confirmPassword']))
    }
  })

  return (
    <AuthLayout title={t('reset.title')} subtitle={t('reset.subtitle', { email })}>
      <form onSubmit={onSubmit} noValidate className="space-y-5">
        <FormError code={formError} />
        <TextField
          label={t('fields.new_password')}
          type="password"
          autoComplete="new-password"
          hint={t('register.password_hint', { min: PASSWORD_MIN })}
          error={errors.newPassword?.message}
          {...register('newPassword')}
        />
        <TextField
          label={t('fields.confirm_password')}
          type="password"
          autoComplete="new-password"
          error={errors.confirmPassword?.message}
          {...register('confirmPassword')}
        />
        <Button type="submit" size="lg" className="w-full" disabled={isSubmitting}>
          {isSubmitting ? t('form.please_wait') : t('reset.submit')}
        </Button>
      </form>
      {formError === 'auth.invalid_token' && <LinkToForgot />}
    </AuthLayout>
  )
}

function LinkToForgot() {
  const { t } = useTranslation()
  return (
    <p className="mt-6 text-center text-sm">
      <Link to="/forgot-password" className="font-medium text-brand-700 hover:underline">
        {t('reset.request_new')}
      </Link>
    </p>
  )
}
