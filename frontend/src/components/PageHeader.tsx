import type { ReactNode } from 'react'

export function PageHeader({ title, intro, children }: { title: string; intro?: string; children?: ReactNode }) {
  return (
    <div className="mb-8 flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 className="text-3xl font-semibold sm:text-4xl">{title}</h1>
        {intro ? <p className="mt-2 max-w-xl text-muted-foreground">{intro}</p> : null}
      </div>
      {children}
    </div>
  )
}
