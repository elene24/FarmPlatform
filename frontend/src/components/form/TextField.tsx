import { useId, useState, type ComponentProps } from 'react'
import { useTranslation } from 'react-i18next'

type Props = ComponentProps<'input'> & {
  label: string
  /** Error code from validation (translated here). */
  error?: string
  hint?: string
}

const inputClasses =
  'block h-12 w-full rounded-[var(--radius-control)] border bg-white px-4 text-base text-stone-900 placeholder:text-stone-400 focus:outline-none focus:ring-2'

export function TextField({ label, error, hint, id, type = 'text', ref, ...props }: Props) {
  const { t } = useTranslation()
  const generatedId = useId()
  const inputId = id ?? generatedId
  const describedBy = error ? `${inputId}-error` : hint ? `${inputId}-hint` : undefined
  const [visible, setVisible] = useState(false)
  const isPassword = type === 'password'

  return (
    <div>
      <label htmlFor={inputId} className="mb-1.5 block text-sm font-medium text-stone-800">
        {label}
      </label>
      <div className="relative">
        <input
          ref={ref}
          id={inputId}
          type={isPassword && visible ? 'text' : type}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy}
          className={`${inputClasses} ${isPassword ? 'pr-24' : ''} ${
            error ? 'border-red-400 focus:ring-red-200' : 'border-stone-300 focus:border-brand-600 focus:ring-brand-100'
          }`}
          {...props}
        />
        {isPassword && (
          <button
            type="button"
            onClick={() => setVisible((v) => !v)}
            aria-pressed={visible}
            className="absolute inset-y-1 right-1 rounded-[calc(var(--radius-control)-4px)] px-3 text-sm font-medium text-brand-700 hover:bg-brand-50"
          >
            {visible ? t('form.hide') : t('form.show')}
          </button>
        )}
      </div>
      {error ? (
        <p id={`${inputId}-error`} className="mt-1.5 text-sm text-red-700">
          {t(`errors.${error}`, { defaultValue: t('errors.unknown') })}
        </p>
      ) : hint ? (
        <p id={`${inputId}-hint`} className="mt-1.5 text-sm text-stone-500">
          {hint}
        </p>
      ) : null}
    </div>
  )
}
