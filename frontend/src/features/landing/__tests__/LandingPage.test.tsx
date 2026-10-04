import { act, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { customerLogin, emptyPage, json, mockApi, renderApp, screenCalls, signIn, wallet } from '@/test/helpers'

afterEach(() => vi.unstubAllGlobals())

describe('the landing page', () => {
  it('says what the product is and offers to create an account or sign in', async () => {
    const { calls } = mockApi({})

    renderApp('/')

    expect(await screen.findByRole('heading', { level: 1, name: 'Send money by mobile number. See the fee first.' })).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: /create an account/i }).length).toBeGreaterThanOrEqual(1)
    expect(screen.getAllByRole('link', { name: 'Sign in' }).length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText(/mobile number that starts with \+947/)).toBeInTheDocument()
    expect(screenCalls(calls)).toHaveLength(0)
  })

  it('shows the confirm screen of a send and marks it as an example, because the names and numbers are not a real account', () => {
    mockApi({})

    renderApp('/')

    const example = screen.getByRole('figure', { name: 'Example of the confirm screen' })
    expect(within(example).getByText('Example')).toBeInTheDocument()
    expect(within(example).getByText('LKR 5,025.00')).toBeInTheDocument()
    expect(within(example).getByText('N*** P***')).toBeInTheDocument()
  })

  it('says in three plain lines what the visitor gets, and lists what keeps the numbers right', () => {
    mockApi({})

    renderApp('/')

    expect(within(screen.getByRole('region', { name: 'What you get' })).getAllByRole('listitem')).toHaveLength(3)
    expect(screen.getByRole('heading', { name: 'How it stays correct' })).toBeInTheDocument()
    expect(screen.getByText('Double entry')).toBeInTheDocument()
  })

  it('has a link to its safeguards and to the API documentation', () => {
    mockApi({})

    renderApp('/')

    const nav = screen.getByRole('navigation', { name: 'Sections' })
    expect(within(nav).getByRole('link', { name: 'How it stays correct' })).toHaveAttribute('href', '#safeguards')
    expect(within(nav).getByRole('link', { name: 'API documentation' })).toHaveAttribute('href', '/swagger')
  })

  it('offers a signed-in user their account instead of a registration', async () => {
    mockApi({
      'POST /auth/login': json(200, customerLogin),
      'GET /wallets/me': json(200, wallet),
      'GET /wallets/me/transactions?page=1&pageSize=5': json(200, emptyPage),
    })
    const { user, router } = renderApp('/login')
    await signIn(user)
    await screen.findByText('LKR 12,450.00')

    await act(async () => {
      await router.navigate('/')
    })

    expect((await screen.findAllByRole('link', { name: 'Open your account' })).length).toBeGreaterThanOrEqual(1)
    expect(screen.queryByRole('link', { name: /create an account/i })).not.toBeInTheDocument()
  })
})
