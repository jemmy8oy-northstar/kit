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
