import { describe, expect, it } from 'vitest'
import { formatDateTime, formatMoney, formatSignedMoney } from '../format'

describe('formatMoney', () => {
  it('writes LKR, thousands separators and two decimals', () => {
    expect(formatMoney(12450)).toBe('LKR 12,450.00')
    expect(formatMoney(1234567.5)).toBe('LKR 1,234,567.50')
    expect(formatMoney(0)).toBe('LKR 0.00')
  })

  it('reads an amount typed as text without turning it into a different number', () => {
    expect(formatMoney('5000')).toBe('LKR 5,000.00')
    expect(formatMoney('10.05')).toBe('LKR 10.05')
  })

  it('shows money out with a minus and money in with a plus', () => {
    expect(formatSignedMoney(1000, 'Sent')).toBe('-LKR 1,000.00')
    expect(formatSignedMoney(1000, 'Received')).toBe('+LKR 1,000.00')
  })
})

describe('formatDateTime', () => {
  it('shows a UTC time in the time zone of the reader', () => {
    const text = formatDateTime('2026-10-04T09:05:00Z')
    expect(text).toMatch(/04 Oct 2026/)
    expect(text).toMatch(/\d{2}:\d{2}/)
  })
})
