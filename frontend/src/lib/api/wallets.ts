import { api } from './client'
import { buildQuery as query } from './query'
import type { HistoryFilter, HistoryItem, LookupResult, Page, Quote, Wallet } from './types'

export const getMyWallet = (signal?: AbortSignal) => api.get<Wallet>('/wallets/me', { signal })

export const getHistory = (filter: HistoryFilter, signal?: AbortSignal) =>
  api.get<Page<HistoryItem>>(`/wallets/me/transactions?${query({ ...filter })}`, { signal })


// Exactly one of the two is given. The server refuses both and neither.
export const lookupWallet = (target: { walletNumber: string } | { phone: string }, signal?: AbortSignal) =>
  api.get<LookupResult>(`/wallets/lookup?${query(target)}`, { signal })

export const getQuote = (amount: string, signal?: AbortSignal) =>
  api.get<Quote>(`/transfers/quote?${query({ amount })}`, { signal })
