import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from '@/components/ui/button'

interface Props {
  page: number
  totalPages: number
  totalCount: number
  noun: [string, string]
  onPage: (page: number) => void
}

// Previous and Next with where you are, for every list of the back office.
export function Pager({ page, totalPages, totalCount, noun, onPage }: Props) {
  return (
    <nav aria-label="Pages" className="mt-6 flex flex-wrap items-center justify-between gap-4 text-sm">
      <p className="text-muted-foreground">
        Page {page} of {Math.max(1, totalPages)}. {totalCount} {totalCount === 1 ? noun[0] : noun[1]}.
      </p>
      <div className="flex gap-2">
        <Button variant="outline" disabled={page <= 1} onClick={() => onPage(page - 1)}>
          <ChevronLeft aria-hidden />
          Previous
        </Button>
        <Button variant="outline" disabled={page >= totalPages} onClick={() => onPage(page + 1)}>
          Next
          <ChevronRight aria-hidden />
        </Button>
      </div>
    </nav>
  )
}
