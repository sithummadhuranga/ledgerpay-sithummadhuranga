import { describe, expect, it } from 'vitest'
import { shouldRetry } from '../queryClient'
import { ApiError } from '@/lib/api/problem'

describe('when a query is tried again', () => {
  it('tries a server error or a lost connection once more', () => {
    expect(shouldRetry(0, new ApiError(500, 'INTERNAL_ERROR'))).toBe(true)
    expect(shouldRetry(0, new ApiError(0, 'NETWORK_ERROR'))).toBe(true)
    expect(shouldRetry(1, new ApiError(500, 'INTERNAL_ERROR'))).toBe(false)
  })

  it('does not ask again for an answer that will not change', () => {
    expect(shouldRetry(0, new ApiError(404, 'WALLET_NOT_FOUND'))).toBe(false)
    expect(shouldRetry(0, new ApiError(403, 'FORBIDDEN'))).toBe(false)
    expect(shouldRetry(0, new ApiError(401, 'UNAUTHENTICATED'))).toBe(false)
  })
})
