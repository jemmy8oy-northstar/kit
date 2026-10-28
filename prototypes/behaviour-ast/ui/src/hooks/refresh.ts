import { useSyncExternalStore } from 'react'

/**
 * BEH-LOADING-2: one signal that every resource on screen re-reads on.
 *
 * Pull-to-reload has to re-read "that page's data", and a page is made of
 * several resources (the board, the bell, the notes panel), each with its own
 * `useReloadableResource`. Rather than teach every page to forward a reload,
 * every resource subscribes to this epoch: bumping it re-runs all of them as a
 * *reload* (same identity), so the page keeps showing its data while the new
 * read is in flight instead of blanking to a spinner.
 *
 * `inFlight` counts reads still running, so the pull indicator can stay up
 * until what it asked for has actually arrived.
 */
let epoch = 0
let inFlight = 0
const listeners = new Set<() => void>()

function emit() {
  for (const l of listeners) l()
}

function subscribe(l: () => void) {
  listeners.add(l)
  return () => {
    listeners.delete(l)
  }
}

/** Re-read every resource currently mounted. */
export function refreshAll() {
  epoch += 1
  emit()
}

export function useRefreshEpoch(): number {
  return useSyncExternalStore(subscribe, () => epoch)
}

/** True while any load is running. */
export function useLoading(): boolean {
  return useSyncExternalStore(subscribe, () => inFlight > 0)
}

/** Called by `useLoad` around every read; returns the matching `end`. */
export function beginLoad(): () => void {
  inFlight += 1
  emit()
  let ended = false
  return () => {
    if (ended) return
    ended = true
    inFlight -= 1
    emit()
  }
}
