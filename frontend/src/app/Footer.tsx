import { Link } from 'react-router-dom'
import { Logo } from '@/components/Logo'
import { useAuth } from '@/features/auth/useAuth'
import { homeFor } from './navigation'

export function Footer() {
  const { user } = useAuth()

  return (
    <footer className="border-t bg-card">
      <div className="mx-auto flex max-w-6xl flex-col gap-6 px-4 py-10 sm:px-6 md:flex-row md:items-start md:justify-between">
        <div className="max-w-sm">
          <Logo />
          <p className="mt-3 text-sm text-muted-foreground">
            A wallet for Sri Lankan rupees with a double-entry ledger, built as an assessment project. Amounts are in LKR.
          </p>
        </div>
        <nav aria-label="Footer" className="flex flex-wrap gap-x-8 gap-y-2 text-sm">
          {user ? (
            <Link to={homeFor(user.roles)} className="hover:underline">
              Open your account
            </Link>
          ) : (
            <>
              <Link to="/login" className="hover:underline">
                Sign in
              </Link>
              <Link to="/register" className="hover:underline">
                Create an account
              </Link>
            </>
          )}
          <a href="/swagger" className="hover:underline">
            API documentation
          </a>
        </nav>
      </div>
    </footer>
  )
}
