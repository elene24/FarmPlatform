import type { PersonalDetailsDto, PersonalDetailsInput } from './types'
import { digitsOnly, normalizeIban, type PersonalForm } from './validation'

export const emptyPersonalForm: PersonalForm = {
  firstName: '',
  lastName: '',
  personalIdNumber: '',
  bankAccountNumber: '',
  bankAccountOwnedByOther: false,
  accountHolderFirstName: '',
  accountHolderLastName: '',
  accountHolderPersonalIdNumber: '',
  alternativePhoneNumber: '',
  legalAddress: '',
  sameAddress: false,
  actualAddress: '',
  location: null,
}

/** Edit form defaults from saved details. Sensitive numbers start empty ("leave empty to keep"). */
export function personalFormFrom(dto: PersonalDetailsDto): PersonalForm {
  return {
    ...emptyPersonalForm,
    firstName: dto.firstName,
    lastName: dto.lastName,
    bankAccountOwnedByOther: dto.bankAccountOwnedByOther,
    accountHolderFirstName: dto.accountHolderFirstName ?? '',
    accountHolderLastName: dto.accountHolderLastName ?? '',
    alternativePhoneNumber: dto.alternativePhoneNumber ?? '',
    legalAddress: dto.legalAddress,
    sameAddress: dto.legalAddress === dto.actualAddress,
    actualAddress: dto.legalAddress === dto.actualAddress ? '' : dto.actualAddress,
    location: { latitude: dto.latitude, longitude: dto.longitude },
  }
}

/** Form values -> API body. Empty sensitive numbers become null, which the API treats as "keep". */
export function toPersonalInput(p: PersonalForm): PersonalDetailsInput {
  const orNull = (value: string) => (value.trim() ? value.trim() : null)
  return {
    firstName: p.firstName.trim(),
    lastName: p.lastName.trim(),
    personalIdNumber: orNull(digitsOnly(p.personalIdNumber)),
    bankAccountNumber: orNull(normalizeIban(p.bankAccountNumber)),
    legalAddress: p.legalAddress.trim(),
    actualAddress: (p.sameAddress ? p.legalAddress : p.actualAddress).trim(),
    latitude: p.location?.latitude ?? null,
    longitude: p.location?.longitude ?? null,
    alternativePhoneNumber: orNull(p.alternativePhoneNumber),
    bankAccountOwnedByOther: p.bankAccountOwnedByOther,
    accountHolderFirstName: p.bankAccountOwnedByOther ? p.accountHolderFirstName.trim() : null,
    accountHolderLastName: p.bankAccountOwnedByOther ? p.accountHolderLastName.trim() : null,
    accountHolderPersonalIdNumber: p.bankAccountOwnedByOther ? orNull(digitsOnly(p.accountHolderPersonalIdNumber)) : null,
  }
}
