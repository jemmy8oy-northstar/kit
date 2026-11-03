import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Projects from '../pages/Projects'
import Settings from '../pages/Settings'
import { SessionContext } from './session'
import type { SessionState } from '../api/types'

const KEY = 'kit.tour.seen'
const signedIn: SessionState = { required: true, signedIn: true, accounts: true, user: 'sam', operator: true }

function stubFetch() {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => ({ ok: true, status: 200, statusText: 'OK', json: async () => ({ projects: [] }) })),
  )
}

function renderPage(page: 'projects' | 'settings', session: SessionState | null) {
  return render(
    <SessionContext.Provider value={session}>
      <MemoryRouter>{page === 'projects' ? <Projects /> : <Settings />}</MemoryRouter>
    </SessionContext.Provider>,
  )
}

const dialog = () => screen.queryByRole('dialog')

beforeEach(() => {
  window.localStorage.clear()
  stubFetch()
})
afterEach(() => {
  vi.unstubAllGlobals()
  window.localStorage.clear()
})

describe('BEH-TOUR-1: a new user is shown around Kit by an onboarding tour', () => {
  it('opens on the first signed-in visit to Projects, and not again after Skip tour', () => {
    const first = renderPage('projects', signedIn)
    const tour = screen.getByRole('dialog', { name: 'Projects' })
    expect(tour).toHaveAttribute('aria-modal', 'true')
    expect(screen.getByText('Step 1 of 6')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Skip tour' }))
    expect(dialog()).not.toBeInTheDocument()
    expect(window.localStorage.getItem(KEY)).toBe('1')

    first.unmount()
    renderPage('projects', signedIn)
    expect(dialog()).not.toBeInTheDocument()
  })

  it('walks the steps with Next and Back, and Done marks it seen', () => {
    renderPage('projects', signedIn)
    expect(screen.queryByRole('button', { name: 'Back' })).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    expect(screen.getByRole('dialog', { name: 'The Board and the Queue' })).toBeInTheDocument()
    expect(screen.getByText('Step 2 of 6')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Back' }))
    expect(screen.getByRole('dialog', { name: 'Projects' })).toBeInTheDocument()

    for (let i = 0; i < 5; i++) fireEvent.click(screen.getByRole('button', { name: 'Next' }))
    expect(screen.getByRole('dialog', { name: 'Settings' })).toBeInTheDocument()
    expect(screen.getByText('Step 6 of 6')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Next' })).not.toBeInTheDocument()
    expect(window.localStorage.getItem(KEY)).toBeNull()

    fireEvent.click(screen.getByRole('button', { name: 'Done' }))
    expect(dialog()).not.toBeInTheDocument()
    expect(window.localStorage.getItem(KEY)).toBe('1')
  })

  it('moves focus into the dialog when it opens', () => {
    renderPage('projects', signedIn)
    expect(screen.getByRole('dialog')).toContainElement(document.activeElement as HTMLElement)
  })

  it('is not shown without a session, or when the Kit has no accounts', () => {
    const none = renderPage('projects', null)
    expect(dialog()).not.toBeInTheDocument()
    none.unmount()
    renderPage('projects', { required: true, signedIn: true })
    expect(dialog()).not.toBeInTheDocument()
  })

  it("is opened again by Settings' Replay the tour, even when already seen", () => {
    window.localStorage.setItem(KEY, '1')
    renderPage('settings', signedIn)
    expect(dialog()).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Replay the tour' }))
    expect(screen.getByRole('dialog', { name: 'Projects' })).toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Skip tour' }))
    expect(dialog()).not.toBeInTheDocument()
  })

  it('treats Escape as skipping the tour', () => {
    renderPage('projects', signedIn)
    fireEvent.keyDown(document, { key: 'Escape' })
    expect(dialog()).not.toBeInTheDocument()
    expect(window.localStorage.getItem(KEY)).toBe('1')
  })

  it('still opens, and does not throw, when the browser refuses storage', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('private mode') })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('private mode') })
    renderPage('projects', signedIn)
    expect(screen.getByRole('dialog')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('button', { name: 'Skip tour' }))
    expect(dialog()).not.toBeInTheDocument()
    vi.restoreAllMocks()
  })
})
