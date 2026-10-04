import { render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router-dom'
import { vi } from 'vitest'
import { Providers } from '@/app/providers'
import { routes as appRoutes } from '@/app/routes'
import { tokenStore } from '@/lib/api/token'
import { makeQueryClient } from '@/lib/queryClient'
import type { LoginResponse } from '@/lib/api/types'

type Reply = Response | (() => Response | Promise<Response>)

export const json = (status: number, body: unknown, headers: Record<string, string> = {}) =>
  new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json', ...headers },
  })

export const problem = (status: number, code: string, extra: Record<string, unknown> = {}, headers: Record<string, string> = {}) =>
  json(status, { title: code, status, code, traceId: 'trace-1', ...extra }, headers)

// Answers the api calls of a test. A key is the method and the path after /api/v1, for example "GET /wallets/me".
// Calling without a reply for a key fails the test, so a screen cannot make a call nobody expected.
export function mockApi(replies: Record<string, Reply>) {
  const calls: { key: string; body: unknown }[] = []
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const key = `${init?.method ?? 'GET'} ${url.replace('/api/v1', '')}`
    calls.push({ key, body: init?.body ? JSON.parse(init.body as string) : undefined })
    const reply = replies[key]
    if (!reply) {
      throw new Error(`No reply set for ${key}`)
    }
    return typeof reply === 'function' ? reply() : reply.clone()
  })
  vi.stubGlobal('fetch', fetchMock)
  return { calls, fetchMock }
}

export function renderApp(path = '/', routes: RouteObject[] = appRoutes) {
  tokenStore.clear()
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  const user = userEvent.setup()
  const view = render(
    <Providers client={makeQueryClient(false)}>
      <RouterProvider router={router} />
    </Providers>,
  )
  return { router, user, ...view }
}

export const customerLogin: LoginResponse = {
  accessToken: 'token-for-tests',
  tokenType: 'Bearer',
  expiresAt: '2026-10-04T10:15:00Z',
  fullName: 'Nimali Perera',
  roles: ['Customer'],
  walletNumber: '482915067314',
}
