import { screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { customerLogin, json, openAs, problem } from '@/test/helpers'

afterEach(() => vi.unstubAllGlobals())

const WALLET = '909566829850'
const lookup = { walletNumber: WALLET, holderName: 'T*** F***', active: true }
const quote = { amount: 5000, fee: 25, total: 5025 }
const receipt = { reference: 'TX7K2M9Q4PXW81', amount: 5000, fee: 25, total: 5025, balanceAfter: 7425, recipientWalletNumber: WALLET, createdAt: '2026-10-04T09:05:00Z' }

const replies = {
  [`GET /wallets/lookup?walletNumber=${WALLET}`]: json(200, lookup),
  'GET /transfers/quote?amount=5000': json(200, quote),
}

type Opened = Awaited<ReturnType<typeof openAs>>

async function fillDetails(user: Opened['user'], over: { recipient?: string; amount?: string; note?: string } = {}) {
  await user.type(await screen.findByLabelText('Wallet number'), over.recipient ?? WALLET)
  await user.type(screen.getByLabelText('Amount'), over.amount ?? '5000')
  if (over.note) {
    await user.type(screen.getByLabelText(/Note/), over.note)
  }
  await user.click(screen.getByRole('button', { name: 'Continue' }))
}

describe('sending money', () => {
  it('shows the fee and the total the server worked out, and sends nothing yet', async () => {
    const { user, calls } = await openAs(customerLogin, replies, '/send')

    await fillDetails(user, { note: 'Birthday present' })

    expect(await screen.findByRole('heading', { name: 'Check and confirm' })).toBeInTheDocument()
    expect(screen.getByText('T*** F***')).toBeInTheDocument()
    expect(screen.getByText(WALLET)).toBeInTheDocument()
    expect(screen.getByText('LKR 25.00')).toBeInTheDocument()
    expect(screen.getByText('LKR 5,025.00')).toBeInTheDocument()
    expect(screen.getByText('Birthday present')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Confirm and send LKR 5,025.00' })).toBeEnabled()
    expect(calls.some((call) => call.key === 'POST /transfers')).toBe(false)
  })

  it('cannot be fired twice: one click sends one request while it is running', async () => {
    let finish: (response: Response) => void = () => undefined
    const running = new Promise<Response>((resolve) => {
      finish = resolve
    })
    const { user, calls } = await openAs(customerLogin, { ...replies, 'POST /transfers': () => running }, '/send')
    await fillDetails(user)
    const confirm = await screen.findByRole('button', { name: 'Confirm and send LKR 5,025.00' })

    await user.dblClick(confirm)

    expect(screen.getByRole('button', { name: 'Sending' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Change details' })).toBeDisabled()
    expect(calls.filter((call) => call.key === 'POST /transfers')).toHaveLength(1)
    finish(json(201, receipt))
    await screen.findByText(/Sent LKR 5,000\.00 to T\*\*\* F\*\*\*\./)
  })

  it('sends the details with one Idempotency-Key and shows a receipt', async () => {
    const { user, calls } = await openAs(customerLogin, { ...replies, 'POST /transfers': json(201, receipt) }, '/send')
    await fillDetails(user, { note: 'Birthday present' })

    await user.click(await screen.findByRole('button', { name: /Confirm and send/ }))

    expect(await screen.findByText(/Sent LKR 5,000\.00 to T\*\*\* F\*\*\*\./)).toBeInTheDocument()
    const sent = calls.find((call) => call.key === 'POST /transfers')!
    expect(sent.headers['Idempotency-Key']).toMatch(/^[0-9a-f-]{36}$/)
    expect(sent.body).toEqual({ amount: '5000', recipientWalletNumber: WALLET, note: 'Birthday present' })
    expect(screen.getByText('TX7K2M9Q4PXW81')).toBeInTheDocument()
    expect(screen.getByText('LKR 7,425.00')).toBeInTheDocument()
  })

  it('keeps the same key when the user tries again after a lost connection, so the money moves once', async () => {
    let attempts = 0
    const { user, calls } = await openAs(
      customerLogin,
      {
        ...replies,
        'POST /transfers': () => {
          if (++attempts === 1) {
            throw new TypeError('Failed to fetch')
          }
          return json(201, receipt)
        },
      },
      '/send',
    )
    await fillDetails(user)
    await user.click(await screen.findByRole('button', { name: /Confirm and send/ }))
    expect(await screen.findByText(/We could not reach the server/)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /Confirm and send/ }))

    await screen.findByText(/Sent LKR 5,000\.00/)
    const keys = calls.filter((call) => call.key === 'POST /transfers').map((call) => call.headers['Idempotency-Key'])
    expect(keys).toHaveLength(2)
    expect(keys[0]).toBe(keys[1])
  })

  it('makes a new key after Change details', async () => {
    const { user, calls } = await openAs(
      customerLogin,
      { ...replies, 'POST /transfers': () => problem(422, 'INSUFFICIENT_FUNDS') },
      '/send',
    )
    await fillDetails(user)
    await user.click(await screen.findByRole('button', { name: /Confirm and send/ }))
    await screen.findByText(/Your balance does not cover/)

    await user.click(screen.getByRole('button', { name: 'Change details' }))
    await user.click(await screen.findByRole('button', { name: 'Continue' }))
    await user.click(await screen.findByRole('button', { name: /Confirm and send/ }))

    await screen.findByText(/Your balance does not cover/)
    const keys = calls.filter((call) => call.key === 'POST /transfers').map((call) => call.headers['Idempotency-Key'])
    expect(keys).toHaveLength(2)
    expect(keys[0]).not.toBe(keys[1])
  })

  it('makes a new key after a refusal, so a second try after a top-up is not answered with the old refusal', async () => {
    const { user, calls } = await openAs(customerLogin, { ...replies, 'POST /transfers': () => problem(422, 'INSUFFICIENT_FUNDS') }, '/send')
    await fillDetails(user)
    await user.click(await screen.findByRole('button', { name: /Confirm and send/ }))
    await screen.findByText(/Your balance does not cover/)

    await user.click(screen.getByRole('button', { name: /Confirm and send/ }))

    await vi.waitFor(() => expect(calls.filter((call) => call.key === 'POST /transfers')).toHaveLength(2))
    const [first, second] = calls.filter((call) => call.key === 'POST /transfers').map((call) => call.headers['Idempotency-Key'])
    expect(second).not.toBe(first)
  })

  it('shows what the server says is wrong when it refuses the details', async () => {
    const { user } = await openAs(
      customerLogin,
      { ...replies, 'POST /transfers': problem(400, 'VALIDATION_FAILED', { errors: { amount: ['Amount must be at least 10.00.'] } }) },
      '/send',
    )
    await fillDetails(user)

    await user.click(await screen.findByRole('button', { name: /Confirm and send/ }))

    expect(await screen.findByText(/Amount must be at least 10\.00\./)).toBeInTheDocument()
  })

  it('says which amounts the balance does not cover and stays on the confirmation', async () => {
    const { user } = await openAs(customerLogin, { ...replies, 'POST /transfers': problem(422, 'INSUFFICIENT_FUNDS') }, '/send')
    await fillDetails(user)

    await user.click(await screen.findByRole('button', { name: /Confirm and send/ }))

    expect(await screen.findByText('Your balance does not cover LKR 5,000.00 plus the LKR 25.00 fee.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Check and confirm' })).toBeInTheDocument()
  })

  it('keeps the form filled in when the wallet cannot be found', async () => {
    const { user } = await openAs(
      customerLogin,
      { ...replies, [`GET /wallets/lookup?walletNumber=${WALLET}`]: problem(404, 'WALLET_NOT_FOUND') },
      '/send',
    )

    await fillDetails(user, { note: 'Rent' })

    expect(await screen.findByText('We could not find that wallet.')).toBeInTheDocument()
    expect(screen.getByLabelText('Wallet number')).toHaveValue(WALLET)
    expect(screen.getByLabelText('Amount')).toHaveValue('5000')
    expect(screen.getByLabelText(/Note/)).toHaveValue('Rent')
  })

  it('does not let a customer send to their own wallet', async () => {
    const { user, calls } = await openAs(
      customerLogin,
      { ...replies, [`GET /wallets/lookup?walletNumber=${WALLET}`]: json(200, { ...lookup, walletNumber: customerLogin.walletNumber }) },
      '/send',
    )

    await fillDetails(user)

    expect(await screen.findByText('You cannot send money to your own wallet.')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Check and confirm' })).not.toBeInTheDocument()
    expect(calls.some((call) => call.key === 'POST /transfers')).toBe(false)
  })

  it('does not go on when the recipient wallet is frozen', async () => {
    const { user } = await openAs(customerLogin, { ...replies, [`GET /wallets/lookup?walletNumber=${WALLET}`]: json(200, { ...lookup, active: false }) }, '/send')

    await fillDetails(user)

    expect(await screen.findByText('A frozen wallet cannot send or receive money.')).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'Check and confirm' })).not.toBeInTheDocument()
  })

  it('shows the server message when the amount is below the smallest transfer', async () => {
    const { user } = await openAs(customerLogin, { ...replies, 'GET /transfers/quote?amount=50': problem(422, 'AMOUNT_BELOW_MINIMUM') }, '/send')

    await fillDetails(user, { amount: '50' })

    expect(await screen.findByText('The amount is below the smallest transfer allowed.')).toBeInTheDocument()
  })

  it('finds a wallet by mobile number too', async () => {
    const { user, calls } = await openAs(
      customerLogin,
      { ...replies, 'GET /wallets/lookup?phone=%2B94754106872': json(200, lookup), 'POST /transfers': json(201, receipt) },
      '/send',
    )
    await user.click(await screen.findByRole('button', { name: 'Mobile number' }))
    await user.type(screen.getByLabelText('Mobile number', { selector: 'input' }), '+94754106872')
    await user.type(screen.getByLabelText('Amount'), '5000')
    await user.click(screen.getByRole('button', { name: 'Continue' }))

    await user.click(await screen.findByRole('button', { name: /Confirm and send/ }))

    await screen.findByText(/Sent LKR 5,000\.00/)
    expect(calls.find((call) => call.key === 'POST /transfers')!.body).toEqual({ amount: '5000', recipientPhone: '+94754106872' })
  })

  it.each([
    ['11 digits', { recipient: '90956682985' }, 'A wallet number has 12 digits.'],
    ['letters', { recipient: '90956682985a' }, 'A wallet number has 12 digits.'],
    ['three decimals', { amount: '10.005' }, 'Enter an amount with at most 2 decimals, such as 5000 or 5000.50.'],
    ['zero', { amount: '0' }, 'The amount must be more than zero.'],
    ['text', { amount: 'abc' }, 'Enter an amount with at most 2 decimals, such as 5000 or 5000.50.'],
    ['a note that is too long', { note: 'n'.repeat(141) }, 'A note can have at most 140 characters.'],
  ])('checks the details before asking the server: %s', async (_name, over, message) => {
    const { user, calls } = await openAs(customerLogin, {}, '/send')
    const before = calls.length

    await fillDetails(user, over)

    expect(await screen.findByText(message)).toBeInTheDocument()
    expect(calls).toHaveLength(before)
  })

  it('starts again for another transfer from the receipt', async () => {
    const { user } = await openAs(customerLogin, { ...replies, 'POST /transfers': json(201, receipt) }, '/send')
    await fillDetails(user)
    await user.click(await screen.findByRole('button', { name: /Confirm and send/ }))
    await screen.findByText(/Sent LKR 5,000\.00/)

    await user.click(screen.getByRole('button', { name: 'Send another' }))

    expect(await screen.findByLabelText('Wallet number')).toHaveValue('')
    expect(screen.getByLabelText('Amount')).toHaveValue('')
  })
})
