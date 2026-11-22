import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { config } from '../config'

/** Development aid: shows whether the backend (and through it the database) is reachable. */
export function ApiStatus() {
  const { t } = useTranslation()
  const [ok, setOk] = useState<boolean | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    fetch(`${config.apiBaseUrl}/health/ready`, { signal: controller.signal })
      .then((r) => setOk(r.ok))
      .catch(() => {
        if (!controller.signal.aborted) setOk(false)
      })
    return () => controller.abort()
  }, [])

  if (ok === null) return null
  return (
    <span className="inline-flex items-center gap-1.5">
      <span className={`size-2 rounded-full ${ok ? 'bg-brand-500' : 'bg-red-500'}`} aria-hidden />
      {t('footer.api_status')}: {ok ? t('footer.api_ok') : t('footer.api_down')}
    </span>
  )
}
