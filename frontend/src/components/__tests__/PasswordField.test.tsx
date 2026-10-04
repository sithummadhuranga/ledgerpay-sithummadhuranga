import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { PasswordField } from '../PasswordField'

describe('a password field', () => {
  it('hides what is typed until the user asks to see it', async () => {
    const user = userEvent.setup()
    render(<PasswordField id="password" label="Password" defaultValue="Kandy-Lake-2026!" />)
    const input = screen.getByLabelText('Password')
    expect(input).toHaveAttribute('type', 'password')

    await user.click(screen.getByRole('button', { name: 'Show password' }))

    expect(input).toHaveAttribute('type', 'text')
    expect(screen.getByRole('button', { name: 'Hide password' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('hides it again on the next press, and says so to a screen reader', async () => {
    const user = userEvent.setup()
    render(<PasswordField id="password" label="Password" />)

    await user.click(screen.getByRole('button', { name: 'Show password' }))
    await user.click(screen.getByRole('button', { name: 'Hide password' }))

    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'password')
    expect(screen.getByRole('button', { name: 'Show password' })).toHaveAttribute('aria-pressed', 'false')
  })

  it('names which password the button shows when there are two on a page', () => {
    render(
      <>
        <PasswordField id="password" label="Password" />
        <PasswordField id="confirm" label="Confirm password" />
      </>,
    )

    expect(screen.getByRole('button', { name: 'Show password' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Show confirm password' })).toBeInTheDocument()
  })

  it('does not submit a form when the show button is pressed', async () => {
    const user = userEvent.setup()
    let submitted = false
    render(
      <form onSubmit={(event) => { event.preventDefault(); submitted = true }}>
        <PasswordField id="password" label="Password" />
      </form>,
    )

    await user.click(screen.getByRole('button', { name: 'Show password' }))

    expect(submitted).toBe(false)
  })

  it('links its error to the input', () => {
    render(<PasswordField id="password" label="Password" error="The password does not meet all the rules." />)

    const input = screen.getByLabelText('Password')
    expect(input).toHaveAttribute('aria-invalid', 'true')
    expect(input.getAttribute('aria-describedby')).toContain(screen.getByText('The password does not meet all the rules.').id)
  })
})
