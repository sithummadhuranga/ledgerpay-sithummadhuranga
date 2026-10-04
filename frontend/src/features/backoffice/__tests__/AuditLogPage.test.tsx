import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { adminLogin, json, openAs, problem } from '@/test/helpers'
import { entry, page } from './samples'

afterEach(() => vi.unstubAllGlobals())

const list = (query: string) => `GET /admin/audit-logs?${query}`

describe('the audit log', () => {
  it('lists when, what, who, about what and from where', async () => {
    await openAs(
      adminLogin,
      {
        [list('page=1&pageSize=20')]: json(200, page([entry(), entry({ action: 'LoginFailed', actorName: null, actorEmail: null, entityType: 'User', entityReference: null, details: 'Unknown account' })], 20)),
      },
      '/backoffice/audit',
    )

    const table = await screen.findByRole('table', { name: 'Audit log, newest first' })
    const [, first, second] = within(table).getAllByRole('row')

    expect(within(first!).getByText('TopUp')).toBeInTheDocument()
    expect(within(first!).getByText('Dilani Senanayake')).toBeInTheDocument()
    expect(within(first!).getByText('dilani.senanayake@example.com')).toBeInTheDocument()
    expect(within(first!).getByText('TXTOPUP000001')).toBeInTheDocument()
    expect(within(first!).getByText('203.0.113.7')).toBeInTheDocument()
    expect(within(second!).getByText('Nobody signed in')).toBeInTheDocument()
    expect(within(second!).getByText('Unknown account')).toBeInTheDocument()
  })

  it('filters by action, who and dates, and keeps them in the address', async () => {
    const { user, router } = await openAs(
      adminLogin,
      {
        [list('page=1&pageSize=20')]: json(200, page([entry()], 20)),
        [list('page=1&pageSize=20&action=TopUp&actor=dilani&from=2026-10-01&to=2026-10-04')]: json(200, page([entry()], 20)),
      },
      '/backoffice/audit',
    )
    await screen.findByRole('table')

    await user.selectOptions(screen.getByLabelText('Action'), 'TopUp')
    await user.type(screen.getByLabelText('Who (name or email)'), '  dilani ')
    await user.type(screen.getByLabelText('From'), '2026-10-01')
    await user.type(screen.getByLabelText('To'), '2026-10-04')
    await user.click(screen.getByRole('button', { name: 'Apply' }))

    await waitFor(() => expect(router.state.location.search).toBe('?action=TopUp&actor=dilani&from=2026-10-01&to=2026-10-04'))
    expect(await screen.findByRole('table')).toBeInTheDocument()
  })

  it('offers every action the server writes', async () => {
    await openAs(adminLogin, { [list('page=1&pageSize=20')]: json(200, page([entry()], 20)) }, '/backoffice/audit')
    await screen.findByRole('table')

    const options = within(screen.getByLabelText('Action')).getAllByRole('option').map((option) => option.textContent)

    expect(options[0]).toBe('All')
    expect(options).toContain('AccountRestricted')
    expect(options).toContain('UserViewed')
    expect(options).toContain('RefreshReuseDetected')
  })

  it('ignores an action in the address that it does not know', async () => {
    await openAs(adminLogin, { [list('page=1&pageSize=20')]: json(200, page([entry()], 20)) }, '/backoffice/audit?action=DropTables')

    expect(await screen.findByRole('table')).toBeInTheDocument()
    expect(screen.getByLabelText('Action')).toHaveValue('')
  })

  it('pages the log', async () => {
    const { user, router } = await openAs(
      adminLogin,
      {
        [list('page=1&pageSize=20')]: json(200, page([entry()], 20, { totalCount: 45, totalPages: 3 })),
        [list('page=2&pageSize=20')]: json(200, page([entry()], 20, { page: 2, totalCount: 45, totalPages: 3 })),
      },
      '/backoffice/audit',
    )
    await screen.findByText('Page 1 of 3. 45 entries.')

    await user.click(screen.getByRole('button', { name: /next/i }))

    expect(await screen.findByText('Page 2 of 3. 45 entries.')).toBeInTheDocument()
    expect(router.state.location.search).toBe('?page=2')
  })

  it('offers the last page when the address asks for one past the end', async () => {
    const { user } = await openAs(
      adminLogin,
      {
        [list('page=9&pageSize=20')]: json(200, page([], 20, { page: 9, totalCount: 45, totalPages: 3 })),
        [list('page=3&pageSize=20')]: json(200, page([entry()], 20, { page: 3, totalCount: 45, totalPages: 3 })),
      },
      '/backoffice/audit?page=9',
    )
    expect(await screen.findByText(/That page does not exist. There are 3 pages./)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Go to the last page' }))

    expect(await screen.findByText('Page 3 of 3. 45 entries.')).toBeInTheDocument()
  })

  it('shows the empty state and the error state', async () => {
    const empty = await openAs(adminLogin, { [list('page=1&pageSize=20&action=Logout')]: json(200, page([], 20)) }, '/backoffice/audit?action=Logout')
    expect(await screen.findByText('No entry matches those filters.')).toBeInTheDocument()
    empty.unmount()

    await openAs(adminLogin, { [list('page=1&pageSize=20')]: problem(500, 'INTERNAL_ERROR') }, '/backoffice/audit')
    expect(await screen.findByRole('alert')).toHaveTextContent('We could not load the audit log.')
  })
})
