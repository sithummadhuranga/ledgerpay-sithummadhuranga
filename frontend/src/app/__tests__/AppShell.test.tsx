import { screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { adminLogin, customerLogin, emptyPage, json, openAs, operatorLogin, overviewReplies, wallet } from '@/test/helpers'

afterEach(() => vi.unstubAllGlobals())

const customerScreens = {
  'GET /wallets/me': json(200, wallet),
  'GET /wallets/me/transactions?page=1&pageSize=5': json(200, emptyPage),
}

describe('the bar and the tab bar of a signed-in page', () => {
  it('shows a customer the wallet, send and history, in the bar and in the tab bar for a phone', async () => {
    await openAs(customerLogin, customerScreens)
    await screen.findByText('LKR 12,450.00')

    const bars = screen.getAllByRole('navigation', { name: 'Main' })

    expect(bars).toHaveLength(2)
    for (const bar of bars) {
      expect(within(bar).getAllByRole('link').map((link) => link.textContent)).toEqual(['Wallet', 'Send', 'History'])
    }
  })

  it('marks the page the user is on', async () => {
    await openAs(customerLogin, customerScreens)
    await screen.findByText('LKR 12,450.00')

    const [bar] = screen.getAllByRole('navigation', { name: 'Main' })

    expect(within(bar!).getByRole('link', { name: 'Wallet' })).toHaveAttribute('aria-current', 'page')
    expect(within(bar!).getByRole('link', { name: 'Send' })).not.toHaveAttribute('aria-current')
  })

  it('shows an operator the top-up and the back-office pages, and no customer pages', async () => {
    await openAs(operatorLogin)

    const [bar] = await screen.findAllByRole('navigation', { name: 'Main' })

    expect(within(bar!).getAllByRole('link').map((link) => link.textContent)).toEqual(['Top up', 'Overview', 'Customers', 'Transactions'])
    expect(screen.queryByRole('link', { name: 'Send' })).not.toBeInTheDocument()
  })

  it('shows an admin the back office, the audit log and the staff, without the top-up', async () => {
    await openAs(adminLogin, overviewReplies)

    const [bar] = await screen.findAllByRole('navigation', { name: 'Main' })

    expect(within(bar!).getAllByRole('link').map((link) => link.textContent)).toEqual(['Overview', 'Customers', 'Transactions', 'Audit log', 'Staff'])
    expect(within(bar!).getByRole('link', { name: 'Overview' })).toHaveAttribute('aria-current', 'page')
  })

  it('lets the keyboard skip the bar', async () => {
    await openAs(customerLogin, customerScreens)

    expect(await screen.findByRole('link', { name: 'Skip to the content' })).toHaveAttribute('href', '#content')
    expect(document.getElementById('content')).not.toBeNull()
  })

  it('shows who is signed in, with the role and the wallet number, in the account menu', async () => {
    const { user } = await openAs(customerLogin, customerScreens)
    await screen.findByText('LKR 12,450.00')

    await user.click(screen.getByRole('button', { name: 'Account menu' }))

    const menu = await screen.findByRole('menu')
    expect(within(menu).getByText('Nimali Perera')).toBeInTheDocument()
    expect(within(menu).getByText('Customer')).toBeInTheDocument()
    expect(within(menu).getByText('482915067314')).toBeInTheDocument()
  })
})
