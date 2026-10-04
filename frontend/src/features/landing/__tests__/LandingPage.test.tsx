import { act, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { customerLogin, emptyPage, json, mockApi, renderApp, signIn, wallet } from '@/test/helpers'

afterEach(() => vi.unstubAllGlobals())

describe('the landing page', () => {
  it('says what the product is and offers to create an account or sign in', async () => {
    const { fetchMock } = mockApi({})

    renderApp('/')

    expect(await screen.findByRole('heading', { level: 1, name: /send rupees the way a bank would count them/i })).toBeInTheDocument()
    expect(screen.getAllByRole('link', { name: /create an account/i }).length).toBeGreaterThanOrEqual(2)
    expect(screen.getAllByRole('link', { name: 'Sign in' }).length).toBeGreaterThanOrEqual(1)
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('marks the statement as an example, because the names and numbers are not a real account', () => {
    mockApi({})

    renderApp('/')

    const example = screen.getByRole('figure', { name: 'Example statement' })
    expect(within(example).getByText('Example')).toBeInTheDocument()
  })

  it('explains how it works in order and lists what keeps the numbers right', () => {
    mockApi({})

    renderApp('/')

    const steps = within(screen.getByRole('heading', { name: 'How it works' }).closest('section')!).getAllByRole('listitem')
    expect(steps).toHaveLength(4)
    expect(screen.getByRole('heading', { name: 'What keeps the numbers right' })).toBeInTheDocument()
    expect(screen.getByText('Double entry')).toBeInTheDocument()
  })

  it('has links to its own sections and to the API documentation', () => {
    mockApi({})

    renderApp('/')

    const nav = screen.getByRole('navigation', { name: 'Sections' })
    expect(within(nav).getByRole('link', { name: 'How it works' })).toHaveAttribute('href', '#how')
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
