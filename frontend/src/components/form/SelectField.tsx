import { useId, type ComponentProps } from 'react'
import { useTranslation } from 'react-i18next'

type Props = ComponentProps<'select'> & { label: string; error?: string; hint?: string }

export function SelectField({ label, error, hint, id, ref, children, ...props }: Props) {
  const { t } = useTranslation()
  const generatedId = useId()
  const selectId = id ?? generatedId

  return (
    <div>
      <label htmlFor={selectId} className="mb-1.5 block text-sm font-medium text-stone-800">
        {label}
      </label>
      <select
        ref={ref}
        id={selectId}
        aria-invalid={error ? true : undefined}
        className={`block h-12 w-full rounded-[var(--radius-control)] border bg-white px-4 text-base text-stone-900 focus:outline-none focus:ring-2 ${
          error ? 'border-red-400 focus:ring-red-200' : 'border-stone-300 focus:border-brand-600 focus:ring-brand-100'
        }`}
        {...props}
      >
        {children}
      </select>
      {error ? (
        <p className="mt-1.5 text-sm text-red-700">{t(`errors.${error}`, { defaultValue: t('errors.unknown') })}</p>
      ) : hint ? (
        <p className="mt-1.5 text-sm text-stone-500">{hint}</p>
      ) : null}
    </div>
  )
}
