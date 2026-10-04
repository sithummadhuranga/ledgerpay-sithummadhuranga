import { Receipt } from '@/components/Receipt'

// The confirm step of a send, drawn on a phone so a visitor sees the product and not a description of it. It is
// marked as an example because the names and numbers are not a real account.
export function PhonePreview() {
  return (
    <figure className="relative mx-auto w-fit" aria-label="Example of the confirm screen">
      <div className="w-[17.5rem] rounded-[2.4rem] border-[9px] border-foreground bg-background sm:w-[19rem]">
        <div className="mx-auto mt-2 h-1.5 w-16 rounded-full bg-foreground/15" aria-hidden />
        <div className="px-5 pt-5 pb-6">
          <p className="mb-3 text-xs text-muted-foreground">Example</p>
          <h2 className="font-heading text-2xl font-semibold">Check and confirm</h2>
          <p className="mt-1 text-sm text-muted-foreground">Nothing has been sent yet.</p>
          <Receipt
            className="mt-5 text-sm"
            rows={[
              { label: 'To', value: 'N*** P***' },
              { label: 'Amount', value: 'LKR 5,000.00', figures: true },
              { label: 'Fee', value: 'LKR 25.00', figures: true },
              { label: 'Total', value: 'LKR 5,025.00', figures: true, strong: true },
              { label: 'Note', value: 'Rent share' },
            ]}
          />
          <div aria-hidden className="mt-5 rounded-md bg-primary px-4 py-3 text-center text-sm font-medium text-primary-foreground">
            Confirm and send LKR 5,025.00
          </div>
        </div>
      </div>
      <div className="absolute -bottom-12 -left-14 hidden w-60 rounded-lg border bg-card px-4 py-3 text-sm sm:block">
        <p className="font-medium">
          <span className="num">+LKR 750.50</span> received
        </p>
        <p className="text-xs text-muted-foreground">From T*** F*** · 4 Oct</p>
      </div>
    </figure>
  )
}
