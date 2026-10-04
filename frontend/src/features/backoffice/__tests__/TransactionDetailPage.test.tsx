import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { json, openAs, operatorLogin, problem } from '@/test/helpers'

afterEach(() => vi.unstubAllGlobals())

const detail = {
  reference: 'TX7K2M9Q4PXW81', type: 'Transfer', status: 'Completed', direction: null, amount: 5000, fee: 25, counterpartyName: null, note: 'Rent',
  failureCode: null, createdAt: '2026-10-04T09:05:00Z', senderWalletNumber: '482915067314', receiverWalletNumber: '909566829850', bankReference: null,
}

describe('one transaction for staff', () => {
  it('shows both wallet numbers, the amounts and the note', async () => {
    await openAs(operatorLogin, { 'GET /transactions/TX7K2M9Q4PXW81': json(200, detail) }, '/backoffice/transactions/TX7K2M9Q4PXW81')

    expect(await screen.findByText('482915067314')).toBeInTheDocument()
    expect(screen.getByText('909566829850')).toBeInTheDocument()
    expect(screen.getByText('LKR 5,000.00')).toBeInTheDocument()
    expect(screen.getByText('LKR 25.00')).toBeInTheDocument()
    expect(screen.getByText('Rent')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Back to the transactions' })).toHaveAttribute('href', '/backoffice/transactions')
  })

  it('shows the bank reference of a top-up and the bank as the sender', async () => {
    await openAs(
      operatorLogin,
      { 'GET /transactions/TX11': json(200, { ...detail, reference: 'TX11', type: 'TopUp', senderWalletNumber: null, bankReference: 'BANK12AB34', fee: 0 }) },
      '/backoffice/transactions/TX11',
    )

    expect(await screen.findByText('BANK12AB34')).toBeInTheDocument()
    expect(screen.getByText('Bank')).toBeInTheDocument()
  })

  it('shows why a failed attempt failed', async () => {
    await openAs(
      operatorLogin,
      { 'GET /transactions/TX22': json(200, { ...detail, reference: 'TX22', status: 'Failed', failureCode: 'WALLET_FROZEN' }) },
      '/backoffice/transactions/TX22',
    )

    expect(await screen.findByText('A frozen wallet cannot send or receive money.')).toBeInTheDocument()
  })

  it('says plainly when there is no such transaction, without a retry', async () => {
    await openAs(operatorLogin, { 'GET /transactions/TX99': problem(404, 'TRANSACTION_NOT_FOUND') }, '/backoffice/transactions/TX99')

    expect(await screen.findByText('We could not find that transaction.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Try again' })).not.toBeInTheDocument()
  })

  it('offers a retry when the server failed', async () => {
    await openAs(operatorLogin, { 'GET /transactions/TX98': problem(500, 'INTERNAL_ERROR') }, '/backoffice/transactions/TX98')

    expect(await screen.findByRole('button', { name: 'Try again' })).toBeInTheDocument()
  })
})
