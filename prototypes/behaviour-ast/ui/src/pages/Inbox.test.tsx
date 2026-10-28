import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Inbox from './Inbox'
import { lowered } from '../inbox'
import type { Behaviour } from '../api/types'

function beh(id: string, over: Partial<Behaviour> = {}): Behaviour {
  return {
    id,
    title: `title of ${id}`,
    actor: 'engineer',
    steps: [],
    open: [],
    filled: [],
    source: { origin: 'defined', ref: null },
    review: { state: 'approved', note: null },
    asks: null,
    pending: false,
    layer: 'ui',
    at: 'x.beh:1',
    in: [],
    ...over,
  }
}

const inferred = { origin: 'inferred', ref: null }
const proposed = { source: inferred, review: { state: 'unreviewed', note: null } }

// alpha: A-PROP and A-ASK and A-NEEDS and A-PAUSED are his; A-QUEUE is the agent's; A-DONE is nobody's.
const details: Record<string, Behaviour[]> = {
  alpha: [
    beh('A-PROP', { ...proposed, priority: 3 }),
    beh('A-ASK', { review: { state: 'requested', note: 'Which colour?' }, priority: 1 }),
    beh('A-NEEDS', { pending: true, needsContext: 'Add the API key', priority: 2 }),
    beh('A-PAUSED', { pending: true, paused: true, priority: 2 }),
    beh('A-QUEUE', { pending: true, priority: 1 }),
    beh('A-DONE'),
  ],
  beta: [beh('B-PROP', { ...proposed, priority: 1 })],
}

/** Every POST the page made, as [path, body]. */
let writes: [string, unknown][] = []
let refuse = false

function mockApi(projects: Record<string, Behaviour[]> = details) {
  writes = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string, init?: { method?: string; body?: string }) => {
      const ok = { ok: true, status: 200, statusText: 'OK' }
      if (init?.method === 'POST') {
        if (refuse) return { ok: false, status: 409, statusText: 'Conflict', json: async () => ({ error: 'x', reason: 'the corpus refused that' }) }
        writes.push([path, JSON.parse(init.body ?? 'null')])
        return { ...ok, json: async () => ({ ok: true }) }
      }
      const rest = decodeURIComponent(path.split('/api/projects/')[1] ?? '')
      if (path.endsWith('/api/session')) return { ...ok, json: async () => ({ required: false, signedIn: true }) }
      if (rest.endsWith('/history')) return { ...ok, json: async () => ({ history: 'none', reason: 'no git', completed: [], added: [] }) }
      if (rest.endsWith('/notes')) return { ...ok, json: async () => ({ notes: [] }) }
      if (rest.endsWith('/schedule')) return { ...ok, json: async () => ({ scheduled: [], backlog: [] }) }
      return { ...ok, json: async () => (rest ? { app: rest, behaviours: projects[rest] } : { projects: Object.keys(projects).map((app) => ({ app })) }) }
    }),
  )
}

async function open() {
  render(
    <MemoryRouter>
      <Inbox />
    </MemoryRouter>,
  )
  await screen.findByTestId('inbox-position')
}

/** The id of the card on screen. */
const current = () => document.querySelector('.id')?.textContent ?? null

beforeEach(() => {
  window.localStorage.clear()
  refuse = false
})
afterEach(() => vi.unstubAllGlobals())

describe('Inbox', () => {
  it('queues only the cards that are his, by priority, across projects (BEH-INBOX-1)', async () => {
    mockApi()
    await open()

    expect(screen.getByTestId('inbox-position')).toHaveTextContent('Card 1 of 5')
    const seen: (string | null)[] = []
    for (let i = 0; i < 5; i++) {
      seen.push(current())
      fireEvent.click(screen.getByRole('button', { name: 'Approve' }))
      await waitFor(() => expect(current() !== seen[i]).toBe(true))
    }
    // The agent's card and the finished one are not in it.
    expect(seen).toEqual(['A-ASK', 'B-PROP', 'A-NEEDS', 'A-PAUSED', 'A-PROP'])
  })

  it('shows the question, or the needs-context line, on the card (BEH-INBOX-1)', async () => {
    mockApi()
    await open()

    expect(screen.getByTestId('inbox-question')).toHaveTextContent('Which colour?')
  })

  it('puts cards he has not opened before the ones he has, in this browser (BEH-INBOX-1)', async () => {
    window.localStorage.setItem('kit.inboxSeen', JSON.stringify(['alpha/A-ASK']))
    mockApi()
    await open()

    // A-ASK is P1 but already opened, so B-PROP leads, and A-ASK comes after every unopened card.
    expect(current()).toBe('B-PROP')
    expect(JSON.parse(window.localStorage.getItem('kit.inboxSeen')!)).toContain('beta/B-PROP')
  })

  it('says so plainly when nothing is waiting on him (BEH-INBOX-1)', async () => {
    mockApi({ alpha: [beh('A-QUEUE', { pending: true }), beh('A-DONE')] })
    render(
      <MemoryRouter>
        <Inbox />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Nothing is waiting on you.')).toBeInTheDocument()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('says so when the last card is answered (BEH-INBOX-1)', async () => {
    mockApi({ alpha: [beh('A-PROP', proposed)] })
    await open()

    fireEvent.click(screen.getByRole('button', { name: 'Approve' }))

    expect(await screen.findByText(/That is all of them/)).toBeInTheDocument()
  })

  it('approve writes the review the card page writes, then moves on (BEH-INBOX-1)', async () => {
    mockApi()
    await open()

    fireEvent.click(screen.getByRole('button', { name: 'Approve' }))

    await waitFor(() => expect(current()).toBe('B-PROP'))
    expect(writes).toEqual([['/api/projects/alpha/behaviours/A-ASK/review', { state: 'approved', note: null }]])
  })

  it('skip moves on without writing anything (BEH-INBOX-1)', async () => {
    mockApi()
    await open()

    fireEvent.click(screen.getByRole('button', { name: 'Skip' }))

    await waitFor(() => expect(current()).toBe('B-PROP'))
    expect(writes).toEqual([])
  })

  it('deny asks for a reason first and sends it (BEH-INBOX-1)', async () => {
    mockApi()
    await open()

    fireEvent.click(screen.getByRole('button', { name: 'Deny' }))
    const confirm = screen.getByRole('button', { name: 'Confirm deny' })
    expect(confirm).toBeDisabled()
    fireEvent.change(screen.getByLabelText(/Reason for denying/), { target: { value: 'it is blue' } })
    fireEvent.click(confirm)

    await waitFor(() => expect(current()).toBe('B-PROP'))
    expect(writes).toEqual([['/api/projects/alpha/behaviours/A-ASK/review', { state: 'denied', note: 'it is blue' }]])
  })

  it('pauses a pending card and resumes a paused one (BEH-INBOX-1)', async () => {
    mockApi({ alpha: [beh('A-NEEDS', { pending: true, needsContext: 'x', priority: 1 }), beh('A-PAUSED', { pending: true, paused: true, priority: 2 })] })
    await open()

    fireEvent.click(screen.getByRole('button', { name: 'Pause' }))
    await waitFor(() => expect(current()).toBe('A-PAUSED'))
    fireEvent.click(screen.getByRole('button', { name: 'Resume' }))
    await screen.findByText(/That is all of them/)

    expect(writes).toEqual([
      ['/api/projects/alpha/behaviours/A-NEEDS/paused', { paused: true }],
      ['/api/projects/alpha/behaviours/A-PAUSED/paused', { paused: false }],
    ])
  })

  it('answer and comment each leave a note on the card, and need words (BEH-INBOX-1)', async () => {
    mockApi()
    await open()
    const answer = screen.getByRole('button', { name: 'Answer' })
    const comment = screen.getByRole('button', { name: 'Comment' })
    expect(answer).toBeDisabled()
    expect(comment).toBeDisabled()

    fireEvent.change(screen.getByLabelText('Your words'), { target: { value: 'red' } })
    fireEvent.click(screen.getByRole('button', { name: 'Answer' }))
    await waitFor(() => expect(current()).toBe('B-PROP'))
    fireEvent.change(screen.getByLabelText('Your words'), { target: { value: 'looks fine' } })
    fireEvent.click(screen.getByRole('button', { name: 'Comment' }))
    await waitFor(() => expect(current()).toBe('A-NEEDS'))

    expect(writes).toEqual([
      ['/api/projects/alpha/notes', { text: 'red', behaviour: 'A-ASK' }],
      ['/api/projects/beta/notes', { text: 'looks fine', behaviour: 'B-PROP' }],
    ])
  })

  it('lower priority adds 1 as an ordinary edit, and stops at 5 (BEH-INBOX-1)', async () => {
    mockApi({ alpha: [beh('A-1', { ...proposed, priority: 2 }), beh('A-5', { ...proposed, priority: 5 })] })
    await open()

    fireEvent.click(screen.getByRole('button', { name: 'Lower priority' }))
    await waitFor(() => expect(current()).toBe('A-5'))

    expect(writes).toEqual([['/api/projects/alpha/behaviours/A-1/priority', { priority: 3 }]])
    expect(screen.getByRole('button', { name: 'Lower priority' })).toBeDisabled()
    expect([1, 2, 4, 5, undefined].map(lowered)).toEqual([2, 3, 5, 5, 5])
  })

  it('a refused write keeps the card on screen and says why (BEH-INBOX-1)', async () => {
    mockApi()
    await open()
    refuse = true

    fireEvent.click(screen.getByRole('button', { name: 'Approve' }))

    expect(await screen.findByText('the corpus refused that')).toBeInTheDocument()
    expect(current()).toBe('A-ASK')
  })
})
