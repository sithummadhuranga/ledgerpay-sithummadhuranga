// A made-up statement that shows what the product keeps for you. It is marked as an example because the
// names and numbers are not a real account.
const rows = [
  { date: '02 Oct', details: 'Bank top-up', note: 'Cash at branch', amount: '+48,250.00', fee: '', balance: '48,250.00' },
  { date: '03 Oct', details: 'Sent to K*** J***', note: 'Rent for October', amount: '-12,450.00', fee: '62.25', balance: '35,737.75' },
  { date: '04 Oct', details: 'Received from T*** F***', note: '', amount: '+750.50', fee: '', balance: '36,488.25' },
  { date: '04 Oct', details: 'Sent to T*** F***', note: 'Lunch and taxi', amount: '-2,300.00', fee: '11.50', balance: '34,176.75' },
]

export function StatementPreview() {
  return (
    <figure className="min-w-0 rounded-xl border border-panel-border bg-panel text-panel-foreground" aria-label="Example statement">
      <figcaption className="flex items-baseline justify-between gap-4 border-b border-panel-border px-5 py-4">
        <span>
          <span className="block text-xs tracking-wide text-panel-muted uppercase">Statement</span>
          <span className="font-heading text-lg">Nimali Perera</span>
        </span>
        <span className="rounded-sm border border-panel-border px-2 py-0.5 text-xs text-panel-muted">Example</span>
      </figcaption>
      <div className="overflow-x-auto">
        <table className="w-full min-w-[30rem] text-sm">
          <thead>
            <tr className="text-left text-xs text-panel-muted">
              <th scope="col" className="px-5 pt-4 pb-2 font-medium">
                Date
              </th>
              <th scope="col" className="pt-4 pb-2 font-medium">
                Details
              </th>
              <th scope="col" className="pt-4 pb-2 pl-3 text-right font-medium">
                Amount
              </th>
              <th scope="col" className="pt-4 pr-3 pb-2 pl-6 text-right font-medium">
                Fee
              </th>
              <th scope="col" className="px-5 pt-4 pb-2 text-right font-medium">
                Balance
              </th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.date + row.details} className="border-t border-panel-border/70 align-top">
                <td className="px-5 py-3 whitespace-nowrap text-panel-muted">{row.date}</td>
                <td className="py-3 pr-3">
                  {row.details}
                  {row.note ? <span className="block text-xs text-panel-muted">{row.note}</span> : null}
                </td>
                <td className="num py-3 pl-3 text-right whitespace-nowrap">{row.amount}</td>
                <td className="num py-3 pr-3 pl-6 text-right whitespace-nowrap text-panel-muted">{row.fee}</td>
                <td className="num px-5 py-3 text-right whitespace-nowrap">{row.balance}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="border-t border-panel-border px-5 py-4 text-xs text-panel-muted">
        Every transfer is posted twice: once out of one wallet and once into another. The balance on each line is the running total.
      </p>
    </figure>
  )
}
