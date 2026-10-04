import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { login } from '@/lib/api/auth'
import { ApiError } from '@/lib/api/problem'
import { tokenStore } from '@/lib/api/token'
import { describeError } from '@/lib/errors'
import { AuthContext, type AuthState, type SessionUser } from './useAuth'

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [user, setUser] = useState<SessionUser | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const endSession = useCallback(() => {
    tokenStore.clear()
    setUser(null)
    queryClient.clear()
  }, [queryClient])

  // The api client calls this when the server no longer accepts the token.
  useEffect(() => {
    tokenStore.whenSessionEnds(() => {
      endSession()
      setNotice(describeError(new ApiError(401, 'UNAUTHENTICATED')))
    })
    return () => tokenStore.whenSessionEnds(null)
  }, [endSession])

  const signIn = useCallback(async (email: string, password: string) => {
    const response = await login({ email, password })
    tokenStore.set(response.accessToken)
    const signedIn = { fullName: response.fullName, roles: response.roles, walletNumber: response.walletNumber }
    setNotice(null)
    setUser(signedIn)
    return signedIn
  }, [])

  const value = useMemo<AuthState>(
    () => ({ user, notice, signIn, signOut: endSession, clearNotice: () => setNotice(null) }),
    [user, notice, signIn, endSession],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
