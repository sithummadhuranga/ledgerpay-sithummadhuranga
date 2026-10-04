import { screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { tokenStore } from '@/lib/api/token'
import { customerLogin, json, mockApi, problem, renderApp, signOut } from '@/test/helpers'

afterEach(() => vi.unstubAllGlobals())

async function fillAndSubmit(user: ReturnType<typeof renderApp>['user'], email = 'nimali.perera@example.com', password = 'Kandy-Lake-2026!') {
  await user.type(await screen.findByLabelText('Email'), email)
  await user.type(screen.getByLabelText('Password'), password)
  await user.click(screen.getByRole('button', { name: 'Sign in' }))
}

describe('the sign-in screen', () => {
  it('asks for both fields before it calls the api', async () => {
    const { fetchMock } = mockApi({})
    const { user } = renderApp('/login')

    await user.click(await screen.findByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Enter your email.')).toBeInTheDocument()
    expect(screen.getByText('Enter your password.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('says plainly that the email or password is wrong', async () => {
    mockApi({ 'POST /auth/login': problem(401, 'INVALID_CREDENTIALS') })
    const { user } = renderApp('/login')

    await fillAndSubmit(user)

    expect(await screen.findByText('The email or password is wrong.')).toBeInTheDocument()
    expect(tokenStore.get()).toBeNull()
  })

  it('tells a locked account how long to wait', async () => {
    mockApi({ 'POST /auth/login': problem(423, 'ACCOUNT_LOCKED', { retryAfterSeconds: 900 }, { 'Retry-After': '900' }) })
    const { user } = renderApp('/login')

    await fillAndSubmit(user)

    expect(await screen.findByText(/locked after too many failed sign-ins\. Try again in 15 minutes/)).toBeInTheDocument()
  })

  it('keeps the button off while the request is running, so a double click sends one request', async () => {
    let finish: (reply: Response) => void = () => undefined
    const pending = new Promise<Response>((resolve) => {
      finish = resolve
    })
    const { fetchMock } = mockApi({ 'POST /auth/login': () => pending })
    const { user } = renderApp('/login')

    await user.type(await screen.findByLabelText('Email'), 'nimali.perera@example.com')
    await user.type(screen.getByLabelText('Password'), 'Kandy-Lake-2026!')
    await user.dblClick(screen.getByRole('button', { name: 'Sign in' }))

    expect(screen.getByRole('button', { name: 'Signing in' })).toBeDisabled()
    expect(fetchMock).toHaveBeenCalledTimes(1)
    finish(problem(401, 'INVALID_CREDENTIALS'))
    await screen.findByText('The email or password is wrong.')
  })

  it('shows what the server says about a field under that field', async () => {
    mockApi({ 'POST /auth/login': problem(400, 'VALIDATION_FAILED', { errors: { email: ['Enter a valid email address.'] } }) })
    const { user } = renderApp('/login')

    await fillAndSubmit(user, 'not-an-email')

    const field = await screen.findByLabelText('Email')
    expect(field).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByText('Enter a valid email address.')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('keeps the token in memory only', async () => {
    mockApi({
      'POST /auth/login': json(200, customerLogin),
      'GET /wallets/me': json(200, { walletNumber: '482915067314', holderName: 'Nimali Perera', balance: 0, availableBalance: 0, currency: 'LKR', status: 'Active' }),
      'GET /wallets/me/transactions?page=1&pageSize=5': json(200, { items: [], page: 1, pageSize: 5, totalCount: 0, totalPages: 0 }),
    })
    const { user } = renderApp('/login')

    await fillAndSubmit(user)

    await screen.findByText('LKR 0.00')
    expect(tokenStore.get()).toBe('token-for-tests')
    expect(JSON.stringify({ ...localStorage })).not.toContain('token-for-tests')
    expect(JSON.stringify({ ...sessionStorage })).not.toContain('token-for-tests')
  })

  it('explains that the session ended when the server stops accepting the token', async () => {
    let calls = 0
    mockApi({
      'POST /auth/login': json(200, customerLogin),
      'GET /wallets/me': () => (++calls > 0 ? problem(401, 'UNAUTHENTICATED') : json(200, {})),
      'GET /wallets/me/transactions?page=1&pageSize=5': () => problem(401, 'UNAUTHENTICATED'),
    })
    const { user, router } = renderApp('/login')

    await fillAndSubmit(user)

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'))
    expect(await screen.findByText('Your session ended. Sign in again.')).toBeInTheDocument()
  })

  it.each(['//evil.example/x', '/\\evil.example', 'https://evil.example/', 'javascript:alert(1)', ''])(
    'goes to the wallet, not to %j, when the page to return to is not a path inside the app',
    async (from) => {
      mockApi({
        'POST /auth/login': json(200, customerLogin),
        'GET /wallets/me': json(200, { walletNumber: '482915067314', holderName: 'Nimali Perera', balance: 5, availableBalance: 5, currency: 'LKR', status: 'Active' }),
        'GET /wallets/me/transactions?page=1&pageSize=5': json(200, { items: [], page: 1, pageSize: 5, totalCount: 0, totalPages: 0 }),
      })
      const { user, router } = renderApp({ pathname: '/login', state: { from } })

      await fillAndSubmit(user)

      await screen.findByText('LKR 5.00')
      expect(router.state.location.pathname).toBe('/wallet')
    },
  )

  it('forgets what the last user loaded when they sign out', async () => {
    mockApi({
      'POST /auth/login': json(200, customerLogin),
      'GET /wallets/me': json(200, { walletNumber: '482915067314', holderName: 'Nimali Perera', balance: 5, availableBalance: 5, currency: 'LKR', status: 'Active' }),
      'GET /wallets/me/transactions?page=1&pageSize=5': json(200, { items: [], page: 1, pageSize: 5, totalCount: 0, totalPages: 0 }),
    })
    const { user, client } = renderApp('/login')
    await fillAndSubmit(user)
    await screen.findByText('LKR 5.00')
    expect(client.getQueryCache().getAll().length).toBeGreaterThan(0)

    await signOut(user)

    await screen.findByRole('heading', { name: 'Sign in' })
    expect(client.getQueryCache().getAll()).toHaveLength(0)
  })
})
