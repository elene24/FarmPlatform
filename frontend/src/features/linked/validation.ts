import { z } from 'zod'
import { normalizePhone } from '../auth/validation'
import { digitsOnly, isPersonalNumber, isValidIban, normalizeIban } from '../profiles/validation'
import type { LinkedPerson, LinkedPersonInput } from './types'

const namePattern = /^\p{L}[\p{L}\p{M} .'-]*$/u
const required = z.string().trim().min(1, 'validation.required')

interface Options {
  /** Editing: an empty ID number / IBAN keeps the stored value. */
  editing?: boolean
  hasStoredBankAccount?: boolean
  /** The representative's own numbers: the consent SMS must not go to them. */
  representativePhones?: (string | null | undefined)[]
}

/** Mirrors LinkedPersonValidator + identity checks in LinkedPersonService. */
export function linkedPersonSchema({ editing = false, hasStoredBankAccount = false, representativePhones = [] }: Options = {}) {
  const own = new Set(representativePhones.map((p) => (p ? normalizePhone(p) : null)).filter(Boolean))
  return z
    .object({
      firstName: required.max(100, 'validation.name_invalid').regex(namePattern, 'validation.name_invalid'),
      lastName: required.max(100, 'validation.name_invalid').regex(namePattern, 'validation.name_invalid'),
      personalIdNumber: z.string(),
      phoneNumber: required,
      legalAddress: required.max(300, 'validation.too_long'),
      sameAddress: z.boolean(),
      actualAddress: z.string().max(300, 'validation.too_long'),
      location: z
        .object({ latitude: z.number(), longitude: z.number() })
        .nullable()
        .refine((v): boolean => v !== null, 'validation.location_required'),
      payToRepresentative: z.enum(['own', 'representative']),
      bankAccountNumber: z.string(),
    })
    .superRefine((v, ctx) => {
      const issue = (path: string, message: string) => ctx.addIssue({ code: 'custom', path: [path], message })
      if (!v.personalIdNumber.trim()) {
        if (!editing) issue('personalIdNumber', 'validation.required')
      } else if (!isPersonalNumber(v.personalIdNumber)) {
        issue('personalIdNumber', 'validation.personal_id_invalid')
      }

      const phone = normalizePhone(v.phoneNumber)
      if (v.phoneNumber.trim() && !phone) issue('phoneNumber', 'validation.phone_invalid')
      else if (phone?.startsWith('+995') && phone[4] !== '5') issue('phoneNumber', 'validation.phone_not_mobile')
      else if (phone && own.has(phone)) issue('phoneNumber', 'linked.phone_is_representatives')

      if (v.payToRepresentative === 'own') {
        if (!v.bankAccountNumber.trim()) {
          if (!editing || !hasStoredBankAccount) issue('bankAccountNumber', 'validation.required')
        } else if (!isValidIban(v.bankAccountNumber)) {
          issue('bankAccountNumber', 'validation.iban_invalid')
        }
      }

      if (!v.sameAddress && !v.actualAddress.trim()) issue('actualAddress', 'validation.required')
    })
}

export type LinkedPersonForm = z.infer<ReturnType<typeof linkedPersonSchema>>

export const emptyLinkedForm: LinkedPersonForm = {
  firstName: '',
  lastName: '',
  personalIdNumber: '',
  phoneNumber: '',
  legalAddress: '',
  sameAddress: true,
  actualAddress: '',
  location: null,
  payToRepresentative: 'own',
  bankAccountNumber: '',
}

export function linkedFormFrom(p: LinkedPerson): LinkedPersonForm {
  return {
    ...emptyLinkedForm,
    firstName: p.firstName,
    lastName: p.lastName,
    phoneNumber: p.phoneNumber,
    legalAddress: p.legalAddress,
    sameAddress: p.legalAddress === p.actualAddress,
    actualAddress: p.legalAddress === p.actualAddress ? '' : p.actualAddress,
    location: { latitude: p.latitude, longitude: p.longitude },
    payToRepresentative: p.payToRepresentative ? 'representative' : 'own',
  }
}

export function toLinkedInput(v: LinkedPersonForm): LinkedPersonInput {
  const payToRepresentative = v.payToRepresentative === 'representative'
  return {
    firstName: v.firstName.trim(),
    lastName: v.lastName.trim(),
    personalIdNumber: v.personalIdNumber.trim() ? digitsOnly(v.personalIdNumber) : null,
    phoneNumber: v.phoneNumber.trim(),
    legalAddress: v.legalAddress.trim(),
    actualAddress: (v.sameAddress ? v.legalAddress : v.actualAddress).trim(),
    latitude: v.location?.latitude ?? null,
    longitude: v.location?.longitude ?? null,
    payToRepresentative,
    bankAccountNumber: !payToRepresentative && v.bankAccountNumber.trim() ? normalizeIban(v.bankAccountNumber) : null,
  }
}
