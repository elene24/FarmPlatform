/** Only allow in-app paths as post-login redirects (prevents open redirects like ?returnTo=https://evil). */
export function safeReturnTo(value: string | null): string | null {
  if (!value) return null
  return value.startsWith('/') && !value.startsWith('//') && !value.startsWith('/\\') ? value : null
}
