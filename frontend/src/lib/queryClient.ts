import { QueryClient } from '@tanstack/react-query'
import { ApiError } from './api/problem'

// A 4xx will not change by asking again. A network failure or a 5xx may, so those are tried once more.
export const shouldRetry = (failureCount: number, error: unknown) =>
  failureCount < 1 && !(error instanceof ApiError && error.status >= 400 && error.status < 500)

export function makeQueryClient(retry: typeof shouldRetry | false = shouldRetry) {
  return new QueryClient({
    defaultOptions: {
      queries: { retry, staleTime: 15_000, refetchOnWindowFocus: false },
    },
  })
}
