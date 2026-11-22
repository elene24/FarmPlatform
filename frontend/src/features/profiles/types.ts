export interface GeoPoint {
  latitude: number
  longitude: number
}

export interface PersonalDetailsDto {
  firstName: string
  lastName: string
  personalIdNumberLast4: string
  bankAccountLast4: string
  legalAddress: string
  actualAddress: string
  latitude: number
  longitude: number
  alternativePhoneNumber: string | null
  bankAccountOwnedByOther: boolean
  accountHolderFirstName: string | null
  accountHolderLastName: string | null
  accountHolderPersonalIdLast4: string | null
  updatedAt: string
}

export interface VehicleDto {
  id: string
  licensePlateNumber: string
  model: string
  passengerCapacity: number
}

export interface OrchardDto {
  id: string
  legalEntityStatusId: number
  legalEntityStatusCode: string
  name: string
  identificationNumber: string
  address: string
  latitude: number
  longitude: number
  areaHectares: number
}

export interface CategoryStatus {
  registered: boolean
  active: boolean
  registeredAt: string | null
}

export interface ProfilesSummary {
  personalDetails: PersonalDetailsDto | null
  picker: { status: CategoryStatus }
  driver: { status: CategoryStatus; vehicles: VehicleDto[] }
  orchardOwner: { status: CategoryStatus; orchards: OrchardDto[] }
}

export interface LegalEntityStatus {
  id: number
  code: string
  nameKa: string
  nameEn: string
}

// Request bodies
export interface PersonalDetailsInput {
  firstName: string
  lastName: string
  personalIdNumber: string | null
  bankAccountNumber: string | null
  legalAddress: string
  actualAddress: string
  latitude: number | null
  longitude: number | null
  alternativePhoneNumber: string | null
  bankAccountOwnedByOther: boolean
  accountHolderFirstName: string | null
  accountHolderLastName: string | null
  accountHolderPersonalIdNumber: string | null
}

export interface VehicleInput {
  licensePlateNumber: string
  model: string
  passengerCapacity: number | null
}

export interface OrchardInput {
  legalEntityStatusId: number | null
  name: string
  identificationNumber: string
  address: string
  latitude: number | null
  longitude: number | null
  areaHectares: number | null
}

export interface CategoryRegistrationInput {
  picker: boolean
  driver: boolean
  orchardOwner: boolean
  personalDetails: PersonalDetailsInput | null
  vehicles: VehicleInput[]
  orchards: OrchardInput[]
}
