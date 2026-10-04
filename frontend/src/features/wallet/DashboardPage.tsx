import { Alert, AlertDescription } from '@/components/ui/alert'
import { CopyButton } from '@/components/CopyButton'
import { EmptyState } from '@/components/EmptyState'
import { ErrorState } from '@/components/ErrorState'
import { PageHeader } from '@/components/PageHeader'
import { Skeleton } from '@/components/ui/skeleton'
import { TransactionRows } from '@/features/transactions/TransactionRows'
import { useRecentTransactions } from '@/features/transactions/queries'
import { describeError } from '@/lib/errors'
import { formatMoney } from '@/lib/format'
import { useMyWallet } from './queries'

const RECENT_COUNT = 5

export function DashboardPage() {
  const wallet = useMyWallet()
  const recent = useRecentTransactions(RECENT_COUNT)

  return (
    <>
      <PageHeader title="Wallet" />

      {wallet.isPending ? (
        <div aria-busy="true" aria-label="Loading your balance" className="mb-8">
          <Skeleton className="mb-2 h-4 w-24" />
          <Skeleton className="h-10 w-64" />
        </div>
      ) : wallet.isError ? (
        <div className="mb-8">
          <ErrorState message={`We could not load your balance. ${describeError(wallet.error)}`} onRetry={() => void wallet.refetch()} />
        </div>
      ) : (
        <section aria-label="Balance" className="mb-8">
          {wallet.data.status === 'Frozen' ? (
            <Alert variant="destructive" className="mb-4">
              <AlertDescription>
                This wallet is frozen. You cannot send or receive money until it is unfrozen. Contact support to ask why.
              </AlertDescription>
            </Alert>
          ) : null}
          <p className="text-sm text-muted-foreground">Balance</p>
          <p className="num text-4xl font-medium">{formatMoney(wallet.data.balance)}</p>
          <div className="mt-2 flex flex-wrap items-center gap-x-2 text-sm text-muted-foreground">
            <span>Wallet number</span>
            <span className="num text-foreground">{wallet.data.walletNumber}</span>
            <CopyButton value={wallet.data.walletNumber} label="Copy wallet number" />
          </div>
        </section>
      )}

      <section aria-label="Recent transactions">
        <h2 className="mb-2 text-base font-semibold">Recent transactions</h2>
        {recent.isPending ? (
          <div aria-busy="true" aria-label="Loading your transactions" className="grid gap-2">
            {Array.from({ length: RECENT_COUNT }, (_, index) => (
              <Skeleton key={index} className="h-9 w-full" />
            ))}
          </div>
        ) : recent.isError ? (
          <ErrorState message={`We could not load your transactions. ${describeError(recent.error)}`} onRetry={() => void recent.refetch()} />
        ) : recent.data.items.length === 0 ? (
          <EmptyState>No transactions yet. Money you send or receive will show here.</EmptyState>
        ) : (
          <TransactionRows items={recent.data.items} caption="Your last transactions, newest first" />
        )}
      </section>
    </>
  )
}
