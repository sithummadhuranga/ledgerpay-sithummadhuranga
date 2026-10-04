// The UTC date n days ago as yyyy-mm-dd, which is how the server counts a day.
export const daysAgoUtc = (days: number, now: Date = new Date()) =>
  new Date(now.getTime() - days * 86_400_000).toISOString().slice(0, 10)
