import { useId, type SelectHTMLAttributes } from 'react'
import { Label } from '@/components/ui/label'
import { cn } from '@/lib/utils'

interface Props extends SelectHTMLAttributes<HTMLSelectElement> {
  label: string
  options: { value: string; label: string }[]
}

// A native select, which is the best control on a phone. It looks like the text fields.
export function SelectField({ label, options, className, id, ...rest }: Props) {
  const generated = useId()
  const fieldId = id ?? generated
  return (
    <div className="grid gap-1.5">
      <Label htmlFor={fieldId}>{label}</Label>
      <select
        id={fieldId}
        className={cn(
          'h-11 w-full min-w-0 rounded-md border border-input bg-card px-3 text-base outline-none focus-visible:ring-2 focus-visible:ring-ring md:h-10 md:text-sm',
          className,
        )}
        {...rest}
      >
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </div>
  )
}
