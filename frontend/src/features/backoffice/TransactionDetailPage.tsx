import { useQuery } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { ErrorState } from '@/components/ErrorState'
import { PageHeader } from '@/components/PageHeader'
import { Receipt } from '@/components/Receipt'
import { Skeleton } from '@/components/ui/skeleton'
import { getTransaction } from '@/lib/api/backoffice'
import { ApiError } from '@/lib/api/problem'
import { describeError } from '@/lib/errors'
import { formatDateTime, formatMoney } from '@/lib/format'

export function TransactionDetailPage() {
  const { reference = '' } = useParams()
  const found = useQuery({
    queryKey: ['transaction', reference],
    queryFn: ({ signal }) => getTransaction(reference, signal),
    retry: false,
  })

  return (
    <>
      <PageHeader title="Transaction" intro={reference} />
      {found.isPending ? (
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
      <Link to="/backoffice/transactions" className="mt-6 inline-block text-sm underline underline-offset-4">
        Back to the transactions
      </Link>
    </>
  )
}
