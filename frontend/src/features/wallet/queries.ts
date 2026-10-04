import { useQuery } from '@tanstack/react-query'
import { getMyWallet } from '@/lib/api/wallets'

// One key per kind of data. A transfer or a top-up refreshes these, so the balance never goes stale.
export const walletKeys = {
  me: ['wallet', 'me'] as const,
}

export function useMyWallet() {
  return useQuery({ queryKey: walletKeys.me, queryFn: ({ signal }) => getMyWallet(signal) })
}
