// Where to go after signing in. Only a path inside the app is accepted: it must start with one slash and not
// with two, or with a slash and a backslash, which a browser would read as another site.
export function safeInternalPath(value: unknown): string {
  if (typeof value !== 'string' || !value.startsWith('/') || value.startsWith('//') || value.startsWith('/\\')) {
    return '/'
  }
  return value
}
