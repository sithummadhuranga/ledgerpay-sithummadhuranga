import { ApiError, NETWORK_ERROR, UNKNOWN_ERROR, type ProblemBody } from './problem'
import { tokenStore } from './token'

// Every call to the API goes through here. Screens never call fetch themselves.
const BASE_URL: string = import.meta.env.VITE_API_BASE_URL ?? '/api/v1'

interface RequestOptions {
  body?: unknown
  idempotencyKey?: string
  signal?: AbortSignal
}

async function request<T>(method: string, path: string, options: RequestOptions = {}): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  const token = tokenStore.get()
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

  // A token that was sent and is no longer good ends the session. A wrong password at sign-in is a 401 too,
  // but it has its own code and no token was sent.
  if (error.code === 'UNAUTHENTICATED' && token) {
    tokenStore.clear()
    tokenStore.sessionEnded()
  }
  throw error
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
  post: <T>(path: string, body: unknown, options?: Omit<RequestOptions, 'body'>) =>
    request<T>('POST', path, { ...options, body }),
}
