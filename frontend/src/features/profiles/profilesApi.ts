import { ApiError, api } from '../../lib/api'
import type { CategoryRegistrationInput, LegalEntityStatus, PersonalDetailsDto, PersonalDetailsInput, ProfilesSummary } from './types'

export const profilesApi = {
  summary: () => api.get<ProfilesSummary>('/api/v1/profiles'),
  register: (input: CategoryRegistrationInput) => api.post<ProfilesSummary>('/api/v1/profiles/registration', input),
  /** Null when the user has not entered personal details yet. */
  async personalDetails(): Promise<PersonalDetailsDto | null> {
    try {
      return await api.get<PersonalDetailsDto>('/api/v1/personal-details/me')
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null
      throw error
    }
  },
  savePersonalDetails: (input: PersonalDetailsInput) => api.put<PersonalDetailsDto>('/api/v1/personal-details/me', input),
  legalStatuses: () => api.get<LegalEntityStatus[]>('/api/v1/lookups/legal-entity-statuses'),
}
