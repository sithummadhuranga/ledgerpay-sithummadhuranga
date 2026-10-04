import { api } from './client'
import type { TopUpReceipt, TopUpRequest, TransactionDetail, WalletStatusReceipt, WalletStatusRequest } from './types'

export const topUp = (body: TopUpRequest, idempotencyKey: string) =>
  api.post<TopUpReceipt>('/admin/topups', body, { idempotencyKey })

export const setWalletStatus = (walletNumber: string, body: WalletStatusRequest) =>
  api.patch<WalletStatusReceipt>(`/admin/wallets/${encodeURIComponent(walletNumber)}/status`, body)

export const getTransaction = (reference: string, signal?: AbortSignal) =>
  api.get<TransactionDetail>(`/transactions/${encodeURIComponent(reference)}`, { signal })
