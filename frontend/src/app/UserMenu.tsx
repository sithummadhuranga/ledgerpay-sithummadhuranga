import { LogOut } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useAuth } from '@/features/auth/useAuth'

const initials = (name: string) =>
  name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((word) => Array.from(word)[0])
    .join('')
    .toUpperCase()

export function UserMenu() {
  const { user, signOut } = useAuth()
  const navigate = useNavigate()
  if (!user) {
    return null
  }

  function leave() {
    signOut()
    navigate('/login', { replace: true })
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        aria-label="Account menu"
        className="flex h-10 items-center gap-2 rounded-md border bg-card py-1 pr-2 pl-1 text-sm outline-none hover:bg-secondary focus-visible:ring-2 focus-visible:ring-ring"
      >
        <span aria-hidden className="grid size-8 place-items-center rounded-sm bg-primary text-xs font-semibold text-primary-foreground">
          {initials(user.fullName)}
        </span>
        <span className="hidden max-w-32 truncate sm:block">{user.fullName}</span>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-64">
        <DropdownMenuGroup>
          <DropdownMenuLabel className="grid gap-0.5 py-2">
            <span className="font-medium text-foreground">{user.fullName}</span>
            <span className="text-xs font-normal text-muted-foreground">{user.roles.join(', ')}</span>
            {user.walletNumber ? <span className="num text-xs font-normal text-muted-foreground">{user.walletNumber}</span> : null}
          </DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem onClick={leave}>
          <LogOut aria-hidden />
          Sign out
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
