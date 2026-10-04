import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { adminLogin, json, openAs, problem } from '@/test/helpers'

afterEach(() => vi.unstubAllGlobals())

const detail = {
  reference: 'TX7K2M9Q4PXW81', type: 'Transfer', status: 'Completed', direction: null, amount: 5000, fee: 25, counterpartyName: null, note: 'Rent',
  failureCode: null, createdAt: '2026-10-04T09:05:00Z', senderWalletNumber: '482915067314', receiverWalletNumber: '909566829850', bankReference: null,
}

type Replies = NonNullable<Parameters<typeof openAs>[1]>

const open = (replies: Replies) => openAs(adminLogin, replies, '/backoffice/transactions')

describe('finding a transaction', () => {
  it('shows both wallet numbers, the amounts and the note', async () => {
    const { user } = await open({ 'GET /transactions/TX7K2M9Q4PXW81': json(200, detail) })

    await user.type(await screen.findByLabelText('Reference'), 'tx7k2m9q4pxw81')
    await user.click(screen.getByRole('button', { name: 'Find' }))

    expect(await screen.findByText('482915067314')).toBeInTheDocument()
    expect(screen.getByText('909566829850')).toBeInTheDocument()
    expect(screen.getByText('LKR 5,000.00')).toBeInTheDocument()
    expect(screen.getByText('LKR 25.00')).toBeInTheDocument()
    expect(screen.getByText('Rent')).toBeInTheDocument()
  })

  it('shows the bank reference of a top-up and the bank as the sender', async () => {
    const { user } = await open({
      'GET /transactions/TX11': json(200, { ...detail, reference: 'TX11', type: 'TopUp', senderWalletNumber: null, bankReference: 'BANK12AB34', fee: 0 }),
    })

    await user.type(await screen.findByLabelText('Reference'), 'TX11')
    await user.click(screen.getByRole('button', { name: 'Find' }))

    expect(await screen.findByText('BANK12AB34')).toBeInTheDocument()
    expect(screen.getByText('Bank')).toBeInTheDocument()
  })

  it('shows why a failed attempt failed', async () => {
    const { user } = await open({
      'GET /transactions/TX22': json(200, { ...detail, reference: 'TX22', status: 'Failed', failureCode: 'WALLET_FROZEN' }),
    })

    await user.type(await screen.findByLabelText('Reference'), 'TX22')
    await user.click(screen.getByRole('button', { name: 'Find' }))

    expect(await screen.findByText('A frozen wallet cannot send or receive money.')).toBeInTheDocument()
  })

  it('says plainly when there is no such transaction', async () => {
    const { user } = await open({ 'GET /transactions/TX99': problem(404, 'TRANSACTION_NOT_FOUND') })

    await user.type(await screen.findByLabelText('Reference'), 'TX99')
    await user.click(screen.getByRole('button', { name: 'Find' }))

    expect(await screen.findByText('We could not find that transaction.')).toBeInTheDocument()
  })

  it('does not search for nothing', async () => {
    const { user, calls } = await open({})
    const before = calls.length
    await screen.findByLabelText('Reference')

    expect(screen.getByRole('button', { name: 'Find' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Find' }))
    expect(calls).toHaveLength(before)
  })
})
