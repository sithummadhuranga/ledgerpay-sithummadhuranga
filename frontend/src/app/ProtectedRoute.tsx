import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { hasRole, useAuth } from '@/features/auth/useAuth'

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
      <main className="mx-auto max-w-sm px-4 py-12 text-sm">
        <h1 className="mb-2 text-xl font-semibold">No access</h1>
        <p className="text-muted-foreground">Your account cannot open this page.</p>
      </main>
    )
  }

  return <Outlet />
}
