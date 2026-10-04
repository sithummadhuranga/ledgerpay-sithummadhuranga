import { fireEvent, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { json, openAs, operatorLogin, problem } from '@/test/helpers'

afterEach(() => vi.unstubAllGlobals())

const receipt = { reference: 'TX55AA11BB22CC', walletNumber: '482915067314', amount: 2500.5, balanceAfter: 14950.5, bankReference: 'BANK12AB34', createdAt: '2026-10-04T09:05:00Z' }

type Opened = Awaited<ReturnType<typeof openAs>>

async function fill(user: Opened['user'], over: Partial<Record<'wallet' | 'amount' | 'bank' | 'note', string>> = {}) {
  await user.type(await screen.findByLabelText('Wallet number'), over.wallet ?? '482915067314')
  await user.type(screen.getByLabelText('Amount'), over.amount ?? '2500.50')
  await user.type(screen.getByLabelText('Bank reference'), over.bank ?? 'bank12ab34')
  if (over.note) {
    await user.type(screen.getByLabelText(/Note/), over.note)
  }
}

const send = (user: Opened['user']) => user.click(screen.getByRole('button', { name: 'Credit the wallet' }))
const topUps = (calls: Opened['calls']) => calls.filter((call) => call.key === 'POST /admin/topups')

describe('the operator top-up', () => {
  it('is the first page an operator sees', async () => {
    const { router } = await openAs(operatorLogin)

    expect(await screen.findByRole('heading', { name: 'Top up a wallet' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/operator/top-up')
  })

  it('credits the wallet with one Idempotency-Key and shows what was credited', async () => {
    const { user, calls } = await openAs(operatorLogin, { 'POST /admin/topups': json(201, receipt) })
    await fill(user, { note: 'Cash at branch' })

    await send(user)

    expect(await screen.findByRole('heading', { name: 'Wallet topped up' })).toBeInTheDocument()
    expect(screen.getByText('LKR 2,500.50')).toBeInTheDocument()
    expect(screen.getByText('LKR 14,950.50')).toBeInTheDocument()
    const sent = topUps(calls)[0]!
    expect(sent.headers['Idempotency-Key']).toMatch(/^[0-9a-f-]{36}$/)
    expect(sent.body).toEqual({ walletNumber: '482915067314', amount: '2500.50', bankReference: 'bank12ab34', note: 'Cash at branch' })
  })

  it('sends no note at all when none was typed', async () => {
    const { user, calls } = await openAs(operatorLogin, { 'POST /admin/topups': json(201, receipt) })
    await fill(user)

    await send(user)

    await screen.findByRole('heading', { name: 'Wallet topped up' })
    expect(Object.keys(topUps(calls)[0]!.body as object)).not.toContain('note')
  })

  it('reuses the key for the same details and makes a new one when any detail changes', async () => {
    const { user, calls } = await openAs(operatorLogin, { 'POST /admin/topups': () => problem(409, 'DUPLICATE_BANK_REFERENCE') })
    await fill(user)

    await send(user)
    await screen.findByText('That bank reference was already used for a top-up.')
    await send(user)
    await user.clear(screen.getByLabelText('Bank reference'))
    await user.type(screen.getByLabelText('Bank reference'), 'bank99zz99')
    await send(user)

    await vi.waitFor(() => expect(topUps(calls)).toHaveLength(3))
    const [first, again, changed] = topUps(calls).map((call) => call.headers['Idempotency-Key'])
    expect(again).toBe(first)
    expect(changed).not.toBe(first)
  })

  it('cannot be fired twice while it is running', async () => {
    let finish: (response: Response) => void = () => undefined
    const running = new Promise<Response>((resolve) => {
      finish = resolve
    })
    const { user, calls } = await openAs(operatorLogin, { 'POST /admin/topups': () => running })
    await fill(user)

    await user.dblClick(screen.getByRole('button', { name: 'Credit the wallet' }))

    expect(screen.getByRole('button', { name: 'Crediting' })).toBeDisabled()
    expect(topUps(calls)).toHaveLength(1)
    finish(json(201, receipt))
    await screen.findByRole('heading', { name: 'Wallet topped up' })
  })

  it.each([
    ['unknown wallet', 404, 'WALLET_NOT_FOUND', 'We could not find that wallet.'],
    ['frozen wallet', 422, 'WALLET_FROZEN', 'A frozen wallet cannot send or receive money.'],
    ['balance limit', 422, 'BALANCE_LIMIT_EXCEEDED', 'That would take the wallet over its balance limit.'],
  ])('says in words what went wrong: %s', async (_name, status, code, message) => {
    const { user } = await openAs(operatorLogin, { 'POST /admin/topups': problem(status, code) })
    await fill(user)

    await send(user)

    expect(await screen.findByText(message)).toBeInTheDocument()
    expect(screen.getByLabelText('Wallet number')).toHaveValue('482915067314')
  })

  it('puts the server message for a field under that field', async () => {
    const { user } = await openAs(operatorLogin, { 'POST /admin/topups': problem(400, 'VALIDATION_FAILED', { errors: { bankReference: ['Bank reference must be 6 to 40 letters and digits.'] } }) })
    await fill(user)

    await send(user)

    expect(await screen.findByText('Bank reference must be 6 to 40 letters and digits.')).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it.each([
    ['a short wallet number', { wallet: '12345' }, 'A wallet number has 12 digits.'],
    ['a bank reference that is too short', { bank: 'ab12' }, 'A bank reference has 6 to 40 letters and digits.'],
    ['a bank reference with a symbol', { bank: 'bank-1234' }, 'A bank reference has 6 to 40 letters and digits.'],
    ['an amount with three decimals', { amount: '1.005' }, 'Enter an amount with at most 2 decimals, such as 5000 or 5000.50.'],
  ])('checks %s before calling the server', async (_name, over, message) => {
    const { user, calls } = await openAs(operatorLogin, {})
    const before = calls.length
    await fill(user, over)

    await send(user)

    expect(await screen.findByText(message)).toBeInTheDocument()
    expect(calls).toHaveLength(before)
  })

  it('starts a new top-up from the receipt with a clean form', async () => {
    const { user } = await openAs(operatorLogin, { 'POST /admin/topups': json(201, receipt) })
    await fill(user)
    await send(user)
    await screen.findByRole('heading', { name: 'Wallet topped up' })

    fireEvent.click(screen.getByRole('button', { name: 'Top up another wallet' }))

    expect(await screen.findByLabelText('Wallet number')).toHaveValue('')
  })
})
