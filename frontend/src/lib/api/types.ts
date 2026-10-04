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

export interface LookupResult {
  walletNumber: string
  holderName: string
  active: boolean
}

export interface Quote {
  amount: number
  fee: number
  total: number
}

export interface TransferRequest {
  recipientWalletNumber?: string
  recipientPhone?: string
  amount: string
  note?: string
}

export interface TransferReceipt {
  reference: string
  amount: number
  fee: number
  total: number
  balanceAfter: number
  recipientWalletNumber: string
  createdAt: string
}

export interface TopUpRequest {
  walletNumber: string
  amount: string
  bankReference: string
  note?: string
}

export interface TopUpReceipt {
  reference: string
  walletNumber: string
  amount: number
  balanceAfter: number
  bankReference: string
  createdAt: string
}

export interface WalletStatusRequest {
  status: WalletStatus
  reason: string
}

export interface WalletStatusReceipt {
  walletNumber: string
  status: WalletStatus
  reason: string
  changedAt: string
}

export interface TransactionDetail {
  reference: string
  type: TransactionType
  status: TransactionStatus
  direction: TransactionDirection | null
  amount: number
  fee: number
  counterpartyName: string | null
  note: string | null
  failureCode: string | null
  createdAt: string
  senderWalletNumber: string | null
  receiverWalletNumber: string | null
  bankReference: string | null
}

export interface HistoryFilter {
  page: number
  pageSize: number
  from?: string
  to?: string
}
