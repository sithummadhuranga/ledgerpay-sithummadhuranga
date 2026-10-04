import { ArrowRight } from 'lucide-react'
import { Footer } from '@/app/Footer'
import { homeFor } from '@/app/navigation'
import { PublicHeader } from '@/app/PublicHeader'
import { ButtonLink } from '@/components/ButtonLink'
import { useAuth } from '@/features/auth/useAuth'
import { PhonePreview } from './PhonePreview'

const gains = [
  { lead: 'You see the fee first.', text: 'Enter an amount and the fee and the total show before anything moves.' },
  { lead: 'Press twice, pay once.', text: 'If the connection drops and you try again, the first result comes back. Nothing is sent twice.' },
  { lead: 'A statement that adds up.', text: 'Every line shows your balance after it, so you can follow each rupee.' },
]

const safeguards = [
  { title: 'Double entry', text: 'Each transaction posts debits and credits that add up to the same amount. Entries are never edited or deleted.' },
  { title: 'Idempotency keys', text: 'Each send carries a key. A repeated request returns the first result and posts nothing new.' },
  { title: 'Frozen wallets', text: 'Staff can freeze a wallet and must give a reason. A frozen wallet cannot send or receive.' },
  { title: 'Sign-in limits', text: 'Five wrong passwords lock the account for 15 minutes. The sign-in token expires after 15 minutes.' },
  { title: 'Masked names', text: 'People you send to see you as N*** P***. Nobody sees your email or balance.' },
]

export function LandingPage() {
  const { user } = useAuth()

  return (
    <div className="flex min-h-svh flex-col">
      <PublicHeader showSections />
      <main id="content" className="flex-1">
        <section className="mx-auto grid max-w-6xl gap-14 px-4 pt-12 pb-16 sm:px-6 sm:pt-16 sm:pb-24 lg:grid-cols-[1.1fr_1fr] lg:items-center lg:gap-10">
          <div>
            <h1 className="max-w-xl text-4xl leading-[1.1] font-semibold sm:text-5xl">Send money by mobile number. See the fee first.</h1>
            <p className="mt-5 max-w-lg text-lg text-muted-foreground">
              A wallet for Sri Lankan rupees. Put money in at a bank, send it to anyone with a wallet, and read back every rupee on your statement.
            </p>
            <div className="mt-8 flex flex-wrap items-center gap-3">
              {user ? (
                <ButtonLink to={homeFor(user.roles)} size="lg">
                  Open your account
                  <ArrowRight aria-hidden />
                </ButtonLink>
              ) : (
                <>
                  <ButtonLink to="/register" size="lg">
                    Create an account
                    <ArrowRight aria-hidden />
                  </ButtonLink>
                  <ButtonLink to="/login" size="lg" variant="outline">
                    Sign in
                  </ButtonLink>
                </>
              )}
            </div>
            {user ? null : <p className="mt-4 text-sm text-muted-foreground">You need an email address and a mobile number that starts with +947.</p>}
          </div>
          <PhonePreview />
        </section>

        <section aria-label="What you get" className="border-y bg-card">
          <ul className="mx-auto grid max-w-6xl divide-y px-4 sm:px-6 md:grid-cols-3 md:divide-x md:divide-y-0">
            {gains.map((item) => (
              <li key={item.lead} className="py-6 md:px-8 md:first:pl-0 md:last:pr-0">
                <p className="font-semibold">{item.lead}</p>
                <p className="mt-1 text-muted-foreground">{item.text}</p>
              </li>
            ))}
          </ul>
        </section>

        <section id="safeguards" className="mx-auto max-w-6xl px-4 py-14 sm:px-6 sm:py-16">
          <h2 className="text-2xl font-semibold">How it stays correct</h2>
          <p className="mt-2 max-w-xl text-muted-foreground">The rules behind the numbers, for anyone who wants to check.</p>
          <dl className="mt-6 grid gap-x-12 sm:grid-cols-2">
            {safeguards.map((item) => (
              <div key={item.title} className="border-t py-4">
                <dt className="font-semibold">{item.title}</dt>
                <dd className="mt-1 text-sm text-muted-foreground">{item.text}</dd>
              </div>
            ))}
          </dl>
        </section>
      </main>
      <Footer />
    </div>
  )
}
