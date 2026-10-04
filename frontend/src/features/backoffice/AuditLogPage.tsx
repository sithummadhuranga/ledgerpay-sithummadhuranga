import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { EmptyState } from '@/components/EmptyState'
import { ErrorState } from '@/components/ErrorState'
import { PageHeader } from '@/components/PageHeader'
import { Pager } from '@/components/Pager'
import { SelectField } from '@/components/SelectField'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import type { AuditFilter } from '@/lib/api/backoffice'
import { auditActions } from '@/lib/auditActions'
import { describeError } from '@/lib/errors'
import { formatDateTime } from '@/lib/format'
import { isDate, positive } from '@/lib/params'
import { useAudit } from './queries'

const PAGE_SIZE = 20

interface Draft {
  action: string
  actor: string
  from: string
  to: string
}

// Who did what and when, newest first. Admins only: it holds the addresses and the names of staff.
export function AuditLogPage() {
  const [params, setParams] = useSearchParams()
  const action = auditActions.find((candidate) => candidate === params.get('action')) ?? ''
  const actor = params.get('actor') ?? ''
  const from = isDate(params.get('from')) ? params.get('from')! : ''
  const to = isDate(params.get('to')) ? params.get('to')! : ''
  const page = positive(params.get('page'))
  const draft: Draft = { action, actor, from, to }
  const filter: AuditFilter = {
    page,
    pageSize: PAGE_SIZE,
    ...(action ? { action } : {}),
    ...(actor ? { actor } : {}),
    ...(from ? { from } : {}),
    ...(to ? { to } : {}),
  }
  const audit = useAudit(filter)

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
      <PageHeader title="Audit log" intro="Every sign-in, top-up, transfer, freeze and look at a customer, newest first. The date filter counts whole days in UTC." />
      <Filters key={JSON.stringify(draft)} initial={draft} onApply={(next) => go(next)} onClear={() => setParams(new URLSearchParams())} filtered={Object.values(draft).some(Boolean)} />

      {audit.isPending ? (
        <div aria-busy="true" aria-label="Loading the audit log" className="grid gap-2">
          {Array.from({ length: 6 }, (_, index) => (
            <Skeleton key={index} className="h-12 w-full" />
          ))}
        </div>
      ) : audit.isError ? (
        <ErrorState message={`We could not load the audit log. ${describeError(audit.error)}`} onRetry={() => void audit.refetch()} />
      ) : audit.data.items.length === 0 ? (
        <EmptyState>No entry matches those filters.</EmptyState>
      ) : (
        <div className={audit.isPlaceholderData ? 'opacity-60 transition-opacity' : undefined}>
          <div className="overflow-x-auto">
            <table className="w-full min-w-[44rem] text-sm">
              <caption className="sr-only">Audit log, newest first</caption>
              <thead>
                <tr className="border-b text-left text-xs text-muted-foreground">
                  <th scope="col" className="py-2 pr-4 font-medium">When</th>
                  <th scope="col" className="py-2 pr-4 font-medium">Action</th>
                  <th scope="col" className="py-2 pr-4 font-medium">Who</th>
                  <th scope="col" className="py-2 pr-4 font-medium">About</th>
                  <th scope="col" className="py-2 font-medium">Address</th>
                </tr>
              </thead>
              <tbody>
                {audit.data.items.map((entry, index) => (
                  <tr key={`${entry.createdAt}-${index}`} className="border-b align-top">
                    <td className="py-3 pr-4 whitespace-nowrap text-muted-foreground">{formatDateTime(entry.createdAt)}</td>
                    <td className="py-3 pr-4 font-medium">{entry.action}</td>
                    <td className="py-3 pr-4">
                      {entry.actorName ? (
                        <>
                          {entry.actorName}
                          <span className="block text-xs text-muted-foreground">{entry.actorEmail}</span>
                        </>
                      ) : (
                        <span className="text-muted-foreground">Nobody signed in</span>
                      )}
                    </td>
                    <td className="py-3 pr-4">
                      {entry.entityType}
                      {entry.entityReference ? <span className="num block text-xs text-muted-foreground">{entry.entityReference}</span> : null}
                      {entry.details ? <span className="block text-xs text-muted-foreground">{entry.details}</span> : null}
                    </td>
                    <td className="num py-3 whitespace-nowrap text-muted-foreground">{entry.ipAddress ?? ''}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pager page={audit.data.page} totalPages={audit.data.totalPages} totalCount={audit.data.totalCount} noun={['entry', 'entries']} onPage={(next) => go(draft, next)} />
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
        if (!reversed) onApply({ ...draft, actor: draft.actor.trim() })
      }}
    >
      <SelectField label="Action" value={draft.action} onChange={(event) => set('action', event.target.value)} className="w-56" options={[{ value: '', label: 'All' }, ...auditActions.map((name) => ({ value: name, label: name }))]} />
      <div className="grid min-w-48 gap-1.5">
        <Label htmlFor="actor">Who (name or email)</Label>
        <Input id="actor" value={draft.actor} maxLength={100} autoComplete="off" onChange={(event) => set('actor', event.target.value)} />
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
