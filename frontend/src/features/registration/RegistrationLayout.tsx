import { useCallback, useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Outlet, useLocation } from 'react-router'
import { RegistrationSteps } from '../../components/RegistrationSteps'
import { FormError } from '../../components/form/FormError'
import { Spinner } from '../../components/ui/Spinner'
import { useProfilesSummary } from '../profiles/useProfilesSummary'
import { PROGRESS, STEP_PATHS, WizardContext, emptyWizard, wizardSteps, type WizardData, type WizardStep } from './wizardContext'

/**
 * Shell for registration steps 2-5. Holds the wizard data so users can move back and forth between steps
 * without losing anything; it lives only as long as the user stays inside /register/*.
 */
export function RegistrationLayout() {
  const { t } = useTranslation()
  const location = useLocation()
  const { summary, error } = useProfilesSummary()
  const [data, setData] = useState<WizardData>(emptyWizard)

  const update = useCallback((patch: Partial<WizardData>) => setData((d) => ({ ...d, ...patch })), [])
  const reset = useCallback(() => setData(emptyWizard), [])

  const steps = useMemo(() => (summary ? wizardSteps(data.categories, summary) : []), [data.categories, summary])
  const next = useCallback((current: WizardStep) => STEP_PATHS[steps[steps.indexOf(current) + 1] ?? 'review'], [steps])
  const previous = useCallback((current: WizardStep) => STEP_PATHS[steps[steps.indexOf(current) - 1] ?? 'categories'], [steps])

  // Ask before leaving the page with unsaved wizard data.
  const dirty = data.personal !== null || data.vehicles.length > 0 || data.orchards.length > 0
  useEffect(() => {
    if (!dirty) return
    const handler = (e: BeforeUnloadEvent) => e.preventDefault()
    window.addEventListener('beforeunload', handler)
    return () => window.removeEventListener('beforeunload', handler)
  }, [dirty])

  const page = (Object.keys(STEP_PATHS) as WizardStep[]).find((s) => STEP_PATHS[s] === location.pathname)
  const progress = location.pathname === '/register/done' ? PROGRESS.done : page ? PROGRESS[page] : 2

  if (error) {
    return (
      <div className="mx-auto max-w-2xl px-4 py-10">
        <FormError code={error} />
      </div>
    )
  }
  if (!summary) return <Spinner />

  return (
    <WizardContext.Provider value={{ data, update, reset, summary, steps, next, previous }}>
      <div className="mx-auto w-full max-w-2xl px-4 py-8 sm:py-12">
        <RegistrationSteps current={progress} />
        <p className="sr-only">{t('wizard.title')}</p>
        <div className="mt-6">
          <Outlet />
        </div>
      </div>
    </WizardContext.Provider>
  )
}
