import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { json, openAs, operatorLogin, problem } from '@/test/helpers'
import { kasun, nimali, page } from './samples'

afterEach(() => vi.unstubAllGlobals())

const list = (query: string) => `GET /admin/users?${query}`
const rows = async () => within(await screen.findByRole('list', { name: 'Customers' })).getAllByRole('listitem')

describe('the customers page', () => {
  it('lists each customer with email, mobile, wallet number, balance and what is wrong', async () => {
    await openAs(operatorLogin, { [list('page=1&pageSize=10')]: json(200, page([nimali, kasun])) }, '/backoffice/users')

    const [first, second] = await rows()

    expect(within(first!).getByRole('link', { name: 'Nimali Perera' })).toHaveAttribute('href', '/backoffice/users/482915067314')
    expect(within(first!).getByText(/nimali.perera@example.com/)).toBeInTheDocument()
    expect(within(first!).getByText('LKR 12,450.00')).toBeInTheDocument()
    expect(within(first!).getByText('Active')).toBeInTheDocument()
    expect(within(second!).getByText('Frozen, Locked')).toBeInTheDocument()
  })

  it('searches by what was typed, keeps the search in the address and sends it trimmed', async () => {
    const { user, router, calls } = await openAs(
      operatorLogin,
      { [list('page=1&pageSize=10')]: json(200, page([nimali, kasun])), [list('page=1&pageSize=10&search=kasun')]: json(200, page([kasun])) },
      '/backoffice/users',
    )
    await rows()

    await user.type(screen.getByLabelText('Search'), '  kasun  ')
    await user.click(screen.getByRole('button', { name: 'Search' }))

    await waitFor(async () => expect(await rows()).toHaveLength(1))
    expect(router.state.location.search).toBe('?q=kasun')
    expect(calls.some((call) => call.key === list('page=1&pageSize=10&search=kasun'))).toBe(true)
  })

  it('filters by status and reads the filter from the address', async () => {
    const { user } = await openAs(
      operatorLogin,
      { [list('page=1&pageSize=10&status=Frozen')]: json(200, page([kasun])), [list('page=1&pageSize=10')]: json(200, page([nimali, kasun])) },
      '/backoffice/users?status=Frozen',
    )

    expect(await rows()).toHaveLength(1)
    expect(screen.getByLabelText('Show')).toHaveValue('Frozen')
    await user.selectOptions(screen.getByLabelText('Show'), '')
    await user.click(screen.getByRole('button', { name: 'Search' }))

    await waitFor(async () => expect(await rows()).toHaveLength(2))
  })

  it('ignores a status in the address that it does not know', async () => {
    await openAs(operatorLogin, { [list('page=1&pageSize=10')]: json(200, page([nimali])) }, '/backoffice/users?status=Bogus')

    expect(await rows()).toHaveLength(1)
  })

  it('pages the list and keeps the page in the address', async () => {
    const { user, router } = await openAs(
      operatorLogin,
      {
        [list('page=1&pageSize=10')]: json(200, page([nimali], 10, { totalCount: 25, totalPages: 3 })),
        [list('page=2&pageSize=10')]: json(200, page([kasun], 10, { page: 2, totalCount: 25, totalPages: 3 })),
      },
      '/backoffice/users',
    )
    expect(await screen.findByText('Page 1 of 3. 25 customers.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /previous/i })).toBeDisabled()

    await user.click(screen.getByRole('button', { name: /next/i }))

    expect(await screen.findByText('Page 2 of 3. 25 customers.')).toBeInTheDocument()
    expect(router.state.location.search).toBe('?page=2')
  })

  it('says so when nobody matches', async () => {
    await openAs(operatorLogin, { [list('page=1&pageSize=10&search=zzz')]: json(200, page([])) }, '/backoffice/users?q=zzz')

    expect(await screen.findByText('No customer matches that.')).toBeInTheDocument()
  })

  it('shows an error with a way to try again', async () => {
    let attempts = 0
    const { user } = await openAs(
      operatorLogin,
      { [list('page=1&pageSize=10')]: () => (++attempts === 1 ? problem(500, 'INTERNAL_ERROR') : json(200, page([nimali]))) },
      '/backoffice/users',
    )

    expect(await screen.findByRole('alert')).toHaveTextContent('We could not load the customers.')
    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await rows()).toHaveLength(1)
  })

  it('limits the search text to 100 characters in the field', async () => {
    await openAs(operatorLogin, { [list('page=1&pageSize=10')]: json(200, page([nimali])) }, '/backoffice/users')
    await rows()

    expect(screen.getByLabelText('Search')).toHaveAttribute('maxlength', '100')
  })
})
