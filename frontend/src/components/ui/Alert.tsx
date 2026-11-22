import type { ReactNode } from 'react'

type Tone = 'error' | 'success' | 'info' | 'warning'

const tones: Record<Tone, string> = {
  error: 'border-red-200 bg-red-50 text-red-800',
  success: 'border-brand-200 bg-brand-50 text-brand-900',
  info: 'border-stone-200 bg-stone-50 text-stone-800',
  warning: 'border-accent-400/40 bg-accent-100 text-stone-900',
}

export function Alert({ tone = 'info', children }: { tone?: Tone; children: ReactNode }) {
  return (
    <div role={tone === 'error' ? 'alert' : 'status'} className={`rounded-[var(--radius-control)] border px-4 py-3 text-sm ${tones[tone]}`}>
      {children}
    </div>
  )
}
