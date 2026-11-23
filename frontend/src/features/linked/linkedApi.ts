import { ApiError, api } from '../../lib/api'
import type { ConsentRequest, LinkedPerson, LinkedPersonInput } from './types'

const base = '/api/v1/linked-persons'

export const linkedApi = {
  list: () => api.get<LinkedPerson[]>(base),
  async get(id: string): Promise<LinkedPerson | null> {
    try {
      return await api.get<LinkedPerson>(`${base}/${id}`)
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null
      throw error
    }
  },
  create: (input: LinkedPersonInput) => api.post<LinkedPerson>(base, input),
  update: (id: string, input: LinkedPersonInput) => api.put<LinkedPerson>(`${base}/${id}`, input),
  remove: (id: string) => api.delete<void>(`${base}/${id}`),
  resendConsent: (id: string) => api.post<LinkedPerson>(`${base}/${id}/resend-consent`),
  confirm: (id: string, code: string) => api.post<LinkedPerson>(`${base}/${id}/confirm`, { code }),
}

/** Public consent page (no sign-in): the token from the SMS link is the only credential. */
export const consentApi = {
  async get(token: string): Promise<ConsentRequest | null> {
    try {
      return await api.get<ConsentRequest>(`/api/v1/consent/${encodeURIComponent(token)}`)
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null
      throw error
    }
  },
  respond: (token: string, action: 'accept' | 'decline' | 'withdraw') =>
    api.post<ConsentRequest>(`/api/v1/consent/${encodeURIComponent(token)}/${action}`),
}
