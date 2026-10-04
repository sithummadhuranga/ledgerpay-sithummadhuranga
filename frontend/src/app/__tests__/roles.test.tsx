import { screen } from '@testing-library/react'
import { act } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { adminLogin, customerLogin, emptyList, emptyPage, json, openAs, operatorLogin, overviewReplies, wallet } from '@/test/helpers'

afterEach(() => vi.unstubAllGlobals())

const staffScreens = {
  ...overviewReplies,
  'GET /admin/users?page=1&pageSize=10': json(200, { ...emptyList, pageSize: 10 }),
  'GET /admin/transactions?page=1&pageSize=10': json(200, { ...emptyList, pageSize: 10 }),
  'GET /admin/audit-logs?page=1&pageSize=20': json(200, { ...emptyList, pageSize: 20 }),
  'GET /admin/staff': json(200, []),
}

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
    const { router } = await openAs(adminLogin, staffScreens)
    await screen.findByRole('heading', { name: 'Needs attention' })

    await visit(router, '/operator/top-up')

    expect(await screen.findByRole('heading', { name: 'No access' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Top up a wallet' })).not.toBeInTheDocument()
  })

  it('keeps a customer out of every back-office page', async () => {
    const { router } = await openAs(customerLogin, customerScreens)
    await screen.findByText('LKR 12,450.00')

    for (const path of [
      '/operator/top-up', '/backoffice', '/backoffice/users', '/backoffice/users/482915067314', '/backoffice/transactions',
      '/backoffice/transactions/TX1', '/backoffice/audit', '/backoffice/staff',
    ]) {
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

  it('lets an operator open the shared back office, and not the audit log or the staff, which are for admins', async () => {
    const { router } = await openAs(operatorLogin, staffScreens)
    await screen.findByRole('heading', { name: 'Top up a wallet' })

    await visit(router, '/backoffice')
    expect(await screen.findByRole('heading', { name: 'Needs attention' })).toBeInTheDocument()
    await visit(router, '/backoffice/users')
    expect(await screen.findByRole('heading', { name: 'Customers' })).toBeInTheDocument()
    await visit(router, '/backoffice/transactions')
    expect(await screen.findByRole('heading', { name: 'Transactions' })).toBeInTheDocument()
    for (const path of ['/backoffice/audit', '/backoffice/staff']) {
      await visit(router, path)
      expect(await screen.findByRole('heading', { name: 'No access' })).toBeInTheDocument()
    }
  })

  it('lets an admin open the audit log and the staff as well as the shared back office', async () => {
    const { router } = await openAs(adminLogin, staffScreens)
    await screen.findByRole('heading', { name: 'Needs attention' })

    await visit(router, '/backoffice/audit')
    expect(await screen.findByRole('heading', { name: 'Audit log' })).toBeInTheDocument()
    await visit(router, '/backoffice/staff')
    expect(await screen.findByRole('heading', { name: 'Staff' })).toBeInTheDocument()
    await visit(router, '/backoffice/users')
    expect(await screen.findByRole('heading', { name: 'Customers' })).toBeInTheDocument()
  })

  it('sends someone who is signed in and opens the sign-in page to the first page of their role', async () => {
    const { router } = await openAs(adminLogin, staffScreens)
    await screen.findByRole('heading', { name: 'Needs attention' })

    await visit(router, '/login')

    expect(router.state.location.pathname).toBe('/backoffice')
  })
})
