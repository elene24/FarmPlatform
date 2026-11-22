import { useTranslation } from 'react-i18next'

const STEPS = ['account', 'categories', 'profiles', 'review', 'submit'] as const

/** Progress indicator for the 5-step registration (spec §16). `current` is 1-based. */
export function RegistrationSteps({ current }: { current: number }) {
  const { t } = useTranslation()

  return (
    <nav aria-label={t('steps.label')}>
      <p className="mb-2 text-sm text-stone-500">{t('steps.progress', { current, total: STEPS.length })}</p>
      <ol className="flex gap-1.5">
        {STEPS.map((step, i) => {
          const n = i + 1
          const state = n < current ? 'done' : n === current ? 'current' : 'todo'
          return (
            <li key={step} className="flex-1" aria-current={state === 'current' ? 'step' : undefined}>
              <span
                className={`block h-1.5 rounded-full ${
                  state === 'todo' ? 'bg-stone-200' : state === 'current' ? 'bg-brand-600' : 'bg-brand-300'
                }`}
              />
              <span className={`mt-1.5 hidden text-xs sm:block ${state === 'current' ? 'font-medium text-brand-800' : 'text-stone-500'}`}>
                {t(`steps.${step}`)}
              </span>
            </li>
          )
        })}
      </ol>
    </nav>
  )
}
