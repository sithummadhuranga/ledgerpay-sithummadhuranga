import { CircleAlert, Landmark } from 'lucide-react'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ApiError } from '@/lib/api/problem'
import type { HistoryItem } from '@/lib/api/types'
import { describeError } from '@/lib/errors'
import { formatDateTime, formatMoney, formatSignedMoney } from '@/lib/format'
import { cn } from '@/lib/utils'

function details(item: HistoryItem): string {
  if (item.type === 'TopUp') {
    return 'Bank top-up'
  }
  const who = item.counterpartyName ?? 'a wallet that could not be found'
  return item.direction === 'Sent' ? `Sent to ${who}` : `Received from ${who}`
}

function Avatar({ item }: { item: HistoryItem }) {
  return (
    <span aria-hidden className="grid size-9 shrink-0 place-items-center rounded-full bg-secondary text-sm font-semibold text-secondary-foreground max-md:hidden">
      {item.type === 'TopUp' ? <Landmark className="size-4" /> : (Array.from(item.counterpartyName ?? '?')[0] ?? '?')}
    </span>
  )
}

interface TransactionTableProps {
  items: HistoryItem[]
  caption: string
  // The dashboard shows date, details and amount. The history adds the fee and the balance after.
  compact?: boolean
}

// A ruled statement, as on paper. On a phone the same table becomes two lines for each entry: the details and the
// amount, then the date with the fee and the balance. Money out is red with a minus, money in has a plus, and a
// failed attempt says so in words and gives its reason, because a colour alone is not enough.
export function TransactionTable({ items, caption, compact = false }: TransactionTableProps) {
  return (
    <Table className="max-md:block">
      <caption className="sr-only">{caption}</caption>
      <TableHeader className="max-md:sr-only">
        <TableRow className="text-xs text-muted-foreground hover:bg-transparent">
          <TableHead scope="col" className="h-10 font-medium">
            Date
          </TableHead>
          <TableHead scope="col" className="h-10 font-medium">
            Details
          </TableHead>
          {compact ? null : (
            <TableHead scope="col" className="h-10 text-right font-medium">
              Fee
            </TableHead>
          )}
          <TableHead scope="col" className="h-10 text-right font-medium">
            Amount
          </TableHead>
          {compact ? null : (
            <TableHead scope="col" className="h-10 text-right font-medium">
              Balance after
            </TableHead>
          )}
        </TableRow>
      </TableHeader>
      <TableBody className="max-md:block">
        {items.map((item) => {
          const failed = item.status === 'Failed'
          const out = item.direction === 'Sent' && !failed
          return (
            <TableRow key={item.reference} className="max-md:grid max-md:grid-cols-[1fr_auto] max-md:gap-x-3 max-md:py-3">
              <TableCell className="text-muted-foreground max-md:col-span-2 max-md:row-start-2 max-md:p-0 max-md:text-xs max-md:whitespace-normal md:py-4 md:whitespace-nowrap">
                {formatDateTime(item.createdAt)}
                {compact ? null : (
                  <span className="num md:hidden">
                    {item.fee > 0 ? ` · Fee ${formatMoney(item.fee)}` : ''}
                    {item.balanceAfter === null ? '' : ` · Balance ${formatMoney(item.balanceAfter)}`}
                  </span>
                )}
              </TableCell>
              <TableCell className="whitespace-normal max-md:col-start-1 max-md:row-start-1 max-md:p-0 md:py-4">
                <div className="flex items-center gap-3">
                  <Avatar item={item} />
                  <div className="min-w-0">
                    <div className="font-medium">{details(item)}</div>
                    {item.note ? <div className="text-muted-foreground">{item.note}</div> : null}
                    {failed ? (
                      <div className="mt-0.5 flex items-start gap-1 text-destructive">
                        <CircleAlert className="mt-0.5 size-3.5 shrink-0" aria-hidden />
                        <span>Failed. {describeError(new ApiError(422, item.failureCode ?? 'UNKNOWN_ERROR'))}</span>
                      </div>
                    ) : null}
                  </div>
                </div>
              </TableCell>
              {compact ? null : (
                <TableCell className="num text-right whitespace-nowrap text-muted-foreground max-md:hidden md:py-4">
                  {item.fee > 0 ? formatMoney(item.fee) : ''}
                </TableCell>
              )}
              <TableCell
                className={cn('num text-right font-medium whitespace-nowrap max-md:col-start-2 max-md:row-start-1 max-md:p-0 md:py-4', out && 'text-destructive')}
              >
                {failed ? formatMoney(item.amount) : formatSignedMoney(item.amount, item.direction)}
              </TableCell>
              {compact ? null : (
                <TableCell className="num text-right whitespace-nowrap max-md:hidden md:py-4">
                  {item.balanceAfter === null ? '' : formatMoney(item.balanceAfter)}
                </TableCell>
              )}
            </TableRow>
          )
        })}
      </TableBody>
    </Table>
  )
}
