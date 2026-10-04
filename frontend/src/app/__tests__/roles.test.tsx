import { screen } from '@testing-library/react'
import { act } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { adminLogin, customerLogin, emptyPage, json, openAs, operatorLogin, wallet } from '@/test/helpers'

afterEach(() => vi.unstubAllGlobals())

const customerScreens = {
  'GET /wallets/me': json(200, wallet),
  'GET /wallets/me/transactions?page=1&pageSize=5': json(200, emptyPage),
}

async function visit(router: Awaited<ReturnType<typeof openAs>>['router'], path: string) {
  await act(async () => {
    await router.navigate(path)
  })
}

describe('who may open which page', () => {
  it('keeps an admin out of the top-up, which only an operator may make', async () => {
    const { router } = await openAs(adminLogin)
    await screen.findByRole('heading', { name: 'Freeze or unfreeze a wallet' })

    await visit(router, '/operator/top-up')

    expect(await screen.findByRole('heading', { name: 'No access' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Top up a wallet' })).not.toBeInTheDocument()
  })

  it('keeps a customer out of every back-office page', async () => {
    const { router } = await openAs(customerLogin, customerScreens)
    await screen.findByText('LKR 12,450.00')

    for (const path of ['/operator/top-up', '/backoffice/wallets', '/backoffice/transactions']) {
      await visit(router, path)
      expect(await screen.findByRole('heading', { name: 'No access' })).toBeInTheDocument()
    }
  })

  it('keeps an operator and an admin out of the customer pages', async () => {
    const operator = await openAs(operatorLogin)
    await screen.findByRole('heading', { name: 'Top up a wallet' })

    for (const path of ['/wallet', '/send', '/history']) {
      await visit(operator.router, path)
      expect(await screen.findByRole('heading', { name: 'No access' })).toBeInTheDocument()
    }
  })

  it('lets an operator open the wallet status and the transaction lookup as well as the top-up', async () => {
    const { router } = await openAs(operatorLogin)
    await screen.findByRole('heading', { name: 'Top up a wallet' })

    await visit(router, '/backoffice/wallets')
    expect(await screen.findByRole('heading', { name: 'Freeze or unfreeze a wallet' })).toBeInTheDocument()
    await visit(router, '/backoffice/transactions')
    expect(await screen.findByRole('heading', { name: 'Find a transaction' })).toBeInTheDocument()
  })

  it('sends someone who is signed in and opens the sign-in page to the first page of their role', async () => {
    const { router } = await openAs(adminLogin)
    await screen.findByRole('heading', { name: 'Freeze or unfreeze a wallet' })

    await visit(router, '/login')

    expect(router.state.location.pathname).toBe('/backoffice/wallets')
  })
})
