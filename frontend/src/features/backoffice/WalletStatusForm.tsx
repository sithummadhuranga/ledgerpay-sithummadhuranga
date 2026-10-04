import { zodResolver } from '@hookform/resolvers/zod'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { TextField } from '@/components/TextField'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { setWalletStatus } from '@/lib/api/backoffice'
import type { WalletStatus } from '@/lib/api/types'
import { describeError } from '@/lib/errors'
import { showFieldErrors } from '@/lib/forms'

const schema = z.object({
  reason: z.string().trim().min(3, 'Give a reason of at least 3 characters.').max(250, 'A reason can have at most 250 characters.'),
})
type Values = z.infer<typeof schema>

// Freezes an active wallet or unfreezes a frozen one, with a reason. The reason is kept in the audit log and is
// never shown to the customer.
export function WalletStatusForm({ walletNumber, status }: { walletNumber: string; status: WalletStatus }) {
  const queryClient = useQueryClient()
  const [problem, setProblem] = useState<string | null>(null)
  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { reason: '' } })
  const next: WalletStatus = status === 'Active' ? 'Frozen' : 'Active'

  const change = useMutation({
    mutationFn: (values: Values) => setWalletStatus(walletNumber, { status: next, reason: values.reason.trim() }),
    onSuccess: async () => {
      setProblem(null)
      form.reset({ reason: '' })
      // The person page, the lists and the overview all show the status.
      await queryClient.invalidateQueries({ queryKey: ['backoffice'] })
    },
    onError: (error) => {
      if (!showFieldErrors(form, error, ['reason'])) {
        setProblem(describeError(error))
      }
    },
  })

  return (
    <form noValidate className="grid max-w-xl gap-4" onSubmit={form.handleSubmit((values) => change.mutate(values))}>
      {problem ? (
        <Alert variant="destructive">
          <AlertDescription>{problem}</AlertDescription>
        </Alert>
      ) : null}
      <TextField
        id="reason"
        label={next === 'Frozen' ? 'Reason for freezing' : 'Reason for unfreezing'}
        hint="3 to 250 characters. Never shown to the customer."
        autoComplete="off"
        error={form.formState.errors.reason?.message}
        {...form.register('reason')}
      />
      <Button type="submit" variant={next === 'Frozen' ? 'destructive' : 'default'} disabled={change.isPending} className="sm:justify-self-start">
        {change.isPending ? 'Saving' : next === 'Frozen' ? 'Freeze this wallet' : 'Unfreeze this wallet'}
      </Button>
    </form>
  )
}
