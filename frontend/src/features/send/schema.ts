import { z } from 'zod'
import { amountField, noteField, phoneField, walletNumberField } from '@/lib/validation'

export type RecipientKind = 'wallet' | 'phone'

// The recipient is a wallet number or a mobile number, and which one decides how it is checked.
export const sendSchema = z
  .object({
    kind: z.enum(['wallet', 'phone']),
    recipient: z.string(),
    amount: amountField,
    note: noteField,
  })
  .superRefine((values, context) => {
    const result = (values.kind === 'wallet' ? walletNumberField : phoneField).safeParse(values.recipient)
    if (!result.success) {
      context.addIssue({ code: 'custom', path: ['recipient'], message: result.error.issues[0]?.message ?? 'Check the recipient.' })
    }
  })

export type SendValues = z.infer<typeof sendSchema>
