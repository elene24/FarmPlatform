import { useTranslation } from 'react-i18next'
import { SUPPORTED_LANGUAGES, type Language } from '../i18n'

const labels: Record<Language, string> = { ka: 'ქარ', en: 'ENG' }

export function LanguageSwitcher() {
  const { i18n, t } = useTranslation()

  return (
    <div role="group" aria-label={t('nav.language')} className="flex rounded-[var(--radius-control)] border border-stone-300 bg-white p-0.5">
      {SUPPORTED_LANGUAGES.map((lng) => {
        const active = i18n.language === lng
        return (
          <button
            key={lng}
            type="button"
            lang={lng}
            aria-pressed={active}
            onClick={() => void i18n.changeLanguage(lng)}
            className={`rounded-[calc(var(--radius-control)-2px)] px-2.5 py-1 text-xs font-medium ${
              active ? 'bg-brand-700 text-white' : 'text-stone-600 hover:bg-stone-100'
            }`}
          >
            {labels[lng]}
          </button>
        )
      })}
    </div>
  )
}
