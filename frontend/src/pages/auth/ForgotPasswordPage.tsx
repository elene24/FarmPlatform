import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { FormError } from '../../components/form/FormError'
import { TextField } from '../../components/form/TextField'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { AuthLayout } from '../../components/ui/Card'
import { authApi } from '../../features/auth/authApi'
import { forgotPasswordSchema, type ForgotPasswordValues } from '../../features/auth/validation'
import { applyServerErrors } from '../../lib/formErrors'

export function ForgotPasswordPage() {
  const { t } = useTranslation()
  const [sent, setSent] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<ForgotPasswordValues>({ resolver: zodResolver(forgotPasswordSchema), defaultValues: { email: '' } })

  const onSubmit = handleSubmit(async ({ email }) => {
    setFormError(null)
    try {
      await authApi.forgotPassword(email)
      setSent(true)
    } catch (error) {
      setFormError(applyServerErrors(error, setError, ['email']))
    }
  })

  return (
    <AuthLayout title={t('forgot.title')} subtitle={sent ? undefined : t('forgot.subtitle')}>
      {sent ? (
        <Alert tone="success">{t('forgot.sent')}</Alert>
      ) : (
        <form onSubmit={onSubmit} noValidate className="space-y-5">
          <FormError code={formError} />
          <TextField label={t('fields.email')} type="email" autoComplete="email" inputMode="email" error={errors.email?.message} {...register('email')} />
          <Button type="submit" size="lg" className="w-full" disabled={isSubmitting}>
            {isSubmitting ? t('form.please_wait') : t('forgot.submit')}
          </Button>
        </form>
      )}
      <p className="mt-6 text-center text-sm">
        <Link to="/login" className="font-medium text-brand-700 hover:underline">
          {t('forgot.back_to_login')}
        </Link>
      </p>
    </AuthLayout>
  )
}
