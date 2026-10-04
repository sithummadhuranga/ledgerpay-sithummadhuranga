import type { ComponentProps } from 'react'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

interface AmountFieldProps extends Omit<ComponentProps<'input'>, 'type'> {
  id: string
  label: string
  error?: string | undefined
}

// An amount typed as text, with LKR in front. The browser keeps the number pad for it on a phone.
export function AmountField({ id, label, error, ...inputProps }: AmountFieldProps) {
  const errorId = `${id}-error`
  return (
    <div className="grid gap-1.5">
      <Label htmlFor={id}>{label}</Label>
      <div className="relative">
        <span aria-hidden className="pointer-events-none absolute inset-y-0 left-3 grid items-center text-sm text-muted-foreground">
          LKR
        </span>
        <Input
          id={id}
          inputMode="decimal"
          autoComplete="off"
          placeholder="0.00"
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? errorId : undefined}
          className="num pl-12"
          {...inputProps}
        />
      </div>
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
