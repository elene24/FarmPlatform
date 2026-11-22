import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'

export function Logo() {
  const { t } = useTranslation()
  return (
    <Link to="/" aria-label={t('app.name')} className="flex shrink-0 items-center gap-2 font-semibold text-brand-800">
      <img src="/favicon.svg" alt="" className="size-8" />
      <span className="hidden text-lg sm:inline">{t('app.name')}</span>
    </Link>
  )
}
