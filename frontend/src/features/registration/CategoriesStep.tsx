import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { FormError } from '../../components/form/FormError'
import { Card } from '../../components/ui/Card'
import { WizardNav } from './WizardNav'
import { STEP_PATHS, useWizard, wizardSteps, type Categories } from './wizardContext'

const OPTIONS = [
  { key: 'picker', icon: '🧺' },
  { key: 'driver', icon: '🚐' },
  { key: 'orchard', icon: '🌳' },
] as const

/** Step 2: pick one or more categories. Categories already active on the account are shown as registered. */
export function CategoriesStep() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { data, update, summary } = useWizard()
  const [selected, setSelected] = useState<Categories>(data.categories)
  const [error, setError] = useState<string | null>(null)

  const active: Categories = {
    picker: summary.picker.status.active,
    driver: summary.driver.status.active,
    orchard: summary.orchardOwner.status.active,
  }
  const inactiveButKnown: Categories = {
    picker: summary.picker.status.registered && !active.picker,
    driver: summary.driver.status.registered && !active.driver,
    orchard: summary.orchardOwner.status.registered && !active.orchard,
  }
  const allActive = active.picker && active.driver && active.orchard

  const submit = (e: FormEvent) => {
    e.preventDefault()
    if (!selected.picker && !selected.driver && !selected.orchard) {
      setError('profile.no_category_selected')
      return
    }
    // Switching categories off drops the data entered for them.
    update({
      categories: selected,
      vehicles: selected.driver ? data.vehicles : [],
      orchards: selected.orchard ? data.orchards : [],
      personal: selected.picker || selected.driver ? data.personal : null,
      serverErrors: {},
    })
    const steps = wizardSteps(selected, summary)
    void navigate(STEP_PATHS[steps[1] ?? 'review'])
  }

  return (
    <Card>
      <h1 className="text-2xl font-semibold">{t('categories.title')}</h1>
      <p className="mt-2 text-stone-600">{allActive ? t('categories.all_registered') : t('categories.subtitle')}</p>

      <form onSubmit={submit} className="mt-6">
        <FormError code={error} />
        <fieldset className="mt-2 space-y-3">
          <legend className="sr-only">{t('categories.title')}</legend>
          {OPTIONS.map(({ key, icon }) => {
            const isActive = active[key]
            const checked = isActive || selected[key]
            return (
              <label
                key={key}
                className={`flex cursor-pointer items-start gap-4 rounded-[var(--radius-card)] border-2 p-4 transition-colors ${
                  isActive
                    ? 'cursor-default border-stone-200 bg-stone-50'
                    : checked
                      ? 'border-brand-600 bg-brand-50'
                      : 'border-stone-200 bg-white hover:border-brand-300'
                }`}
              >
                <input
                  type="checkbox"
                  className="mt-1 size-5 shrink-0 accent-brand-700"
                  checked={checked}
                  disabled={isActive}
                  onChange={(e) => {
                    setError(null)
                    setSelected((s) => ({ ...s, [key]: e.target.checked }))
                  }}
                />
                <span className="text-2xl" aria-hidden>
                  {icon}
                </span>
                <span className="flex-1">
                  <span className="block font-semibold text-stone-900">{t(`categories.${key}`)}</span>
                  <span className="mt-0.5 block text-sm text-stone-600">{t(`categories.${key}_text`)}</span>
                  {isActive && <span className="mt-1 inline-block text-xs font-medium text-brand-700">✓ {t('categories.registered')}</span>}
                  {inactiveButKnown[key] && <span className="mt-1 inline-block text-xs font-medium text-accent-600">{t('categories.reactivate')}</span>}
                </span>
              </label>
            )
          })}
        </fieldset>
        {!allActive && <WizardNav back={undefined} />}
      </form>
    </Card>
  )
}
