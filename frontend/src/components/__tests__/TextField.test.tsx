import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { TextField } from '../TextField'

describe('a text field', () => {
  it('has a visible label that names the input', () => {
    render(<TextField id="email" label="Email" />)

    expect(screen.getByLabelText('Email')).toBeInTheDocument()
  })

  it('links its error to the input, so a screen reader reads it with the field', () => {
    render(<TextField id="email" label="Email" error="Enter a valid email address." />)

    const input = screen.getByLabelText('Email')
    const error = screen.getByText('Enter a valid email address.')
    expect(input).toHaveAttribute('aria-invalid', 'true')
    expect(input.getAttribute('aria-describedby')).toContain(error.id)
  })

  it('puts the error in a live region, so it is announced when it appears', () => {
    render(<TextField id="email" label="Email" error="Enter a valid email address." />)

    expect(screen.getByText('Enter a valid email address.').closest('[aria-live]')).toHaveAttribute('aria-live', 'polite')
  })

  it('links its hint too, and says nothing about errors when there is none', () => {
    render(<TextField id="phone" label="Mobile number" hint="Start with +947, then 8 digits." />)

    const input = screen.getByLabelText('Mobile number')
    expect(input.getAttribute('aria-describedby')).toContain(screen.getByText('Start with +947, then 8 digits.').id)
    expect(input).not.toHaveAttribute('aria-invalid')
  })
})
