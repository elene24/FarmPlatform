import { useTranslation } from 'react-i18next'
import { LinkButton } from '../components/ui/Button'

/** Stand-in for pages built in later phases, so routing and navigation can be tested now. */
export function PlaceholderPage({ name }: { name: string }) {
  const { t } = useTranslation()
  return (
    <div className="mx-auto max-w-xl px-4 py-20 text-center">
      <p className="text-sm font-medium text-stone-500">{name}</p>
      <h1 className="mt-2 text-2xl font-semibold">{t('placeholder.title')}</h1>
      <p className="mt-2 text-stone-600">{t('placeholder.text')}</p>
      <LinkButton to="/" variant="secondary" className="mt-6">
        {t('placeholder.back')}
      </LinkButton>
    </div>
  )
}
