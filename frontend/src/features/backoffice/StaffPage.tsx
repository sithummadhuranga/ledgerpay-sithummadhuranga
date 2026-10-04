import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { ErrorState } from '@/components/ErrorState'
import { PageHeader } from '@/components/PageHeader'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { setRestriction } from '@/lib/api/backoffice'
import type { StaffMember } from '@/lib/api/types'
import { describeError } from '@/lib/errors'
import { formatDateTime } from '@/lib/format'
import { backOfficeKeys, useStaff } from './queries'

// The operators and admins, and the one thing an admin may do to an operator: restrict the account or lift it.
export function StaffPage() {
  const staff = useStaff()

  return (
    <>
      <PageHeader
        title="Staff"
        intro="A restricted operator cannot sign in, every session of theirs ends at once and the token they hold stops working. The reason stays in the audit log."
      />
      {staff.isPending ? (
        <div role="status" aria-busy="true" aria-label="Loading the staff" className="grid gap-2">
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-16 w-full" />
        </div>
      ) : staff.isError ? (
        <ErrorState message={`We could not load the staff. ${describeError(staff.error)}`} onRetry={() => void staff.refetch()} />
      ) : (
        <ul aria-label="Staff" className="divide-y border-y">
          {staff.data.map((member) => (
            <MemberRow key={member.email} member={member} />
          ))}
        </ul>
      )}
    </>
  )
}

function MemberRow({ member }: { member: StaffMember }) {
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [reason, setReason] = useState('')
  const [problem, setProblem] = useState<string | null>(null)
  const tooShort = reason.trim().length < 3

  const change = useMutation({
    mutationFn: () => setRestriction({ email: member.email, restricted: !member.restricted, reason: reason.trim() }),
    onSuccess: async () => {
      setProblem(null)
      setOpen(false)
      setReason('')
      await queryClient.invalidateQueries({ queryKey: backOfficeKeys.staff })
    },
    onError: async (error) => {
      setProblem(describeError(error))
      await queryClient.invalidateQueries({ queryKey: backOfficeKeys.staff })
    },
  })

  return (
    <li className="py-4">
      <div className="flex flex-wrap items-center justify-between gap-x-6 gap-y-3">
        <div className="min-w-0">
          <p className="font-medium">
            {member.fullName} <span className="ml-1 rounded-sm border px-1.5 py-0.5 text-xs font-normal text-muted-foreground">{member.role}</span>
          </p>
          <p className="truncate text-sm text-muted-foreground">{member.email}</p>
          {member.restricted ? (
            <p className="mt-1 text-sm text-destructive">
              Restricted {member.restrictedAt ? formatDateTime(member.restrictedAt) : ''}
              {member.restrictedBy ? ` by ${member.restrictedBy}` : ''}. {member.restrictedReason}
            </p>
          ) : null}
        </div>
        {member.role === 'Operator' ? (
          <Button
            variant={member.restricted ? 'outline' : 'destructive'}
            size="sm"
            aria-expanded={open}
            aria-label={`${member.restricted ? 'Lift the restriction on' : 'Restrict'} ${member.fullName}`}
            onClick={() => setOpen((current) => !current)}
          >
            {member.restricted ? 'Lift restriction' : 'Restrict'}
          </Button>
        ) : (
          <span className="text-sm text-muted-foreground">Cannot be restricted</span>
        )}
      </div>

      {open ? (
        <form
          className="mt-4 grid max-w-xl gap-3"
          onSubmit={(event) => {
            event.preventDefault()
            if (!tooShort) change.mutate()
          }}
        >
          {problem ? (
            <Alert variant="destructive">
              <AlertDescription>{problem}</AlertDescription>
            </Alert>
          ) : null}
          <div className="grid gap-1.5">
            <Label htmlFor={`reason-${member.email}`}>Reason (3 to 250 characters)</Label>
            <Input id={`reason-${member.email}`} value={reason} maxLength={250} autoComplete="off" onChange={(event) => setReason(event.target.value)} />
          </div>
          <Button type="submit" variant={member.restricted ? 'default' : 'destructive'} disabled={tooShort || change.isPending} className="sm:justify-self-start">
            {change.isPending ? 'Saving' : member.restricted ? 'Confirm and lift the restriction' : 'Confirm and restrict'}
          </Button>
        </form>
      ) : null}
    </li>
  )
}
