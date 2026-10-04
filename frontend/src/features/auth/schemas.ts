import { z } from 'zod'

export const loginSchema = z.object({
  email: z.string().trim().min(1, 'Enter your email.'),
  password: z.string().min(1, 'Enter your password.'),
})
export type LoginValues = z.infer<typeof loginSchema>

// What the password must hold. The same list is shown before the user types and ticked as they type.
// The server checks the same rules and is the one that decides.
export const passwordRules = [
  { id: 'length', text: '10 to 128 characters', met: (password: string) => password.length >= 10 && password.length <= 128 },
  { id: 'upper', text: 'An upper case letter', met: (password: string) => /\p{Lu}/u.test(password) },
  { id: 'lower', text: 'A lower case letter', met: (password: string) => /\p{Ll}/u.test(password) },
  { id: 'digit', text: 'A digit', met: (password: string) => /\p{Nd}/u.test(password) },
  { id: 'symbol', text: 'A symbol, such as ! or -', met: (password: string) => /[\p{P}\p{S}]/u.test(password) },
] as const

export const registerSchema = z
  .object({
    fullName: z.string().trim().min(2, 'Enter your full name, at least 2 letters.').max(100, 'A name can have at most 100 characters.'),
    email: z
      .string()
      .trim()
      .regex(/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/, 'Enter a valid email address.'),
    phone: z.string().trim().regex(/^\+947\d{8}$/, 'Enter a mobile number like +94771284635.'),
    password: z.string().refine((password) => passwordRules.every((rule) => rule.met(password)), 'The password does not meet all the rules.'),
    confirmPassword: z.string().min(1, 'Type the password again.'),
  })
  .refine((values) => values.password === values.confirmPassword, {
    path: ['confirmPassword'],
    message: 'The two passwords are not the same.',
  })
export type RegisterValues = z.infer<typeof registerSchema>
