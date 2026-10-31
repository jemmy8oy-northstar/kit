import { useMemo } from 'react'
import { fetchFeatureSwitches } from '../api/client'
import { useReloadableResource } from '../hooks/useResource'
import { FlagsContext, useFlags, type Flags } from './flags'

/**
 * BEH-FLAGS-2: asks the server once which of Kit's features are off for this session's group and
 * hands the answer down. While it loads, or if it cannot be read, everything shows: this hides
 * UI per group, it is not a security boundary (the API is not refused; BEH-USERS-* decides what
 * an account may reach).
 */
export function FlagsProvider({ children }: { children: React.ReactNode }) {
  const { resource, reload } = useReloadableResource(fetchFeatureSwitches, [])
  const value = useMemo<Flags>(() => {
    const switches = resource.state === 'ready' ? resource.value : null
    const off = new Set(switches?.off ?? [])
    return { switches, isOn: (feature) => !off.has(feature), reload }
    // `reload` is a new function every render; the resource is what changes the answer.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [resource])
  return <FlagsContext.Provider value={value}>{children}</FlagsContext.Provider>
}

/** The short message in place of a page whose feature is off for this group. */
export function NotAvailable() {
  return (
    <>
      <h1>Not available</h1>
      <p className="muted">This part of Kit isn't switched on for you.</p>
    </>
  )
}

/** A page that is its feature's: shown when the feature is on for this group, else the message. */
export function FeatureGate({ feature, children }: { feature: string; children: React.ReactNode }) {
  return useFlags().isOn(feature) ? <>{children}</> : <NotAvailable />
}
