import { createContext, useContext } from 'react'
import type { FeatureSwitches } from '../api/types'

/**
 * BEH-FLAGS-2: which of Kit's features own a page or a navigation entry. A small explicit table,
 * not derived: the corpus names features, not routes, and a feature with no row here has a switch
 * that does nothing visible yet. Settings is deliberately absent (switching it off for testers
 * would take away their sign-out), and so are the Projects list and the nav-wide features
 * (FEAT-NAV, FEAT-PROJECTS), which are the shell the switches live in.
 */
export const FEATURE_PAGES: Record<string, { path: string; label: string }> = {
  'FEAT-BOARD': { path: '/board', label: 'Board' },
  'FEAT-STAGES': { path: '/stages', label: 'Stages' },
  'FEAT-EPICS': { path: '/epics', label: 'Epics' },
  'FEAT-INBOX': { path: '/inbox', label: 'Inbox' },
  'FEAT-HEALTH': { path: '/health', label: 'Health' },
  'FEAT-ACTIVITY': { path: '/activity', label: 'Activity' },
  'FEAT-NOTIFICATIONS': { path: '/notifications', label: 'Notifications' },
}

export interface Flags {
  /** What the server last said; null until it has, or when it could not be reached. */
  switches: FeatureSwitches | null
  /** Whether the feature is on for this session's group. Unknown is on: this hides UI, it does not guard anything. */
  isOn: (feature: string) => boolean
  reload: () => void
}

export const FlagsContext = createContext<Flags>({ switches: null, isOn: () => true, reload: () => {} })

export function useFlags(): Flags {
  return useContext(FlagsContext)
}
