/** The run's own instant, in the reader's locale; an unparseable stamp is shown as it came. */
export function when(iso: string): string {
  const d = new Date(iso)
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' })
}

/** How long it ran, or "running" while it has no end. */
export function took(r: { started: string; ended?: string | null }): string {
  if (!r.ended) return 'running'
  const s = Math.max(0, Math.round((Date.parse(r.ended) - Date.parse(r.started)) / 1000))
  return s < 60 ? `${s}s` : `${Math.floor(s / 60)}m ${s % 60}s`
}

/**
 * A run's cost in words (BEH-UI-8). A run with no recorded cost is unknown, never zero, so a run recorded at
 * $0.00 and one with nothing recorded read differently.
 */
export function costText(usd: number | null): string {
  return usd === null ? 'cost unknown' : `cost $${usd.toFixed(2)}`
}
