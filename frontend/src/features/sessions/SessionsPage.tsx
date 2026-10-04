import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ErrorState } from '@/components/ErrorState'
import { PageHeader } from '@/components/PageHeader'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/useAuth'
import { endSessionById, listSessions } from '@/lib/api/auth'
import type { SessionInfo } from '@/lib/api/types'
import { describeDevice } from '@/lib/device'
import { describeError } from '@/lib/errors'
import { formatDateTime } from '@/lib/format'

const sessionKeys = { all: ['sessions'] as const }

// Every browser that is signed in to this account, with a way to sign any of them out.
export function SessionsPage() {
  const { signOut } = useAuth()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [problem, setProblem] = useState<string | null>(null)

  const sessions = useQuery({ queryKey: sessionKeys.all, queryFn: ({ signal }) => listSessions(signal) })

  const end = useMutation({
    mutationFn: async (session: SessionInfo) => {
      await endSessionById(session.id)
      return session
    },
    onSuccess: async (session) => {
      setProblem(null)
      if (session.current) {
        // This browser's own session is gone, so there is nothing left to show here.
        void signOut()
        navigate('/login', { replace: true })
        return
      }
      await queryClient.invalidateQueries({ queryKey: sessionKeys.all })
    },
    onError: async (error) => {
      setProblem(describeError(error))
      // The list may be out of date, for example when the session was already ended from another device.
      await queryClient.invalidateQueries({ queryKey: sessionKeys.all })
    },
  })

  return (
    <>
      <PageHeader title="Your sessions" intro="The browsers where you are signed in. Sign out of any you do not recognise." />

      {problem ? (
        <Alert variant="destructive" className="mb-5 max-w-2xl">
          <AlertDescription>{problem}</AlertDescription>
        </Alert>
      ) : null}

      {sessions.isPending ? (
        <div aria-busy="true" aria-label="Loading your sessions" className="grid max-w-2xl gap-2">
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
        </div>
      ) : sessions.isError ? (
        <ErrorState message={`We could not load your sessions. ${describeError(sessions.error)}`} onRetry={() => void sessions.refetch()} />
      ) : (
        <>
          <ul aria-label="Signed-in browsers" className="max-w-2xl divide-y border-y">
            {sessions.data.map((session) => {
              const device = describeDevice(session.userAgent)
              return (
                <li key={session.id} className="flex flex-wrap items-center justify-between gap-x-6 gap-y-3 py-4">
                  <div className="min-w-0">
                    <p className="font-medium">
                      {device}
                      {session.current ? <span className="ml-2 rounded-sm border px-1.5 py-0.5 text-xs font-normal text-muted-foreground">This browser</span> : null}
                    </p>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {session.ipAddress ? <span className="num">{session.ipAddress}</span> : 'Address unknown'} · Signed in {formatDateTime(session.signedInAt)}
                    </p>
                    <p className="text-sm text-muted-foreground">Last used {formatDateTime(session.lastActiveAt)}</p>
                  </div>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={end.isPending}
                    aria-label={`Sign out ${device}${session.current ? ' (this browser)' : ''}, signed in ${formatDateTime(session.signedInAt)}`}
                    onClick={() => end.mutate(session)}
                  >
                    Sign out
                  </Button>
                </li>
              )
            })}
          </ul>
          <p className="mt-4 max-w-2xl text-sm text-muted-foreground">
            A browser you sign out can keep using the access it already has for up to 15 minutes. It cannot get more.
          </p>
        </>
      )}
    </>
  )
}
