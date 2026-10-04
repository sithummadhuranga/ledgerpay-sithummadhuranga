import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { ButtonLink } from '@/components/ButtonLink'
import { Skeleton } from '@/components/ui/skeleton'
import { hasRole, useAuth } from '@/features/auth/useAuth'
import { homeFor } from './navigation'

// Sends anyone who is not signed in to the sign-in screen, and brings them back after. With roles, it also
// keeps out a signed-in user who has none of them. The server checks the role too: this only saves a wasted screen.
export function ProtectedRoute({ roles }: { roles?: string[] }) {
  const { user, restoring } = useAuth()
  const location = useLocation()

  // After a reload the answer is not known for a moment. Sending the person to sign in now would throw away a session
  // the cookie is about to restore.
  if (restoring) {
    return (
      <div role="status" aria-busy="true" aria-label="Opening your account" className="mx-auto grid max-w-3xl gap-3 px-4 py-10">
        <Skeleton className="h-10 w-1/2" />
        <Skeleton className="h-32 w-full" />
      </div>
    )
  }

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }

  if (roles && !hasRole(user, ...roles)) {
    return (
      <div className="max-w-md">
        <h1 className="text-3xl font-semibold">No access</h1>
        <p className="mt-2 text-muted-foreground">Your account cannot open this page.</p>
        <ButtonLink to={homeFor(user.roles)} className="mt-6">
          Go to your start page
        </ButtonLink>
      </div>
    )
  }

  return <Outlet />
}
