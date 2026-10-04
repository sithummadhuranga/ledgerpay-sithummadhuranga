import { describe, expect, it } from 'vitest'
import { homeFor, navigationFor } from '../navigation'

describe('what each role can open', () => {
  it('gives a customer the wallet, send and history', () => {
    expect(navigationFor(['Customer']).map((item) => item.to)).toEqual(['/wallet', '/send', '/history'])
  })

  it('gives an operator the top-up, wallet status and transaction lookup', () => {
    expect(navigationFor(['Operator']).map((item) => item.to)).toEqual(['/operator/top-up', '/backoffice/wallets', '/backoffice/transactions'])
  })

  it('gives an admin the back office without the top-up, which only an operator may make', () => {
    expect(navigationFor(['Admin']).map((item) => item.to)).toEqual(['/backoffice/wallets', '/backoffice/transactions'])
  })

  it('opens each role on its first page', () => {
    expect(homeFor(['Customer'])).toBe('/wallet')
    expect(homeFor(['Operator'])).toBe('/operator/top-up')
    expect(homeFor(['Admin'])).toBe('/backoffice/wallets')
  })

  it('shows a user with no known role nothing in the menu and sends them to the start', () => {
    expect(navigationFor([])).toEqual(navigationFor(['Customer']))
  })
})
