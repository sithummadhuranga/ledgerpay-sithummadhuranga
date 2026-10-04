import { LogOut } from 'lucide-react'
import { Link, Outlet, useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/features/auth/useAuth'

// A narrow top bar with the wallet number and sign out, then one column of content.
export function AppLayout() {
  const { user, signOut } = useAuth()
  const navigate = useNavigate()

  function leave() {
    signOut()
    navigate('/login', { replace: true })
  }

  return (
    <>
      <header className="border-b bg-card">
        <div className="mx-auto flex max-w-3xl items-center justify-between gap-4 px-4 py-2">
          <Link to="/" className="font-semibold tracking-tight">
            LedgerPay
          </Link>
          <div className="flex items-center gap-3 text-sm">
            {user?.walletNumber ? <span className="num text-muted-foreground">{user.walletNumber}</span> : null}
            <Button variant="ghost" size="sm" onClick={leave}>
              <LogOut aria-hidden />
              Sign out
            </Button>
          </div>
        </div>
      </header>
      <main className="mx-auto max-w-3xl px-4 py-8">
        <Outlet />
      </main>
    </>
  )
}
