import { PageHeader } from '@/components/PageHeader'
import { hasRole, useAuth } from '@/features/auth/useAuth'
import { DashboardPage } from '@/features/wallet/DashboardPage'

// Customers have a wallet and land on it. A back-office account has none to show.
export function Home() {
  const { user } = useAuth()

  if (hasRole(user, 'Customer')) {
    return <DashboardPage />
  }

  return (
    <>
      <PageHeader title="Signed in" />
      <p className="text-sm text-muted-foreground">This account has no wallet of its own to show.</p>
    </>
  )
}
