import { z } from 'zod'

// Messages are error codes; components translate them with t(`errors.${code}`).
// Rules mirror AuthValidator / PhoneNumberNormalizer on the server.

export const PASSWORD_MIN = 10
export const PASSWORD_MAX = 128

/** Same rules as the backend: local Georgian mobile (5XX...), +995/00995/995 prefixes, or any +E.164 number. */
export function normalizePhone(input: string): string | null {
  const trimmed = input.trim()
  if (!trimmed || !/^\+?[0-9\s\-().]+$/.test(trimmed)) return null
  let hasPlus = trimmed.startsWith('+')
  let digits = trimmed.replace(/\D/g, '')
  if (!hasPlus && digits.startsWith('00')) {
    digits = digits.slice(2)
    hasPlus = true
  }
  if (!hasPlus) {
    if (digits.length === 9 && digits.startsWith('5')) digits = '995' + digits
    else if (!(digits.length === 12 && digits.startsWith('995'))) return null
  }
  if (digits.startsWith('995') && digits.length !== 12) return null
  if (digits.length < 8 || digits.length > 15 || digits.startsWith('0')) return null
  return '+' + digits
}

const email = z
  .string()
  .trim()
  .min(1, 'validation.required')
  .max(254, 'validation.email_invalid')
  .pipe(z.email('validation.email_invalid'))

const newPassword = z
  .string()
  .min(1, 'validation.required')
  .min(PASSWORD_MIN, 'validation.password_too_short')
  .max(PASSWORD_MAX, 'validation.password_too_long')

export const loginSchema = z.object({
  email,
  password: z.string().min(1, 'validation.required'),
  rememberMe: z.boolean(),
})

export const registerSchema = z
  .object({
    email,
    phoneNumber: z
      .string()
      .min(1, 'validation.required')
      .refine((v) => normalizePhone(v) !== null, 'validation.phone_invalid'),
    password: newPassword,
    confirmPassword: z.string().min(1, 'validation.required'),
    acceptTerms: z.boolean().refine((v) => v, 'validation.terms_required'),
  })
  .refine((v) => v.password === v.confirmPassword, {
    path: ['confirmPassword'],
    message: 'validation.passwords_mismatch',
  })

export const forgotPasswordSchema = z.object({ email })

export const resetPasswordSchema = z
  .object({
    newPassword,
    confirmPassword: z.string().min(1, 'validation.required'),
  })
  .refine((v) => v.newPassword === v.confirmPassword, {
    path: ['confirmPassword'],
    message: 'validation.passwords_mismatch',
  })

export type LoginValues = z.infer<typeof loginSchema>
export type RegisterValues = z.infer<typeof registerSchema>
export type ForgotPasswordValues = z.infer<typeof forgotPasswordSchema>
export type ResetPasswordValues = z.infer<typeof resetPasswordSchema>
