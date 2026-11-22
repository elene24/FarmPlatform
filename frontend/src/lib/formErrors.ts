import type { FieldValues, Path, UseFormSetError } from 'react-hook-form'
import { ApiError } from './api'

/**
 * Puts server-side field errors (problem.errors: field -> [codes]) onto the matching form inputs and
 * returns the code to show above the form, if any. Field names are camelCase on both sides.
 */
export function applyServerErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  knownFields: readonly Path<T>[],
): string | null {
  if (!(error instanceof ApiError)) return 'unknown'

  let placedOnField = false
  for (const [field, codes] of Object.entries(error.problem.errors ?? {})) {
    if ((knownFields as readonly string[]).includes(field) && codes.length > 0) {
      setError(field as Path<T>, { type: 'server', message: codes[0] })
      placedOnField = true
    }
  }

  // Errors shown next to an input don't also need a banner above the form.
  if (placedOnField) return null
  return error.problem.code ?? 'unknown'
}
