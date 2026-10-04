import { ApiError, NETWORK_ERROR, UNKNOWN_ERROR } from './api/problem'
import { formatMoney } from './format'

// What the user reads for each code the API can send, in one place. A screen never writes its own error sentence.
// Amounts come from the screen that has them, so a message can name the real numbers.
export interface ErrorContext {
  amount?: string
  fee?: string
}

type Describe = (error: ApiError, context: ErrorContext) => string

const minutes = (seconds: number | undefined) => Math.max(1, Math.ceil((seconds ?? 900) / 60))
const plural = (count: number, word: string) => `${count} ${word}${count === 1 ? '' : 's'}`

const messages: Record<string, Describe> = {
  VALIDATION_FAILED: (error) => {
    const details = Object.values(error.errors).flat()
    return details.length > 0 ? `Some details need fixing. ${details.join(' ')}` : 'Some details need fixing.'
  },
  IDEMPOTENCY_KEY_REQUIRED: () => 'We could not send this request. Try again.',
  UNAUTHENTICATED: () => 'Your session ended. Sign in again.',
  ACCOUNT_RESTRICTED: () => 'This account has been restricted by an administrator. Ask one to lift it.',
  STAFF_NOT_FOUND: () => 'We could not find an operator or admin with that email.',
  STAFF_NOT_RESTRICTABLE: () => 'Only an operator account can be restricted, and not your own.',
  ACCOUNT_ALREADY_IN_STATE: () => 'The account is already in that state.',
  INVALID_REFRESH_TOKEN: () => 'Your session ended. Sign in again.',
  SESSION_NOT_FOUND: () => 'That session is already signed out.',
  INVALID_CREDENTIALS: () => 'The email or password is wrong.',
  FORBIDDEN: () => 'You do not have access to this.',
  ACCOUNT_LOCKED: (error) =>
    `This account is locked after too many failed sign-ins. Try again in ${plural(minutes(error.retryAfterSeconds), 'minute')}.`,
  WALLET_NOT_FOUND: () => 'We could not find that wallet.',
  TRANSACTION_NOT_FOUND: () => 'We could not find that transaction.',
  RECIPIENT_NOT_FOUND: () => 'We could not find a wallet for that recipient. Check the number and try again.',
  EMAIL_ALREADY_REGISTERED: () => 'That email is already registered.',
  PHONE_ALREADY_REGISTERED: () => 'That mobile number is already registered.',
  DUPLICATE_BANK_REFERENCE: () => 'That bank reference was already used for a top-up.',
  IDEMPOTENCY_KEY_REUSED: () => 'This request was already sent with different details. Start again.',
  WALLET_ALREADY_IN_STATE: () => 'The wallet is already in that state.',
  SELF_TRANSFER_NOT_ALLOWED: () => 'You cannot send money to your own wallet.',
  AMOUNT_BELOW_MINIMUM: () => 'The amount is below the smallest transfer allowed.',
  AMOUNT_ABOVE_MAXIMUM: () => 'The amount is above the largest transfer allowed.',
  WALLET_FROZEN: () => 'A frozen wallet cannot send or receive money.',
  INSUFFICIENT_FUNDS: (_, { amount, fee }) =>
    amount && fee
      ? `Your balance does not cover ${formatMoney(amount)} plus the ${formatMoney(fee)} fee.`
      : 'Your balance does not cover the amount plus the fee.',
  RECEIVER_BALANCE_LIMIT_EXCEEDED: () => 'The recipient wallet cannot hold that much.',
  BALANCE_LIMIT_EXCEEDED: () => 'That would take the wallet over its balance limit.',
  RATE_LIMITED: (error) =>
    `Too many requests. Try again in ${plural(error.retryAfterSeconds ?? 60, 'second')}.`,
  INTERNAL_ERROR: (error) =>
    `Something went wrong on our side. Try again.${error.traceId ? ` Quote ${error.traceId} if it keeps happening.` : ''}`,
  NOT_FOUND: () => 'We could not find that.',
  METHOD_NOT_ALLOWED: () => 'We could not complete that request.',
  PAYLOAD_TOO_LARGE: () => 'That request is too large.',
  UNSUPPORTED_MEDIA_TYPE: () => 'We could not complete that request.',
  [NETWORK_ERROR]: () => 'We could not reach the server. Check your connection and try again.',
  [UNKNOWN_ERROR]: () => 'Something went wrong. Try again.',
}

export function describeError(error: unknown, context: ErrorContext = {}): string {
  if (!(error instanceof ApiError)) {
    return messages[UNKNOWN_ERROR]!(new ApiError(0, UNKNOWN_ERROR), context)
  }
  const describe = messages[error.code] ?? messages[UNKNOWN_ERROR]!
  return describe(error, context)
}

export const knownErrorCodes = Object.keys(messages)
