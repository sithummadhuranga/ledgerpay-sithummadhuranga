import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Check } from 'lucide-react'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { AmountField } from '@/components/AmountField'
import { CopyButton } from '@/components/CopyButton'
import { PageHeader } from '@/components/PageHeader'
import { Receipt } from '@/components/Receipt'
import { TextField } from '@/components/TextField'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { ButtonLink } from '@/components/ButtonLink'
import { transactionKeys } from '@/features/transactions/queries'
import { useAuth } from '@/features/auth/useAuth'
import { walletKeys } from '@/features/wallet/queries'
import { ApiError } from '@/lib/api/problem'
import { sendMoney } from '@/lib/api/transfers'
import type { LookupResult, Quote, TransferReceipt, TransferRequest } from '@/lib/api/types'
import { getQuote, lookupWallet } from '@/lib/api/wallets'
import { describeError } from '@/lib/errors'
import { formatMoney } from '@/lib/format'
import { newIdempotencyKey } from '@/lib/idempotency'
import { cn } from '@/lib/utils'
import { sendSchema, type RecipientKind, type SendValues } from './schema'

interface Review {
  values: SendValues
  recipient: LookupResult
  quote: Quote
  // One key for this attempt. It is kept while the user retries and replaced when the details change.
  key: string
}

const kinds: { id: RecipientKind; label: string; field: string; hint: string }[] = [
  { id: 'wallet', label: 'Wallet number', field: 'Wallet number', hint: '12 digits' },
  { id: 'phone', label: 'Mobile number', field: 'Mobile number', hint: 'Start with +947, then 8 digits' },
]

export function SendMoneyPage() {
  const { user } = useAuth()
  const queryClient = useQueryClient()
  const [review, setReview] = useState<Review | null>(null)
  const [receipt, setReceipt] = useState<TransferReceipt | null>(null)
  const [recipientName, setRecipientName] = useState('')
  const [problem, setProblem] = useState<string | null>(null)
  const form = useForm<SendValues>({
    resolver: zodResolver(sendSchema),
    defaultValues: { kind: 'wallet', recipient: '', amount: '', note: '' },
  })
  const kind = useWatch({ control: form.control, name: 'kind' })
  const { errors } = form.formState

  // Step one: find the wallet and ask the server for the fee. Nothing is sent yet.
  const prepare = useMutation({
    mutationFn: async (values: SendValues): Promise<Review> => {
      const target = values.kind === 'wallet' ? { walletNumber: values.recipient.trim() } : { phone: values.recipient.trim() }
      const [recipient, quote] = await Promise.all([lookupWallet(target), getQuote(values.amount.trim())])
      if (recipient.walletNumber === user?.walletNumber) {
        throw new ApiError(422, 'SELF_TRANSFER_NOT_ALLOWED')
      }
      if (!recipient.active) {
        throw new ApiError(422, 'WALLET_FROZEN')
      }
      return { values, recipient, quote, key: newIdempotencyKey() }
    },
    onSuccess: (next) => {
      setProblem(null)
      setReview(next)
    },
    onError: (error) => setProblem(describeError(error)),
  })

  // Step two: send it, with the key made for this attempt.
  const send = useMutation({
    mutationFn: (current: Review) => {
      const body: TransferRequest = {
        amount: current.values.amount.trim(),
        ...(current.values.kind === 'wallet'
          ? { recipientWalletNumber: current.values.recipient.trim() }
          : { recipientPhone: current.values.recipient.trim() }),
        ...(current.values.note.trim() ? { note: current.values.note.trim() } : {}),
      }
      return sendMoney(body, current.key)
    },
    onSuccess: async (done, current) => {
      setProblem(null)
      setRecipientName(current.recipient.holderName)
      setReceipt(done)
      setReview(null)
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: walletKeys.me }),
        queryClient.invalidateQueries({ queryKey: transactionKeys.all }),
      ])
    },
    onError: (error, current) =>
      setProblem(describeError(error, { amount: current.values.amount, fee: String(current.quote.fee) })),
  })

  function startOver() {
    setReceipt(null)
    setReview(null)
    setProblem(null)
    send.reset()
    form.reset({ kind: 'wallet', recipient: '', amount: '', note: '' })
  }

  if (receipt) {
    return (
      <>
        <PageHeader title="Money sent" />
        <div className="max-w-xl">
          <div role="status" className="mb-6 flex items-start gap-3 rounded-lg border bg-accent px-4 py-3 text-accent-foreground">
            <Check aria-hidden className="mt-0.5 size-5 shrink-0" />
            <p>
              Sent {formatMoney(receipt.amount)} to {recipientName}.
            </p>
          </div>
          <Receipt
            rows={[
              { label: 'Amount', value: formatMoney(receipt.amount), figures: true },
              { label: 'Fee', value: formatMoney(receipt.fee), figures: true },
              { label: 'Total taken', value: formatMoney(receipt.total), figures: true, strong: true },
              { label: 'Your balance now', value: formatMoney(receipt.balanceAfter), figures: true },
              { label: 'Reference', value: receipt.reference, figures: true },
            ]}
          />
          <div className="mt-6 flex flex-wrap gap-3">
            <Button onClick={startOver}>Send another</Button>
            <ButtonLink to="/history" variant="outline">
              See history
            </ButtonLink>
            <CopyButton value={receipt.reference} subject="reference" />
          </div>
        </div>
      </>
    )
  }

  if (review) {
    const { values, recipient, quote } = review
    return (
      <>
        <PageHeader title="Check and confirm" intro="Nothing has been sent yet." />
        <div className="max-w-xl">
          {problem ? (
            <Alert variant="destructive" className="mb-5">
              <AlertDescription>{problem}</AlertDescription>
            </Alert>
          ) : null}
          <Receipt
            rows={[
              { label: 'To', value: recipient.holderName },
              { label: 'Wallet number', value: recipient.walletNumber, figures: true },
              { label: 'Amount', value: formatMoney(quote.amount), figures: true },
              { label: 'Fee', value: formatMoney(quote.fee), figures: true },
              { label: 'Total taken from you', value: formatMoney(quote.total), figures: true, strong: true },
              ...(values.note.trim() ? [{ label: 'Note', value: values.note.trim() }] : []),
            ]}
          />
          <div className="mt-6 flex flex-wrap gap-3">
            <Button
              size="lg"
              disabled={send.isPending}
              onClick={() => {
                if (!send.isPending) {
                  send.mutate(review)
                }
              }}
            >
              {send.isPending ? 'Sending' : `Confirm and send ${formatMoney(quote.total)}`}
            </Button>
            <Button
              size="lg"
              variant="outline"
              disabled={send.isPending}
              onClick={() => {
                // Changing the details means a new attempt, so the next confirmation gets a new key.
                setReview(null)
                setProblem(null)
                send.reset()
              }}
            >
              Change details
            </Button>
          </div>
        </div>
      </>
    )
  }

  const current = kinds.find((item) => item.id === kind) ?? kinds[0]!
  return (
    <>
      <PageHeader title="Send money" intro="Find the wallet, then you see the fee and the total before anything is sent." />
      <form
        noValidate
        className="grid max-w-xl gap-6"
        onSubmit={form.handleSubmit((values) => {
          setProblem(null)
          prepare.mutate(values)
        })}
      >
        {problem ? (
          <Alert variant="destructive">
            <AlertDescription>{problem}</AlertDescription>
          </Alert>
        ) : null}

        <fieldset className="grid gap-2">
          <legend className="mb-1.5 text-sm font-medium">Send to</legend>
          <div className="grid grid-cols-2 gap-1 rounded-md border bg-secondary p-1">
            {kinds.map((item) => (
              <button
                key={item.id}
                type="button"
                aria-pressed={kind === item.id}
                onClick={() => {
                  form.setValue('kind', item.id)
                  form.setValue('recipient', '')
                  form.clearErrors('recipient')
                }}
                className={cn(
                  'h-10 rounded-sm text-sm font-medium outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  kind === item.id ? 'bg-card shadow-none ring-1 ring-border' : 'text-muted-foreground hover:text-foreground',
                )}
              >
                {item.label}
              </button>
            ))}
          </div>
        </fieldset>

        <TextField
          id="recipient"
          label={current.field}
          hint={current.hint}
          inputMode={kind === 'wallet' ? 'numeric' : 'tel'}
          autoComplete="off"
          error={errors.recipient?.message}
          {...form.register('recipient')}
        />
        <AmountField id="amount" label="Amount" error={errors.amount?.message} {...form.register('amount')} />
        <TextField id="note" label="Note (optional)" hint="Up to 140 characters. The person you send to can read it." error={errors.note?.message} {...form.register('note')} />

        <Button type="submit" size="lg" disabled={prepare.isPending} className="sm:justify-self-start">
          {prepare.isPending ? 'Checking' : 'Continue'}
        </Button>
      </form>
    </>
  )
}
