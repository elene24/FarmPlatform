import { ApiError, api } from '../../lib/api'
import type { AdminLinkedPicker, AdminStats, AdminUser, AdminUserDetail, LinkedStatusFilter, Paged, UserRoleFilter } from './types'

const query = (params: Record<string, string | number | undefined>) => {
  const q = new URLSearchParams()
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== '') q.set(k, String(v))
  return q.toString()
}

export const adminApi = {
  stats: () => api.get<AdminStats>('/api/v1/admin/stats'),
  users: (role: UserRoleFilter, search: string, page: number) =>
    api.get<Paged<AdminUser>>(`/api/v1/admin/users?${query({ role, search, page })}`),
  linkedPickers: (status: LinkedStatusFilter, search: string, page: number) =>
    api.get<Paged<AdminLinkedPicker>>(`/api/v1/admin/linked-pickers?${query({ status, search, page })}`),
  async user(id: string): Promise<AdminUserDetail | null> {
    try {
      return await api.get<AdminUserDetail>(`/api/v1/admin/users/${id}`)
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) return null
      throw error
    }
  },
}

/** Formats an ISO date as DD.MM.YYYY (Georgian) or "23 Nov 2026" (English) without relying on browser locale data. */
export function formatDate(iso: string | null | undefined, language: string) {
  if (!iso) return '—'
  const d = new Date(iso)
  if (language === 'en') return d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' })
  return `${String(d.getDate()).padStart(2, '0')}.${String(d.getMonth() + 1).padStart(2, '0')}.${d.getFullYear()}`
}
