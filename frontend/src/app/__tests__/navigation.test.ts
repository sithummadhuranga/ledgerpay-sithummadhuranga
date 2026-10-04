import { describe, expect, it } from 'vitest'
import { homeFor, navigationFor } from '../navigation'

describe('what each role can open', () => {
  it('gives a customer the wallet, send and history', () => {
    expect(navigationFor(['Customer']).map((item) => item.to)).toEqual(['/wallet', '/send', '/history'])
  })

  it('gives an operator the top-up and the back office that both staff roles share', () => {
    expect(navigationFor(['Operator']).map((item) => item.to)).toEqual([
      '/operator/top-up', '/backoffice', '/backoffice/users', '/backoffice/transactions',
    ])
  })

  it('gives an admin the shared back office plus the audit log and the staff, and no top-up, which only an operator may make', () => {
    expect(navigationFor(['Admin']).map((item) => item.to)).toEqual([
      '/backoffice', '/backoffice/users', '/backoffice/transactions', '/backoffice/audit', '/backoffice/staff',
    ])
  })

  it('marks the overview as active only on its own address, because every other back-office address starts with it', () => {
    const overview = navigationFor(['Admin']).find((item) => item.to === '/backoffice')
    expect(overview?.end).toBe(true)
  })

  it('opens each role on its first page', () => {
    expect(homeFor(['Customer'])).toBe('/wallet')
    expect(homeFor(['Operator'])).toBe('/operator/top-up')
    expect(homeFor(['Admin'])).toBe('/backoffice')
  })

  it('shows a user with no known role nothing in the menu and sends them to the start', () => {
    expect(navigationFor([])).toEqual(navigationFor(['Customer']))
  })
})
