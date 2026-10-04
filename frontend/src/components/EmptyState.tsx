import type { ReactNode } from 'react'

// One plain sentence and the next thing to do.
export function EmptyState({ children, action }: { children: ReactNode; action?: ReactNode }) {
  return (
    <div className="border-y py-8 text-sm text-muted-foreground">
      <p>{children}</p>
      {action ? <div className="mt-3">{action}</div> : null}
    </div>
  )
}
