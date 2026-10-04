import { Check, Copy } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'

export function CopyButton({ value, label }: { value: string; label: string }) {
  const [copied, setCopied] = useState(false)

  async function copy() {
    try {
      await navigator.clipboard.writeText(value)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch {
      // Copying can be refused, for example on a page without permission. The number is still on screen.
    }
  }

  return (
    <Button variant="ghost" size="sm" onClick={copy} aria-label={label}>
      {copied ? <Check aria-hidden /> : <Copy aria-hidden />}
      <span aria-live="polite">{copied ? 'Copied' : 'Copy'}</span>
    </Button>
  )
}
