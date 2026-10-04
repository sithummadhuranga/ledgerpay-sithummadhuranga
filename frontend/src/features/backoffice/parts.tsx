import { Link } from 'react-router-dom'
import { ApiError } from '@/lib/api/problem'
import type { StaffTransaction, UserSummary } from '@/lib/api/types'
import { describeError } from '@/lib/errors'
import { formatDateTime, formatMoney } from '@/lib/format'
import { cn } from '@/lib/utils'

// A short word for what is wrong with a customer, or nothing when there is nothing.
export function UserFlags({ user }: { user: Pick<UserSummary, 'walletStatus' | 'locked'> }) {
  const flags = [user.walletStatus === 'Frozen' ? 'Frozen' : null, user.locked ? 'Locked' : null].filter(Boolean)
  return flags.length === 0 ? (
    <span className="text-muted-foreground">Active</span>
  ) : (
    <span className="font-medium text-destructive">{flags.join(', ')}</span>
  )
}

const typeLabel = (item: StaffTransaction) => (item.type === 'TopUp' ? 'Top-up' : 'Transfer')

function Party({ number, name, fallback }: { number: string | null; name: string | null; fallback: string }) {
  if (!number) {
    return <span className="text-muted-foreground">{fallback}</span>
  }
  return (
    <Link to={`/backoffice/users/${number}`} className="underline-offset-4 hover:underline">
      {name ?? 'Customer'} <span className="num text-muted-foreground">{number}</span>
    </Link>
  )
}

// One transaction as staff read it: both parties, the amounts, and why it failed if it did.
export function StaffTransactionList({ items, label }: { items: StaffTransaction[]; label: string }) {
  return (
    <ul aria-label={label} className="divide-y border-y">
      {items.map((item) => (
        <li key={item.reference} className="grid gap-x-6 gap-y-1 py-4 sm:grid-cols-[1fr_auto]">
          <div className="min-w-0">
            <p className="flex flex-wrap items-baseline gap-x-3">
              <Link to={`/backoffice/transactions/${item.reference}`} className="num font-medium underline-offset-4 hover:underline">
                {item.reference}
              </Link>
              <span className="text-sm text-muted-foreground">
                {typeLabel(item)} · {formatDateTime(item.createdAt)}
              </span>
            </p>
            <p className="mt-1 text-sm">
              <Party number={item.senderWalletNumber} name={item.senderName} fallback="Bank" />
              <span className="text-muted-foreground"> to </span>
              <Party number={item.receiverWalletNumber} name={item.receiverName} fallback={item.requestedReceiver ? `${item.requestedReceiver} (no wallet)` : 'No wallet'} />
            </p>
            {item.status === 'Failed' && item.failureCode ? (
              <p className="mt-1 text-sm text-destructive">Failed. {describeError(new ApiError(422, item.failureCode))}</p>
            ) : null}
          </div>
          <div className="num text-right sm:self-start">
            <p className="font-medium">{formatMoney(item.amount)}</p>
            {item.fee > 0 ? <p className="text-sm text-muted-foreground">Fee {formatMoney(item.fee)}</p> : null}
            <p className={cn('mt-0.5 text-xs', item.status === 'Failed' ? 'text-destructive' : 'text-muted-foreground')}>{item.status}</p>
          </div>
        </li>
      ))}
    </ul>
  )
}
