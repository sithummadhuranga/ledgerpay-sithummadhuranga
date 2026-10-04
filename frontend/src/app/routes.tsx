import type { RouteObject } from 'react-router-dom'
import { LoginPage } from '@/features/auth/LoginPage'
import { RegisterPage } from '@/features/auth/RegisterPage'
import { SessionsPage } from '@/features/sessions/SessionsPage'
import { TopUpPage } from '@/features/backoffice/TopUpPage'
import { TransactionLookupPage } from '@/features/backoffice/TransactionLookupPage'
import { WalletStatusPage } from '@/features/backoffice/WalletStatusPage'
import { HistoryPage } from '@/features/history/HistoryPage'
import { LandingPage } from '@/features/landing/LandingPage'
import { SendMoneyPage } from '@/features/send/SendMoneyPage'
import { DashboardPage } from '@/features/wallet/DashboardPage'
import { AppShell } from './AppShell'
import { NotFoundPage } from './NotFoundPage'
import { ProtectedRoute } from './ProtectedRoute'

// Kept apart from the router so a test can build the same routes in memory.
// Anyone can open the landing page and the two forms. Everything else needs a sign-in, and each group names the
// roles that may open it.
export const routes: RouteObject[] = [
  { path: '/', element: <LandingPage /> },
  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppShell />,
        children: [
          { path: '/sessions', element: <SessionsPage /> },
          {
            element: <ProtectedRoute roles={['Customer']} />,
            children: [
              { path: '/wallet', element: <DashboardPage /> },
              { path: '/send', element: <SendMoneyPage /> },
              { path: '/history', element: <HistoryPage /> },
            ],
          },
          { element: <ProtectedRoute roles={['Operator']} />, children: [{ path: '/operator/top-up', element: <TopUpPage /> }] },
          {
            element: <ProtectedRoute roles={['Operator', 'Admin']} />,
            children: [
              { path: '/backoffice/wallets', element: <WalletStatusPage /> },
              { path: '/backoffice/transactions', element: <TransactionLookupPage /> },
            ],
          },
        ],
      },
    ],
  },
  { path: '*', element: <NotFoundPage /> },
]
