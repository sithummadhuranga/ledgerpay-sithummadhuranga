import { api } from './client'
import type { HistoryFilter, HistoryItem, LookupResult, Page, Quote, Wallet } from './types'

const query = (values: Record<string, string | number | undefined>) => {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(values)) {
    if (value !== undefined && value !== '') {
      search.set(key, String(value))
    }
  }
  return search.toString()
}

export const getMyWallet = (signal?: AbortSignal) => api.get<Wallet>('/wallets/me', { signal })

export const getHistory = (filter: HistoryFilter, signal?: AbortSignal) =>
  api.get<Page<HistoryItem>>(`/wallets/me/transactions?${query({ ...filter })}`, { signal })


// Exactly one of the two is given. The server refuses both and neither.
export const lookupWallet = (target: { walletNumber: string } | { phone: string }, signal?: AbortSignal) =>
  api.get<LookupResult>(`/wallets/lookup?${query(target)}`, { signal })

export const getQuote = (amount: string, signal?: AbortSignal) =>
  api.get<Quote>(`/transfers/quote?${query({ amount })}`, { signal })
