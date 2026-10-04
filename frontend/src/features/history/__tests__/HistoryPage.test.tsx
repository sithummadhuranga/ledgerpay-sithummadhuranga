import { act, fireEvent, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { HistoryItem } from '@/lib/api/types'
import { customerLogin, json, openAs, problem } from '@/test/helpers'

afterEach(() => vi.unstubAllGlobals())

const item = (n: number, over: Partial<HistoryItem> = {}): HistoryItem => ({
  reference: `TX${n}`, type: 'Transfer', direction: 'Sent', amount: 1000 + n, fee: 10, counterpartyName: 'K*** J***', note: null,
  status: 'Completed', createdAt: '2026-10-04T09:05:00Z', balanceAfter: 5000 - n, failureCode: null, ...over,
})

const pageOf = (items: HistoryItem[], page = 1, totalCount = items.length, totalPages = 1) =>
  json(200, { items, page, pageSize: 10, totalCount, totalPages })
const url = (query: string) => `GET /wallets/me/transactions?${query}`

describe('the history page', () => {
  it('lists the entries with the fee and the balance after each one', async () => {
    await openAs(customerLogin, { [url('page=1&pageSize=10')]: pageOf([item(1), item(2, { direction: 'Received', fee: 0 })]) }, '/history')

    const table = await screen.findByRole('table', { name: 'Your transactions, newest first' })

    const rows = within(table).getAllByRole('row')
    expect(rows).toHaveLength(3)
    expect(within(rows[1]!).getByText('-LKR 1,001.00')).toBeInTheDocument()
    expect(within(rows[1]!).getByText('LKR 10.00')).toBeInTheDocument()
    expect(within(rows[1]!).getAllByText('LKR 4,999.00').length).toBeGreaterThanOrEqual(1)
    expect(within(rows[2]!).getByText('+LKR 1,002.00')).toBeInTheDocument()
  })

  it('says how many pages there are and turns Previous off on the first one', async () => {
    await openAs(customerLogin, { [url('page=1&pageSize=10')]: pageOf([item(1)], 1, 25, 3) }, '/history')

    expect(await screen.findByText('Page 1 of 3. 25 entries.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /previous/i })).toBeDisabled()
    expect(screen.getByRole('button', { name: /next/i })).toBeEnabled()
  })

  it('asks for the next page and keeps it in the address', async () => {
    const { user, router, calls } = await openAs(
      customerLogin,
      { [url('page=1&pageSize=10')]: pageOf([item(1)], 1, 25, 3), [url('page=2&pageSize=10')]: pageOf([item(11)], 2, 25, 3) },
      '/history',
    )
    await screen.findByText('Page 1 of 3. 25 entries.')

    await user.click(screen.getByRole('button', { name: /next/i }))

    expect(await screen.findByText('Page 2 of 3. 25 entries.')).toBeInTheDocument()
    expect(router.state.location.search).toBe('?page=2')
    expect(calls.some((call) => call.key === url('page=2&pageSize=10'))).toBe(true)
    expect(screen.getByRole('button', { name: /previous/i })).toBeEnabled()
  })

  it('reads the filters from the address, so a refresh keeps them', async () => {
    const { calls } = await openAs(
      customerLogin,
      { [url('page=2&pageSize=10&from=2026-10-01&to=2026-10-04')]: pageOf([item(11)], 2, 12, 2) },
      '/history?page=2&from=2026-10-01&to=2026-10-04',
    )

    await screen.findByRole('table')

    expect(screen.getByLabelText('From')).toHaveValue('2026-10-01')
    expect(screen.getByLabelText('To')).toHaveValue('2026-10-04')
    expect(calls.at(-1)?.key).toBe(url('page=2&pageSize=10&from=2026-10-01&to=2026-10-04'))
  })

  it('applies a date range from page one and puts it in the address', async () => {
    const { user, router } = await openAs(
      customerLogin,
      { [url('page=1&pageSize=10')]: pageOf([item(1)]), [url('page=1&pageSize=10&from=2026-10-02&to=2026-10-03')]: pageOf([item(3)]) },
      '/history',
    )
    await screen.findByRole('table')

    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-10-02' } })
    fireEvent.change(screen.getByLabelText('To'), { target: { value: '2026-10-03' } })
    await user.click(screen.getByRole('button', { name: 'Apply' }))

    expect(await screen.findByText('LKR 4,997.00', { selector: 'td' })).toBeInTheDocument()
    expect(router.state.location.search).toBe('?from=2026-10-02&to=2026-10-03')
  })

  it('clears the range and goes back to everything', async () => {
    const { user, router } = await openAs(
      customerLogin,
      { [url('page=1&pageSize=10')]: pageOf([item(1)]), [url('page=1&pageSize=10&from=2026-10-02')]: pageOf([item(3)]) },
      '/history?from=2026-10-02',
    )
    await screen.findByRole('table')

    await user.click(screen.getByRole('button', { name: 'Clear' }))

    expect(await screen.findByLabelText('From')).toHaveValue('')
    expect(router.state.location.search).toBe('')
  })

  it('will not apply a range that ends before it starts, and says why', async () => {
    const { user, calls } = await openAs(customerLogin, { [url('page=1&pageSize=10')]: pageOf([item(1)]) }, '/history')
    await screen.findByRole('table')
    const before = calls.length

    fireEvent.change(screen.getByLabelText('From'), { target: { value: '2026-10-05' } })
    fireEvent.change(screen.getByLabelText('To'), { target: { value: '2026-10-04' } })

    expect(await screen.findByText('The from date is after the to date.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Apply' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Apply' }))
    expect(calls).toHaveLength(before)
  })

  it('says so when a date range holds nothing', async () => {
    await openAs(customerLogin, { [url('page=1&pageSize=10&from=2026-01-01')]: pageOf([]) }, '/history?from=2026-01-01')

    expect(await screen.findByText('No transactions in this date range.')).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('says there is nothing yet when the wallet has no entries at all', async () => {
    await openAs(customerLogin, { [url('page=1&pageSize=10')]: pageOf([]) }, '/history')

    expect(await screen.findByText('No transactions yet. Money you send or receive will show here.')).toBeInTheDocument()
  })

  it('shows a refused transfer as failed with its reason and no balance', async () => {
    await openAs(customerLogin, { [url('page=1&pageSize=10')]: pageOf([item(1, { status: 'Failed', failureCode: 'INSUFFICIENT_FUNDS', balanceAfter: null, fee: 0 })]) }, '/history')

    expect(await screen.findByText(/Failed\. Your balance does not cover the amount plus the fee\./)).toBeInTheDocument()
  })

  it('says what failed and tries again when asked', async () => {
    let attempts = 0
    const { user } = await openAs(
      customerLogin,
      { [url('page=1&pageSize=10')]: () => (++attempts === 1 ? problem(500, 'INTERNAL_ERROR') : pageOf([item(1)])) },
      '/history',
    )

    expect(await screen.findByText(/We could not load your transactions\./)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('table')).toBeInTheDocument()
  })

  it('shows a skeleton while the first page loads', async () => {
    const never = new Promise<Response>(() => undefined)
    await openAs(customerLogin, { [url('page=1&pageSize=10')]: () => never }, '/history')

    expect(await screen.findByLabelText('Loading your transactions')).toHaveAttribute('aria-busy', 'true')
  })

  it('says the date filter counts days in UTC', async () => {
    await openAs(customerLogin, { [url('page=1&pageSize=10')]: pageOf([item(1)]) }, '/history')

    expect(await screen.findByText(/counts whole days in UTC/)).toBeInTheDocument()
  })

  it('offers the last page when the address asks for one past the end', async () => {
    const { user, router } = await openAs(
      customerLogin,
      {
        [url('page=99&pageSize=10')]: pageOf([], 99, 25, 3),
        [url('page=3&pageSize=10')]: pageOf([item(25)], 3, 25, 3),
      },
      '/history?page=99',
    )

    expect(await screen.findByText('That page does not exist.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Go to the last page' }))

    expect(await screen.findByText('Page 3 of 3. 25 entries.')).toBeInTheDocument()
    expect(router.state.location.search).toBe('?page=3')
  })

  it('shows the dates of the address again when the user goes Back', async () => {
    const { router } = await openAs(
      customerLogin,
      { [url('from=2026-10-01&page=1&pageSize=10')]: pageOf([item(1)]), [url('page=1&pageSize=10')]: pageOf([item(2)]) },
      '/history?from=2026-10-01',
    )
    expect(await screen.findByLabelText('From')).toHaveValue('2026-10-01')

    await act(async () => {
      await router.navigate('/history')
    })

    expect(await screen.findByLabelText('From')).toHaveValue('')
  })
})
