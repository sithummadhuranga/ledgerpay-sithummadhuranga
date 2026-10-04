// What the API sends when something is wrong: RFC 7807 Problem Details with a stable code and a trace id.
export interface ProblemBody {
  title?: string
  status?: number
  code?: string
  traceId?: string
  errors?: Record<string, string[]>
  retryAfterSeconds?: number
}

// Every failed call becomes one of these, so a screen only has to look at the code.
export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly traceId: string | undefined
  readonly errors: Record<string, string[]>
  readonly retryAfterSeconds: number | undefined

  constructor(status: number, code: string, body: ProblemBody = {}, retryAfterSeconds?: number) {
    super(body.title ?? code)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.traceId = body.traceId
    this.errors = body.errors ?? {}
    this.retryAfterSeconds = retryAfterSeconds ?? body.retryAfterSeconds
  }
}

// The call did not reach the API at all, or what came back was not an answer from it.
export const NETWORK_ERROR = 'NETWORK_ERROR'
export const UNKNOWN_ERROR = 'UNKNOWN_ERROR'
