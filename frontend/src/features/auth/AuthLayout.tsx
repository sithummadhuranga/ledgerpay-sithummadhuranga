import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Logo } from '@/components/Logo'
import { StatementPreview } from '@/features/landing/StatementPreview'

// Sign in and register share a frame: the form on the left, and on a wide screen a dark panel with the example
// statement on the right. On a phone there is only the form.
export function AuthLayout({ title, intro, children }: { title: string; intro?: string; children: ReactNode }) {
  return (
    <div className="grid min-h-svh lg:grid-cols-[minmax(0,1fr)_minmax(0,1.05fr)]">
      <main id="content" className="flex flex-col px-4 py-6 sm:px-10 lg:px-16">
        <Link to="/" aria-label="LedgerPay home" className="self-start">
          <Logo />
        </Link>
        <div className="mx-auto flex w-full max-w-md flex-1 flex-col justify-center py-10">
          <h1 className="text-3xl font-semibold sm:text-4xl">{title}</h1>
          {intro ? <p className="mt-2 text-muted-foreground">{intro}</p> : null}
          <div className="mt-8">{children}</div>
        </div>
      </main>
      <aside aria-hidden className="relative hidden bg-panel ledger-rules lg:flex lg:flex-col lg:justify-center lg:px-14">
        <p className="mb-6 max-w-md font-heading text-3xl leading-snug text-panel-foreground">Every rupee leaves one wallet and arrives in another.</p>
        <div className="min-w-0">
          <StatementPreview />
        </div>
      </aside>
    </div>
  )
}
