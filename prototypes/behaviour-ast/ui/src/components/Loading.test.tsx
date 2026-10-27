import { afterEach, describe, expect, it, vi } from 'vitest'
import { act, render, screen } from '@testing-library/react'
import Loading, { SLOW_MS } from './Loading'
import SignIn from './SignIn'

/** BEH-LOADING-1: a wait that looks like a wait, and says so when it goes on. */
describe('Loading (BEH-LOADING-1)', () => {
  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it("opens on Kit's name and a spinner while it asks the server who you are", () => {
    vi.stubGlobal('fetch', vi.fn(() => new Promise(() => {})))
    const { container } = render(<SignIn><p>the corpus</p></SignIn>)
    expect(screen.getByRole('status')).toHaveTextContent('Opening Kit…')
    expect(screen.getByText('Kit')).toHaveClass('brand')
    expect(container.querySelector('.spinner')).not.toBeNull()
    expect(screen.queryByText('the corpus')).not.toBeInTheDocument()
  })

  it('says it is still waiting once the server is slow, and not before', () => {
    vi.useFakeTimers()
    render(<Loading label="Loading…" />)
    act(() => { vi.advanceTimersByTime(SLOW_MS - 1) })
    expect(screen.queryByText(/Still waiting/)).not.toBeInTheDocument()
    act(() => { vi.advanceTimersByTime(1) })
    expect(screen.getByRole('status')).toHaveTextContent("Still waiting for Kit's server…")
  })
})
