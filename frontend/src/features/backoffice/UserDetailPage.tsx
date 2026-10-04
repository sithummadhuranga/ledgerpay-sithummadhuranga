import { Link, useParams } from 'react-router-dom'
import { ButtonLink } from '@/components/ButtonLink'
import { CopyButton } from '@/components/CopyButton'
import { ErrorState } from '@/components/ErrorState'
import { PageHeader } from '@/components/PageHeader'
import { Receipt } from '@/components/Receipt'
import { Skeleton } from '@/components/ui/skeleton'
import { hasRole, useAuth } from '@/features/auth/useAuth'
import { ApiError } from '@/lib/api/problem'
import { describeError } from '@/lib/errors'
import { formatDateTime, formatMoney } from '@/lib/format'
import { StaffTransactionList } from './parts'
import { useUser } from './queries'
import { WalletStatusForm } from './WalletStatusForm'

// One customer: who they are, what is wrong if anything, the latest money, and the one thing staff may do here.
export function UserDetailPage() {
  const { walletNumber = '' } = useParams()
  const { user: staff } = useAuth()
  const person = useUser(walletNumber)

  if (person.isPending) {
    return (
      <div role="status" aria-busy="true" aria-label="Loading the customer" className="grid max-w-xl gap-3">
        <Skeleton className="h-10 w-2/3" />
        <Skeleton className="h-40 w-full" />
      </div>
    )
  }

  if (person.isError) {
    const notFound = person.error instanceof ApiError && person.error.status === 404
    return (
      <>
        <PageHeader title="Customer" />
        <ErrorState message={describeError(person.error)} {...(notFound ? {} : { onRetry: () => void person.refetch() })} />
        <Link to="/backoffice/users" className="mt-4 inline-block text-sm underline underline-offset-4">
          Back to the customers
        </Link>
      </>
    )
  }

  const data = person.data
  const frozen = data.walletStatus === 'Frozen'

  return (
    <>
      <PageHeader title={data.fullName}>
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <span className="num">{data.walletNumber}</span>
          <CopyButton value={data.walletNumber} subject="wallet number" />
        </div>
      </PageHeader>

      <div className="grid gap-10 lg:grid-cols-[1fr_1fr]">
        <section aria-labelledby="profile">
          <h2 id="profile" className="mb-3 text-xl font-semibold">
            Account
          </h2>
          <Receipt
            rows={[
              { label: 'Email', value: data.email },
              { label: 'Mobile', value: data.phone, figures: true },
              { label: 'Balance', value: formatMoney(data.balance), figures: true, strong: true },
              { label: 'Wallet', value: frozen ? 'Frozen' : 'Active' },
              ...(frozen && data.statusReason ? [{ label: 'Freeze reason', value: data.statusReason }] : []),
              ...(data.statusChangedAt ? [{ label: frozen ? 'Frozen' : 'Last changed', value: `${formatDateTime(data.statusChangedAt)}${data.statusChangedBy ? ` by ${data.statusChangedBy}` : ''}` }] : []),
              { label: 'Sign-in', value: data.locked && data.lockedUntil ? `Locked until ${formatDateTime(data.lockedUntil)}` : 'Not locked' },
              { label: 'Wrong passwords since the last sign-in', value: String(data.failedLoginCount), figures: true },
              { label: 'Customer since', value: formatDateTime(data.createdAt) },
            ]}
          />
        </section>

        <section aria-labelledby="actions">
          <h2 id="actions" className="mb-3 text-xl font-semibold">
            {frozen ? 'Unfreeze the wallet' : 'Freeze the wallet'}
          </h2>
          <p className="mb-4 max-w-md text-sm text-muted-foreground">
            A frozen wallet cannot send or receive money. Give a reason, which stays in the audit log.
          </p>
          <WalletStatusForm key={data.walletStatus} walletNumber={data.walletNumber} status={data.walletStatus} />
          {hasRole(staff, 'Operator') ? (
            <ButtonLink to={`/operator/top-up?wallet=${data.walletNumber}`} variant="outline" className="mt-6">
              Top up this wallet
            </ButtonLink>
          ) : null}
        </section>
      </div>

      <section aria-labelledby="recent" className="mt-12">
        <div className="mb-3 flex flex-wrap items-baseline justify-between gap-3">
          <h2 id="recent" className="text-xl font-semibold">
            Latest transactions
          </h2>
          <Link to={`/backoffice/transactions?wallet=${data.walletNumber}`} className="text-sm underline underline-offset-4">
            See all of them
          </Link>
        </div>
        {data.recentTransactions.length === 0 ? (
          <p className="text-sm text-muted-foreground">This wallet has no transactions yet.</p>
        ) : (
          <StaffTransactionList items={data.recentTransactions} label="Latest transactions" />
        )}
      </section>
    </>
  )
}
