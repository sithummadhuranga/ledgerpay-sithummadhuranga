import { Footer } from './Footer'
import { PublicHeader } from './PublicHeader'
import { ButtonLink } from '@/components/ButtonLink'

export function NotFoundPage() {
  return (
    <div className="flex min-h-svh flex-col">
      <PublicHeader />
      <main id="content" className="mx-auto w-full max-w-6xl flex-1 px-4 py-20 sm:px-6">
        <p className="num text-sm text-muted-foreground">404</p>
        <h1 className="mt-2 text-4xl font-semibold">There is nothing at this address.</h1>
        <p className="mt-3 max-w-md text-muted-foreground">The page may have moved, or the link may have a typing mistake.</p>
        <ButtonLink to="/" className="mt-8">
          Go to the start
        </ButtonLink>
      </main>
      <Footer />
    </div>
  )
}
