import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { EmptyState } from '@/components/EmptyState'
import { ErrorState } from '@/components/ErrorState'
import { PageHeader } from '@/components/PageHeader'
import { BeyondLastPage, Pager } from '@/components/Pager'
import { SelectField } from '@/components/SelectField'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import type { UserFilter } from '@/lib/api/backoffice'
import { describeError } from '@/lib/errors'
import { formatMoney } from '@/lib/format'
import { positive } from '@/lib/params'
import { UserFlags } from './parts'
import { useUsers } from './queries'

const PAGE_SIZE = 10
const statuses = ['Active', 'Frozen', 'Locked'] as const

// Find a customer by part of a name, an email, a mobile number or a wallet number. The search is in the address.
export function UsersPage() {
  const [params, setParams] = useSearchParams()
  const search = params.get('q') ?? ''
  const status = statuses.find((candidate) => candidate === params.get('status'))
  const page = positive(params.get('page'))
  const filter: UserFilter = { page, pageSize: PAGE_SIZE, ...(search ? { search } : {}), ...(status ? { status } : {}) }
  const users = useUsers(filter)

  function go(next: { q: string; status: string }, nextPage = 1) {
    const query = new URLSearchParams()
    if (next.q) query.set('q', next.q)
    if (next.status) query.set('status', next.status)
    if (nextPage > 1) query.set('page', String(nextPage))
    setParams(query)
  }

  return (
    <>
      <PageHeader title="Customers" intro="Find a customer by name, email, mobile number or wallet number. Open one to see the wallet and freeze it." />
      <SearchForm key={`${search}|${status ?? ''}`} initial={{ q: search, status: status ?? '' }} onSubmit={(next) => go(next)} />

      {users.isPending ? (
        <div role="status" aria-busy="true" aria-label="Loading customers" className="grid gap-2">
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} className="h-16 w-full" />
          ))}
        </div>
      ) : users.isError ? (
        <ErrorState message={`We could not load the customers. ${describeError(users.error)}`} onRetry={() => void users.refetch()} />
      ) : users.data.items.length === 0 && page > 1 && users.data.totalPages > 0 ? (
        <BeyondLastPage totalPages={users.data.totalPages} onLast={() => go({ q: search, status: status ?? '' }, users.data.totalPages)} />
      ) : users.data.items.length === 0 ? (
        <EmptyState>{search || status ? 'No customer matches that.' : 'There are no customers yet.'}</EmptyState>
      ) : (
        <div className={users.isPlaceholderData ? 'opacity-60 transition-opacity' : undefined}>
          <ul aria-label="Customers" className="divide-y border-y">
            {users.data.items.map((user) => (
              <li key={user.walletNumber} className="grid gap-x-6 gap-y-1 py-4 sm:grid-cols-[1fr_auto]">
                <div className="min-w-0">
                  <Link to={`/backoffice/users/${user.walletNumber}`} className="font-medium underline-offset-4 hover:underline">
                    {user.fullName}
                  </Link>
                  <p className="mt-0.5 truncate text-sm text-muted-foreground">
                    {user.email} · <span className="num">{user.phone}</span>
                  </p>
                  <p className="num text-sm text-muted-foreground">{user.walletNumber}</p>
                </div>
                <div className="num text-right text-sm sm:self-start">
                  <p className="font-medium">{formatMoney(user.balance)}</p>
                  <p>
                    <UserFlags user={user} />
                  </p>
                </div>
              </li>
            ))}
          </ul>
          <Pager page={users.data.page} totalPages={users.data.totalPages} totalCount={users.data.totalCount} noun={['customer', 'customers']} onPage={(next) => go({ q: search, status: status ?? '' }, next)} />
        </div>
      )}
    </>
  )
}

function SearchForm({ initial, onSubmit }: { initial: { q: string; status: string }; onSubmit: (next: { q: string; status: string }) => void }) {
  const [q, setQ] = useState(initial.q)
  const [status, setStatus] = useState(initial.status)
  return (
    <form
      role="search"
      className="mb-8 flex flex-wrap items-end gap-4"
      onSubmit={(event) => {
        event.preventDefault()
        onSubmit({ q: q.trim(), status })
      }}
    >
      <div className="grid min-w-56 flex-1 gap-1.5">
        <Label htmlFor="search">Search</Label>
        <Input id="search" value={q} maxLength={100} autoComplete="off" onChange={(event) => setQ(event.target.value)} placeholder="Name, email, mobile or wallet number" />
      </div>
      <SelectField
        label="Show"
        value={status}
        onChange={(event) => setStatus(event.target.value)}
        className="w-44"
        options={[{ value: '', label: 'Everyone' }, { value: 'Active', label: 'Active' }, { value: 'Frozen', label: 'Frozen wallets' }, { value: 'Locked', label: 'Locked accounts' }]}
      />
      <Button type="submit">Search</Button>
    </form>
  )
}
