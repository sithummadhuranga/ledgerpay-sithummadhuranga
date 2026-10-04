import { cn } from '@/lib/utils'

// Two ruled lines of different length on a green square, the way a debit and a credit sit in a ledger.
export function LogoMark({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 28 28" aria-hidden className={cn('size-7 shrink-0', className)}>
      <rect width="28" height="28" rx="6" fill="currentColor" />
      <path d="M7.5 9.5h13M7.5 14h13M7.5 18.5h7" stroke="var(--primary-foreground)" strokeWidth="2" strokeLinecap="round" fill="none" />
    </svg>
  )
}

export function Logo({ className, onDark = false }: { className?: string; onDark?: boolean }) {
  return (
    <span className={cn('inline-flex items-center gap-2 font-heading text-xl font-semibold tracking-tight', className)}>
      <LogoMark className={onDark ? 'text-panel-foreground [&_path]:stroke-[var(--panel)]' : 'text-primary'} />
      LedgerPay
    </span>
  )
}
