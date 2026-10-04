import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { EmptyState } from '@/components/EmptyState'
import { ErrorState } from '@/components/ErrorState'
import { PageHeader } from '@/components/PageHeader'
import { BeyondLastPage, Pager } from '@/components/Pager'
import { SelectField } from '@/components/SelectField'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import type { StaffTransactionFilter } from '@/lib/api/backoffice'
import { describeError } from '@/lib/errors'
import { isDate, positive } from '@/lib/params'
import { StaffTransactionList } from './parts'
import { useStaffTransactions } from './queries'

const PAGE_SIZE = 10
const types = ['TopUp', 'Transfer'] as const
const statuses = ['Completed', 'Failed'] as const

interface Draft {
  type: string
  status: string
  wallet: string
  from: string
  to: string
}

// Every transaction on the system, newest first, with filters in the address. A reference goes straight to its page.
export function StaffTransactionsPage() {
  const [params, setParams] = useSearchParams()
  const navigate = useNavigate()
  const type = types.find((candidate) => candidate === params.get('type'))
  const status = statuses.find((candidate) => candidate === params.get('status'))
  const wallet = /^[0-9]{12}$/.test(params.get('wallet') ?? '') ? params.get('wallet')! : ''
  const from = isDate(params.get('from')) ? params.get('from')! : ''
  const to = isDate(params.get('to')) ? params.get('to')! : ''
  const page = positive(params.get('page'))
  const draft: Draft = { type: type ?? '', status: status ?? '', wallet, from, to }

  const filter: StaffTransactionFilter = {
    page,
    pageSize: PAGE_SIZE,
    ...(type ? { type } : {}),
    ...(status ? { status } : {}),
    ...(wallet ? { walletNumber: wallet } : {}),
    ...(from ? { from } : {}),
    ...(to ? { to } : {}),
  }
  const transactions = useStaffTransactions(filter)

  function go(next: Draft, nextPage = 1) {
    const query = new URLSearchParams()
    for (const [key, value] of Object.entries(next)) {
      if (value) query.set(key, value)
    }
    if (nextPage > 1) query.set('page', String(nextPage))
    setParams(query)
  }

  return (
    <>
      <PageHeader title="Transactions" intro="Every top-up and transfer, newest first. The date filter counts whole days in UTC." />
      <form
        role="search"
        aria-label="Find a transaction by its reference"
        className="mb-6 flex max-w-xl flex-wrap items-end gap-3"
        onSubmit={(event) => {
          event.preventDefault()
          const reference = String(new FormData(event.currentTarget).get('reference') ?? '').trim().toUpperCase()
          if (reference) navigate(`/backoffice/transactions/${encodeURIComponent(reference)}`)
        }}
      >
        <div className="grid min-w-56 flex-1 gap-1.5">
          <Label htmlFor="reference">Reference</Label>
          <Input id="reference" name="reference" autoComplete="off" placeholder="TX…" className="num" />
        </div>
        <Button type="submit" variant="outline">
          Find
        </Button>
      </form>

      <Filters key={JSON.stringify(draft)} initial={draft} onApply={(next) => go(next)} onClear={() => setParams(new URLSearchParams())} filtered={Object.values(draft).some(Boolean)} />

      {transactions.isPending ? (
        <div role="status" aria-busy="true" aria-label="Loading transactions" className="grid gap-2">
          {Array.from({ length: 5 }, (_, index) => (
            <Skeleton key={index} className="h-20 w-full" />
          ))}
        </div>
      ) : transactions.isError ? (
        <ErrorState message={`We could not load the transactions. ${describeError(transactions.error)}`} onRetry={() => void transactions.refetch()} />
      ) : transactions.data.items.length === 0 && page > 1 && transactions.data.totalPages > 0 ? (
        <BeyondLastPage totalPages={transactions.data.totalPages} onLast={() => go(draft, transactions.data.totalPages)} />
      ) : transactions.data.items.length === 0 ? (
        <EmptyState>{Object.values(draft).some(Boolean) ? 'No transaction matches those filters.' : 'There are no transactions yet.'}</EmptyState>
      ) : (
        <div className={transactions.isPlaceholderData ? 'opacity-60 transition-opacity' : undefined}>
          <StaffTransactionList items={transactions.data.items} label="Transactions" />
          <Pager
            page={transactions.data.page}
            totalPages={transactions.data.totalPages}
            totalCount={transactions.data.totalCount}
            noun={['transaction', 'transactions']}
            onPage={(next) => go(draft, next)}
          />
        </div>
      )}
    </>
  )
}

function Filters({ initial, onApply, onClear, filtered }: { initial: Draft; onApply: (next: Draft) => void; onClear: () => void; filtered: boolean }) {
  const [draft, setDraft] = useState(initial)
  const reversed = !!draft.from && !!draft.to && draft.from > draft.to
  const set = (key: keyof Draft, value: string) => setDraft({ ...draft, [key]: value })

  return (
    <form
      aria-label="Filters"
      className="mb-8 flex flex-wrap items-end gap-4"
      onSubmit={(event) => {
        event.preventDefault()
        if (!reversed) onApply({ ...draft, wallet: draft.wallet.trim() })
      }}
    >
      <SelectField label="Type" value={draft.type} onChange={(event) => set('type', event.target.value)} className="w-36" options={[{ value: '', label: 'All' }, { value: 'TopUp', label: 'Top-ups' }, { value: 'Transfer', label: 'Transfers' }]} />
      <SelectField label="Status" value={draft.status} onChange={(event) => set('status', event.target.value)} className="w-36" options={[{ value: '', label: 'All' }, { value: 'Completed', label: 'Completed' }, { value: 'Failed', label: 'Failed' }]} />
      <div className="grid gap-1.5">
        <Label htmlFor="wallet">Wallet number</Label>
        <Input id="wallet" value={draft.wallet} inputMode="numeric" maxLength={12} autoComplete="off" className="num w-44" onChange={(event) => set('wallet', event.target.value)} />
      </div>
      <div className="grid gap-1.5">
        <Label htmlFor="from">From</Label>
        <Input id="from" type="date" value={draft.from} className="w-44" onChange={(event) => set('from', event.target.value)} />
      </div>
      <div className="grid gap-1.5">
        <Label htmlFor="to">To</Label>
        <Input id="to" type="date" value={draft.to} className="w-44" aria-invalid={reversed ? true : undefined} aria-describedby={reversed ? 'range-error' : undefined} onChange={(event) => set('to', event.target.value)} />
      </div>
      <Button type="submit" disabled={reversed}>
        Apply
      </Button>
      {filtered ? (
        <Button type="button" variant="ghost" onClick={onClear}>
          Clear
        </Button>
      ) : null}
      <div aria-live="polite" className="basis-full">
        {reversed ? (
          <p id="range-error" className="text-sm text-destructive">
            The from date is after the to date.
          </p>
        ) : null}
      </div>
    </form>
  )
}
