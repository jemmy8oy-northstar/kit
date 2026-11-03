/** BEH-TOUR-1: remembered in this browser only; "1" once the tour was skipped or finished. */
const KEY = 'kit.tour.seen'

/** Private mode can refuse storage: then the tour simply counts as not seen, and nothing throws. */
export function tourSeen(): boolean {
  try {
    return window.localStorage.getItem(KEY) === '1'
  } catch {
    return false
  }
}

export function markTourSeen(): void {
  try {
    window.localStorage.setItem(KEY, '1')
  } catch {
    // Not remembered; the tour may show again next visit.
  }
}
