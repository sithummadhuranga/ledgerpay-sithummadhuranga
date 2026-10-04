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
  const calls: { key: string; body: unknown; headers: Record<string, string> }[] = []
  const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
    const key = `${init?.method ?? 'GET'} ${url.replace('/api/v1', '')}`
    calls.push({ key, body: init?.body ? JSON.parse(init.body as string) : undefined, headers: (init?.headers ?? {}) as Record<string, string> })
    const reply = replies[key]
    if (!reply) {
      throw new Error(`No reply set for ${key}`)
    }
    return typeof reply === 'function' ? reply() : reply.clone()
  })
  vi.stubGlobal('fetch', fetchMock)
  return { calls, fetchMock }
}

export function renderApp(entry: string | { pathname: string; state?: unknown } = '/', routes: RouteObject[] = appRoutes) {
  tokenStore.clear()
  const router = createMemoryRouter(routes, { initialEntries: [entry] })
  const user = userEvent.setup()
  const client = makeQueryClient(false)
  const view = render(
    <Providers client={client}>
      <RouterProvider router={router} />
    </Providers>,
  )
  return { router, user, client, ...view }
}

export const customerLogin: LoginResponse = {
  accessToken: 'token-for-tests',
  tokenType: 'Bearer',
  expiresAt: '2026-10-04T10:15:00Z',
  fullName: 'Nimali Perera',
  roles: ['Customer'],
  walletNumber: '482915067314',
}

export const operatorLogin: LoginResponse = { ...customerLogin, fullName: 'Dilani Senanayake', roles: ['Operator'], walletNumber: null }

export const adminLogin: LoginResponse = { ...customerLogin, fullName: 'Chamara Rajapaksa', roles: ['Admin'], walletNumber: null }

export const wallet = { walletNumber: '482915067314', holderName: 'Nimali Perera', balance: 12450, availableBalance: 12450, currency: 'LKR', status: 'Active' }

export const emptyPage = { items: [], page: 1, pageSize: 5, totalCount: 0, totalPages: 0 }

// Fills the sign-in form and submits it.
export async function signIn(user: ReturnType<typeof renderApp>['user'], email = 'nimali.perera@example.com') {
  const { screen } = await import('@testing-library/react')
  await user.type(await screen.findByLabelText('Email'), email)
  await user.type(screen.getByLabelText('Password'), 'Kandy-Lake-2026!')
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
}

// Opens the account menu and chooses Sign out.
export async function signOut(user: ReturnType<typeof renderApp>['user']) {
  const { screen } = await import('@testing-library/react')
  await user.click(screen.getByRole('button', { name: 'Account menu' }))
  await user.click(await screen.findByRole('menuitem', { name: /sign out/i }))
}

// Signs a user in through the real sign-in screen and returns the rendered app, so a test starts where a person would.
export async function openAs(login: LoginResponse, replies: Record<string, Reply> = {}, start = '/login') {
  const api = mockApi({ 'POST /auth/login': json(200, login), ...replies })
  const view = renderApp(start)
  await signIn(view.user, 'someone@example.com')
  return { ...view, ...api }
}

