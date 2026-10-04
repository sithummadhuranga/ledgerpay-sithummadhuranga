import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useSearchParams } from 'react-router-dom'
import { z } from 'zod'
import { AmountField } from '@/components/AmountField'
import { PageHeader } from '@/components/PageHeader'
import { Receipt } from '@/components/Receipt'
import { TextField } from '@/components/TextField'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { topUp } from '@/lib/api/backoffice'
import type { TopUpReceipt } from '@/lib/api/types'
import { describeError } from '@/lib/errors'
import { showFieldErrors } from '@/lib/forms'
import { formatMoney } from '@/lib/format'
import { newIdempotencyKey, wasRefused } from '@/lib/idempotency'
import { amountField, noteField, walletNumberField } from '@/lib/validation'

const schema = z.object({
  walletNumber: walletNumberField,
  amount: amountField,
  bankReference: z.string().trim().regex(/^[A-Za-z0-9]{6,40}$/, 'A bank reference has 6 to 40 letters and digits.'),
  note: noteField,
})
type Values = z.infer<typeof schema>

const empty: Values = { walletNumber: '', amount: '', bankReference: '', note: '' }

export function TopUpPage() {
  const queryClient = useQueryClient()
  const [receipt, setReceipt] = useState<TopUpReceipt | null>(null)
  const [problem, setProblem] = useState<string | null>(null)
  // One key for one attempt. Sending the same details again after a lost connection reuses the key, so the credit
  // is made once. Different details, or an attempt the server refused, are a new attempt and get a new key.
  const attempt = useRef<{ signature: string; key: string } | null>(null)
  // A customer's page links here with the wallet number filled in.
  const [params] = useSearchParams()
  const preset = /^[0-9]{12}$/.test(params.get('wallet') ?? '') ? params.get('wallet')! : ''
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { ...empty, walletNumber: preset } })

  const credit = useMutation({
    mutationFn: (values: Values) => {
      const signature = JSON.stringify(values)
      if (attempt.current?.signature !== signature) {
        attempt.current = { signature, key: newIdempotencyKey() }
      }
      const { note, ...rest } = values
      return topUp(
        { ...rest, bankReference: values.bankReference.trim(), ...(note.trim() ? { note: note.trim() } : {}) },
        attempt.current.key,
      )
    },
    onSuccess: async (done) => {
      attempt.current = null
      setProblem(null)
      setReceipt(done)
      await queryClient.invalidateQueries({ queryKey: ['transactions'] })
    },
    onError: (error) => {
      // A refused attempt is finished, so the next try gets a new key.
      if (wasRefused(error)) {
        attempt.current = null
      }
      if (!showFieldErrors(form, error, ['walletNumber', 'amount', 'bankReference', 'note'])) {
        setProblem(describeError(error))
      }
    },
  })

  const { errors } = form.formState

  if (receipt) {
    return (
      <>
        <PageHeader title="Wallet topped up" />
        <div className="max-w-xl">
          <Receipt
            rows={[
              { label: 'Wallet', value: receipt.walletNumber, figures: true },
              { label: 'Amount credited', value: formatMoney(receipt.amount), figures: true, strong: true },
              { label: 'Balance now', value: formatMoney(receipt.balanceAfter), figures: true },
              { label: 'Bank reference', value: receipt.bankReference, figures: true },
              { label: 'Reference', value: receipt.reference, figures: true },
            ]}
          />
          <Button
            className="mt-6"
            onClick={() => {
              setReceipt(null)
              form.reset(empty)
            }}
          >
            Top up another wallet
          </Button>
        </div>
      </>
    )
  }

  return (
    <>
      <PageHeader title="Top up a wallet" intro="Credit a customer wallet after a bank deposit. A bank reference can be used once." />
      <form noValidate className="grid max-w-xl gap-6" onSubmit={form.handleSubmit((values) => credit.mutate(values))}>
        {problem ? (
          <Alert variant="destructive">
            <AlertDescription>{problem}</AlertDescription>
          </Alert>
        ) : null}
        <TextField id="walletNumber" label="Wallet number" hint="12 digits" inputMode="numeric" autoComplete="off" error={errors.walletNumber?.message} {...form.register('walletNumber')} />
        <AmountField id="amount" label="Amount" error={errors.amount?.message} {...form.register('amount')} />
        <TextField id="bankReference" label="Bank reference" hint="6 to 40 letters and digits. It is stored in capitals." autoComplete="off" error={errors.bankReference?.message} {...form.register('bankReference')} />
        <TextField id="note" label="Note (optional)" error={errors.note?.message} {...form.register('note')} />
        <Button type="submit" size="lg" disabled={credit.isPending} className="sm:justify-self-start">
          {credit.isPending ? 'Crediting' : 'Credit the wallet'}
        </Button>
      </form>
    </>
  )
}
