import { useTranslation } from 'react-i18next'
import { Button, LinkButton } from '../../components/ui/Button'

/** Back / Next buttons at the bottom of a wizard step. Next is a submit button for the step's form. */
export function WizardNav({ back, nextLabel, busy }: { back?: string; nextLabel?: string; busy?: boolean }) {
  const { t } = useTranslation()
  return (
    <div className="mt-8 flex flex-col-reverse gap-3 sm:flex-row sm:justify-between">
      {back ? (
        <LinkButton to={back} variant="secondary" size="lg">
          ← {t('wizard.back')}
        </LinkButton>
      ) : (
        <span />
      )}
      <Button type="submit" size="lg" disabled={busy}>
        {busy ? t('form.please_wait') : (nextLabel ?? t('wizard.next'))} {busy ? '' : '→'}
      </Button>
    </div>
  )
}
