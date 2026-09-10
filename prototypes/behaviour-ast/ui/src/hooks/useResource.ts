import { useEffect, useState } from 'react'
import { ApiError } from '../api/client'

/**
 * Three states, and they are deliberately not two. "Loading" and "loaded but
 * empty" render the same thing if you collapse them, which is the same class of
 * mistake as reporting unavailable coverage as zero.
 */
export type Resource<T> =
  | { state: 'loading' }
  | { state: 'error'; message: string }
  | { state: 'ready'; value: T }

export function useResource<T>(load: () => Promise<T>, deps: unknown[]): Resource<T> {
  return useReloadableResource(load, deps).resource
}

/**
 * The same three states, plus a way to ask for them again.
 *
 * A write is only half of his loop; the other half is seeing the *generated
 * output* change because of it. Kit derives the test from the corpus on every
 * read, so re-fetching after a write is what makes the edit visible — without
 * it the page would show his new step next to the test that predates it, which
 * is worse than showing nothing, because it looks like the generator ignored
 * him.
 */
export function useReloadableResource<T>(
  load: () => Promise<T>,
  deps: unknown[],
): { resource: Resource<T>; reload: () => void } {
  const [nonce, setNonce] = useState(0)
  const resource = useLoad(load, [...deps, nonce])
  return { resource, reload: () => setNonce((n) => n + 1) }
}

function useLoad<T>(load: () => Promise<T>, deps: unknown[]): Resource<T> {
  const [resource, setResource] = useState<Resource<T>>({ state: 'loading' })

  useEffect(() => {
    let live = true
    setResource({ state: 'loading' })

    load()
      .then((value) => {
        if (live) setResource({ state: 'ready', value })
      })
      .catch((e: unknown) => {
        if (!live) return
        const message =
          e instanceof ApiError || e instanceof Error ? e.message : String(e)
        setResource({ state: 'error', message })
      })

    // Navigating away mid-request must not write the old app's data into the
    // new app's page.
    return () => {
      live = false
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps)

  return resource
}
