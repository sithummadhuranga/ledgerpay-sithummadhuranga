import type { ReactNode } from 'react'

// The frame of the sign-in and register screens: a narrow column, the name, the title. No hero.
export function AuthShell({ title, children }: { title: string; children: ReactNode }) {
  return (
    <main className="mx-auto w-full max-w-sm px-4 py-12">
      <p className="mb-8 font-semibold tracking-tight">LedgerPay</p>
      <h1 className="mb-6 text-xl font-semibold">{title}</h1>
      {children}
    </main>
  )
}
