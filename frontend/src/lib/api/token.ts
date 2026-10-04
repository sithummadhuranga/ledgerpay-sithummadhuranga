// The access token lives in this variable and nowhere else: not in localStorage, not in sessionStorage.
// A page reload forgets it, and the refresh cookie gets a new one.
let accessToken: string | null = null
let onSessionEnded: (() => void) | null = null

export const tokenStore = {
  get: () => accessToken,
  set: (token: string) => {
    accessToken = token
  },
  clear: () => {
    accessToken = null
  },
  // The client calls this when the API says the token is no longer good.
  whenSessionEnds: (handler: (() => void) | null) => {
    onSessionEnded = handler
  },
  sessionEnded: () => onSessionEnded?.(),
}
