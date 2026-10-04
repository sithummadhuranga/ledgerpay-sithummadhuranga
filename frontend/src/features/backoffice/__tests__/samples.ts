import type { AuditEntry, StaffMember, StaffTransaction, UserDetail, UserSummary } from '@/lib/api/types'

// People and money for the back-office tests.
export const nimali: UserSummary = {
  walletNumber: '482915067314', fullName: 'Nimali Perera', email: 'nimali.perera@example.com', phone: '+94771284635',
  balance: 12450, walletStatus: 'Active', locked: false, createdAt: '2026-10-02T10:00:00Z',
}
export const kasun: UserSummary = {
  walletNumber: '909566829850', fullName: 'Kasun Jayawardena', email: 'kasun.jayawardena@example.com', phone: '+94712390581',
  balance: 300, walletStatus: 'Frozen', locked: true, createdAt: '2026-10-02T11:00:00Z',
}

export const transfer: StaffTransaction = {
  reference: 'TX7K2M9Q4PXW81', type: 'Transfer', status: 'Completed', amount: 5000, fee: 25, failureCode: null,
  senderWalletNumber: nimali.walletNumber, senderName: nimali.fullName, receiverWalletNumber: kasun.walletNumber, receiverName: kasun.fullName,
  requestedReceiver: null, bankReference: null, note: 'Rent', createdAt: '2026-10-04T09:05:00Z',
}
export const refused: StaffTransaction = {
  ...transfer, reference: 'TXFAILED00001', status: 'Failed', failureCode: 'INSUFFICIENT_FUNDS', receiverWalletNumber: null, receiverName: null,
  requestedReceiver: '+94700000000', fee: 0,
}
export const topUp: StaffTransaction = {
  ...transfer, reference: 'TXTOPUP000001', type: 'TopUp', fee: 0, senderWalletNumber: null, senderName: null, bankReference: 'BANK12AB34', note: null,
}

export const detail: UserDetail = {
  ...nimali, statusReason: null, statusChangedAt: null, statusChangedBy: null, lockedUntil: null, failedLoginCount: 0,
  recentTransactions: [transfer, topUp],
}
export const frozenDetail: UserDetail = {
  ...kasun, statusReason: 'Reported lost phone', statusChangedAt: '2026-10-04T08:00:00Z', statusChangedBy: 'Dilani Senanayake',
  lockedUntil: '2026-10-04T10:30:00Z', failedLoginCount: 5, recentTransactions: [],
}

export const page = <T,>(items: T[], pageSize = 10, extra: Partial<{ page: number; totalCount: number; totalPages: number }> = {}) => ({
  items, page: 1, pageSize, totalCount: items.length, totalPages: items.length === 0 ? 0 : 1, ...extra,
})

export const entry = (over: Partial<AuditEntry> = {}): AuditEntry => ({
  createdAt: '2026-10-04T09:05:00Z', action: 'TopUp', entityType: 'Transaction', entityReference: 'TXTOPUP000001',
  actorName: 'Dilani Senanayake', actorEmail: 'dilani.senanayake@example.com', ipAddress: '203.0.113.7', correlationId: 'c-1', details: null, ...over,
})

export const operatorMember: StaffMember = {
  fullName: 'Dilani Senanayake', email: 'dilani.senanayake@example.com', role: 'Operator', restricted: false, restrictedAt: null,
  restrictedReason: null, restrictedBy: null, createdAt: '2026-10-02T10:00:00Z',
}
export const adminMember: StaffMember = { ...operatorMember, fullName: 'Chamara Rajapaksa', email: 'chamara.rajapaksa@example.com', role: 'Admin' }
