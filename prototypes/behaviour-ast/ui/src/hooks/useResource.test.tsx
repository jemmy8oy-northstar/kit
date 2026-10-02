import { describe, expect, it } from 'vitest'
import { act, render, screen } from '@testing-library/react'
import { useResource } from './useResource'

/**
 * The three rules in `useLoad` that nothing asserted.
 *
 * `BehaviourPage.test.tsx` covers the fourth — the reload that must not blank
 * the page — and its comment explains why its stub is delayed: a promise that
 * resolves inside the same React batch never lets `loading` commit, so the bug
 * is invisible. The same is true of everything here, which is why every stub in
 * this file is a DEFERRED the test resolves by hand.
 *
 * These are races, so the ordering is the test. Resolving in the order the
 * requests were made proves nothing: the whole question is what happens when
 * they come back out of order.
 */

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

function Harness({ app, load }: { app: string; load: (app: string) => Promise<string> }) {
  const resource = useResource(() => load(app), [app])
  if (resource.state === 'loading') return <p>loading…</p>
  if (resource.state === 'error') return <p role="alert">{resource.message}</p>
  return <p role="status">{resource.value}</p>
}

describe('useResource', () => {
  it('shows loading when you navigate to a DIFFERENT resource, not the previous one\'s value', async () => {
    // The distinction `identity` exists to draw. A reload of the same resource
    // keeps the old value on screen (that is what stops a write blanking the
    // page); a navigation to another one must NOT, or the previous project's
    // behaviours sit under the new project's heading and look like its own.
    const a = deferred<string>()
    const b = deferred<string>()
    const loads: Record<string, Promise<string>> = { a: a.promise, b: b.promise }
    const load = (app: string) => loads[app]

    const { rerender } = render(<Harness app="a" load={load} />)
    await act(async () => {
      a.resolve('behaviours of A')
    })
    expect(screen.getByRole('status')).toHaveTextContent('behaviours of A')

    rerender(<Harness app="b" load={load} />)

    // B has not answered yet. The only honest thing to show is that we are
    // waiting — showing A's behaviours here is the lie.
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
    expect(screen.getByText('loading…')).toBeInTheDocument()

    await act(async () => {
      b.resolve('behaviours of B')
    })
    expect(screen.getByRole('status')).toHaveTextContent('behaviours of B')
  })

  it('a request that comes back AFTER you navigated away does not overwrite the new page', async () => {
    // The stale-response race, and the reason the effect returns a cleanup that
    // flips `live`. Without it a slow project you already left lands on top of
    // the one you are looking at — silently, and looking exactly like real data.
    const slow = deferred<string>()
    const fast = deferred<string>()
    const loads: Record<string, Promise<string>> = { slow: slow.promise, fast: fast.promise }
    const load = (app: string) => loads[app]

    const { rerender } = render(<Harness app="slow" load={load} />)
    rerender(<Harness app="fast" load={load} />)

    await act(async () => {
      fast.resolve('behaviours of FAST')
    })
    expect(screen.getByRole('status')).toHaveTextContent('behaviours of FAST')

    // The abandoned request now answers. It must be dropped on the floor.
    await act(async () => {
      slow.resolve('behaviours of SLOW')
    })
    expect(screen.getByRole('status')).toHaveTextContent('behaviours of FAST')
    expect(screen.queryByText(/SLOW/)).not.toBeInTheDocument()
  })

  it('a FAILURE from a request you navigated away from does not replace the new page with its error', async () => {
    // The same race down the catch path. An error is louder than stale data —
    // it swaps the whole page for "Could not read this" about a project the
    // reader is no longer on.
    const slow = deferred<string>()
    const fast = deferred<string>()
    const loads: Record<string, Promise<string>> = { slow: slow.promise, fast: fast.promise }
    const load = (app: string) => loads[app]

    const { rerender } = render(<Harness app="slow" load={load} />)
    rerender(<Harness app="fast" load={load} />)

    await act(async () => {
      fast.resolve('behaviours of FAST')
    })

    await act(async () => {
      slow.reject(new Error('slow.beh:3 — expected a verb'))
    })
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByRole('status')).toHaveTextContent('behaviours of FAST')
  })

  it('CONTROL: a failure of the resource you are actually on DOES reach the screen', async () => {
    // Otherwise the three assertions above are satisfied by a hook that never
    // reports an error at all.
    const only = deferred<string>()
    const load = () => only.promise

    render(<Harness app="only" load={load} />)
    await act(async () => {
      only.reject(new Error('only.beh:3 — expected a verb'))
    })

    expect(screen.getByRole('alert')).toHaveTextContent('expected a verb')
  })
})
