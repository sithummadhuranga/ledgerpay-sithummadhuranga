import { useQuery } from '@tanstack/react-query'
import { getRecentTransactions } from '@/lib/api/wallets'

export const transactionKeys = {
  recent: (count: number) => ['transactions', 'recent', count] as const,
}

export function useRecentTransactions(count: number) {
  return useQuery({ queryKey: transactionKeys.recent(count), queryFn: ({ signal }) => getRecentTransactions(count, signal) })
}
