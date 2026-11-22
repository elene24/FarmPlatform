import { z } from 'zod'
import { normalizePhone } from '../auth/validation'

// Mirrors ProfileValidator / GeorgianIdentifiers on the server. Messages are error codes (see errors.* in i18n).

export const digitsOnly = (value: string) => value.replace(/\D/g, '')

export function isPersonalNumber(value: string): boolean {
  return /^[\d ]+$/.test(value.trim()) && digitsOnly(value).length === 11
}

export function isCompanyId(value: string): boolean {
  return /^[\d ]+$/.test(value.trim()) && digitsOnly(value).length === 9
}

export const normalizeIban = (value: string) => value.replace(/[^0-9a-z]/gi, '').toUpperCase()

/** ISO 13616 mod-97 check; Georgian IBANs must be GE + 2 digits + 2 letters + 16 digits (22 chars). */
export function isValidIban(value: string): boolean {
  const iban = normalizeIban(value)
  if (!/^[A-Z]{2}\d{2}[A-Z0-9]{11,30}$/.test(iban)) return false
  if (iban.startsWith('GE') && !/^GE\d{2}[A-Z]{2}\d{16}$/.test(iban)) return false
  const rearranged = iban.slice(4) + iban.slice(0, 4)
  const numeric = rearranged.replace(/[A-Z]/g, (c) => String(c.charCodeAt(0) - 55))
  return BigInt(numeric) % 97n === 1n
}

/** Groups an IBAN in blocks of 4 for display: GE29 NB00 0000 ... */
export const formatIban = (value: string) => normalizeIban(value).replace(/(.{4})/g, '$1 ').trim()

export function isValidIdentificationNumber(value: string, statusCode: string | undefined): boolean {
  if (statusCode === 'llc') return isCompanyId(value)
  if (statusCode === 'individual_entrepreneur' || statusCode === 'natural_person') return isPersonalNumber(value)
  return isCompanyId(value) || isPersonalNumber(value)
}

export function identificationErrorCode(statusCode: string | undefined): string {
  if (statusCode === 'llc') return 'validation.company_id_invalid'
  if (statusCode === 'individual_entrepreneur' || statusCode === 'natural_person') return 'validation.personal_id_invalid'
  return 'validation.identification_number_invalid'
}

/** Accepts "12,5" as well as "12.5". Returns null when not a number. */
export function parseDecimal(value: string): number | null {
  const normalized = value.trim().replace(',', '.')
  if (!/^\d+(\.\d+)?$/.test(normalized)) return null
  return Number(normalized)
}

const required = z.string().trim().min(1, 'validation.required')
const text = (max: number) => required.max(max, 'validation.too_long')
const namePattern = /^\p{L}[\p{L}\p{M} .'-]*$/u

const location = z
  .object({ latitude: z.number(), longitude: z.number() })
  .nullable()
  .refine((v): boolean => v !== null, 'validation.location_required')

interface PersonalSchemaOptions {
  /** Editing saved details: an empty ID number / IBAN means "keep the stored value". */
  editing?: boolean
  /** The account holder's ID is already stored, so it may be left empty when editing. */
  hasStoredHolderId?: boolean
  /** The account's main phone number; the alternative number must be different. */
  primaryPhone?: string | null
}

/**
 * Personal details shared by the Picker and Driver forms (registration step and profile page).
 * When "this bank account does not belong to me" is ticked, the holder's name and ID become required.
 */
export function personalSchema({ editing = false, hasStoredHolderId = false, primaryPhone = null }: PersonalSchemaOptions = {}) {
  return z
    .object({
      firstName: required.max(100, 'validation.name_invalid').regex(namePattern, 'validation.name_invalid'),
      lastName: required.max(100, 'validation.name_invalid').regex(namePattern, 'validation.name_invalid'),
      personalIdNumber: z.string(),
      bankAccountNumber: z.string(),
      bankAccountOwnedByOther: z.boolean(),
      accountHolderFirstName: z.string(),
      accountHolderLastName: z.string(),
      accountHolderPersonalIdNumber: z.string(),
      alternativePhoneNumber: z.string(),
      legalAddress: text(300),
      sameAddress: z.boolean(),
      actualAddress: z.string().max(300, 'validation.too_long'),
      location,
    })
    .superRefine((v, ctx) => {
      const issue = (path: string, message: string) => ctx.addIssue({ code: 'custom', path: [path], message })

      if (!v.personalIdNumber.trim()) {
        if (!editing) issue('personalIdNumber', 'validation.required')
      } else if (!isPersonalNumber(v.personalIdNumber)) {
        issue('personalIdNumber', 'validation.personal_id_invalid')
      }

      if (!v.bankAccountNumber.trim()) {
        if (!editing) issue('bankAccountNumber', 'validation.required')
      } else if (!isValidIban(v.bankAccountNumber)) {
        issue('bankAccountNumber', 'validation.iban_invalid')
      }

      if (v.bankAccountOwnedByOther) {
        for (const field of ['accountHolderFirstName', 'accountHolderLastName'] as const) {
          const value = v[field].trim()
          if (!value) issue(field, 'validation.required')
          else if (value.length > 100 || !namePattern.test(value)) issue(field, 'validation.name_invalid')
        }
        const holderId = v.accountHolderPersonalIdNumber
        if (!holderId.trim()) {
          if (!(editing && hasStoredHolderId)) issue('accountHolderPersonalIdNumber', 'validation.required')
        } else if (!isPersonalNumber(holderId)) {
          issue('accountHolderPersonalIdNumber', 'validation.personal_id_invalid')
        } else if (v.personalIdNumber.trim() && digitsOnly(holderId) === digitsOnly(v.personalIdNumber)) {
          issue('accountHolderPersonalIdNumber', 'validation.holder_is_self')
        }
      }

      if (v.alternativePhoneNumber.trim()) {
        const alternative = normalizePhone(v.alternativePhoneNumber)
        if (!alternative) issue('alternativePhoneNumber', 'validation.phone_invalid')
        else if (primaryPhone && alternative === normalizePhone(primaryPhone)) issue('alternativePhoneNumber', 'validation.phone_same_as_primary')
      }

      if (!v.sameAddress && !v.actualAddress.trim()) issue('actualAddress', 'validation.required')
    })
}

export const vehicleSchema = z.object({
  licensePlateNumber: required.refine((v) => /^[A-Za-z0-9]{4,10}$/.test(v.replace(/[^A-Za-z0-9]/g, '')), 'validation.plate_invalid'),
  model: text(100),
  passengerCapacity: required.refine((v) => /^\d+$/.test(v.trim()) && Number(v) >= 1 && Number(v) <= 60, 'validation.capacity_invalid'),
})

export const vehiclesFormSchema = z.object({ vehicles: z.array(vehicleSchema).min(1, 'profile.vehicle_required') })

export const orchardSchema = z.object({
  legalEntityStatusId: z.string().min(1, 'validation.required'),
  statusCode: z.string().optional(),
  name: text(200),
  identificationNumber: required,
  address: text(300),
  areaHectares: required.refine((v) => {
    const n = parseDecimal(v)
    return n !== null && n > 0 && n <= 100000 && Math.round(n * 10000) / 10000 === n
  }, 'validation.area_invalid'),
  location,
}).superRefine((v, ctx) => {
  if (v.identificationNumber.trim() && !isValidIdentificationNumber(v.identificationNumber, v.statusCode)) {
    ctx.addIssue({ code: 'custom', path: ['identificationNumber'], message: identificationErrorCode(v.statusCode) })
  }
})

export const orchardsFormSchema = z.object({ orchards: z.array(orchardSchema).min(1, 'profile.orchard_required') })

export type PersonalForm = z.infer<ReturnType<typeof personalSchema>>
export type VehicleForm = z.infer<typeof vehicleSchema>
export type OrchardForm = z.infer<typeof orchardSchema>
