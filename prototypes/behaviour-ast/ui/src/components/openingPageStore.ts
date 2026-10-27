import { IDEAS } from '../board'

/** BEH-SETTINGS-2: the navbar's top-level pages, any of which Kit can open on. Projects (`/`) is the default. */
export const OPENING_PAGES: readonly { path: string; label: string }[] = [
  { path: '/', label: 'Projects' },
  { path: '/board', label: 'Board' },
  { path: `/projects/${IDEAS}`, label: 'Ideas' },
  { path: '/activity', label: 'Activity' },
  { path: '/notifications', label: 'Notifications' },
]

const KEY = 'kit.openingPage'

/** The chosen page; Projects when nothing is stored, storage is unavailable, or the stored page no longer exists. */
export function readOpeningPage(): string {
  try {
    const stored = window.localStorage.getItem(KEY)
    return OPENING_PAGES.some((p) => p.path === stored) ? stored! : '/'
  } catch {
    return '/'
  }
}

export function saveOpeningPage(path: string) {
  try {
    window.localStorage.setItem(KEY, path)
  } catch {
    // A browser that refuses storage keeps the default; the select still shows the choice until reload.
  }
}
