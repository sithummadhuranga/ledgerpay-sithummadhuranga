import type { RouteObject } from 'react-router-dom'
import { LoginPage } from '@/features/auth/LoginPage'
import { RegisterPage } from '@/features/auth/RegisterPage'
import { SessionsPage } from '@/features/sessions/SessionsPage'
import { AuditLogPage } from '@/features/backoffice/AuditLogPage'
import { BackOfficePage } from '@/features/backoffice/BackOfficePage'
import { StaffPage } from '@/features/backoffice/StaffPage'
import { StaffTransactionsPage } from '@/features/backoffice/StaffTransactionsPage'
import { TopUpPage } from '@/features/backoffice/TopUpPage'
import { TransactionDetailPage } from '@/features/backoffice/TransactionDetailPage'
import { UserDetailPage } from '@/features/backoffice/UserDetailPage'
import { UsersPage } from '@/features/backoffice/UsersPage'
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
              { path: '/backoffice', element: <BackOfficePage /> },
              { path: '/backoffice/users', element: <UsersPage /> },
              { path: '/backoffice/users/:walletNumber', element: <UserDetailPage /> },
              { path: '/backoffice/transactions', element: <StaffTransactionsPage /> },
              { path: '/backoffice/transactions/:reference', element: <TransactionDetailPage /> },
            ],
          },
          {
            element: <ProtectedRoute roles={['Admin']} />,
            children: [
              { path: '/backoffice/audit', element: <AuditLogPage /> },
              { path: '/backoffice/staff', element: <StaffPage /> },
            ],
          },
        ],
      },
    ],
  },
  { path: '*', element: <NotFoundPage /> },
]
