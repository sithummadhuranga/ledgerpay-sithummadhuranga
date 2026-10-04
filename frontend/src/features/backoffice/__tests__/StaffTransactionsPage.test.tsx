import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { adminLogin, json, openAs, operatorLogin, problem } from '@/test/helpers'
import { page, refused, topUp, transfer } from './samples'

afterEach(() => vi.unstubAllGlobals())

const list = (query: string) => `GET /admin/transactions?${query}`
const rows = async () => within(await screen.findByRole('list', { name: 'Transactions' })).getAllByRole('listitem')

describe('the transactions page', () => {
  it('lists both parties, the amount and the fee, and where the money came from for a top-up', async () => {
    await openAs(operatorLogin, { [list('page=1&pageSize=10')]: json(200, page([transfer, topUp])) }, '/backoffice/transactions')

    const [first, second] = await rows()

    expect(within(first!).getByRole('link', { name: 'TX7K2M9Q4PXW81' })).toHaveAttribute('href', '/backoffice/transactions/TX7K2M9Q4PXW81')
    expect(within(first!).getByRole('link', { name: /Nimali Perera/ })).toHaveAttribute('href', '/backoffice/users/482915067314')
    expect(within(first!).getByRole('link', { name: /Kasun Jayawardena/ })).toHaveAttribute('href', '/backoffice/users/909566829850')
    expect(within(first!).getByText('LKR 5,000.00')).toBeInTheDocument()
    expect(within(first!).getByText('Fee LKR 25.00')).toBeInTheDocument()
    expect(within(second!).getByText('Bank')).toBeInTheDocument()
    expect(within(second!).queryByText(/^Fee/)).not.toBeInTheDocument()
  })

  it('shows why a transfer failed and who it was for when there was no wallet', async () => {
    await openAs(operatorLogin, { [list('page=1&pageSize=10')]: json(200, page([refused])) }, '/backoffice/transactions')

    const [row] = await rows()

    expect(within(row!).getByText(/Failed. Your balance does not cover/)).toBeInTheDocument()
    expect(within(row!).getByText('+94700000000 (no wallet)')).toBeInTheDocument()
  })

  it('applies the filters, sends them to the server and keeps them in the address', async () => {
    const { user, router, calls } = await openAs(
      operatorLogin,
      {
        [list('page=1&pageSize=10')]: json(200, page([transfer, refused])),
        [list('page=1&pageSize=10&type=Transfer&status=Failed&walletNumber=482915067314&from=2026-10-01&to=2026-10-04')]: json(200, page([refused])),
      },
      '/backoffice/transactions',
    )
    await rows()

    await user.selectOptions(screen.getByLabelText('Type'), 'Transfer')
    await user.selectOptions(screen.getByLabelText('Status'), 'Failed')
    await user.type(screen.getByLabelText('Wallet number'), '482915067314')
    await user.type(screen.getByLabelText('From'), '2026-10-01')
    await user.type(screen.getByLabelText('To'), '2026-10-04')
    await user.click(screen.getByRole('button', { name: 'Apply' }))

    await waitFor(async () => expect(await rows()).toHaveLength(1))
    expect(router.state.location.search).toBe('?type=Transfer&status=Failed&wallet=482915067314&from=2026-10-01&to=2026-10-04')
    expect(calls.some((call) => call.key.includes('walletNumber=482915067314'))).toBe(true)
  })

  it('reads the filters from the address, so a link from another page lands filtered', async () => {
    await openAs(
      operatorLogin,
      { [list('page=1&pageSize=10&type=Transfer&status=Failed')]: json(200, page([refused])) },
      '/backoffice/transactions?type=Transfer&status=Failed',
    )

    expect(await rows()).toHaveLength(1)
    expect(screen.getByLabelText('Type')).toHaveValue('Transfer')
    expect(screen.getByLabelText('Status')).toHaveValue('Failed')
    expect(screen.getByRole('button', { name: 'Clear' })).toBeInTheDocument()
  })

  it('ignores a wallet number in the address that is not twelve digits', async () => {
    await openAs(operatorLogin, { [list('page=1&pageSize=10')]: json(200, page([transfer])) }, '/backoffice/transactions?wallet=abc')

    expect(await rows()).toHaveLength(1)
    expect(screen.getByLabelText('Wallet number')).toHaveValue('')
  })

  it('refuses a from date after the to date without calling the server', async () => {
    const { user, calls } = await openAs(operatorLogin, { [list('page=1&pageSize=10')]: json(200, page([transfer])) }, '/backoffice/transactions')
    await rows()
    const before = calls.length

    await user.type(screen.getByLabelText('From'), '2026-10-05')
    await user.type(screen.getByLabelText('To'), '2026-10-04')

    expect(await screen.findByText('The from date is after the to date.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Apply' })).toBeDisabled()
    expect(calls).toHaveLength(before)
  })

  it('clears the filters', async () => {
    const { user, router } = await openAs(
      operatorLogin,
      { [list('page=1&pageSize=10&status=Failed')]: json(200, page([refused])), [list('page=1&pageSize=10')]: json(200, page([transfer, refused])) },
      '/backoffice/transactions?status=Failed',
    )
    await rows()

    await user.click(screen.getByRole('button', { name: 'Clear' }))

    await waitFor(async () => expect(await rows()).toHaveLength(2))
    expect(router.state.location.search).toBe('')
  })

  it('goes to the page of a reference typed in capitals, from any case', async () => {
    const { user, router } = await openAs(
      adminLogin,
      { [list('page=1&pageSize=10')]: json(200, page([transfer])), 'GET /transactions/TX7K2M9Q4PXW81': json(200, { ...transfer, direction: null, counterpartyName: null }) },
      '/backoffice/transactions',
    )
    await rows()

    await user.type(screen.getByLabelText('Reference'), 'tx7k2m9q4pxw81')
    await user.click(screen.getByRole('button', { name: 'Find' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/backoffice/transactions/TX7K2M9Q4PXW81'))
  })

  it('pages the list', async () => {
    const { user, router } = await openAs(
      operatorLogin,
      {
        [list('page=1&pageSize=10')]: json(200, page([transfer], 10, { totalCount: 12, totalPages: 2 })),
        [list('page=2&pageSize=10')]: json(200, page([refused], 10, { page: 2, totalCount: 12, totalPages: 2 })),
      },
      '/backoffice/transactions',
    )
    await screen.findByText('Page 1 of 2. 12 transactions.')

    await user.click(screen.getByRole('button', { name: /next/i }))

    expect(await screen.findByText('Page 2 of 2. 12 transactions.')).toBeInTheDocument()
    expect(router.state.location.search).toBe('?page=2')
  })

  it('shows the empty state and the error state', async () => {
    const empty = await openAs(operatorLogin, { [list('page=1&pageSize=10&status=Failed')]: json(200, page([])) }, '/backoffice/transactions?status=Failed')
    expect(await screen.findByText('No transaction matches those filters.')).toBeInTheDocument()
    empty.unmount()

    await openAs(operatorLogin, { [list('page=1&pageSize=10')]: problem(500, 'INTERNAL_ERROR') }, '/backoffice/transactions')
    expect(await screen.findByRole('alert')).toHaveTextContent('We could not load the transactions.')
  })
})
