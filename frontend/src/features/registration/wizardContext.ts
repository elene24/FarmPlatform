import { createContext, useContext } from 'react'
import type { OrchardForm, PersonalForm, VehicleForm } from '../profiles/validation'
import type { ProfilesSummary } from '../profiles/types'

export interface Categories {
  picker: boolean
  driver: boolean
  orchard: boolean
}

/** Everything entered in the wizard. Kept in memory only (never in browser storage: it contains ID/IBAN). */
export interface WizardData {
  categories: Categories
  personal: PersonalForm | null
  vehicles: VehicleForm[]
  orchards: OrchardForm[]
  /** Field errors returned by the server on submit, keyed by API path (e.g. "vehicles.0.model"). */
  serverErrors: Record<string, string[]>
}

export type WizardStep = 'categories' | 'personal' | 'vehicles' | 'orchards' | 'review'

export const STEP_PATHS: Record<WizardStep, string> = {
  categories: '/register/categories',
  personal: '/register/personal',
  vehicles: '/register/driver',
  orchards: '/register/orchard',
  review: '/register/review',
}

/** Which progress-bar step (1-5 from the spec) each wizard page belongs to. */
export const PROGRESS: Record<WizardStep | 'done', number> = {
  categories: 2,
  personal: 3,
  vehicles: 3,
  orchards: 3,
  review: 4,
  done: 5,
}

export const emptyWizard: WizardData = {
  categories: { picker: false, driver: false, orchard: false },
  personal: null,
  vehicles: [],
  orchards: [],
  serverErrors: {},
}

/**
 * The pages the user must go through for the chosen categories. Data that already exists on the account is
 * reused instead of asked again: personal details are shared by Picker and Driver, and a reactivated Driver /
 * Orchard owner keeps their vehicles / orchards.
 */
export function wizardSteps(categories: Categories, summary: ProfilesSummary): WizardStep[] {
  const steps: WizardStep[] = ['categories']
  if ((categories.picker || categories.driver) && !summary.personalDetails) steps.push('personal')
  if (categories.driver && summary.driver.vehicles.length === 0) steps.push('vehicles')
  if (categories.orchard && summary.orchardOwner.orchards.length === 0) steps.push('orchards')
  steps.push('review')
  return steps
}

export interface WizardContextValue {
  data: WizardData
  update: (patch: Partial<WizardData>) => void
  reset: () => void
  summary: ProfilesSummary
  steps: WizardStep[]
  next: (current: WizardStep) => string
  previous: (current: WizardStep) => string
}

export const WizardContext = createContext<WizardContextValue | null>(null)

export function useWizard(): WizardContextValue {
  const value = useContext(WizardContext)
  if (!value) throw new Error('useWizard must be used inside <RegistrationLayout>')
  return value
}

/** Server errors for one form, with the API prefix removed ("personalDetails.firstName" -> "firstName"). */
export function errorsFor(serverErrors: Record<string, string[]>, prefix: string): [string, string][] {
  return Object.entries(serverErrors)
    .filter(([field]) => field.startsWith(prefix))
    .map(([field, codes]) => [field.slice(prefix.length), codes[0] ?? 'validation.invalid'])
}
