import { useEffect, useState } from 'react'

/** How long before a wait is worth explaining: past this, the server is slow, not the page. */
export const SLOW_MS = 5000

/**
 * BEH-LOADING-1: a wait that looks like a wait. A spinner with what is being
 * waited for, and, if it goes on past SLOW_MS, a second line saying so, so a
 * slow server never reads as a page that has stopped.
 *
 * `splash` is the first paint, before Kit knows whether you are signed in: Kit's
 * name in the middle of the screen, not a word stranded in the corner.
 */
export default function Loading({ label, splash = false }: { label: string; splash?: boolean }) {
  const [slow, setSlow] = useState(false)
  useEffect(() => {
    const t = setTimeout(() => setSlow(true), SLOW_MS)
    return () => clearTimeout(t)
  }, [])

  return (
    <div role="status" className={splash ? 'loading loading-splash' : 'loading'}>
      {splash && <span className="brand">Kit</span>}
      <span className="loading-line">
        <span className="spinner" aria-hidden="true" />
        <span>{label}</span>
      </span>
      {slow && <span className="muted">Still waiting for Kit&apos;s server…</span>}
    </div>
  )
}
