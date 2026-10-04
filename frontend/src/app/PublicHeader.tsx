import { Link } from 'react-router-dom'
import { Logo } from '@/components/Logo'
import { ButtonLink } from '@/components/ButtonLink'
import { useAuth } from '@/features/auth/useAuth'
import { homeFor } from './navigation'

// The bar on the pages anyone can open. The links in the middle are for the landing page only.
export function PublicHeader({ showSections = false }: { showSections?: boolean }) {
  const { user } = useAuth()

  return (
    <header className="sticky top-0 z-30 border-b bg-background">
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-4 sm:px-6">
        <Link to="/" aria-label="LedgerPay home">
          <Logo />
        </Link>
        {showSections ? (
          <nav aria-label="Sections" className="hidden items-center gap-8 text-sm md:flex">
            <a href="#how" className="text-muted-foreground hover:text-foreground">
              How it works
            </a>
            <a href="#safeguards" className="text-muted-foreground hover:text-foreground">
              Safeguards
            </a>
            <a href="/swagger" className="text-muted-foreground hover:text-foreground">
              API documentation
            </a>
          </nav>
        ) : null}
        <div className="flex items-center gap-2">
          {user ? (
            <ButtonLink to={homeFor(user.roles)}>
              Open your account
            </ButtonLink>
          ) : (
            <>
              <ButtonLink to="/login" variant="ghost">
                Sign in
              </ButtonLink>
              <ButtonLink to="/register" className="max-sm:hidden">
                Create an account
              </ButtonLink>
            </>
          )}
        </div>
      </div>
    </header>
  )
}
