export interface CurrentUser {
  id: string
  email: string
  emailConfirmed: boolean
  phoneNumber: string | null
  preferredLanguage: 'ka' | 'en'
}

export interface RegisterInput {
  email: string
  password: string
  confirmPassword: string
  phoneNumber: string
  acceptTerms: boolean
  preferredLanguage: string
}
