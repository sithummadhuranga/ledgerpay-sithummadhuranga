import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link, Navigate, useLocation } from 'react-router-dom'
import { useState } from 'react'
import { AuthShell } from '@/components/AuthShell'
import { TextField } from '@/components/TextField'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { showFieldErrors } from '@/lib/forms'
import { describeError } from '@/lib/errors'
import { safeInternalPath } from '@/lib/navigation'
import { useAuth } from './useAuth'
import { loginSchema, type LoginValues } from './schemas'

interface LocationState {
  from?: string
  registeredEmail?: string
}

export function LoginPage() {
  const { user, notice, signIn, clearNotice } = useAuth()
  const state = (useLocation().state ?? {}) as LocationState
  const [problem, setProblem] = useState<string | null>(null)
  const form = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: state.registeredEmail ?? '', password: '' },
  })

  // Once signed in, go back to the page that was asked for. This also covers a signed-in user who opens /login.
  if (user) {
    return <Navigate to={safeInternalPath(state.from)} replace />
  }

  const onSubmit = form.handleSubmit(async (values) => {
    setProblem(null)
    clearNotice()
    try {
      await signIn(values.email, values.password)
    } catch (error) {
      if (!showFieldErrors(form, error, ['email', 'password'])) {
        setProblem(describeError(error))
      }
    }
  })

  const { errors, isSubmitting } = form.formState
  const message = problem ?? notice ?? (state.registeredEmail ? 'Your account is ready. Sign in to continue.' : null)

  return (
    <AuthShell title="Sign in">
      {message ? (
        <Alert variant={problem ? 'destructive' : 'default'} className="mb-4">
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      ) : null}
      <form onSubmit={onSubmit} noValidate className="grid gap-4">
        <TextField id="email" label="Email" type="email" autoComplete="email" error={errors.email?.message} {...form.register('email')} />
        <TextField
          id="password"
          label="Password"
          type="password"
          autoComplete="current-password"
          error={errors.password?.message}
          {...form.register('password')}
        />
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Signing in' : 'Sign in'}
        </Button>
      </form>
      <p className="mt-6 text-sm text-muted-foreground">
        No account yet?{' '}
        <Link to="/register" className="text-primary underline underline-offset-4">
          Create one
        </Link>
      </p>
    </AuthShell>
  )
}
