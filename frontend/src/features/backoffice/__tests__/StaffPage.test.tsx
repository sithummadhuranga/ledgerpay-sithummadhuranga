import { screen, waitFor, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { adminLogin, json, openAs, problem } from '@/test/helpers'
import { adminMember, operatorMember } from './samples'

afterEach(() => vi.unstubAllGlobals())

const members = [operatorMember, adminMember]
const patch = 'PATCH /admin/staff/restriction'

describe('the staff page', () => {
  it('lists operators and admins and offers a restriction for an operator only', async () => {
    await openAs(adminLogin, { 'GET /admin/staff': json(200, members) }, '/backoffice/staff')

    const rows = within(await screen.findByRole('list', { name: 'Staff' })).getAllByRole('listitem')

    expect(rows).toHaveLength(2)
    expect(within(rows[0]!).getByText('Operator')).toBeInTheDocument()
    expect(within(rows[0]!).getByRole('button', { name: 'Restrict Dilani Senanayake' })).toBeInTheDocument()
    expect(within(rows[1]!).getByText('Admin')).toBeInTheDocument()
    expect(within(rows[1]!).getByText('Cannot be restricted')).toBeInTheDocument()
    expect(within(rows[1]!).queryByRole('button')).not.toBeInTheDocument()
  })

  it('restricts an operator with a reason and shows the new state', async () => {
    let current = members
    const { user, calls } = await openAs(
      adminLogin,
      {
        'GET /admin/staff': () => json(200, current),
        [patch]: () => {
          current = [{ ...operatorMember, restricted: true, restrictedAt: '2026-10-04T09:05:00Z', restrictedReason: 'Suspected misuse', restrictedBy: 'Chamara Rajapaksa' }, adminMember]
          return json(200, current[0])
        },
      },
      '/backoffice/staff',
    )

    await user.click(await screen.findByRole('button', { name: 'Restrict Dilani Senanayake' }))
    await user.type(screen.getByLabelText(/Reason/), '  Suspected misuse  ')
    await user.click(screen.getByRole('button', { name: 'Confirm and restrict' }))

    expect(await screen.findByText(/Restricted .* by Chamara Rajapaksa\. Suspected misuse/)).toBeInTheDocument()
    expect(calls.find((call) => call.key === patch)!.body).toEqual({ email: 'dilani.senanayake@example.com', restricted: true, reason: 'Suspected misuse' })
    expect(screen.getByRole('button', { name: 'Lift the restriction on Dilani Senanayake' })).toBeInTheDocument()
  })

  it('lifts a restriction', async () => {
    const restricted = { ...operatorMember, restricted: true, restrictedAt: '2026-10-04T09:05:00Z', restrictedReason: 'Suspected misuse', restrictedBy: 'Chamara Rajapaksa' }
    const { user, calls } = await openAs(
      adminLogin,
      { 'GET /admin/staff': json(200, [restricted, adminMember]), [patch]: json(200, operatorMember) },
      '/backoffice/staff',
    )

    await user.click(await screen.findByRole('button', { name: 'Lift the restriction on Dilani Senanayake' }))
    await user.type(screen.getByLabelText(/Reason/), 'Cleared after review')
    await user.click(screen.getByRole('button', { name: 'Confirm and lift the restriction' }))

    await waitFor(() => expect(calls.some((call) => call.key === patch)).toBe(true))
    expect(calls.find((call) => call.key === patch)!.body).toEqual({ email: 'dilani.senanayake@example.com', restricted: false, reason: 'Cleared after review' })
  })

  it('does not send a reason shorter than three characters', async () => {
    const { user, calls } = await openAs(adminLogin, { 'GET /admin/staff': json(200, members) }, '/backoffice/staff')

    await user.click(await screen.findByRole('button', { name: 'Restrict Dilani Senanayake' }))
    await user.type(screen.getByLabelText(/Reason/), 'no')

    expect(screen.getByRole('button', { name: 'Confirm and restrict' })).toBeDisabled()
    expect(calls.some((call) => call.key === patch)).toBe(false)
  })

  it('says in words when the server refuses', async () => {
    const { user } = await openAs(
      adminLogin,
      { 'GET /admin/staff': json(200, members), [patch]: problem(409, 'ACCOUNT_ALREADY_IN_STATE') },
      '/backoffice/staff',
    )

    await user.click(await screen.findByRole('button', { name: 'Restrict Dilani Senanayake' }))
    await user.type(screen.getByLabelText(/Reason/), 'Suspected misuse')
    await user.click(screen.getByRole('button', { name: 'Confirm and restrict' }))

    expect(await screen.findByText('The account is already in that state.')).toBeInTheDocument()
  })

  it('shows the loading and error states with a way to try again', async () => {
    let attempts = 0
    const { user } = await openAs(
      adminLogin,
      { 'GET /admin/staff': () => (++attempts === 1 ? problem(500, 'INTERNAL_ERROR') : json(200, members)) },
      '/backoffice/staff',
    )

    expect(await screen.findByRole('alert')).toHaveTextContent('We could not load the staff.')
    await user.click(screen.getByRole('button', { name: 'Try again' }))

    expect(await screen.findByRole('list', { name: 'Staff' })).toBeInTheDocument()
  })
})
