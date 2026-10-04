import { CircleAlert } from 'lucide-react'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api/problem'
import type { HistoryItem } from '@/lib/api/types'
import { describeError } from '@/lib/errors'
import { formatDateTime, formatMoney, formatSignedMoney } from '@/lib/format'

function details(item: HistoryItem): string {
  if (item.type === 'TopUp') {
    return 'Bank top-up'
  }
  const who = item.counterpartyName ?? 'a wallet that could not be found'
  return item.direction === 'Sent' ? `Sent to ${who}` : `Received from ${who}`
}

// A ruled table, as on a bank statement. Money out is red with a minus, money in has a plus. A failed attempt says
// so in words and gives its reason, because a colour alone is not enough.
export function TransactionRows({ items, caption }: { items: HistoryItem[]; caption: string }) {
  return (
    <Table>
      <caption className="sr-only">{caption}</caption>
      <TableHeader>
        <TableRow>
          <TableHead scope="col" className="hidden sm:table-cell">
            Date
          </TableHead>
          <TableHead scope="col">Details</TableHead>
          <TableHead scope="col" className="text-right">
            Amount
          </TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {items.map((item) => (
          <TableRow key={item.reference}>
            <TableCell className="hidden whitespace-nowrap sm:table-cell">{formatDateTime(item.createdAt)}</TableCell>
            <TableCell className="whitespace-normal">
              <div>{details(item)}</div>
              <div className="text-muted-foreground sm:hidden">{formatDateTime(item.createdAt)}</div>
              {item.note ? <div className="text-muted-foreground">{item.note}</div> : null}
              {item.status === 'Failed' ? (
                <div className="mt-0.5 flex items-center gap-1 text-destructive">
                  <CircleAlert className="size-3.5" aria-hidden />
                  <span>Failed. {describeError(new ApiError(422, item.failureCode ?? 'UNKNOWN_ERROR'))}</span>
                </div>
              ) : null}
            </TableCell>
            <TableCell className={`num text-right whitespace-nowrap ${item.direction === 'Sent' && item.status === 'Completed' ? 'text-destructive' : ''}`}>
              {item.status === 'Failed' ? formatMoney(item.amount) : formatSignedMoney(item.amount, item.direction)}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
