import { act, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { tokenStore } from '@/lib/api/token'
import { customerLogin, emptyPage, json, mockApi, operatorLogin, problem, renderApp, signIn, signOut, wallet } from '@/test/helpers'

afterEach(() => vi.unstubAllGlobals())

const dashboard = {
  'GET /wallets/me': json(200, wallet),
  'GET /wallets/me/transactions?page=1&pageSize=5': json(200, emptyPage),
}

describe('a page reload', () => {
  it('opens the page that was asked for when the refresh cookie still works', async () => {
    const { calls } = mockApi({ 'POST /auth/refresh': json(200, customerLogin), ...dashboard })

    const { router } = renderApp('/wallet')

    expect(await screen.findByText('LKR 12,450.00')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/wallet')
    expect(calls.filter((call) => call.key === 'POST /auth/refresh')).toHaveLength(1)
    expect(tokenStore.get()).toBe(customerLogin.accessToken)
  })

  it('does not send the visitor to sign in before the cookie has answered', async () => {
    let answer: (response: Response) => void = () => undefined
    const pending = new Promise<Response>((resolve) => (answer = resolve))
    mockApi({ 'POST /auth/refresh': () => pending, ...dashboard })

    const { router } = renderApp('/wallet')

    expect(await screen.findByLabelText('Opening your account')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/wallet')
    expect(screen.queryByLabelText('Email')).not.toBeInTheDocument()
    await act(async () => answer(json(200, customerLogin)))
    expect(await screen.findByText('LKR 12,450.00')).toBeInTheDocument()
  })

  it('goes to sign in, and comes back after, when there is no cookie', async () => {
    mockApi({ 'POST /auth/refresh': problem(401, 'INVALID_REFRESH_TOKEN') })

    const { router } = renderApp('/history?page=2')

    expect(await screen.findByLabelText('Email')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
    expect(router.state.location.state).toMatchObject({ from: '/history?page=2' })
    expect(screen.queryByText(/Your session ended/)).not.toBeInTheDocument()
  })

  it('treats a server that cannot be reached as signed out, with no message', async () => {
    mockApi({
      'POST /auth/refresh': () => {
        throw new TypeError('Failed to fetch')
      },
    })

    renderApp('/wallet')

    expect(await screen.findByLabelText('Email')).toBeInTheDocument()
  })

  it('takes a signed-in user who opens the sign-in page to the start page of their role', async () => {
    mockApi({ 'POST /auth/refresh': json(200, operatorLogin) })

    const { router } = renderApp('/login')

    await waitFor(() => expect(router.state.location.pathname).toBe('/operator/top-up'))
  })

  it('shows the landing page as the page of a signed-in user once the cookie has answered', async () => {
    mockApi({ 'POST /auth/refresh': json(200, customerLogin) })

    renderApp('/')

    expect((await screen.findAllByRole('link', { name: 'Open your account' })).length).toBeGreaterThanOrEqual(1)
  })

  it('does not let a slow restore put an old session over a sign-in that began meanwhile', async () => {
    let answer: (response: Response) => void = () => undefined
    const pending = new Promise<Response>((resolve) => (answer = resolve))
    const newer = { ...customerLogin, accessToken: 'token-of-the-new-sign-in' }
    mockApi({
      'POST /auth/refresh': () => pending,
      'POST /auth/login': json(200, newer),
      ...dashboard,
    })
    const { user } = renderApp('/login')
    await signIn(user)

    await act(async () => answer(json(200, { ...operatorLogin, accessToken: 'token-of-the-old-session' })))

    expect(await screen.findByText('LKR 12,450.00')).toBeInTheDocument()
    expect(tokenStore.get()).toBe('token-of-the-new-sign-in')
  })
})

describe('signing out', () => {
  it('tells the server, so the cookie stops working too', async () => {
    const { calls } = mockApi({ 'POST /auth/refresh': json(200, customerLogin), ...dashboard })
    const { user, router } = renderApp('/wallet')
    await screen.findByText('LKR 12,450.00')

    await signOut(user)

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'))
    expect(calls.filter((call) => call.key === 'POST /auth/logout')).toHaveLength(1)
    expect(tokenStore.get()).toBeNull()
  })

  it('still signs the screen out when the server cannot be told', async () => {
    mockApi({
      'POST /auth/refresh': json(200, customerLogin),
      'POST /auth/logout': () => {
        throw new TypeError('Failed to fetch')
      },
      ...dashboard,
    })
    const { user, router } = renderApp('/wallet')
    await screen.findByText('LKR 12,450.00')

    await signOut(user)

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'))
    expect(tokenStore.get()).toBeNull()
  })

  it('does not call the api again for a person who is signed out', async () => {
    const { calls } = mockApi({ 'POST /auth/refresh': json(200, customerLogin), ...dashboard })
    const { user } = renderApp('/wallet')
    await screen.findByText('LKR 12,450.00')
    await signOut(user)
    await screen.findByLabelText('Email')
    const before = calls.length

    await act(async () => new Promise((resolve) => setTimeout(resolve, 50)))

    expect(calls).toHaveLength(before)
  })
})

describe('an access token that runs out while the page is open', () => {
  it('is renewed by the cookie and the screen carries on', async () => {
    let refreshes = 0
    mockApi({
      'POST /auth/refresh': () => (++refreshes === 1 ? json(200, customerLogin) : json(200, { ...customerLogin, accessToken: 'second-token' })),
      'GET /wallets/me': () => (tokenStore.get() === 'second-token' ? json(200, wallet) : problem(401, 'UNAUTHENTICATED')),
      'GET /wallets/me/transactions?page=1&pageSize=5': json(200, emptyPage),
    })

    renderApp('/wallet')

    expect(await screen.findByText('LKR 12,450.00')).toBeInTheDocument()
    expect(refreshes).toBe(2)
  })

  it('sends the person to sign in with a message when the cookie has gone too', async () => {
    mockApi({
      'POST /auth/refresh': (() => {
        let count = 0
        return () => (++count === 1 ? json(200, customerLogin) : problem(401, 'INVALID_REFRESH_TOKEN'))
      })(),
      'GET /wallets/me': problem(401, 'UNAUTHENTICATED'),
      'GET /wallets/me/transactions?page=1&pageSize=5': json(200, emptyPage),
    })

    const { router } = renderApp('/wallet')

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'))
    expect(await screen.findByText(/Your session ended/)).toBeInTheDocument()
  })
})
