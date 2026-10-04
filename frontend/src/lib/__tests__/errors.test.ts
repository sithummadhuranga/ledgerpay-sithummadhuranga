import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { ApiError } from '../api/problem'
import { describeError, knownErrorCodes } from '../errors'

// The server's list of codes is the source of truth. A new code there fails this test until it has a message here.
const serverCodes = [
  ...readFileSync(resolve(process.cwd(), '../backend/src/LedgerPay.Domain/Constants/ErrorCodes.cs'), 'utf8').matchAll(
    /public const string \w+ = "([A-Z_]+)"/g,
  ),
].map((match) => match[1]!)

describe('error messages', () => {
  it('has a message for every code the server can send', () => {
    expect(serverCodes.length).toBeGreaterThan(20)
    expect(serverCodes.filter((code) => !knownErrorCodes.includes(code))).toEqual([])
  })

  it('gives no message that is empty, shouts, or says oops', () => {
    for (const code of knownErrorCodes) {
      const text = describeError(new ApiError(400, code))
      expect(text.length).toBeGreaterThan(10)
      expect(text).not.toMatch(/!|oops/i)
      expect(text).toMatch(/^[\x20-\x7E]+$/)
    }
  })

  it('tells a locked account how many minutes to wait', () => {
    const text = describeError(new ApiError(423, 'ACCOUNT_LOCKED', {}, 900))
    expect(text).toContain('15 minutes')
  })

  it('says one minute and one second in the singular', () => {
    expect(describeError(new ApiError(423, 'ACCOUNT_LOCKED', {}, 30))).toContain('in 1 minute.')
    expect(describeError(new ApiError(423, 'ACCOUNT_LOCKED', {}, 60))).toContain('in 1 minute.')
    expect(describeError(new ApiError(423, 'ACCOUNT_LOCKED', {}, 61))).toContain('in 2 minutes.')
    expect(describeError(new ApiError(429, 'RATE_LIMITED', {}, 1))).toContain('in 1 second.')
    expect(describeError(new ApiError(429, 'RATE_LIMITED', {}, 30))).toContain('in 30 seconds.')
  })

  it('names the amount and the fee when the balance is too low', () => {
    const text = describeError(new ApiError(422, 'INSUFFICIENT_FUNDS'), { amount: '5000', fee: '25' })
    expect(text).toBe('Your balance does not cover LKR 5,000.00 plus the LKR 25.00 fee.')
  })

  it('still says something useful when the screen has no amounts', () => {
    expect(describeError(new ApiError(422, 'INSUFFICIENT_FUNDS'))).toContain('does not cover')
  })

  it('gives the trace id so a report can be matched to a log line', () => {
    expect(describeError(new ApiError(500, 'INTERNAL_ERROR', { traceId: 'abc123' }))).toContain('abc123')
  })

  it('falls back for a code it does not know and for something that is not an api error', () => {
    expect(describeError(new ApiError(418, 'TEAPOT'))).toBe('Something went wrong. Try again.')
    expect(describeError(new Error('boom'))).toBe('Something went wrong. Try again.')
  })
})
