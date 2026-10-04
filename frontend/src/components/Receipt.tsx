import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

export interface ReceiptRow {
  label: string
  value: ReactNode
  // Money, references and wallet numbers are set in the figures face so the digits line up.
  figures?: boolean
  strong?: boolean
}

// A ruled list of label and value pairs, the way a slip from a bank counter reads.
export function Receipt({ rows, className }: { rows: ReceiptRow[]; className?: string }) {
  return (
    <dl className={cn('divide-y border-y', className)}>
      {rows.map((row) => (
        <div key={row.label} className="flex items-baseline justify-between gap-6 py-3">
          <dt className="text-sm text-muted-foreground">{row.label}</dt>
          <dd className={cn('text-right', row.figures && 'num', row.strong && 'text-lg font-semibold')}>{row.value}</dd>
        </div>
      ))}
    </dl>
  )
}
