import { z } from 'zod'

// The formats the server also checks. They are here so a mistake is caught before a round trip. The server decides.
export const walletNumberField = z.string().trim().regex(/^[0-9]{12}$/, 'A wallet number has 12 digits.')

export const phoneField = z.string().trim().regex(/^\+947[0-9]{8}$/, 'Enter a mobile number like +94771284635.')

// An amount stays text, so nothing is rounded on the way. It needs digits, at most two decimals, and more than zero.
export const amountField = z
  .string()
  .trim()
  .regex(/^[0-9]+(\.[0-9]{1,2})?$/, 'Enter an amount with at most 2 decimals, such as 5000 or 5000.50.')
  .refine((amount) => Number(amount) > 0, 'The amount must be more than zero.')

export const noteField = z.string().max(140, 'A note can have at most 140 characters.')
