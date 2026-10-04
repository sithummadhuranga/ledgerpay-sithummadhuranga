import { api } from './client'
import { buildQuery } from './query'
import type {
  AuditEntry,
  Page,
  StaffMember,
  StaffTransaction,
  TopUpReceipt,
  TopUpRequest,
  TransactionDetail,
  TransactionStatus,
  TransactionType,
  UserDetail,
  UserSummary,
  WalletStatus,
  WalletStatusReceipt,
  WalletStatusRequest,
} from './types'

export interface UserFilter {
  page: number
  pageSize: number
  search?: string
  status?: 'Active' | 'Frozen' | 'Locked'
}

export interface StaffTransactionFilter {
  page: number
  pageSize: number
  type?: TransactionType
  status?: TransactionStatus
  walletNumber?: string
  from?: string
  to?: string
}

export interface AuditFilter {
  page: number
  pageSize: number
  action?: string
  actor?: string
  from?: string
  to?: string
}


export const topUp = (body: TopUpRequest, idempotencyKey: string) =>
  api.post<TopUpReceipt>('/admin/topups', body, { idempotencyKey })

export const setWalletStatus = (walletNumber: string, body: WalletStatusRequest) =>
  api.patch<WalletStatusReceipt>(`/admin/wallets/${encodeURIComponent(walletNumber)}/status`, body)

export const getTransaction = (reference: string, signal?: AbortSignal) =>
  api.get<TransactionDetail>(`/transactions/${encodeURIComponent(reference)}`, { signal })

export const listUsers = (filter: UserFilter, signal?: AbortSignal) =>
  api.get<Page<UserSummary>>(`/admin/users?${buildQuery({ ...filter })}`, { signal })

export const getUser = (walletNumber: string, signal?: AbortSignal) =>
  api.get<UserDetail>(`/admin/users/${encodeURIComponent(walletNumber)}`, { signal })

export const listStaffTransactions = (filter: StaffTransactionFilter, signal?: AbortSignal) =>
  api.get<Page<StaffTransaction>>(`/admin/transactions?${buildQuery({ ...filter })}`, { signal })

export const listAudit = (filter: AuditFilter, signal?: AbortSignal) =>
  api.get<Page<AuditEntry>>(`/admin/audit-logs?${buildQuery({ ...filter })}`, { signal })

export const listStaff = (signal?: AbortSignal) => api.get<StaffMember[]>('/admin/staff', { signal })

export const setRestriction = (body: { email: string; restricted: boolean; reason: string }) =>
  api.patch<StaffMember>('/admin/staff/restriction', body)

export type { WalletStatus }
