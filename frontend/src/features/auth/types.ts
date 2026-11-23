export interface CurrentUser {
  id: string
  email: string
  emailConfirmed: boolean
  phoneNumber: string | null
  preferredLanguage: 'ka' | 'en'
  /** Can open the admin panel (/admin). */
  isAdmin: boolean
}

export interface RegisterInput {
  email: string
  password: string
  confirmPassword: string
  phoneNumber: string
  acceptTerms: boolean
  preferredLanguage: string
}
