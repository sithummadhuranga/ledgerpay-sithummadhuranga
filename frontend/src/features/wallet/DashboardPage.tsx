import { ArrowLeftRight, ListOrdered } from 'lucide-react'
import { Link } from 'react-router-dom'
import { CopyButton } from '@/components/CopyButton'
import { EmptyState } from '@/components/EmptyState'
import { ErrorState } from '@/components/ErrorState'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { ButtonLink } from '@/components/ButtonLink'
import { Skeleton } from '@/components/ui/skeleton'
import { TransactionTable } from '@/features/transactions/TransactionTable'
import { useRecentTransactions } from '@/features/transactions/queries'
import { describeError } from '@/lib/errors'
import { formatMoney } from '@/lib/format'
import { useMyWallet } from './queries'

const RECENT_COUNT = 5

export function DashboardPage() {
  const wallet = useMyWallet()
  const recent = useRecentTransactions(RECENT_COUNT)

  return (
    <div className="grid gap-10">
      <section aria-label="Balance">
        {wallet.isPending ? (
          <div aria-busy="true" aria-label="Loading your balance" className="rounded-xl bg-panel p-6 sm:p-8">
            <Skeleton className="mb-3 h-4 w-24 bg-panel-border" />
            <Skeleton className="h-12 w-72 max-w-full bg-panel-border" />
            <Skeleton className="mt-6 h-4 w-56 bg-panel-border" />
          </div>
        ) : wallet.isError ? (
          <ErrorState message={`We could not load your balance. ${describeError(wallet.error)}`} onRetry={() => void wallet.refetch()} />
        ) : (
          <>
            {wallet.data.status === 'Frozen' ? (
              <Alert variant="destructive" className="mb-4">
                <AlertDescription>
                  This wallet is frozen. You cannot send or receive money until it is unfrozen. Contact support to ask why.
                </AlertDescription>
              </Alert>
            ) : null}
            <div className="rounded-xl bg-panel p-6 text-panel-foreground sm:p-8">
              <p className="text-sm text-panel-muted">Balance</p>
              <p className="num mt-1 text-4xl font-medium sm:text-5xl">{formatMoney(wallet.data.balance)}</p>
              <div className="mt-5 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-panel-muted">
                <span>Wallet number</span>
                <span className="num text-panel-foreground">{wallet.data.walletNumber}</span>
                <CopyButton value={wallet.data.walletNumber} subject="wallet number" variant="onPanelOutline" />
              </div>
              <div className="mt-8 flex flex-wrap gap-3">
                <ButtonLink to="/send" variant="onPanel">
                  <ArrowLeftRight aria-hidden />
                  Send money
                </ButtonLink>
                <ButtonLink to="/history" variant="onPanelOutline">
                  <ListOrdered aria-hidden />
                  History
                </ButtonLink>
              </div>
            </div>
          </>
        )}
      </section>

      <section aria-label="Recent transactions">
        <div className="mb-3 flex items-baseline justify-between gap-4">
          <h2 className="text-2xl font-semibold">Recent transactions</h2>
          <Link to="/history" className="text-sm font-medium text-primary underline underline-offset-4">
            See all
          </Link>
        </div>
        {recent.isPending ? (
          <div aria-busy="true" aria-label="Loading your transactions" className="grid gap-2">
            {Array.from({ length: RECENT_COUNT }, (_, index) => (
              <Skeleton key={index} className="h-14 w-full" />
            ))}
          </div>
        ) : recent.isError ? (
          <ErrorState message={`We could not load your transactions. ${describeError(recent.error)}`} onRetry={() => void recent.refetch()} />
        ) : recent.data.items.length === 0 ? (
          <EmptyState
            action={
              <ButtonLink to="/send" variant="outline">
                Send money
              </ButtonLink>
            }
          >
            No transactions yet. Money you send or receive will show here.
          </EmptyState>
        ) : (
          <TransactionTable items={recent.data.items} caption="Your last transactions, newest first" compact />
        )}
      </section>
    </div>
  )
}
