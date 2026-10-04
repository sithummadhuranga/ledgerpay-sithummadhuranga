import { Check, Circle } from 'lucide-react'
import { passwordRules } from './schemas'

// Shown before the user types, then ticked as each rule is met. A rule is marked by an icon and by words
// for screen readers, never by colour alone.
export function PasswordRules({ password }: { password: string }) {
  return (
    <div>
      <p id="password-rules" className="mb-1 text-sm text-muted-foreground">
        The password needs:
      </p>
      <ul aria-labelledby="password-rules" className="grid gap-0.5 text-sm">
        {passwordRules.map((rule) => {
          const met = rule.met(password)
          return (
            <li key={rule.id} className="flex items-center gap-2">
              {met ? <Check className="size-3.5" aria-hidden /> : <Circle className="size-3.5 text-muted-foreground" aria-hidden />}
              <span className={met ? '' : 'text-muted-foreground'}>{rule.text}</span>
              <span className="sr-only">{met ? 'met' : 'not met yet'}</span>
            </li>
          )
        })}
      </ul>
    </div>
  )
}
