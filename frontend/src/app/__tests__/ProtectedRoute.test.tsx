import { screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { RouteObject } from 'react-router-dom'
import { ProtectedRoute } from '@/app/ProtectedRoute'
import { routes as appRoutes } from '@/app/routes'
import { tokenStore } from '@/lib/api/token'
import { customerLogin, json, mockApi, renderApp } from '@/test/helpers'

afterEach(() => vi.unstubAllGlobals())

const wallet = { walletNumber: '482915067314', holderName: 'Nimali Perera', balance: 12450, availableBalance: 12450, currency: 'LKR', status: 'Active' }
const emptyPage = { items: [], page: 1, pageSize: 5, totalCount: 0, totalPages: 0 }

async function signIn(user: ReturnType<typeof renderApp>['user']) {
  await user.type(await screen.findByLabelText('Email'), 'nimali.perera@example.com')
  await user.type(screen.getByLabelText('Password'), 'Kandy-Lake-2026!')
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
}

describe('protected routes', () => {
  it('sends someone who is not signed in to the sign-in screen', async () => {
    mockApi({})

    const { router } = renderApp('/')

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
  })

  it('brings the user back to the page they asked for once they have signed in', async () => {
    mockApi({
      'POST /auth/login': json(200, customerLogin),
      'GET /wallets/me': json(200, wallet),
      'GET /wallets/me/transactions?page=1&pageSize=5': json(200, emptyPage),
    })

    const { user, router } = renderApp('/')
    await signIn(user)

    expect(await screen.findByText('LKR 12,450.00')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/')
  })

  it('keeps a signed-in user out of a page for another role', async () => {
    mockApi({ 'POST /auth/login': json(200, customerLogin) })
    const routes: RouteObject[] = [
      ...appRoutes,
      { element: <ProtectedRoute roles={['Operator']} />, children: [{ path: '/operator', element: <p>top-up form</p> }] },
    ]

    const { user, router } = renderApp('/operator', routes)
    await signIn(user)

    expect(await screen.findByRole('heading', { name: 'No access' })).toBeInTheDocument()
    expect(screen.queryByText('top-up form')).not.toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/operator')
  })

  it('lets a user with the role in', async () => {
    mockApi({ 'POST /auth/login': json(200, { ...customerLogin, roles: ['Operator'], walletNumber: null }) })
    const routes: RouteObject[] = [
      ...appRoutes,
      { element: <ProtectedRoute roles={['Operator']} />, children: [{ path: '/operator', element: <p>top-up form</p> }] },
    ]

    const { user } = renderApp('/operator', routes)
    await signIn(user)

    expect(await screen.findByText('top-up form')).toBeInTheDocument()
  })

  it('forgets the token on sign out and asks for a new sign in', async () => {
    mockApi({
      'POST /auth/login': json(200, customerLogin),
      'GET /wallets/me': json(200, wallet),
      'GET /wallets/me/transactions?page=1&pageSize=5': json(200, emptyPage),
    })
    const { user, router } = renderApp('/')
    await signIn(user)
    await screen.findByText('LKR 12,450.00')
    expect(tokenStore.get()).toBe('token-for-tests')

    await user.click(screen.getByRole('button', { name: /sign out/i }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'))
    expect(tokenStore.get()).toBeNull()
  })
})
