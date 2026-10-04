import { createContext, useContext } from 'react'

export interface SessionUser {
  fullName: string
  roles: string[]
  walletNumber: string | null
}

export interface AuthState {
  user: SessionUser | null
  // True until the first answer to "is there a session in this browser?". A protected page waits for it.
  restoring: boolean
  // Why the last session ended, shown once on the sign-in screen.
  notice: string | null
  signIn: (email: string, password: string) => Promise<SessionUser>
  signOut: () => Promise<void>
  clearNotice: () => void
}

export const AuthContext = createContext<AuthState | null>(null)

export function useAuth(): AuthState {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used inside AuthProvider.')
  }
  return context
}

export const hasRole = (user: SessionUser | null, ...roles: string[]) => !!user && roles.some((role) => user.roles.includes(role))
