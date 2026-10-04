import { describe, expect, it } from 'vitest'
import { safeInternalPath } from '../navigation'

describe('safeInternalPath', () => {
  it('keeps a path inside the app, with its query', () => {
    expect(safeInternalPath('/history')).toBe('/history')
    expect(safeInternalPath('/history?page=2&from=2026-10-01')).toBe('/history?page=2&from=2026-10-01')
    expect(safeInternalPath('/')).toBe('/')
  })

  it.each(['//evil.example', '/\\evil.example', 'https://evil.example', 'javascript:alert(1)', 'history', '', undefined, null, 42])(
    'sends %j to the wallet instead',
    (value) => {
      expect(safeInternalPath(value)).toBe('/')
    },
  )
})
