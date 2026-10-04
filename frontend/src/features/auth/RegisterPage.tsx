import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { AuthShell } from '@/components/AuthShell'
import { TextField } from '@/components/TextField'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { register } from '@/lib/api/auth'
import { describeError } from '@/lib/errors'
import { showFieldErrors } from '@/lib/forms'
import { useAuth } from './useAuth'
import { PasswordRules } from './PasswordRules'
import { registerSchema, type RegisterValues } from './schemas'

export function RegisterPage() {
  const { user } = useAuth()
  const navigate = useNavigate()
  const [problem, setProblem] = useState<string | null>(null)
  const form = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { fullName: '', email: '', phone: '', password: '' },
  })
  const password = useWatch({ control: form.control, name: 'password' })

  if (user) {
    return <Navigate to="/" replace />
  }

  const onSubmit = form.handleSubmit(async (values) => {
    setProblem(null)
    try {
      await register(values)
      navigate('/login', { replace: true, state: { registeredEmail: values.email } })
    } catch (error) {
      // The server's message for the field wins. Anything it did not tie to a field is shown above the form.
      if (!showFieldErrors(form, error, ['fullName', 'email', 'phone', 'password'])) {
        setProblem(describeError(error))
      }
    }
  })

  const { errors, isSubmitting } = form.formState

  return (
    <AuthShell title="Create an account">
      {problem ? (
        <Alert variant="destructive" className="mb-4">
          <AlertDescription>{problem}</AlertDescription>
        </Alert>
      ) : null}
      <form onSubmit={onSubmit} noValidate className="grid gap-4">
        <TextField id="fullName" label="Full name" autoComplete="name" error={errors.fullName?.message} {...form.register('fullName')} />
        <TextField id="email" label="Email" type="email" autoComplete="email" error={errors.email?.message} {...form.register('email')} />
        <TextField
          id="phone"
          label="Mobile number"
          type="tel"
          autoComplete="tel"
          hint="Start with +947, then 8 digits."
          error={errors.phone?.message}
          {...form.register('phone')}
        />
        <TextField
          id="password"
          label="Password"
          type="password"
          autoComplete="new-password"
          error={errors.password?.message}
          {...form.register('password')}
        />
        <PasswordRules password={password} />
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Creating account' : 'Create account'}
        </Button>
      </form>
      <p className="mt-6 text-sm text-muted-foreground">
        Already have an account?{' '}
        <Link to="/login" className="text-primary underline underline-offset-4">
          Sign in
        </Link>
      </p>
    </AuthShell>
  )
}
