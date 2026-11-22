import { useTranslation } from 'react-i18next'
import { LinkButton } from '../components/ui/Button'

export function NotFoundPage() {
  const { t } = useTranslation()
  return (
    <div className="mx-auto max-w-xl px-4 py-20 text-center">
      <p className="text-5xl font-bold text-brand-700">404</p>
      <h1 className="mt-3 text-2xl font-semibold">{t('not_found.title')}</h1>
      <LinkButton to="/" variant="secondary" className="mt-6">
        {t('placeholder.back')}
      </LinkButton>
    </div>
  )
}
