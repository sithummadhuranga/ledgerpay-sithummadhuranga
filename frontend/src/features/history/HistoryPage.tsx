import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { EmptyState } from '@/components/EmptyState'
import { ErrorState } from '@/components/ErrorState'
import { PageHeader } from '@/components/PageHeader'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { TransactionTable } from '@/features/transactions/TransactionTable'
import { useHistory } from '@/features/transactions/queries'
import { describeError } from '@/lib/errors'

const PAGE_SIZE = 10
const isDate = (value: string | null): value is string => !!value && /^\d{4}-\d{2}-\d{2}$/.test(value)

// The inputs start from the address. When the address changes (Back, Forward, Clear) the key on this component
// makes it start again from the new dates.
function DateFilter({ from, to, onApply }: { from: string; to: string; onApply: (next: { from: string; to: string }) => void }) {
  const [draft, setDraft] = useState({ from, to })
  const reversed = !!draft.from && !!draft.to && draft.from > draft.to

  return (
    <form
      className="mb-8 flex flex-wrap items-end gap-4"
      onSubmit={(event) => {
        event.preventDefault()
        if (!reversed) {
          onApply(draft)
        }
      }}
    >
      <div className="grid gap-1.5">
        <Label htmlFor="from">From</Label>
        <Input id="from" type="date" value={draft.from} onChange={(event) => setDraft({ ...draft, from: event.target.value })} className="w-44" />
      </div>
      <div className="grid gap-1.5">
        <Label htmlFor="to">To</Label>
        <Input
          id="to"
          type="date"
          value={draft.to}
          aria-invalid={reversed ? true : undefined}
          aria-describedby={reversed ? 'range-error' : undefined}
          onChange={(event) => setDraft({ ...draft, to: event.target.value })}
          className="w-44"
        />
      </div>
      <Button type="submit" disabled={reversed}>
        Apply
      </Button>
      {from || to ? (
        <Button type="button" variant="ghost" onClick={() => onApply({ from: '', to: '' })}>
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

// The filters live in the address, so a refresh or a shared link keeps them.
export function HistoryPage() {
  const [params, setParams] = useSearchParams()
  const from = isDate(params.get('from')) ? params.get('from')! : ''
  const to = isDate(params.get('to')) ? params.get('to')! : ''
  const page = Math.max(1, Math.floor(Number(params.get('page'))) || 1)
  const history = useHistory({ page, pageSize: PAGE_SIZE, ...(from ? { from } : {}), ...(to ? { to } : {}) })
  function apply(next: { from: string; to: string }, nextPage = 1) {
    const search = new URLSearchParams()
    if (next.from) search.set('from', next.from)
    if (next.to) search.set('to', next.to)
    if (nextPage > 1) search.set('page', String(nextPage))
    setParams(search)
  }

  const data = history.data

  return (
    <>
      <PageHeader title="History" intro="Every entry on your wallet, newest first. The date filter counts whole days in UTC, and times are shown in your time zone." />

      <DateFilter key={`${from}|${to}`} from={from} to={to} onApply={apply} />

      {history.isPending ? (
        <div role="status" aria-busy="true" aria-label="Loading your transactions" className="grid gap-2">
          {Array.from({ length: 6 }, (_, index) => (
            <Skeleton key={index} className="h-14 w-full" />
          ))}
        </div>
      ) : history.isError ? (
        <ErrorState message={`We could not load your transactions. ${describeError(history.error)}`} onRetry={() => void history.refetch()} />
      ) : data && data.items.length === 0 && data.totalPages > 0 && page > data.totalPages ? (
        <EmptyState
          action={
            <Button variant="outline" onClick={() => apply({ from, to }, data.totalPages)}>
              Go to the last page
            </Button>
          }
        >
          That page does not exist.
        </EmptyState>
      ) : data && data.items.length === 0 ? (
        <EmptyState>{from || to ? 'No transactions in this date range.' : 'No transactions yet. Money you send or receive will show here.'}</EmptyState>
      ) : data ? (
        <div className={history.isPlaceholderData ? 'opacity-60 transition-opacity' : undefined}>
          <TransactionTable items={data.items} caption="Your transactions, newest first" />
          <nav aria-label="Pages" className="mt-6 flex flex-wrap items-center justify-between gap-4 text-sm">
            <p className="text-muted-foreground">
              Page {data.page} of {Math.max(1, data.totalPages)}. {data.totalCount} {data.totalCount === 1 ? 'entry' : 'entries'}.
            </p>
            <div className="flex gap-2">
              <Button variant="outline" disabled={data.page <= 1} onClick={() => apply({ from, to }, data.page - 1)}>
                <ChevronLeft aria-hidden />
                Previous
              </Button>
              <Button variant="outline" disabled={data.page >= data.totalPages} onClick={() => apply({ from, to }, data.page + 1)}>
                Next
                <ChevronRight aria-hidden />
              </Button>
            </div>
          </nav>
        </div>
      ) : null}
    </>
  )
}
