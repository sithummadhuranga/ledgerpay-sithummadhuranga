import { ArrowLeftRight, Landmark, ListOrdered, ReceiptText, ShieldCheck, Wallet, type LucideIcon } from 'lucide-react'

export interface NavItem {
  to: string
  label: string
  icon: LucideIcon
}

const customer: NavItem[] = [
  { to: '/wallet', label: 'Wallet', icon: Wallet },
  { to: '/send', label: 'Send', icon: ArrowLeftRight },
  { to: '/history', label: 'History', icon: ListOrdered },
]

const operator: NavItem[] = [
  { to: '/operator/top-up', label: 'Top up', icon: Landmark },
  { to: '/backoffice/wallets', label: 'Wallets', icon: ShieldCheck },
  { to: '/backoffice/transactions', label: 'Transactions', icon: ReceiptText },
]

const admin: NavItem[] = operator.filter((item) => item.to !== '/operator/top-up')

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
