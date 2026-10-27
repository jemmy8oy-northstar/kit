import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { Link, MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { OpeningPage, OpeningPageSetting } from './OpeningPage'
import { readOpeningPage } from './openingPageStore'
import { GitHubReturn } from '../pages/Settings'

function Probe() {
  const { pathname, search } = useLocation()
  return <p data-testid="at">{pathname + search}</p>
}

function app(entry: string) {
  return (
    <MemoryRouter initialEntries={[entry]}>
      <GitHubReturn />
      <OpeningPage />
      <Link to="/">Projects</Link>
      <Routes><Route path="*" element={<Probe />} /></Routes>
    </MemoryRouter>
  )
}

beforeEach(() => { window.localStorage.clear() })
afterEach(() => { window.localStorage.clear() })

describe('Opening page (BEH-SETTINGS-2)', () => {
  it('opens on the page chosen in Settings, and remembers it in this browser', async () => {
    render(<MemoryRouter><OpeningPageSetting /></MemoryRouter>)
    fireEvent.change(screen.getByLabelText(/Kit opens on/), { target: { value: '/board' } })
    expect(screen.getByRole('status')).toHaveTextContent('Kit opens on Board')
    expect(readOpeningPage()).toBe('/board')

    render(app('/'))
    expect(await screen.findByText('/board')).toBeInTheDocument()
  })

  it('opens on Projects when nothing is chosen, or the choice is no longer a page', async () => {
    render(app('/'))
    expect(screen.getByTestId('at')).toHaveTextContent(/^\/$/)
    window.localStorage.setItem('kit.openingPage', '/gone')
    expect(readOpeningPage()).toBe('/')
  })

  it('never redirects a link to any other page', () => {
    window.localStorage.setItem('kit.openingPage', '/board')
    render(app('/activity'))
    expect(screen.getByTestId('at')).toHaveTextContent('/activity')
  })

  it('lets the Projects link reach Projects once Kit is open', async () => {
    window.localStorage.setItem('kit.openingPage', '/board')
    render(app('/'))
    expect(await screen.findByText('/board')).toBeInTheDocument()
    fireEvent.click(screen.getByRole('link', { name: 'Projects' }))
    expect(screen.getByTestId('at')).toHaveTextContent(/^\/$/)
  })

  it('lets a GitHub connect return to the root go on to Settings', async () => {
    window.localStorage.setItem('kit.openingPage', '/board')
    render(app('/?installation_id=42&setup_action=install'))
    expect(await screen.findByText('/settings?installation_id=42&setup_action=install')).toBeInTheDocument()
  })
})
