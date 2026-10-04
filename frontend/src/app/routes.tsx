import type { RouteObject } from 'react-router-dom'
import { LoginPage } from '@/features/auth/LoginPage'
import { RegisterPage } from '@/features/auth/RegisterPage'
import { AppLayout } from './AppLayout'
import { Home } from './Home'
import { NotFoundPage } from './NotFoundPage'
import { ProtectedRoute } from './ProtectedRoute'

// Kept apart from the router so a test can build the same routes in memory.
export const routes: RouteObject[] = [
  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },
  {
    element: <ProtectedRoute />,
    children: [{ element: <AppLayout />, children: [{ index: true, element: <Home /> }] }],
  },
  { path: '*', element: <NotFoundPage /> },
]
