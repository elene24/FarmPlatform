import type { LinkedPerson } from '../linked/types'
import type { ProfilesSummary } from '../profiles/types'

export interface AdminStats {
  users: number
  pickers: number
  drivers: number
  orchardOwners: number
  admins: number
  linkedPickers: number
  linkedPickersConfirmed: number
}

export interface Paged<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export type UserRoleFilter = 'all' | 'picker' | 'driver' | 'orchard_owner' | 'admin' | 'none'
export type LinkedStatusFilter = 'all' | 'pending' | 'confirmed' | 'declined' | 'revoked'

export interface AdminUser {
  id: string
  email: string
  phoneNumber: string | null
  emailConfirmed: boolean
  isActive: boolean
  isAdmin: boolean
  firstName: string | null
  lastName: string | null
  picker: boolean
  driver: boolean
  orchardOwner: boolean
  linkedPickers: number
  createdAt: string
}

export interface AdminUserDetail {
  account: AdminUser
  preferredLanguage: string
  termsAcceptedAt: string | null
  profiles: ProfilesSummary
  linkedPickers: LinkedPerson[]
}

export interface AdminLinkedPicker {
  id: string
  firstName: string
  lastName: string
  personalIdNumberLast4: string
  phoneNumber: string
  actualAddress: string
  payToRepresentative: boolean
  consentStatus: LinkedPerson['consentStatus']
  consentedAt: string | null
  createdAt: string
  representativeId: string
  representativeEmail: string
  representativeFirstName: string | null
  representativeLastName: string | null
}
