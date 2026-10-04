import { api } from './client'
import type { HistoryItem, Page, Wallet } from './types'

export const getMyWallet = (signal?: AbortSignal) => api.get<Wallet>('/wallets/me', { signal })

export const getRecentTransactions = (count: number, signal?: AbortSignal) =>
  api.get<Page<HistoryItem>>(`/wallets/me/transactions?page=1&pageSize=${count}`, { signal })
