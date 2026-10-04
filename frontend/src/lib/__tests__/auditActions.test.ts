import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { auditActions } from '../auditActions'

const serverActions = [
  ...readFileSync(resolve(process.cwd(), '../backend/src/LedgerPay.Domain/Constants/AuditActions.cs'), 'utf8').matchAll(
    /public const string \w+ = "(\w+)"/g,
  ),
].map((match) => match[1]!)

describe('the audit actions', () => {
  it('are the same as the ones the server writes', () => {
    expect(serverActions.length).toBeGreaterThan(10)
    expect([...auditActions].sort()).toEqual([...serverActions].sort())
  })
})
