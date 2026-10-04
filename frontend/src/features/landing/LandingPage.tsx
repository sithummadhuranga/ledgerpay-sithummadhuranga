import { ArrowRight } from 'lucide-react'
import { Footer } from '@/app/Footer'
import { homeFor } from '@/app/navigation'
import { PublicHeader } from '@/app/PublicHeader'
import { ButtonLink } from '@/components/ButtonLink'
import { useAuth } from '@/features/auth/useAuth'
import { StatementPreview } from './StatementPreview'

const steps = [
  { title: 'Open a wallet', text: 'Register with your email and mobile number. You get a wallet with its own 12 digit number and a balance of zero.' },
  { title: 'Add money', text: 'Pay in at a bank. An operator credits your wallet against the bank reference, and a reference can be used only once.' },
  { title: 'Send it', text: 'Find a wallet by its number or the mobile number, check the name, read the fee and the total, then confirm.' },
  { title: 'Read the statement', text: 'Every line shows the running balance after it, so you can follow where each rupee went.' },
]

const safeguards = [
  { title: 'Double entry', text: 'Each transaction posts debits and credits that add up to the same amount. Entries are never edited or deleted.' },
  { title: 'One send per confirm', text: 'Each send carries a key. If a connection drops and the request is sent again, the first result comes back and nothing is posted twice.' },
  { title: 'Frozen wallets', text: 'Staff can freeze a wallet and must give a reason. A frozen wallet cannot send or receive until it is unfrozen.' },
  { title: 'Sign-in limits', text: 'Five wrong passwords lock the account for 15 minutes. A sign-in lasts 15 minutes before the token has to be renewed.' },
  { title: 'Names stay masked', text: 'People you send to see you as N*** P***, not your full name, and nobody sees your email or balance.' },
]

export function LandingPage() {
  const { user } = useAuth()

  return (
    <div className="flex min-h-svh flex-col">
      <PublicHeader showSections />
      <main id="content" className="flex-1">
        <section className="mx-auto grid max-w-6xl gap-12 px-4 py-14 sm:px-6 sm:py-20 lg:grid-cols-[1.05fr_1fr] lg:items-center lg:gap-16">
          <div>
            <p className="mb-4 text-sm font-medium text-primary">A wallet for Sri Lankan rupees</p>
            <h1 className="text-4xl leading-[1.08] font-semibold sm:text-5xl lg:text-6xl">Send rupees the way a bank would count them.</h1>
            <p className="mt-6 max-w-xl text-lg text-muted-foreground">
              Top up at a bank, send to anyone by wallet number or mobile number, and keep a statement that always adds up. You see the fee before
              you confirm, and pressing confirm twice sends once.
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
          </div>
          <div className="min-w-0">
            <StatementPreview />
          </div>
        </section>

        <section id="how" className="border-y bg-card">
          <div className="mx-auto max-w-6xl px-4 py-14 sm:px-6 sm:py-20">
            <h2 className="text-3xl font-semibold sm:text-4xl">How it works</h2>
            <ol className="mt-10 grid gap-x-12 gap-y-8 sm:grid-cols-2">
              {steps.map((step, index) => (
                <li key={step.title} className="grid grid-cols-[2.5rem_1fr] gap-4 border-t pt-5">
                  <span className="num text-sm text-muted-foreground">{String(index + 1).padStart(2, '0')}</span>
                  <div>
                    <h3 className="text-xl font-semibold">{step.title}</h3>
                    <p className="mt-2 text-muted-foreground">{step.text}</p>
                  </div>
                </li>
              ))}
            </ol>
          </div>
        </section>

        <section id="safeguards" className="mx-auto max-w-6xl px-4 py-14 sm:px-6 sm:py-20">
          <div className="grid gap-10 lg:grid-cols-[1fr_1.6fr]">
            <div>
              <h2 className="text-3xl font-semibold sm:text-4xl">What keeps the numbers right</h2>
              <p className="mt-4 max-w-md text-muted-foreground">
                A wallet has to be correct before it is fast or pretty. These are the rules the system holds itself to.
              </p>
            </div>
            <dl className="divide-y border-y">
              {safeguards.map((item) => (
                <div key={item.title} className="grid gap-1 py-5 sm:grid-cols-[11rem_1fr] sm:gap-6">
                  <dt className="font-semibold">{item.title}</dt>
                  <dd className="text-muted-foreground">{item.text}</dd>
                </div>
              ))}
            </dl>
          </div>
        </section>

        {user ? null : (
          <section className="bg-panel text-panel-foreground ledger-rules">
            <div className="mx-auto flex max-w-6xl flex-col items-start gap-6 px-4 py-14 sm:px-6 sm:py-16 md:flex-row md:items-center md:justify-between">
              <div>
                <h2 className="text-3xl font-semibold sm:text-4xl">Open a wallet in a minute.</h2>
                <p className="mt-2 text-panel-muted">You need an email address and a mobile number that starts with +947.</p>
              </div>
              <ButtonLink to="/register" size="lg" variant="onPanel">
                Create an account
                <ArrowRight aria-hidden />
              </ButtonLink>
            </div>
          </section>
        )}
      </main>
      <Footer />
    </div>
  )
}
