import type { ComponentProps } from 'react'
import { Link } from 'react-router-dom'
import { buttonVariants } from '@/components/ui/button'
import { cn } from '@/lib/utils'

type ButtonLinkProps = ComponentProps<typeof Link> & Pick<Parameters<typeof buttonVariants>[0] & object, 'variant' | 'size'>

// A link that looks like a button. It stays a real link, so a screen reader says "link" and the user can open it in a new tab.
export function ButtonLink({ variant, size, className, ...props }: ButtonLinkProps) {
  return <Link className={cn(buttonVariants({ variant, size }), className)} {...props} />
}
