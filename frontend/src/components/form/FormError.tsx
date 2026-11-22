import { useTranslation } from 'react-i18next'
import { Alert } from '../ui/Alert'

/** Banner for a form-level error code (e.g. auth.invalid_credentials). */
export function FormError({ code }: { code: string | null }) {
  const { t } = useTranslation()
  if (!code) return null
  return <Alert tone="error">{t(`errors.${code}`, { defaultValue: t('errors.unknown') })}</Alert>
}
