import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import { PageHeader } from '@/components/PageHeader'
import { TextField } from '@/components/TextField'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { setWalletStatus } from '@/lib/api/backoffice'
import type { WalletStatusReceipt } from '@/lib/api/types'
import { describeError } from '@/lib/errors'
import { showFieldErrors } from '@/lib/forms'
import { walletNumberField } from '@/lib/validation'
import { cn } from '@/lib/utils'

const schema = z.object({
  walletNumber: walletNumberField,
  status: z.enum(['Frozen', 'Active']),
  reason: z.string().trim().min(3, 'Give a reason of at least 3 characters.').max(250, 'A reason can have at most 250 characters.'),
})
type Values = z.infer<typeof schema>

const actions: { id: Values['status']; label: string }[] = [
  { id: 'Frozen', label: 'Freeze' },
  { id: 'Active', label: 'Unfreeze' },
]

export function WalletStatusPage() {
  const [done, setDone] = useState<WalletStatusReceipt | null>(null)
  const [problem, setProblem] = useState<string | null>(null)
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { walletNumber: '', status: 'Frozen', reason: '' } })
  const status = useWatch({ control: form.control, name: 'status' })
  const { errors } = form.formState

  const change = useMutation({
    mutationFn: (values: Values) => setWalletStatus(values.walletNumber.trim(), { status: values.status, reason: values.reason.trim() }),
    onSuccess: (receipt) => {
      setProblem(null)
      setDone(receipt)
      form.reset({ walletNumber: '', status: 'Frozen', reason: '' })
    },
    onError: (error) => {
      setDone(null)
      if (!showFieldErrors(form, error, ['walletNumber', 'status', 'reason'])) {
        setProblem(describeError(error))
      }
    },
  })

  return (
    <>
      <PageHeader title="Freeze or unfreeze a wallet" intro="A frozen wallet cannot send or receive money. The reason is kept in the audit log and is never shown to the customer." />
      <form noValidate className="grid max-w-xl gap-6" onSubmit={form.handleSubmit((values) => change.mutate(values))}>
        {problem ? (
          <Alert variant="destructive">
            <AlertDescription>{problem}</AlertDescription>
          </Alert>
        ) : null}
        {done ? (
          <div role="status" className="rounded-lg border bg-accent px-4 py-3 text-accent-foreground">
            Wallet <span className="num">{done.walletNumber}</span> is now {done.status === 'Frozen' ? 'frozen' : 'active'}.
          </div>
        ) : null}

        <TextField id="walletNumber" label="Wallet number" hint="12 digits" inputMode="numeric" autoComplete="off" error={errors.walletNumber?.message} {...form.register('walletNumber')} />

        <fieldset className="grid gap-2">
          <legend className="mb-1.5 text-sm font-medium">Action</legend>
          <div className="grid max-w-xs grid-cols-2 gap-1 rounded-md border bg-secondary p-1">
            {actions.map((action) => (
              <button
                key={action.id}
                type="button"
                aria-pressed={status === action.id}
                onClick={() => form.setValue('status', action.id)}
                className={cn(
                  'h-10 rounded-sm text-sm font-medium outline-none focus-visible:ring-2 focus-visible:ring-ring',
                  status === action.id ? 'bg-card ring-1 ring-border' : 'text-muted-foreground hover:text-foreground',
                )}
              >
                {action.label}
              </button>
            ))}
          </div>
        </fieldset>

        <TextField id="reason" label="Reason" hint="3 to 250 characters" autoComplete="off" error={errors.reason?.message} {...form.register('reason')} />
        <Button type="submit" size="lg" disabled={change.isPending} className="sm:justify-self-start">
          {change.isPending ? 'Saving' : status === 'Frozen' ? 'Freeze the wallet' : 'Unfreeze the wallet'}
        </Button>
      </form>
    </>
  )
}
