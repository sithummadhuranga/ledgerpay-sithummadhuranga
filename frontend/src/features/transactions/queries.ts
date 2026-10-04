import { keepPreviousData, useQuery } from '@tanstack/react-query'
import type { HistoryFilter } from '@/lib/api/types'
import { getHistory } from '@/lib/api/wallets'

export const transactionKeys = {
  all: ['transactions'] as const,
  recent: (count: number) => ['transactions', 'recent', count] as const,
  history: (filter: HistoryFilter) => ['transactions', 'history', filter] as const,
}

export function useRecentTransactions(count: number) {
  return useQuery({
    queryKey: transactionKeys.recent(count),
    queryFn: ({ signal }) => getHistory({ page: 1, pageSize: count }, signal),
  })
}

// While the next page loads, the page on screen stays, so paging does not flash a skeleton each time.
export function useHistory(filter: HistoryFilter) {
  return useQuery({
    queryKey: transactionKeys.history(filter),
    queryFn: ({ signal }) => getHistory(filter, signal),
    placeholderData: keepPreviousData,
  })
}
