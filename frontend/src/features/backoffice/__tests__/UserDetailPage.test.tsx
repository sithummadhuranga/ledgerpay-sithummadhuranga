import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { adminLogin, json, openAs, operatorLogin, problem } from '@/test/helpers'
import { detail, frozenDetail, nimali } from './samples'

afterEach(() => vi.unstubAllGlobals())

const url = (walletNumber: string) => `GET /admin/users/${walletNumber}`
const patch = (walletNumber: string) => `PATCH /admin/wallets/${walletNumber}/status`
const receipt = { walletNumber: nimali.walletNumber, status: 'Frozen', reason: 'Reported lost phone', changedAt: '2026-10-04T09:05:00Z' }

describe('a customer page', () => {
  it('shows who they are, the balance and the latest transactions', async () => {
    await openAs(operatorLogin, { [url(nimali.walletNumber)]: json(200, detail) }, `/backoffice/users/${nimali.walletNumber}`)

    expect(await screen.findByRole('heading', { name: 'Nimali Perera' })).toBeInTheDocument()
    expect(screen.getByText('nimali.perera@example.com')).toBeInTheDocument()
    expect(screen.getByText('+94771284635')).toBeInTheDocument()
    expect(screen.getByText('LKR 12,450.00')).toBeInTheDocument()
    const latest = within(screen.getByRole('list', { name: 'Latest transactions' })).getAllByRole('listitem')
    expect(latest).toHaveLength(2)
    expect(within(latest[0]!).getByRole('link', { name: 'TX7K2M9Q4PXW81' })).toHaveAttribute('href', '/backoffice/transactions/TX7K2M9Q4PXW81')
    expect(within(latest[1]!).getByText('Bank')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'See all of them' })).toHaveAttribute('href', '/backoffice/transactions?wallet=482915067314')
  })

  it('shows why a wallet is frozen, who froze it and when, and that the account is locked', async () => {
    await openAs(operatorLogin, { [url('909566829850')]: json(200, frozenDetail) }, '/backoffice/users/909566829850')

    expect(await screen.findByText('Reported lost phone')).toBeInTheDocument()
    expect(screen.getByText(/by Dilani Senanayake/)).toBeInTheDocument()
    expect(screen.getByText(/Locked until/)).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Unfreeze the wallet' })).toBeInTheDocument()
    expect(screen.getByText('This wallet has no transactions yet.')).toBeInTheDocument()
  })

  it('freezes an active wallet with a reason and shows the page again', async () => {
    let current = detail
    const { user, calls } = await openAs(
      operatorLogin,
      {
        [url(nimali.walletNumber)]: () => json(200, current),
        [patch(nimali.walletNumber)]: () => {
          current = { ...detail, walletStatus: 'Frozen', statusReason: 'Reported lost phone' }
          return json(200, receipt)
        },
      },
      `/backoffice/users/${nimali.walletNumber}`,
    )
    await screen.findByRole('heading', { name: 'Freeze the wallet' })

    await user.type(screen.getByLabelText('Reason for freezing'), '  Reported lost phone  ')
    await user.click(screen.getByRole('button', { name: 'Freeze this wallet' }))

    expect(await screen.findByRole('heading', { name: 'Unfreeze the wallet' })).toBeInTheDocument()
    expect(calls.find((call) => call.key === patch(nimali.walletNumber))!.body).toEqual({ status: 'Frozen', reason: 'Reported lost phone' })
    expect(screen.getByText('Reported lost phone')).toBeInTheDocument()
  })

  it('unfreezes a frozen wallet', async () => {
    const { user, calls } = await openAs(
      operatorLogin,
      { [url('909566829850')]: json(200, frozenDetail), [patch('909566829850')]: json(200, { ...receipt, walletNumber: '909566829850', status: 'Active' }) },
      '/backoffice/users/909566829850',
    )

    await user.type(await screen.findByLabelText('Reason for unfreezing'), 'Checked the report')
    await user.click(screen.getByRole('button', { name: 'Unfreeze this wallet' }))

    await waitFor(() => expect(calls.some((call) => call.key === patch('909566829850'))).toBe(true))
    expect(calls.find((call) => call.key === patch('909566829850'))!.body).toEqual({ status: 'Active', reason: 'Checked the report' })
  })

  it('asks for a reason of at least three characters before it calls the server', async () => {
    const { user, calls } = await openAs(operatorLogin, { [url(nimali.walletNumber)]: json(200, detail) }, `/backoffice/users/${nimali.walletNumber}`)
    await screen.findByRole('heading', { name: 'Freeze the wallet' })

    await user.type(screen.getByLabelText('Reason for freezing'), 'ab')
    await user.click(screen.getByRole('button', { name: 'Freeze this wallet' }))

    expect(await screen.findByText('Give a reason of at least 3 characters.')).toBeInTheDocument()
    expect(calls.some((call) => call.key === patch(nimali.walletNumber))).toBe(false)
  })

  it('says in words when the server refuses, and keeps the reason typed', async () => {
    const { user } = await openAs(
      operatorLogin,
      { [url(nimali.walletNumber)]: json(200, detail), [patch(nimali.walletNumber)]: problem(409, 'WALLET_ALREADY_IN_STATE') },
      `/backoffice/users/${nimali.walletNumber}`,
    )
    await user.type(await screen.findByLabelText('Reason for freezing'), 'Reported lost phone')

    await user.click(screen.getByRole('button', { name: 'Freeze this wallet' }))

    expect(await screen.findByText('The wallet is already in that state.')).toBeInTheDocument()
    expect(screen.getByLabelText('Reason for freezing')).toHaveValue('Reported lost phone')
  })

  it('offers the operator a top-up with the wallet filled in, and the admin none', async () => {
    const operator = await openAs(operatorLogin, { [url(nimali.walletNumber)]: json(200, detail) }, `/backoffice/users/${nimali.walletNumber}`)
    expect(await screen.findByRole('link', { name: 'Top up this wallet' })).toHaveAttribute('href', '/operator/top-up?wallet=482915067314')
    operator.unmount()

    await openAs(adminLogin, { [url(nimali.walletNumber)]: json(200, detail) }, `/backoffice/users/${nimali.walletNumber}`)
    await screen.findByRole('heading', { name: 'Freeze the wallet' })
    expect(screen.queryByRole('link', { name: 'Top up this wallet' })).not.toBeInTheDocument()
  })

  it('says so when the wallet does not exist and has no retry', async () => {
    await openAs(operatorLogin, { [url('100000000007')]: problem(404, 'WALLET_NOT_FOUND') }, '/backoffice/users/100000000007')

    expect(await screen.findByRole('alert')).toHaveTextContent('We could not find that wallet.')
    expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to the customers' })).toHaveAttribute('href', '/backoffice/users')
  })
})
