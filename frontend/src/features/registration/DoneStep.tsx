import { useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { LinkButton } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { useWizard } from './wizardContext'

/** Step 5: registration submitted. */
export function DoneStep() {
  const { t } = useTranslation()
  const { reset } = useWizard()

  // Registration is saved: forget the entered data (it contains the ID number and IBAN).
  useEffect(() => reset(), [reset])

  return (
    <Card className="text-center">
      <p className="text-5xl" aria-hidden>
        🎉
      </p>
      <h1 className="mt-4 text-2xl font-semibold">{t('done.title')}</h1>
      <p className="mt-2 text-stone-600">{t('done.text')}</p>
      <LinkButton to="/dashboard" size="lg" className="mt-6">
        {t('done.to_dashboard')}
      </LinkButton>
    </Card>
  )
}
