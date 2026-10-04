// The actions the server writes to the audit log, in the order the screen offers them. A test compares this list
// with the server's own, so a new action there fails until it is added here.
export const auditActions = [
  'Register',
  'LoginSucceeded',
  'LoginFailed',
  'AccountLocked',
  'RefreshRotated',
  'RefreshReuseDetected',
  'RefreshGraceUsed',
  'Logout',
  'SessionRevoked',
  'TopUp',
  'TopUpFailed',
  'Transfer',
  'TransferFailed',
  'WalletFrozen',
  'WalletUnfrozen',
  'UserViewed',
  'AccountRestricted',
  'AccountRestrictionLifted',
] as const
