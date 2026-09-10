import { useEffect, useRef, useState } from 'react'
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
  // `deps` is passed twice on purpose: once as part of what triggers a re-load,
  // and once as the identity of WHICH resource is being loaded. `useLoad` needs
  // both to tell "load the next project" from "load this project again".
  const resource = useLoad(load, [...deps, nonce], deps)
  return { resource, reload: () => setNonce((n) => n + 1) }
}

/**
 * ⚠️ **`identity` is what stops a reload blanking the page, and that is not
 * cosmetic — it is why the write confirmation exists at all.**
 *
 * Every write ends by calling `reload()`. Without this, the re-fetch put the
 * resource back to `loading`, `ResourceView` swapped the page for a spinner,
 * and every form on it UNMOUNTED — taking its `useWrite` state with it. The
 * form remounted `idle`, so the note saying *which file was written and that
 * Kit did not commit it* was destroyed by the very reload the write triggered.
 * Decision 2's guarantee is only a guarantee to him if he can watch it hold,
 * and he never could.
 *
 * 🔑 **The suite could not see this and stayed green through it.** A stubbed
 * `fetch` resolves inside the same batch, so React never commits the `loading`
 * render and nothing unmounts. It took running the real server in a real
 * browser — the failure needs a network round-trip to exist at all
 * ([[green-suite-over-a-mock]]). The test added beside this delays its stub by
 * a tick for exactly that reason.
 *
 * A *navigation* still shows `loading`: holding the previous project's
 * behaviours on screen under a new project's heading would be the read-side
 * version of the same lie.
 */
function useLoad<T>(load: () => Promise<T>, deps: unknown[], identity?: unknown[]): Resource<T> {
  const [resource, setResource] = useState<Resource<T>>({ state: 'loading' })
  const lastIdentity = useRef<string | null>(null)

  useEffect(() => {
    let live = true
    const key = identity === undefined ? null : JSON.stringify(identity)
    const isReload = key !== null && key === lastIdentity.current
    lastIdentity.current = key

    setResource((prev) => (isReload && prev.state === 'ready' ? prev : { state: 'loading' }))

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
