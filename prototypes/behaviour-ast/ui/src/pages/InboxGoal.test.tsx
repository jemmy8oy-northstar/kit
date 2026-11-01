import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Inbox from './Inbox'
import { goalQuestions, keyOf } from '../inbox'
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

interface Fake {
  summary?: Record<string, unknown>
  detail: Record<string, unknown>
}

// alpha has an open epic (a goal); beta has none; gamma has none but is marked maintained.
const epic = { id: 'EPIC-1', title: 'Ship it', at: 'x:1' }
const projects: Record<string, Fake> = {
  alpha: {
    detail: {
      behaviours: [beh('A-1', { pending: true, in: ['FEAT-1'] })],
      features: [{ id: 'FEAT-1', title: 'F', in: [], at: 'x:1', epics: ['EPIC-1'] }],
      epics: [epic],
    },
  },
  beta: { detail: { behaviours: [beh('B-1')] } },
  gamma: { summary: { goal: 'maintained' }, detail: { behaviours: [beh('G-1')] } },
}

let writes: [string, unknown][] = []

function mockApi(only: string[] = Object.keys(projects)) {
  writes = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string, init?: { method?: string; body?: string }) => {
      const ok = { ok: true, status: 200, statusText: 'OK' }
      if (init?.method === 'POST') {
        writes.push([path, JSON.parse(init.body ?? 'null')])
        return { ...ok, json: async () => ({ ok: true }) }
      }
      const rest = decodeURIComponent(path.split('/api/projects/')[1] ?? '')
      if (path.endsWith('/api/session')) return { ...ok, json: async () => ({ required: false, signedIn: true }) }
      if (rest.endsWith('/history')) return { ...ok, json: async () => ({ history: 'none', reason: 'no git', completed: [], added: [] }) }
      if (rest.endsWith('/notes')) return { ...ok, json: async () => ({ notes: [] }) }
      if (rest.endsWith('/schedule')) return { ...ok, json: async () => ({ scheduled: [], backlog: [] }) }
      if (rest) return { ...ok, json: async () => ({ app: rest, ...projects[rest].detail }) }
      return { ...ok, json: async () => ({ projects: only.map((app) => ({ app, ...projects[app].summary })) }) }
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

beforeEach(() => window.localStorage.clear())
afterEach(() => vi.unstubAllGlobals())

describe('the goal question in the Inbox', () => {
  it('BEH-NARR-2: asks once for a project with no goal epic, with the default said, and not for one with a goal or marked maintained', async () => {
    mockApi()
    await open()

    expect(screen.getByTestId('inbox-position')).toHaveTextContent('Card 1 of 1')
    expect(screen.getByRole('heading', { name: 'What is beta working towards?' })).toBeInTheDocument()
    expect(screen.getByTestId('inbox-goal-default')).toHaveTextContent(
      "If you don't answer, I'll treat it as maintained: fix what breaks, build nothing new.",
    )
  })

  it('BEH-NARR-2: "Just maintaining it" marks the project maintained and the card leaves', async () => {
    mockApi()
    await open()

    fireEvent.click(screen.getByRole('button', { name: 'Just maintaining it' }))

    expect(await screen.findByText(/That is all of them/)).toBeInTheDocument()
    expect(writes).toEqual([['/api/projects/beta/goal', { maintained: true }]])
  })

  it('BEH-NARR-2: words leave a note on the project, the card leaves, and it is not asked again in this browser', async () => {
    mockApi()
    await open()
    const send = screen.getByRole('button', { name: 'Working towards this' })
    expect(send).toBeDisabled()

    fireEvent.change(screen.getByLabelText('Working towards…'), { target: { value: 'a public launch' } })
    fireEvent.click(send)

    expect(await screen.findByText(/That is all of them/)).toBeInTheDocument()
    expect(writes).toEqual([['/api/projects/beta/notes', { text: 'Working towards: a public launch' }]])
    expect(JSON.parse(window.localStorage.getItem('kit.goalAnswered')!)).toEqual(['beta'])
  })

  it('BEH-NARR-2: a project he already answered is not asked again', async () => {
    window.localStorage.setItem('kit.goalAnswered', JSON.stringify(['beta']))
    mockApi()
    render(
      <MemoryRouter>
        <Inbox />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Nothing is waiting on you.')).toBeInTheDocument()
  })

  it('BEH-NARR-2: its key is stable and distinct from a behaviour card of the same name', () => {
    const [q] = goalQuestions([
      { summary: { app: 'beta' }, behaviours: [], places: new Map(), error: null, bells: new Set(), detail: { app: 'beta', behaviours: [] } as never },
    ])

    expect(keyOf(q)).toBe('goal:beta')
    expect(keyOf({ app: 'beta', project: 'beta', b: beh('goal:beta') })).toBe('beta/goal:beta')
  })
})
