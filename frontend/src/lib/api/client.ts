import { ApiError, NETWORK_ERROR, UNKNOWN_ERROR, type ProblemBody } from './problem'
import { tokenStore } from './token'
import type { LoginResponse } from './types'

// Every call to the API goes through here. Screens never call fetch themselves.
const BASE_URL: string = import.meta.env.VITE_API_BASE_URL ?? '/api/v1'

interface RequestOptions {
  body?: unknown
  idempotencyKey?: string
  signal?: AbortSignal
  // The call does not carry the access token. The refresh and sign-out calls use the cookie only.
  anonymous?: boolean
  // Set on the one repeat after a token was renewed, so a call is never repeated twice.
  repeated?: boolean
}

export type RenewResult = { status: 'renewed'; response: LoginResponse } | { status: 'ended' } | { status: 'unreachable' }

async function request<T>(method: string, path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  const token = options.anonymous ? null : tokenStore.get()
  if (token) {
    headers.Authorization = `Bearer ${token}`
  }
  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }
  if (options.idempotencyKey) {
    headers['Idempotency-Key'] = options.idempotencyKey
  }

  let response: Response
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      method,
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: options.signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error
    }
    throw new ApiError(0, NETWORK_ERROR)
  }

  if (response.ok) {
    return (response.status === 204 ? undefined : await response.json()) as T
  }

  const problem = await readProblem(response)
  const retryAfter = Number(response.headers.get('Retry-After'))
  const error = new ApiError(
    response.status,
    problem.code ?? UNKNOWN_ERROR,
    problem,
    Number.isFinite(retryAfter) && retryAfter > 0 ? retryAfter : undefined,
  )

  // A token that was sent and is no longer good. A wrong password at sign-in is a 401 too, but it has its own code
  // and no token was sent.
  if (error.code === 'UNAUTHENTICATED' && token && !options.repeated) {
    const current = tokenStore.get()

    // Another call already got a new token while this one was in flight, so the answer is about the old token.
    if (current && current !== token) {
      return request<T>(method, path, { ...options, repeated: true })
    }

    if (current === token) {
      const renewed = await renewSession()
      if (renewed.status === 'renewed') {
        return request<T>(method, path, { ...options, repeated: true })
      }
      if (renewed.status === 'unreachable') {
        throw new ApiError(0, NETWORK_ERROR)
      }

      tokenStore.clear()
      tokenStore.sessionEnded()
    }
  } else if (error.code === 'UNAUTHENTICATED' && token && tokenStore.get() === token) {
    // The renewed token was refused as well, so there is nothing left to try.
    tokenStore.clear()
    tokenStore.sessionEnded()
  }
  throw error
}

// Swaps the refresh cookie for a new access token. Calls made while one swap is running wait for it, because the
// cookie can be used once. Tabs of the browser take turns through a lock, for the same reason.
let renewing: Promise<RenewResult> | null = null

export function renewSession(): Promise<RenewResult> {
  renewing ??= withTabLock(renewOnce).finally(() => {
    renewing = null
  })
  return renewing
}

const withTabLock = <T>(work: () => Promise<T>): Promise<T> =>
  'locks' in navigator ? navigator.locks.request('ledgerpay-refresh', work) : work()

async function renewOnce(): Promise<RenewResult> {
  try {
    const response = await request<LoginResponse>('POST', '/auth/refresh', { anonymous: true })
    tokenStore.set(response.accessToken)
    return { status: 'renewed', response }
  } catch (error) {
    // The server said the cookie is no good. Anything else (offline, busy, broken) may pass, so the session stays.
    return error instanceof ApiError && (error.status === 401 || error.status === 403) ? { status: 'ended' } : { status: 'unreachable' }
  }
}

async function readProblem(response: Response): Promise<ProblemBody> {
  try {
    return (await response.json()) as ProblemBody
  } catch {
    return {}
  }
}

export const api = {
  get: <T>(path: string, options?: Omit<RequestOptions, 'body'>) => request<T>('GET', path, options),
  post: <T>(path: string, body?: unknown, options?: Omit<RequestOptions, 'body'>) =>
    request<T>('POST', path, { ...options, body }),
  patch: <T>(path: string, body: unknown, options?: Omit<RequestOptions, 'body'>) =>
    request<T>('PATCH', path, { ...options, body }),
  delete: <T>(path: string, options?: Omit<RequestOptions, 'body'>) => request<T>('DELETE', path, options),
}
