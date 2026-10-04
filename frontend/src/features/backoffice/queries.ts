import { keepPreviousData, useQuery } from '@tanstack/react-query'
import {
  getUser,
  listAudit,
  listStaff,
  listStaffTransactions,
  listUsers,
  type AuditFilter,
  type StaffTransactionFilter,
  type UserFilter,
} from '@/lib/api/backoffice'

export const backOfficeKeys = {
  users: (filter: UserFilter) => ['backoffice', 'users', filter] as const,
  user: (walletNumber: string) => ['backoffice', 'user', walletNumber] as const,
  transactions: (filter: StaffTransactionFilter) => ['backoffice', 'transactions', filter] as const,
  audit: (filter: AuditFilter) => ['backoffice', 'audit', filter] as const,
  staff: ['backoffice', 'staff'] as const,
}

// While the next page loads, the page on screen stays, so paging does not flash a skeleton each time.
export const useUsers = (filter: UserFilter) =>
  useQuery({ queryKey: backOfficeKeys.users(filter), queryFn: ({ signal }) => listUsers(filter, signal), placeholderData: keepPreviousData })

export const useUser = (walletNumber: string) =>
  useQuery({ queryKey: backOfficeKeys.user(walletNumber), queryFn: ({ signal }) => getUser(walletNumber, signal), retry: false })

export const useStaffTransactions = (filter: StaffTransactionFilter) =>
  useQuery({
    queryKey: backOfficeKeys.transactions(filter),
    queryFn: ({ signal }) => listStaffTransactions(filter, signal),
    placeholderData: keepPreviousData,
  })

export const useAudit = (filter: AuditFilter) =>
  useQuery({ queryKey: backOfficeKeys.audit(filter), queryFn: ({ signal }) => listAudit(filter, signal), placeholderData: keepPreviousData })

export const useStaff = () => useQuery({ queryKey: backOfficeKeys.staff, queryFn: ({ signal }) => listStaff(signal) })
