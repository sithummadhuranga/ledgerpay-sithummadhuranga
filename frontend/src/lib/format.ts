// The only place money and dates become text. Money is only shown here, never added up or multiplied.
const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const dateTime = new Intl.DateTimeFormat('en-GB', {
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

export function formatMoney(value: number | string): string {
  // Intl reads a numeric string exactly, so an amount typed as text is not turned into a float first.
  return `LKR ${money.format(value as `${number}`)}`
}

// Money with its direction: a plus for money in, a minus for money out.
export function formatSignedMoney(value: number | string, direction: 'Sent' | 'Received'): string {
  return `${direction === 'Sent' ? '-' : '+'}${formatMoney(value)}`
}

// The API sends UTC. The reader sees their own time zone.
export function formatDateTime(isoUtc: string): string {
  return dateTime.format(new Date(isoUtc))
}
