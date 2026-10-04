import { act, screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { SessionInfo } from '@/lib/api/types'
import { tokenStore } from '@/lib/api/token'
import { adminLogin, customerLogin, emptyPage, json, mockApi, problem, renderApp, wallet } from '@/test/helpers'

afterEach(() => vi.unstubAllGlobals())

// The browsers in the list, and not the links of the app around it.
const browsers = async () => within(await screen.findByRole('list', { name: 'Signed-in browsers' })).getAllByRole('listitem')

const chromeMac = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36'
const safariIphone = 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1'

const here: SessionInfo = {
  id: '11111111-1111-1111-1111-111111111111', signedInAt: '2026-10-04T09:00:00Z', lastActiveAt: '2026-10-04T09:30:00Z',
  ipAddress: '203.0.113.7', userAgent: chromeMac, current: true,
}
const there: SessionInfo = {
  id: '22222222-2222-2222-2222-222222222222', signedInAt: '2026-10-02T18:00:00Z', lastActiveAt: '2026-10-03T08:15:00Z',
  ipAddress: '198.51.100.20', userAgent: safariIphone, current: false,
}

const signedInAs = (login: typeof customerLogin) => ({
  'POST /auth/refresh': json(200, login),
  'GET /wallets/me': json(200, wallet),
  'GET /wallets/me/transactions?page=1&pageSize=5': json(200, emptyPage),
})

describe('the sessions page', () => {
  it('lists each browser with its device, address and times, and marks this one', async () => {
    mockApi({ ...signedInAs(customerLogin), 'GET /auth/sessions': json(200, [here, there]) })

    renderApp('/sessions')

    const items = await browsers()
    expect(items).toHaveLength(2)
    expect(within(items[0]!).getByText('Chrome on macOS')).toBeInTheDocument()
    expect(within(items[0]!).getByText('This browser')).toBeInTheDocument()
    expect(within(items[0]!).getByText('203.0.113.7')).toBeInTheDocument()
    expect(within(items[1]!).getByText('Safari on iPhone')).toBeInTheDocument()
    expect(within(items[1]!).queryByText('This browser')).not.toBeInTheDocument()
  })

  it('opens for every role, since everyone has sessions', async () => {
    mockApi({ ...signedInAs(adminLogin), 'GET /auth/sessions': json(200, [here]) })

    renderApp('/sessions')

    expect(await screen.findByRole('heading', { name: 'Your sessions' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'No access' })).not.toBeInTheDocument()
  })

  it('is one click from the account menu', async () => {
    mockApi({ ...signedInAs(customerLogin), 'GET /auth/sessions': json(200, [here]) })
    const { user, router } = renderApp('/wallet')
    await screen.findByText('LKR 12,450.00')

    await user.click(screen.getByRole('button', { name: 'Account menu' }))
    await user.click(await screen.findByRole('menuitem', { name: 'Your sessions' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/sessions'))
  })

  it('signs another browser out and shows the list again', async () => {
    let listed = [here, there]
    const { calls } = mockApi({
      ...signedInAs(customerLogin),
      'GET /auth/sessions': () => json(200, listed),
      [`DELETE /auth/sessions/${there.id}`]: () => {
        listed = [here]
        return new Response(null, { status: 204 })
      },
    })
    const { user } = renderApp('/sessions')
    await browsers()

    await user.click(screen.getByRole('button', { name: /^Sign out Safari on iPhone/ }))

    await waitFor(async () => expect(await browsers()).toHaveLength(1))
    expect(calls.filter((call) => call.key === `DELETE /auth/sessions/${there.id}`)).toHaveLength(1)
    expect(screen.queryByText('Safari on iPhone')).not.toBeInTheDocument()
  })

  it('signs this browser out when its own session is ended, and goes to sign in', async () => {
    const { calls } = mockApi({
      ...signedInAs(customerLogin),
      'GET /auth/sessions': json(200, [here, there]),
      [`DELETE /auth/sessions/${here.id}`]: new Response(null, { status: 204 }),
    })
    const { user, router } = renderApp('/sessions')
    await browsers()

    await user.click(screen.getByRole('button', { name: /^Sign out Chrome on macOS \(this browser\)/ }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'))
    expect(tokenStore.get()).toBeNull()
    expect(calls.filter((call) => call.key === 'POST /auth/logout')).toHaveLength(1)
  })

  it('says so and shows the list again when the session was already gone', async () => {
    let listed = [here, there]
    mockApi({
      ...signedInAs(customerLogin),
      'GET /auth/sessions': () => json(200, listed),
      [`DELETE /auth/sessions/${there.id}`]: () => {
        listed = [here]
        return problem(404, 'SESSION_NOT_FOUND')
      },
    })
    const { user } = renderApp('/sessions')
    await browsers()

    await user.click(screen.getByRole('button', { name: /^Sign out Safari on iPhone/ }))

    expect(await screen.findByText('That session is already signed out.')).toBeInTheDocument()
    await waitFor(async () => expect(await browsers()).toHaveLength(1))
  })

  it('keeps the buttons off while a sign out is running', async () => {
    let finish: (response: Response) => void = () => undefined
    const running = new Promise<Response>((resolve) => (finish = resolve))
    mockApi({
      ...signedInAs(customerLogin),
      'GET /auth/sessions': json(200, [here, there]),
      [`DELETE /auth/sessions/${there.id}`]: () => running,
    })
    const { user } = renderApp('/sessions')
    await browsers()

    await user.click(screen.getByRole('button', { name: /^Sign out Safari on iPhone/ }))

    await waitFor(() => screen.getAllByRole('button', { name: /^Sign out / }).forEach((button) => expect(button).toBeDisabled()))
    await act(async () => finish(new Response(null, { status: 204 })))
  })

  it('shows a loading state and then an error with a way to try again', async () => {
    let attempts = 0
    mockApi({
      ...signedInAs(customerLogin),
      'GET /auth/sessions': () => (++attempts === 1 ? problem(500, 'INTERNAL_ERROR') : json(200, [here])),
    })
    const { user } = renderApp('/sessions')

    expect(await screen.findByRole('alert')).toHaveTextContent('We could not load your sessions.')
    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('Chrome on macOS')).toBeInTheDocument()
  })

  it('tells the person that a signed-out browser keeps its access for a short while', async () => {
    mockApi({ ...signedInAs(customerLogin), 'GET /auth/sessions': json(200, [here]) })

    renderApp('/sessions')

    expect(await screen.findByText(/up to 15 minutes/)).toBeInTheDocument()
  })
})
