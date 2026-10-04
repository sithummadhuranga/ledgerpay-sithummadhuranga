import { Check, Copy } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { Button } from '@/components/ui/button'

// The button says what it copies, and a separate live region announces that it worked.
export function CopyButton({ value, subject }: { value: string; subject: string }) {
  const [copied, setCopied] = useState(false)
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  useEffect(() => () => clearTimeout(timer.current), [])

  async function copy() {
    try {
      await navigator.clipboard.writeText(value)
      setCopied(true)
      clearTimeout(timer.current)
      timer.current = setTimeout(() => setCopied(false), 2000)
    } catch {
      // Copying can be refused, for example on a page without permission. The number is still on screen.
    }
  }

  return (
    <>
      <Button variant="ghost" size="sm" onClick={copy}>
        {copied ? <Check aria-hidden /> : <Copy aria-hidden />}
        {copied ? 'Copied' : 'Copy'}{' '}
        <span className="sr-only">{subject}</span>
      </Button>
      <span className="sr-only" aria-live="polite">
        {copied ? `${subject.charAt(0).toUpperCase()}${subject.slice(1)} copied` : ''}
      </span>
    </>
  )
}
