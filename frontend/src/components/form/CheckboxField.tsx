import { useId, type ComponentProps, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

type Props = Omit<ComponentProps<'input'>, 'type'> & { label: ReactNode; error?: string }

export function CheckboxField({ label, error, id, ref, ...props }: Props) {
  const { t } = useTranslation()
  const generatedId = useId()
  const inputId = id ?? generatedId

  return (
    <div>
      <div className="flex items-start gap-3">
        <input
          ref={ref}
          id={inputId}
          type="checkbox"
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `${inputId}-error` : undefined}
          className="mt-0.5 size-5 shrink-0 rounded border-stone-400 accent-brand-700"
          {...props}
        />
        <label htmlFor={inputId} className="text-sm text-stone-700">
          {label}
        </label>
      </div>
      {error && (
        <p id={`${inputId}-error`} className="mt-1.5 text-sm text-red-700">
          {t(`errors.${error}`, { defaultValue: t('errors.unknown') })}
        </p>
      )}
    </div>
  )
}
