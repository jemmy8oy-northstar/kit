/**
 * The installed iPhone app runs without Safari's chrome, so iOS gives it no
 * pull-to-refresh of its own. In a browser tab Safari already has one, and
 * drawing a second would fight it (BEH-LOADING-2).
 */
export function isInstalledApp(): boolean {
  const nav = navigator as Navigator & { standalone?: boolean }
  if (nav.standalone === true) return true
  return typeof window.matchMedia === 'function' && window.matchMedia('(display-mode: standalone)').matches
}
