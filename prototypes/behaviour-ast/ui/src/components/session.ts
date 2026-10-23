import { createContext, useContext } from 'react'
import type { SessionState } from '../api/types'

/**
 * What `SignIn` learned from the server, handed down so the account area does
 * not ask a second time. `null` outside a `SignIn` — nothing to show there.
 */
export const SessionContext = createContext<SessionState | null>(null)

export function useSession(): SessionState | null {
  return useContext(SessionContext)
}
