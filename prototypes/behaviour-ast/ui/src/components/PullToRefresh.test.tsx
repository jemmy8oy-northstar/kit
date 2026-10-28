import { afterEach, describe, expect, it, vi } from 'vitest'
import { act, fireEvent, render, screen } from '@testing-library/react'
import PullToRefresh, { PULL_THRESHOLD } from './PullToRefresh'
import { isInstalledApp } from './installedApp'
import { useResource } from '../hooks/useResource'

function Page({ load }: { load: () => Promise<string> }) {
  const r = useResource(load, ['page'])
  return <p>{r.state === 'ready' ? r.value : r.state}</p>
}

function pullBy(dy: number) {
  fireEvent.touchStart(window, { touches: [{ clientY: 100 }] })
  // The indicator moves at half the finger's speed, so the finger travels twice the distance.
  fireEvent.touchMove(window, { touches: [{ clientY: 100 + dy * 2 }] })
  fireEvent.touchEnd(window, { touches: [] })
}

/** BEH-LOADING-2: on the installed iPhone app, pulling the page down reloads it. */
describe('PullToRefresh (BEH-LOADING-2)', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    window.scrollY = 0
  })

  it("a pull past the threshold re-reads the page's data, keeping the page on screen while it does", async () => {
    let n = 0
    let release: (v: string) => void = () => {}
    const load = vi.fn(() => {
      n += 1
      if (n === 1) return Promise.resolve('first read')
      return new Promise<string>((r) => { release = r })
    })
    render(<><PullToRefresh enabled /><Page load={load} /></>)
    expect(await screen.findByText('first read')).toBeInTheDocument()

    act(() => pullBy(PULL_THRESHOLD))
    expect(load).toHaveBeenCalledTimes(2)
    expect(screen.getByRole('status')).toHaveTextContent('Reloading…')
    // A reload, not a navigation: the old data stays, no blank spinner page.
    expect(screen.getByText('first read')).toBeInTheDocument()

    await act(async () => { release('second read') })
    expect(await screen.findByText('second read')).toBeInTheDocument()
    await vi.waitFor(() => expect(screen.queryByRole('status')).not.toBeInTheDocument())
  })

  it('letting go short of the threshold reloads nothing', async () => {
    const load = vi.fn(() => Promise.resolve('data'))
    render(<><PullToRefresh enabled /><Page load={load} /></>)
    await screen.findByText('data')
    act(() => pullBy(PULL_THRESHOLD - 1))
    expect(load).toHaveBeenCalledTimes(1)
  })

  it('a drag that starts below the top of the page is a scroll, and reloads nothing', async () => {
    const load = vi.fn(() => Promise.resolve('data'))
    render(<><PullToRefresh enabled /><Page load={load} /></>)
    await screen.findByText('data')
    window.scrollY = 300
    act(() => pullBy(PULL_THRESHOLD * 2))
    expect(load).toHaveBeenCalledTimes(1)
  })

  it('says how far along the pull is', () => {
    render(<PullToRefresh enabled />)
    fireEvent.touchStart(window, { touches: [{ clientY: 100 }] })
    fireEvent.touchMove(window, { touches: [{ clientY: 120 }] })
    expect(screen.getByRole('status')).toHaveTextContent('Pull to reload')
    fireEvent.touchMove(window, { touches: [{ clientY: 100 + PULL_THRESHOLD * 2 }] })
    expect(screen.getByRole('status')).toHaveTextContent('Release to reload')
  })

  it('in a browser tab, where Safari has its own, it draws nothing and listens for nothing', async () => {
    const load = vi.fn(() => Promise.resolve('data'))
    render(<><PullToRefresh enabled={false} /><Page load={load} /></>)
    await screen.findByText('data')
    act(() => pullBy(PULL_THRESHOLD * 2))
    expect(load).toHaveBeenCalledTimes(1)
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('knows the installed app from a browser tab', () => {
    vi.stubGlobal('matchMedia', (q: string) => ({ matches: q === '(display-mode: standalone)' }))
    expect(isInstalledApp()).toBe(true)
    vi.stubGlobal('matchMedia', () => ({ matches: false }))
    expect(isInstalledApp()).toBe(false)
  })
})
