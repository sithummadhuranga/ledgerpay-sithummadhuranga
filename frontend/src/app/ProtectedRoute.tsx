import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { ButtonLink } from '@/components/ButtonLink'
import { hasRole, useAuth } from '@/features/auth/useAuth'
import { homeFor } from './navigation'

// Sends anyone who is not signed in to the sign-in screen, and brings them back after. With roles, it also
// keeps out a signed-in user who has none of them. The server checks the role too: this only saves a wasted screen.
export function ProtectedRoute({ roles }: { roles?: string[] }) {
  const { user } = useAuth()
  const location = useLocation()

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
