// The shapes the API sends. Amounts arrive as numbers and are only ever shown, never added up here:
// the server works out every fee and total.
export type WalletStatus = 'Active' | 'Frozen'
export type TransactionType = 'TopUp' | 'Transfer'
export type TransactionStatus = 'Completed' | 'Failed'
export type TransactionDirection = 'Sent' | 'Received'

export interface LoginRequest {
  email: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  tokenType: string
  expiresAt: string
  fullName: string
  roles: string[]
  walletNumber: string | null
}

export interface RegisterRequest {
  fullName: string
  email: string
  phone: string
  password: string
}

export interface RegisterResponse {
  fullName: string
  email: string
  phone: string
  walletNumber: string
}

export interface Wallet {
  walletNumber: string
  holderName: string
  balance: number
  availableBalance: number
  currency: string
  status: WalletStatus
}

export interface HistoryItem {
  reference: string
  type: TransactionType
  direction: TransactionDirection
  amount: number
  fee: number
  counterpartyName: string | null
  note: string | null
  status: TransactionStatus
  createdAt: string
  balanceAfter: number | null
  failureCode: string | null
}

export interface Page<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}
