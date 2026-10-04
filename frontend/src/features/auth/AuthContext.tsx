import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { login, logout } from '@/lib/api/auth'
import { renewSession } from '@/lib/api/client'
import { ApiError } from '@/lib/api/problem'
import { tokenStore } from '@/lib/api/token'
import type { LoginResponse } from '@/lib/api/types'
import { describeError } from '@/lib/errors'
import { AuthContext, type AuthState, type SessionUser } from './useAuth'

const toUser = (response: LoginResponse): SessionUser => ({
  fullName: response.fullName,
  roles: response.roles,
  walletNumber: response.walletNumber,
})

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [user, setUser] = useState<SessionUser | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [restoring, setRestoring] = useState(true)
  const restored = useRef<Promise<void>>(Promise.resolve())

  const endSession = useCallback(() => {
    tokenStore.clear()
    setUser(null)
    queryClient.clear()
  }, [queryClient])

  // A reload forgets the access token, so the first thing the app does is ask whether the refresh cookie still
  // opens a session. Without a cookie the answer is no, and the visitor sees the page as a signed-out visitor does.
  useEffect(() => {
    let active = true
    restored.current = renewSession().then((outcome) => {
      if (active) {
        if (outcome.status === 'renewed') {
          setUser(toUser(outcome.response))
        }
        setRestoring(false)
      }
    })
    return () => {
      active = false
    }
  }, [])

  // The api client calls this when the server no longer accepts the token and the cookie could not renew it.
  useEffect(() => {
    tokenStore.whenSessionEnds(() => {
      endSession()
      setNotice(describeError(new ApiError(401, 'UNAUTHENTICATED')))
    })
    return () => tokenStore.whenSessionEnds(null)
  }, [endSession])

  const signIn = useCallback(async (email: string, password: string) => {
    // A restore that is still running must not finish after this sign-in and put the old session over the new one.
    await restored.current
    const response = await login({ email, password })
    tokenStore.set(response.accessToken)
    const signedIn = toUser(response)
    setNotice(null)
    setUser(signedIn)
    return signedIn
  }, [])

  // The screen is signed out at once. The server is told after, so the cookie stops working too. If the server cannot
  // be reached the cookie stays valid until it expires or the session is ended from another device.
  const signOut = useCallback(async () => {
    endSession()
    try {
      await logout()
    } catch {
      // Nothing more can be done from here.
    }
  }, [endSession])

  const value = useMemo<AuthState>(
    () => ({ user, restoring, notice, signIn, signOut, clearNotice: () => setNotice(null) }),
    [user, restoring, notice, signIn, signOut],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
