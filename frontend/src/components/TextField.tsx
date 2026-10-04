import type { ComponentProps } from 'react'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

interface TextFieldProps extends ComponentProps<'input'> {
  id: string
  label: string
  error?: string | undefined
  hint?: string
}

// A label above the field, a hint if there is one, and the error under the field it belongs to.
// The error sits in a polite live region and the input points at it, so a screen reader reads it.
export function TextField({ id, label, error, hint, ...inputProps }: TextFieldProps) {
  const hintId = `${id}-hint`
  const errorId = `${id}-error`
  const describedBy = [hint ? hintId : null, error ? errorId : null].filter(Boolean).join(' ') || undefined

  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <Input id={id} aria-invalid={error ? true : undefined} aria-describedby={describedBy} {...inputProps} />
      {hint ? (
        <p id={hintId} className="text-sm text-muted-foreground">
          {hint}
        </p>
      ) : null}
      <div aria-live="polite">
        {error ? (
          <p id={errorId} className="text-sm text-destructive">
            {error}
          </p>
        ) : null}
      </div>
    </div>
  )
}
