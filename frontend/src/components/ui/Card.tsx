import type { ReactNode } from 'react'

export function Card({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <div className={`rounded-[var(--radius-card)] border border-stone-200 bg-white p-6 sm:p-8 ${className}`}>{children}</div>
  )
}

/** Centered narrow column for auth forms. */
export function AuthLayout({ title, subtitle, children }: { title: string; subtitle?: string; children: ReactNode }) {
  return (
    <div className="mx-auto w-full max-w-md px-4 py-10 sm:py-16">
      <Card>
        <h1 className="text-2xl font-semibold text-stone-900">{title}</h1>
        {subtitle && <p className="mt-2 text-stone-600">{subtitle}</p>}
        <div className="mt-6">{children}</div>
      </Card>
    </div>
  )
}
