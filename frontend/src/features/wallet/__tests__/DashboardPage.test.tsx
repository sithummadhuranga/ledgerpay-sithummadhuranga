import { screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { HistoryItem } from '@/lib/api/types'
import { customerLogin, json, mockApi, problem, renderApp } from '@/test/helpers'

afterEach(() => vi.unstubAllGlobals())

const wallet = { walletNumber: '482915067314', holderName: 'Nimali Perera', balance: 12450, availableBalance: 12450, currency: 'LKR', status: 'Active' }
const recentKey = 'GET /wallets/me/transactions?page=1&pageSize=5'

const sent: HistoryItem = {
  reference: 'TX1', type: 'Transfer', direction: 'Sent', amount: 5000, fee: 25, counterpartyName: 'K*** J***', note: 'rent',
  status: 'Completed', createdAt: '2026-10-04T09:05:00Z', balanceAfter: 12450, failureCode: null,
}
const received: HistoryItem = {
  ...sent, reference: 'TX2', direction: 'Received', amount: 750.5, fee: 0, counterpartyName: 'T*** F***', note: null,
}
const topUp: HistoryItem = { ...sent, reference: 'TX3', type: 'TopUp', direction: 'Received', amount: 20000, fee: 0, counterpartyName: null, note: null }
const refused: HistoryItem = {
  ...sent, reference: 'TX4', status: 'Failed', failureCode: 'INSUFFICIENT_FUNDS', balanceAfter: null, amount: 90000,
}

async function open(replies: Record<string, Parameters<typeof mockApi>[0][string]>) {
  mockApi({ 'POST /auth/login': json(200, customerLogin), ...replies })
  const view = renderApp('/login')
  await view.user.type(await screen.findByLabelText('Email'), 'nimali.perera@example.com')
  await view.user.type(screen.getByLabelText('Password'), 'Kandy-Lake-2026!')
  await view.user.click(screen.getByRole('button', { name: 'Sign in' }))
  return view
}

const page = (items: HistoryItem[]) => json(200, { items, page: 1, pageSize: 5, totalCount: items.length, totalPages: items.length ? 1 : 0 })

describe('the dashboard', () => {
  it('shows skeletons while the balance and the transactions load', async () => {
    const never = new Promise<Response>(() => undefined)
    await open({ 'GET /wallets/me': () => never, [recentKey]: () => never })

    expect(await screen.findByLabelText('Loading your balance')).toHaveAttribute('aria-busy', 'true')
    expect(screen.getByLabelText('Loading your transactions')).toBeInTheDocument()
  })

  it('shows the balance, the wallet number and the last transactions as a table', async () => {
    await open({ 'GET /wallets/me': json(200, wallet), [recentKey]: page([sent, received, topUp]) })

    expect(await screen.findByText('LKR 12,450.00')).toBeInTheDocument()
    expect(screen.getByText('482915067314', { selector: 'span.text-foreground' })).toBeInTheDocument()
    const table = screen.getByRole('table', { name: 'Your last transactions, newest first' })
    expect(within(table).getAllByRole('row')).toHaveLength(4)
    expect(within(table).getByText('Sent to K*** J***')).toBeInTheDocument()
    expect(within(table).getByText('-LKR 5,000.00')).toBeInTheDocument()
    expect(within(table).getByText('+LKR 750.50')).toBeInTheDocument()
    expect(within(table).getByText('Bank top-up')).toBeInTheDocument()
    expect(within(table).getByText('rent')).toBeInTheDocument()
  })

  it('says a refused transfer failed and why, in words', async () => {
    await open({ 'GET /wallets/me': json(200, wallet), [recentKey]: page([refused]) })

    const table = await screen.findByRole('table')

    expect(within(table).getByText(/Failed\. Your balance does not cover the amount plus the fee\./)).toBeInTheDocument()
    expect(within(table).getByText('LKR 90,000.00')).toBeInTheDocument()
  })

  it('says there is nothing yet when there are no transactions', async () => {
    await open({ 'GET /wallets/me': json(200, wallet), [recentKey]: page([]) })

    expect(await screen.findByText('No transactions yet. Money you send or receive will show here.')).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('says what failed and lets the user try again', async () => {
    let attempts = 0
    const { user } = await open({
      'GET /wallets/me': json(200, wallet),
      [recentKey]: () => (++attempts === 1 ? problem(500, 'INTERNAL_ERROR') : page([sent])),
    })

    expect(await screen.findByText(/We could not load your transactions\./)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByText('Sent to K*** J***')).toBeInTheDocument()
  })

  it('tells the holder a frozen wallet is frozen, without a reason', async () => {
    await open({ 'GET /wallets/me': json(200, { ...wallet, status: 'Frozen' }), [recentKey]: page([]) })

    expect(await screen.findByText(/This wallet is frozen\./)).toBeInTheDocument()
  })

  it('copies the wallet number', async () => {
    const { user } = await open({ 'GET /wallets/me': json(200, wallet), [recentKey]: page([]) })

    await user.click(await screen.findByRole('button', { name: 'Copy wallet number' }))

    expect(await navigator.clipboard.readText()).toBe('482915067314')
    expect(screen.getByText('Copied')).toBeInTheDocument()
  })
})
