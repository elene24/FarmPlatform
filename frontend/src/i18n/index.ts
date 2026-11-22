import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import { readPreference, writePreference } from '../lib/storage'
import en from './locales/en.json'
import ka from './locales/ka.json'

export const SUPPORTED_LANGUAGES = ['ka', 'en'] as const
export type Language = (typeof SUPPORTED_LANGUAGES)[number]

const STORAGE_KEY = 'fp.lang'
const stored = readPreference(STORAGE_KEY)
const initial: Language = stored === 'en' || stored === 'ka' ? stored : 'ka' // Georgian is the default

void i18n.use(initReactI18next).init({
  resources: { ka: { translation: ka }, en: { translation: en } },
  lng: initial,
  fallbackLng: 'ka',
  interpolation: { escapeValue: false }, // React already escapes output
})

document.documentElement.lang = initial

i18n.on('languageChanged', (lng) => {
  document.documentElement.lang = lng
  writePreference(STORAGE_KEY, lng)
})

export default i18n
