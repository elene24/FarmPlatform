export type ConsentStatus = 'pending' | 'confirmed' | 'declined' | 'revoked'

export interface LinkedPerson {
  id: string
  firstName: string
  lastName: string
  personalIdNumberLast4: string
  phoneNumber: string
  legalAddress: string
  actualAddress: string
  latitude: number
  longitude: number
  payToRepresentative: boolean
  bankAccountLast4: string | null
  consentStatus: ConsentStatus
  consentMethod: 'code' | 'link' | null
  consentedAt: string | null
  consentWithdrawnAt: string | null
  lastConsentSmsAt: string | null
  canResendSms: boolean
  createdAt: string
}

export interface LinkedPersonInput {
  firstName: string
  lastName: string
  personalIdNumber: string | null
  phoneNumber: string
  legalAddress: string
  actualAddress: string
  latitude: number | null
  longitude: number | null
  payToRepresentative: boolean
  bankAccountNumber: string | null
}

export interface ConsentRequest {
  personFirstName: string
  representativeName: string
  payToRepresentative: boolean
  bankAccountLast4: string | null
  consentStatus: ConsentStatus
}
