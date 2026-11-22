import type { ComponentProps } from 'react'
import { Link } from 'react-router'
import { buttonClasses, type Size, type Variant } from './buttonStyles'

type ButtonProps = ComponentProps<'button'> & { variant?: Variant; size?: Size }

export function Button({ variant, size, className, type = 'button', ...props }: ButtonProps) {
  return <button type={type} className={buttonClasses(variant, size, className)} {...props} />
}

type LinkButtonProps = ComponentProps<typeof Link> & { variant?: Variant; size?: Size }

export function LinkButton({ variant, size, className, ...props }: LinkButtonProps) {
  return <Link className={buttonClasses(variant, size, typeof className === 'string' ? className : '')} {...props} />
}
