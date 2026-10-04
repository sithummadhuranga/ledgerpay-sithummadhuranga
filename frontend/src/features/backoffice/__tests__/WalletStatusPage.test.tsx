import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { adminLogin, json, openAs, operatorLogin, problem } from '@/test/helpers'

afterEach(() => vi.unstubAllGlobals())

const frozen = { walletNumber: '482915067314', status: 'Frozen', reason: 'Checking a report', changedAt: '2026-10-04T09:05:00Z' }

type Opened = Awaited<ReturnType<typeof openAs>>

async function fill(user: Opened['user'], reason = 'Checking a report', wallet = '482915067314') {
  await user.type(await screen.findByLabelText('Wallet number'), wallet)
  await user.type(screen.getByLabelText('Reason'), reason)
}

describe('freezing and unfreezing a wallet', () => {
  it('is the first page of an admin', async () => {
    const { router } = await openAs(adminLogin)

    expect(await screen.findByRole('heading', { name: 'Freeze or unfreeze a wallet' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/backoffice/wallets')
  })

  it('freezes a wallet with a reason and says it is frozen', async () => {
    const { user, calls } = await openAs(adminLogin, { 'PATCH /admin/wallets/482915067314/status': json(200, frozen) })
    await fill(user)

    await user.click(screen.getByRole('button', { name: 'Freeze the wallet' }))

    expect(await screen.findByText(/is now frozen\./)).toBeInTheDocument()
    expect(calls.find((call) => call.key.startsWith('PATCH'))!.body).toEqual({ status: 'Frozen', reason: 'Checking a report' })
  })

  it('unfreezes when that action is chosen', async () => {
    const { user, calls } = await openAs(operatorLogin, { 'PATCH /admin/wallets/482915067314/status': json(200, { ...frozen, status: 'Active' }) }, '/backoffice/wallets')
    await fill(user, 'Report closed')

    await user.click(screen.getByRole('button', { name: 'Unfreeze' }))
    await user.click(screen.getByRole('button', { name: 'Unfreeze the wallet' }))

    expect(await screen.findByText(/is now active\./)).toBeInTheDocument()
    expect(calls.find((call) => call.key.startsWith('PATCH'))!.body).toEqual({ status: 'Active', reason: 'Report closed' })
  })

  it('says when the wallet is already in that state', async () => {
    const { user } = await openAs(adminLogin, { 'PATCH /admin/wallets/482915067314/status': problem(409, 'WALLET_ALREADY_IN_STATE') })
    await fill(user)

    await user.click(screen.getByRole('button', { name: 'Freeze the wallet' }))

    expect(await screen.findByText('The wallet is already in that state.')).toBeInTheDocument()
  })

  it('says when the wallet does not exist', async () => {
    const { user } = await openAs(adminLogin, { 'PATCH /admin/wallets/100000000000/status': problem(404, 'WALLET_NOT_FOUND') })
    await fill(user, 'Checking a report', '100000000000')

    await user.click(screen.getByRole('button', { name: 'Freeze the wallet' }))

    expect(await screen.findByText('We could not find that wallet.')).toBeInTheDocument()
  })

  it.each([
    ['a reason that is too short', 'ab', '482915067314', 'Give a reason of at least 3 characters.'],
    ['a reason of only spaces', '    ', '482915067314', 'Give a reason of at least 3 characters.'],
    ['a reason that is too long', 'r'.repeat(251), '482915067314', 'A reason can have at most 250 characters.'],
    ['a wallet number that is too short', 'Checking a report', '4829', 'A wallet number has 12 digits.'],
  ])('checks %s before calling the server', async (_name, reason, wallet, message) => {
    const { user, calls } = await openAs(adminLogin, {})
    const before = calls.length
    await fill(user, reason, wallet)

    await user.click(screen.getByRole('button', { name: 'Freeze the wallet' }))

    expect(await screen.findByText(message)).toBeInTheDocument()
    expect(calls).toHaveLength(before)
  })

  it('says the reason is never shown to the customer', async () => {
    await openAs(adminLogin)

    expect(await screen.findByText(/never shown to the customer/)).toBeInTheDocument()
  })
})
