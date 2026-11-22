export type Variant = 'primary' | 'accent' | 'secondary' | 'ghost' | 'ghostOnDark'
export type Size = 'md' | 'lg'

const base =
  'inline-flex items-center justify-center gap-2 rounded-[var(--radius-control)] font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-50'

const variants: Record<Variant, string> = {
  primary: 'bg-brand-700 text-white hover:bg-brand-800',
  accent: 'bg-accent-500 text-stone-950 hover:bg-accent-400',
  secondary: 'border border-stone-300 bg-white text-stone-800 hover:bg-stone-100',
  ghost: 'text-stone-700 hover:bg-stone-100',
  ghostOnDark: 'text-white hover:bg-white/10',
}

const sizes: Record<Size, string> = {
  md: 'h-10 px-4 text-sm',
  // Large, thumb-friendly targets for primary actions on phones.
  lg: 'h-12 px-6 text-base',
}

export function buttonClasses(variant: Variant = 'primary', size: Size = 'md', className = ''): string {
  return `${base} ${variants[variant]} ${sizes[size]} ${className}`.trim()
}
