import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <main className="mx-auto max-w-sm px-4 py-12 text-sm">
      <h1 className="mb-2 text-xl font-semibold">Page not found</h1>
      <p className="text-muted-foreground">
        There is nothing at this address.{' '}
        <Link to="/" className="text-primary underline underline-offset-4">
          Go to your wallet
        </Link>
      </p>
    </main>
  )
}
