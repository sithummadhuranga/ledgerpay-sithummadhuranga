import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { ErrorState } from '@/components/ErrorState'
import { PageHeader } from '@/components/PageHeader'
import { Receipt } from '@/components/Receipt'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { getTransaction } from '@/lib/api/backoffice'
import { ApiError } from '@/lib/api/problem'
import { describeError } from '@/lib/errors'
import { formatDateTime, formatMoney } from '@/lib/format'

export function TransactionLookupPage() {
  const [draft, setDraft] = useState('')
  const [reference, setReference] = useState('')
  const found = useQuery({
    queryKey: ['transaction', reference],
    queryFn: ({ signal }) => getTransaction(reference, signal),
    enabled: reference !== '',
    retry: false,
  })

  return (
    <>
      <PageHeader title="Find a transaction" intro="Look a transaction up by its reference. You see both wallet numbers and, for a top-up, the bank reference." />
      <form
        className="mb-8 flex max-w-xl flex-wrap items-end gap-3"
        onSubmit={(event) => {
          event.preventDefault()
          setReference(draft.trim().toUpperCase())
        }}
      >
        <div className="grid min-w-56 flex-1 gap-1.5">
          <Label htmlFor="reference">Reference</Label>
          <Input id="reference" value={draft} onChange={(event) => setDraft(event.target.value)} autoComplete="off" placeholder="TX…" className="num" />
        </div>
        <Button type="submit" disabled={draft.trim() === ''}>
          Find
        </Button>
      </form>

      {reference === '' ? null : found.isPending ? (
        <div aria-busy="true" aria-label="Looking up the transaction" className="grid max-w-xl gap-2">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-10 w-full" />
        </div>
      ) : found.isError ? (
        <ErrorState
          message={describeError(found.error)}
          {...(found.error instanceof ApiError && found.error.status < 500 ? {} : { onRetry: () => void found.refetch() })}
        />
      ) : (
        <div className="max-w-xl">
          <Receipt
            rows={[
              { label: 'Reference', value: found.data.reference, figures: true },
              { label: 'Type', value: found.data.type === 'TopUp' ? 'Top-up' : 'Transfer' },
              { label: 'Status', value: found.data.status },
              ...(found.data.failureCode ? [{ label: 'Reason', value: describeError(new ApiError(422, found.data.failureCode)) }] : []),
              { label: 'Amount', value: formatMoney(found.data.amount), figures: true },
              { label: 'Fee', value: formatMoney(found.data.fee), figures: true },
              { label: 'From wallet', value: found.data.senderWalletNumber ?? 'Bank', figures: true },
              { label: 'To wallet', value: found.data.receiverWalletNumber ?? 'Not found', figures: true },
              ...(found.data.bankReference ? [{ label: 'Bank reference', value: found.data.bankReference, figures: true }] : []),
              ...(found.data.note ? [{ label: 'Note', value: found.data.note }] : []),
              { label: 'Time', value: formatDateTime(found.data.createdAt) },
            ]}
          />
        </div>
      )}
    </>
  )
}
