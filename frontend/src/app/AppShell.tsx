import { NavLink, Outlet } from 'react-router-dom'
import { Logo } from '@/components/Logo'
import { cn } from '@/lib/utils'
import { useAuth } from '@/features/auth/useAuth'
import { homeFor, navigationFor } from './navigation'
import { UserMenu } from './UserMenu'

// The frame of every signed-in page: a bar with the links of the user's role, and on a phone a tab bar
// at the bottom where a thumb reaches it.
export function AppShell() {
  const { user } = useAuth()
  const items = navigationFor(user?.roles ?? [])

  return (
    <div className="flex min-h-svh flex-col">
      <a href="#content" className="skip-link">
        Skip to the content
      </a>
      <header className="sticky top-0 z-30 border-b bg-background">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-6 px-4 sm:px-6">
          <div className="flex items-center gap-10">
            <NavLink to={homeFor(user?.roles ?? [])} aria-label="LedgerPay home">
              <Logo />
            </NavLink>
            <nav aria-label="Main" className="hidden items-center gap-1 md:flex">
              {items.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  className={({ isActive }) =>
                    cn(
                      'rounded-md px-3 py-2 text-sm font-medium text-muted-foreground hover:bg-secondary hover:text-foreground',
                      isActive && 'bg-accent text-accent-foreground',
                    )
                  }
                >
                  {item.label}
                </NavLink>
              ))}
            </nav>
          </div>
          <UserMenu />
        </div>
      </header>

      <main id="content" className="mx-auto w-full max-w-6xl flex-1 px-4 py-6 pb-28 sm:px-6 sm:py-10 md:pb-10">
        <Outlet />
      </main>

      <nav aria-label="Main" className="fixed inset-x-0 bottom-0 z-30 border-t bg-card pb-[env(safe-area-inset-bottom)] md:hidden">
        <ul className="mx-auto grid max-w-md" style={{ gridTemplateColumns: `repeat(${items.length}, minmax(0, 1fr))` }}>
          {items.map((item) => (
            <li key={item.to}>
              <NavLink
                to={item.to}
                className={({ isActive }) =>
                  cn(
                    'flex min-h-16 flex-col items-center justify-center gap-1 text-xs font-medium text-muted-foreground',
                    isActive && 'text-primary',
                  )
                }
              >
                {({ isActive }) => (
                  <>
                    <item.icon aria-hidden className={cn('size-5', isActive && 'stroke-[2.4]')} />
                    {item.label}
                  </>
                )}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
    </div>
  )
}
