import { screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { json, mockApi, problem, renderApp } from '@/test/helpers'

afterEach(() => vi.unstubAllGlobals())

async function fill(user: ReturnType<typeof renderApp>['user'], values: Partial<Record<'name' | 'email' | 'phone' | 'password', string>> = {}) {
  await user.type(await screen.findByLabelText('Full name'), values.name ?? 'Nimali Perera')
  await user.type(screen.getByLabelText('Email'), values.email ?? 'nimali.perera@example.com')
  await user.type(screen.getByLabelText('Mobile number'), values.phone ?? '+94771284635')
  await user.type(screen.getByLabelText('Password'), values.password ?? 'Kandy-Lake-2026!')
  await user.type(screen.getByLabelText('Confirm password'), values.password ?? 'Kandy-Lake-2026!')
}

describe('the register screen', () => {
  it('lists the password rules before the user types anything', async () => {
    mockApi({})
    renderApp('/register')

    const rules = await screen.findByRole('list', { name: 'The password needs:' })

    expect(within(rules).getAllByRole('listitem')).toHaveLength(5)
    expect(within(rules).getAllByText('not met yet')).toHaveLength(5)
  })

  it('marks each rule as it is met, in words as well as with an icon', async () => {
    mockApi({})
    const { user } = renderApp('/register')

    await user.type(await screen.findByLabelText('Password'), 'kandylake')

    const rules = screen.getByRole('list', { name: 'The password needs:' })
    const rule = (text: string) => within(rules).getByText(text).closest('li')!
    expect(within(rule('A lower case letter')).getByText('met')).toBeInTheDocument()
    expect(within(rule('An upper case letter')).getByText('not met yet')).toBeInTheDocument()
    expect(within(rule('A digit')).getByText('not met yet')).toBeInTheDocument()
  })

  it('refuses a mobile number in the wrong format without calling the api', async () => {
    const { fetchMock } = mockApi({})
    const { user } = renderApp('/register')

    await fill(user, { phone: '0771284635' })
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByText('Enter a mobile number like +94771284635.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('puts a taken-email message from the server under the email field', async () => {
    mockApi({ 'POST /auth/register': problem(409, 'EMAIL_ALREADY_REGISTERED') })
    const { user } = renderApp('/register')

    await fill(user)
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByText('That email is already registered.')).toBeInTheDocument()
  })

  it('goes to sign in with the email filled in once the account exists', async () => {
    const { calls } = mockApi({
      'POST /auth/register': json(201, { fullName: 'Nimali Perera', email: 'nimali.perera@example.com', phone: '+94771284635', walletNumber: '482915067314' }),
    })
    const { user, router } = renderApp('/register')

    await fill(user)
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByText('Your account is ready. Sign in to continue.')).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
    expect(screen.getByLabelText('Email')).toHaveValue('nimali.perera@example.com')
    expect(calls[0]?.body).toEqual({ fullName: 'Nimali Perera', email: 'nimali.perera@example.com', phone: '+94771284635', password: 'Kandy-Lake-2026!' })
  })

  it('asks for the password twice and says when the two are not the same', async () => {
    const { fetchMock } = mockApi({})
    const { user } = renderApp('/register')

    await fill(user)
    await user.clear(screen.getByLabelText('Confirm password'))
    await user.type(screen.getByLabelText('Confirm password'), 'Kandy-Lake-2027!')
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    expect(await screen.findByText('The two passwords are not the same.')).toBeInTheDocument()
    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('does not send the repeated password to the api', async () => {
    const { calls } = mockApi({
      'POST /auth/register': json(201, { fullName: 'Nimali Perera', email: 'nimali.perera@example.com', phone: '+94771284635', walletNumber: '482915067314' }),
    })
    const { user } = renderApp('/register')

    await fill(user)
    await user.click(screen.getByRole('button', { name: 'Create account' }))

    await screen.findByText('Your account is ready. Sign in to continue.')
    expect(Object.keys(calls[0]!.body as object)).not.toContain('confirmPassword')
  })

  it('lets the user see each password they typed', async () => {
    mockApi({})
    const { user } = renderApp('/register')
    await user.type(await screen.findByLabelText('Password'), 'Kandy-Lake-2026!')

    await user.click(screen.getByRole('button', { name: 'Show password' }))

    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'text')
    expect(screen.getByLabelText('Confirm password')).toHaveAttribute('type', 'password')
  })
})

