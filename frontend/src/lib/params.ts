// Reading the filters of a list from the address. Anything that is not the right shape counts as not set.
export const positive = (value: string | null) => Math.max(1, Math.floor(Number(value)) || 1)

export const isDate = (value: string | null): value is string => !!value && /^\d{4}-\d{2}-\d{2}$/.test(value)
