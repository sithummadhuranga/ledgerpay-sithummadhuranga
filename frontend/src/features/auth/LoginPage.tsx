import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Link, Navigate, useLocation } from 'react-router-dom'
import { homeFor } from '@/app/navigation'
import { PasswordField } from '@/components/PasswordField'
import { TextField } from '@/components/TextField'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { describeError } from '@/lib/errors'
import { showFieldErrors } from '@/lib/forms'
import { safeInternalPath } from '@/lib/navigation'
import { AuthLayout } from './AuthLayout'
import { loginSchema, type LoginValues } from './schemas'
import { useAuth } from './useAuth'

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

  // Once signed in, go back to the page that was asked for, or to the first page of the user's role.
  // This also covers a signed-in user who opens /login.
  if (user) {
    return <Navigate to={safeInternalPath(state.from, homeFor(user.roles))} replace />
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
    <AuthLayout title="Sign in" intro="Use the email address you registered with.">
      {message ? (
        <Alert variant={problem ? 'destructive' : 'default'} className="mb-5">
          <AlertDescription>{message}</AlertDescription>
        </Alert>
      ) : null}
      <form onSubmit={onSubmit} noValidate className="grid gap-5">
        <TextField id="email" label="Email" type="email" autoComplete="email" error={errors.email?.message} {...form.register('email')} />
        <PasswordField id="password" label="Password" autoComplete="current-password" error={errors.password?.message} {...form.register('password')} />
        <Button type="submit" size="lg" disabled={isSubmitting}>
          {isSubmitting ? 'Signing in' : 'Sign in'}
        </Button>
      </form>
      <p className="mt-8 text-sm text-muted-foreground">
        No account yet?{' '}
        <Link to="/register" className="font-medium text-primary underline underline-offset-4">
          Create one
        </Link>
      </p>
    </AuthLayout>
  )
}
