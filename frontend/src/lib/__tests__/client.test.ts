import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { ApiError } from '../api/problem'
import { tokenStore } from '../api/token'

function respond(status: number, body: unknown, headers: Record<string, string> = {}) {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json', ...headers },
  })
}

const fetchMock = vi.fn()

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock)
  tokenStore.clear()
  tokenStore.whenSessionEnds(null)
})

afterEach(() => {
  fetchMock.mockReset()
  vi.unstubAllGlobals()
})

describe('the api client', () => {
  it('sends the token as a bearer header and the body as json', async () => {
    tokenStore.set('abc.def.ghi')
    fetchMock.mockResolvedValue(respond(200, { ok: true }))

    await api.post('/transfers', { amount: '100.00' }, { idempotencyKey: 'key-1' })

    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    const headers = init.headers as Record<string, string>
    expect(url).toBe('/api/v1/transfers')
    expect(headers.Authorization).toBe('Bearer abc.def.ghi')
    expect(headers['Idempotency-Key']).toBe('key-1')
    expect(headers['Content-Type']).toBe('application/json')
    expect(init.body).toBe('{"amount":"100.00"}')
  })

  it('sends no authorization header when nobody is signed in', async () => {
    fetchMock.mockResolvedValue(respond(200, {}))

    await api.get('/health')

    const headers = (fetchMock.mock.calls[0] as [string, RequestInit])[1].headers as Record<string, string>
    expect(headers.Authorization).toBeUndefined()
    expect(headers['Content-Type']).toBeUndefined()
  })

  it('turns problem details into an error with the code, the field errors and the trace id', async () => {
    fetchMock.mockResolvedValue(
      respond(400, { title: 'One or more fields are not valid', code: 'VALIDATION_FAILED', traceId: 'abc123', errors: { email: ['Enter a valid email address.'] } }),
    )

    const error = await api.post('/auth/register', {}).catch((caught: unknown) => caught)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 400, code: 'VALIDATION_FAILED', traceId: 'abc123' })
    expect((error as ApiError).errors.email).toEqual(['Enter a valid email address.'])
  })

  it('reads how long to wait from the Retry-After header', async () => {
    fetchMock.mockResolvedValue(respond(423, { code: 'ACCOUNT_LOCKED' }, { 'Retry-After': '900' }))

    const error = (await api.post('/auth/login', {}).catch((caught: unknown) => caught)) as ApiError

    expect(error.code).toBe('ACCOUNT_LOCKED')
    expect(error.retryAfterSeconds).toBe(900)
  })

  it('reports a call that never reached the server as a network error', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'))

    const error = (await api.get('/wallets/me').catch((caught: unknown) => caught)) as ApiError

    expect(error.code).toBe('NETWORK_ERROR')
  })

  it('reports an answer that is not problem details as an unknown error', async () => {
    fetchMock.mockResolvedValue(new Response('<html>bad gateway</html>', { status: 502 }))

    const error = (await api.get('/wallets/me').catch((caught: unknown) => caught)) as ApiError

    expect(error).toMatchObject({ status: 502, code: 'UNKNOWN_ERROR' })
  })

  it('ends the session when a token that was sent is refused', async () => {
    const ended = vi.fn()
    tokenStore.whenSessionEnds(ended)
    tokenStore.set('old-token')
    fetchMock.mockResolvedValue(respond(401, { code: 'UNAUTHENTICATED' }))

    await api.get('/wallets/me').catch(() => undefined)

    expect(ended).toHaveBeenCalledOnce()
    expect(tokenStore.get()).toBeNull()
  })

  it('does not end a newer session when a 401 comes back for a request from an older one', async () => {
    const ended = vi.fn()
    tokenStore.whenSessionEnds(ended)
    tokenStore.set('old-token')
    let answer: (response: Response) => void = () => undefined
    fetchMock.mockReturnValue(new Promise<Response>((resolve) => (answer = resolve)))

    const slow = api.get('/wallets/me').catch(() => undefined)
    tokenStore.set('new-token')
    answer(respond(401, { code: 'UNAUTHENTICATED' }))
    await slow

    expect(ended).not.toHaveBeenCalled()
    expect(tokenStore.get()).toBe('new-token')
  })

  it('does not end a session for a wrong password, which is a 401 with no token', async () => {
    const ended = vi.fn()
    tokenStore.whenSessionEnds(ended)
    fetchMock.mockResolvedValue(respond(401, { code: 'INVALID_CREDENTIALS' }))

    await api.post('/auth/login', {}).catch(() => undefined)

    expect(ended).not.toHaveBeenCalled()
  })

  it('answers undefined for a 204', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))

    await expect(api.post('/auth/logout')).resolves.toBeUndefined()
  })

  describe('when the access token has run out', () => {
    const renewed = (token: string) => respond(200, { accessToken: token, tokenType: 'Bearer', expiresAt: 'x', fullName: 'N', roles: ['Customer'], walletNumber: '1' })
    const headersOf = (call: unknown[]) => (call[1] as RequestInit).headers as Record<string, string>
    const urlOf = (call: unknown[]) => call[0] as string

    it('renews it through the cookie and repeats the call once with the new token and the same key', async () => {
      tokenStore.set('old-token')
      fetchMock
        .mockResolvedValueOnce(respond(401, { code: 'UNAUTHENTICATED' }))
        .mockResolvedValueOnce(renewed('new-token'))
        .mockResolvedValueOnce(respond(201, { ok: true }))

      const result = await api.post('/transfers', { amount: '100.00' }, { idempotencyKey: 'key-1' })

      expect(result).toEqual({ ok: true })
      const calls = fetchMock.mock.calls
      expect(calls.map(urlOf)).toEqual(['/api/v1/transfers', '/api/v1/auth/refresh', '/api/v1/transfers'])
      expect(headersOf(calls[2]!).Authorization).toBe('Bearer new-token')
      expect(headersOf(calls[2]!)['Idempotency-Key']).toBe('key-1')
      expect(tokenStore.get()).toBe('new-token')
    })

    it('asks for the renewal without the expired token', async () => {
      tokenStore.set('old-token')
      fetchMock
        .mockResolvedValueOnce(respond(401, { code: 'UNAUTHENTICATED' }))
        .mockResolvedValueOnce(renewed('new-token'))
        .mockResolvedValueOnce(respond(200, {}))

      await api.get('/wallets/me')

      const refresh = fetchMock.mock.calls[1]!
      expect((refresh[1] as RequestInit).method).toBe('POST')
      expect(headersOf(refresh).Authorization).toBeUndefined()
    })

    it('renews once for calls that fail together', async () => {
      tokenStore.set('old-token')
      fetchMock.mockImplementation(async (url: string, init?: RequestInit) => {
        if (url.endsWith('/auth/refresh')) {
          return renewed('new-token')
        }
        const sent = (init?.headers as Record<string, string> | undefined)?.Authorization
        return sent === 'Bearer new-token' ? respond(200, { ok: true }) : respond(401, { code: 'UNAUTHENTICATED' })
      })

      await Promise.all([api.get('/wallets/me'), api.get('/wallets/me/transactions'), api.get('/transfers/quote')])

      expect(fetchMock.mock.calls.filter((call) => urlOf(call).endsWith('/auth/refresh'))).toHaveLength(1)
    })

    it('ends the session when the cookie no longer works', async () => {
      const ended = vi.fn()
      tokenStore.whenSessionEnds(ended)
      tokenStore.set('old-token')
      fetchMock
        .mockResolvedValueOnce(respond(401, { code: 'UNAUTHENTICATED' }))
        .mockResolvedValueOnce(respond(401, { code: 'INVALID_REFRESH_TOKEN' }))

      const error = (await api.get('/wallets/me').catch((caught: unknown) => caught)) as ApiError

      expect(error.code).toBe('UNAUTHENTICATED')
      expect(ended).toHaveBeenCalledOnce()
      expect(tokenStore.get()).toBeNull()
      expect(fetchMock).toHaveBeenCalledTimes(2)
    })

    it('keeps the session and reports a network error when the renewal cannot be reached', async () => {
      const ended = vi.fn()
      tokenStore.whenSessionEnds(ended)
      tokenStore.set('old-token')
      fetchMock.mockResolvedValueOnce(respond(401, { code: 'UNAUTHENTICATED' })).mockRejectedValueOnce(new TypeError('Failed to fetch'))

      const error = (await api.get('/wallets/me').catch((caught: unknown) => caught)) as ApiError

      expect(error.code).toBe('NETWORK_ERROR')
      expect(ended).not.toHaveBeenCalled()
      expect(tokenStore.get()).toBe('old-token')
    })

    it('keeps the session when the renewal is refused for a busy server', async () => {
      const ended = vi.fn()
      tokenStore.whenSessionEnds(ended)
      tokenStore.set('old-token')
      fetchMock.mockResolvedValueOnce(respond(401, { code: 'UNAUTHENTICATED' })).mockResolvedValueOnce(respond(429, { code: 'RATE_LIMITED' }))

      await api.get('/wallets/me').catch(() => undefined)

      expect(ended).not.toHaveBeenCalled()
      expect(tokenStore.get()).toBe('old-token')
    })

    it('repeats a call only once, and ends the session when the new token is refused too', async () => {
      const ended = vi.fn()
      tokenStore.whenSessionEnds(ended)
      tokenStore.set('old-token')
      fetchMock
        .mockResolvedValueOnce(respond(401, { code: 'UNAUTHENTICATED' }))
        .mockResolvedValueOnce(renewed('new-token'))
        .mockResolvedValueOnce(respond(401, { code: 'UNAUTHENTICATED' }))

      await api.get('/wallets/me').catch(() => undefined)

      expect(fetchMock).toHaveBeenCalledTimes(3)
      expect(ended).toHaveBeenCalledOnce()
    })

    it('does not try to renew for a wrong password', async () => {
      fetchMock.mockResolvedValue(respond(401, { code: 'INVALID_CREDENTIALS' }))

      await api.post('/auth/login', {}).catch(() => undefined)

      expect(fetchMock).toHaveBeenCalledTimes(1)
    })
  })
})
