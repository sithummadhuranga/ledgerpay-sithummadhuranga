import { screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { daysAgoUtc } from '@/lib/dates'
import { adminLogin, json, openAs, operatorLogin, overviewReplies, problem } from '@/test/helpers'
import { kasun, page, refused } from './samples'

afterEach(() => vi.unstubAllGlobals())

const frozen = 'GET /admin/users?page=1&pageSize=5&status=Frozen'
const locked = 'GET /admin/users?page=1&pageSize=5&status=Locked'
const failed = `GET /admin/transactions?page=1&pageSize=5&type=Transfer&status=Failed&from=${daysAgoUtc(7)}`

describe('the needs-attention overview', () => {
  it('lists frozen wallets, locked accounts and refused transfers, each with its count and a link to all of them', async () => {
    await openAs(
      adminLogin,
      {
        [frozen]: json(200, page([kasun], 5, { totalCount: 7 })),
        [locked]: json(200, page([kasun], 5, { totalCount: 2 })),
        [failed]: json(200, page([refused], 5, { totalCount: 3 })),
      },
    )

    const frozenSection = await screen.findByRole('region', { name: 'Frozen wallets' })
    expect(await within(frozenSection).findByText('7')).toBeInTheDocument()
    expect(within(frozenSection).getByRole('link', { name: 'Kasun Jayawardena' })).toHaveAttribute('href', '/backoffice/users/909566829850')
    expect(within(frozenSection).getByRole('link', { name: 'See all' })).toHaveAttribute('href', '/backoffice/users?status=Frozen')
    const lockedSection = screen.getByRole('region', { name: 'Locked accounts' })
    expect(await within(lockedSection).findByText('2')).toBeInTheDocument()
    expect(within(lockedSection).getByRole('link', { name: 'See all' })).toHaveAttribute('href', '/backoffice/users?status=Locked')
    const failedSection = screen.getByRole('region', { name: 'Refused transfers' })
    expect(await within(failedSection).findByText(/Your balance does not cover/)).toBeInTheDocument()
    expect(within(failedSection).getByRole('link', { name: 'See all' })).toHaveAttribute('href', `/backoffice/transactions?type=Transfer&status=Failed&from=${daysAgoUtc(7)}`)
  })

  it('says plainly when there is nothing to look at', async () => {
    await openAs(operatorLogin, overviewReplies, '/backoffice')

    expect(await screen.findByText('No wallet is frozen.')).toBeInTheDocument()
    expect(screen.getByText('No account is locked.')).toBeInTheDocument()
    expect(screen.getByText('No transfer has been refused in the last 7 days.')).toBeInTheDocument()
  })

  it('shows an error in one part and still shows the others', async () => {
    await openAs(adminLogin, { ...overviewReplies, [locked]: problem(500, 'INTERNAL_ERROR') })

    expect(await screen.findByRole('alert')).toHaveTextContent('We could not load this.')
    expect(await screen.findByText('No wallet is frozen.')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: 'Try again' })).toHaveLength(1)
  })
})
