import { ApiError } from './api/problem'

// A new Idempotency-Key for one attempt to move money. The server answers the same key and the same details with
// the same result, so a retry after a lost connection cannot send the money twice. A key is made when the user
// reaches the confirmation, kept for retries, and replaced as soon as the details change or the money has moved.
export const newIdempotencyKey = () => crypto.randomUUID()

// True when the server has answered and refused the request, so it is known that nothing moved. The server stores
// that refusal under the key and would give the same refusal to a second try, so a second try needs a new key.
// A lost connection or a 5xx is different: the money may have moved, and only the same key makes the retry safe.
// 429 is left out because the request was never looked at.
export const wasRefused = (error: unknown) =>
  error instanceof ApiError && error.status >= 400 && error.status < 500 && error.status !== 429
