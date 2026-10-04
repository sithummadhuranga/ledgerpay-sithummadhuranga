import { ArrowLeftRight, Landmark, LayoutDashboard, ListOrdered, ReceiptText, ScrollText, UserRoundCog, Users, Wallet, type LucideIcon } from 'lucide-react'

export interface NavItem {
  to: string
  label: string
  icon: LucideIcon
  // The link is active only on exactly this address, because other addresses start with it.
  end?: boolean
}

const customer: NavItem[] = [
  { to: '/wallet', label: 'Wallet', icon: Wallet },
  { to: '/send', label: 'Send', icon: ArrowLeftRight },
  { to: '/history', label: 'History', icon: ListOrdered },
]

// What operators and admins both do: see what needs a look, find customers, read transactions.
const backOffice: NavItem[] = [
  { to: '/backoffice', label: 'Overview', icon: LayoutDashboard, end: true },
  { to: '/backoffice/users', label: 'Customers', icon: Users },
  { to: '/backoffice/transactions', label: 'Transactions', icon: ReceiptText },
]

// An operator also tops wallets up. An admin also reads the audit log and restricts operators.
const operator: NavItem[] = [{ to: '/operator/top-up', label: 'Top up', icon: Landmark }, ...backOffice]

const admin: NavItem[] = [
  ...backOffice,
  { to: '/backoffice/audit', label: 'Audit log', icon: ScrollText },
  { to: '/backoffice/staff', label: 'Staff', icon: UserRoundCog },
]

// What each role can open. The server checks the role again on every call: this only decides what is shown.
export function navigationFor(roles: string[]): NavItem[] {
  if (roles.includes('Operator')) {
    return operator
  }
  if (roles.includes('Admin')) {
    return admin
  }
  return customer
}

// The first screen after signing in.
export function homeFor(roles: string[]): string {
  return navigationFor(roles)[0]?.to ?? '/'
}
