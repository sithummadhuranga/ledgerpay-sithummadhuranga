import type { ReactNode } from 'react'

export function PageHeader({ title, children }: { title: string; children?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-baseline justify-between gap-2 border-b pb-3">
      <h1 className="text-xl font-semibold">{title}</h1>
      {children}
    </div>
  )
}
