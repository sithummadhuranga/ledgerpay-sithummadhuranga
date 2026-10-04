import { Link } from 'react-router-dom'
import { ErrorState } from '@/components/ErrorState'
import { PageHeader } from '@/components/PageHeader'
import { Skeleton } from '@/components/ui/skeleton'
import { describeError } from '@/lib/errors'
import { formatMoney } from '@/lib/format'
import { StaffTransactionList, UserFlags } from './parts'
import { useStaffTransactions, useUsers } from './queries'

const SHOWN = 5

function Section({ title, count, seeAll, children }: { title: string; count: number | null; seeAll: string; children: React.ReactNode }) {
  return (
    <section aria-label={title}>
      <div className="mb-3 flex flex-wrap items-baseline justify-between gap-3">
        <h2 className="text-xl font-semibold">
          {title}
          {count !== null ? <span className="num ml-2 text-base font-normal text-muted-foreground">{count}</span> : null}
        </h2>
        <Link to={seeAll} className="text-sm underline underline-offset-4">
          See all
        </Link>
      </div>
      {children}
    </section>
  )
}

const Loading = ({ label }: { label: string }) => (
  <div aria-busy="true" aria-label={label} className="grid gap-2">
    <Skeleton className="h-14 w-full" />
    <Skeleton className="h-14 w-full" />
  </div>
)

// What needs a look today: frozen wallets, locked accounts and transfers that were refused.
export function BackOfficePage() {
  const frozen = useUsers({ page: 1, pageSize: SHOWN, status: 'Frozen' })
  const locked = useUsers({ page: 1, pageSize: SHOWN, status: 'Locked' })
  const failed = useStaffTransactions({ page: 1, pageSize: SHOWN, type: 'Transfer', status: 'Failed' })

  return (
    <>
      <PageHeader title="Needs attention" intro="Frozen wallets, locked accounts and transfers that were refused, newest first. Open one to look closer." />
      <div className="grid gap-12">
        <Section title="Frozen wallets" count={frozen.data?.totalCount ?? null} seeAll="/backoffice/users?status=Frozen">
          {frozen.isPending ? (
            <Loading label="Loading frozen wallets" />
          ) : frozen.isError ? (
            <ErrorState message={`We could not load this. ${describeError(frozen.error)}`} onRetry={() => void frozen.refetch()} />
          ) : frozen.data.items.length === 0 ? (
            <p className="text-sm text-muted-foreground">No wallet is frozen.</p>
          ) : (
            <UserRows items={frozen.data.items} />
          )}
        </Section>

        <Section title="Locked accounts" count={locked.data?.totalCount ?? null} seeAll="/backoffice/users?status=Locked">
          {locked.isPending ? (
            <Loading label="Loading locked accounts" />
          ) : locked.isError ? (
            <ErrorState message={`We could not load this. ${describeError(locked.error)}`} onRetry={() => void locked.refetch()} />
          ) : locked.data.items.length === 0 ? (
            <p className="text-sm text-muted-foreground">No account is locked.</p>
          ) : (
            <UserRows items={locked.data.items} />
          )}
        </Section>

        <Section title="Refused transfers" count={failed.data?.totalCount ?? null} seeAll="/backoffice/transactions?type=Transfer&status=Failed">
          {failed.isPending ? (
            <Loading label="Loading refused transfers" />
          ) : failed.isError ? (
            <ErrorState message={`We could not load this. ${describeError(failed.error)}`} onRetry={() => void failed.refetch()} />
          ) : failed.data.items.length === 0 ? (
            <p className="text-sm text-muted-foreground">No transfer has been refused.</p>
          ) : (
            <StaffTransactionList items={failed.data.items} label="Refused transfers" />
          )}
        </Section>
      </div>
    </>
  )
}

function UserRows({ items }: { items: import('@/lib/api/types').UserSummary[] }) {
  return (
    <ul className="divide-y border-y">
      {items.map((user) => (
        <li key={user.walletNumber} className="flex flex-wrap items-baseline justify-between gap-x-6 gap-y-1 py-3">
          <div>
            <Link to={`/backoffice/users/${user.walletNumber}`} className="font-medium underline-offset-4 hover:underline">
              {user.fullName}
            </Link>
            <p className="num text-sm text-muted-foreground">{user.walletNumber}</p>
          </div>
          <div className="num text-right text-sm">
            <p>{formatMoney(user.balance)}</p>
            <UserFlags user={user} />
          </div>
        </li>
      ))}
    </ul>
  )
}
