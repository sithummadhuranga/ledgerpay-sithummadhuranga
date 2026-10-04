import type { FieldValues, Path, UseFormReturn } from 'react-hook-form'
import { ApiError } from './api/problem'

// Puts the API's per-field messages under the fields they belong to. The API names a field the way the body
// was written, in camel case. Returns true when it found at least one field, so the screen knows whether
// anything is left to show at the top of the form.
export function showFieldErrors<T extends FieldValues>(form: UseFormReturn<T>, error: unknown, fields: Path<T>[]): boolean {
  if (!(error instanceof ApiError)) {
    return false
  }

  let found = false
  for (const field of fields) {
    const messages = error.errors[field]
    if (messages?.[0]) {
      form.setError(field, { message: messages[0] })
      found = true
    }
  }
  return found
}
