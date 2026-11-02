import { afterEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Chat from './Chat'
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

// alpha has two items waiting on him; beta has none.
const details: Record<string, Behaviour[]> = {
  alpha: [
    beh('A-ASK', { review: { state: 'requested', note: 'Which colour?' }, priority: 1 }),
    beh('A-NEEDS', { pending: true, needsContext: 'Add the API key', priority: 2 }),
  ],
  beta: [beh('B-DONE')],
}

/** Every POST the page made, as [path, body]. */
let posts: [string, { behaviour?: string; messages?: { from: string; text: string }[]; text?: string }][] = []

function mockApi(
  triage: unknown = { configured: true, model: 'claude-haiku-5-5', remaining: 9 },
  chat?: () => { ok: boolean; status: number; body: unknown },
  projects: Record<string, Behaviour[]> = details,
) {
  posts = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string, init?: { method?: string; body?: string }) => {
      const ok = { ok: true, status: 200, statusText: 'OK' }
      if (init?.method === 'POST') {
        posts.push([path, JSON.parse(init.body ?? 'null')])
        if (path.endsWith('/chat')) {
          const r = chat?.() ?? { ok: true, status: 200, body: { reply: `reply ${posts.filter(([p]) => p.endsWith('/chat')).length}`, remaining: 8 } }
          return { ok: r.ok, status: r.status, statusText: 'x', json: async () => r.body }
        }
        return { ...ok, json: async () => ({ ok: true }) }
      }
      if (path.endsWith('/api/triage')) return { ...ok, json: async () => triage }
      const rest = decodeURIComponent(path.split('/api/projects/')[1] ?? '')
      if (path.endsWith('/api/session')) return { ...ok, json: async () => ({ required: false, signedIn: true }) }
      if (rest.endsWith('/history')) return { ...ok, json: async () => ({ history: 'none', reason: 'no git', completed: [], added: [] }) }
      if (rest.endsWith('/notes')) return { ...ok, json: async () => ({ notes: [] }) }
      if (rest.endsWith('/schedule')) return { ...ok, json: async () => ({ scheduled: [], backlog: [] }) }
      return { ...ok, json: async () => (rest ? { app: rest, behaviours: projects[rest] } : { projects: Object.keys(projects).map((app) => ({ app, goal: 'maintained' })) }) }
    }),
  )
}

async function open() {
  render(
    <MemoryRouter>
      <Chat />
    </MemoryRouter>,
  )
  await screen.findByTestId('chat')
}

async function say(text: string, reply: string) {
  fireEvent.change(screen.getByLabelText('Message the spec agent'), { target: { value: text } })
  fireEvent.click(screen.getByRole('button', { name: 'Send' }))
  await screen.findByText(reply)
}

const chats = () => posts.filter(([p]) => p.endsWith('/chat'))

afterEach(() => vi.unstubAllGlobals())

describe('BEH-CHAT-1: a Chat page talks about the project picked in a select box', () => {
  it('is not offered when the spec agent is off', async () => {
    mockApi({ configured: false, model: null, remaining: null })
    render(
      <MemoryRouter>
        <Chat />
      </MemoryRouter>,
    )
    await screen.findByTestId('chat-off')
    expect(screen.queryByTestId('chat')).toBeNull()
  })

  it('a project change says so, keeps the history, and tells the agent once', async () => {
    mockApi()
    await open()
    expect(screen.getByText('Now talking about alpha.')).toBeTruthy()

    await say('hello', 'reply 1')
    expect(chats()[0][0]).toBe('/api/projects/alpha/chat')
    expect(chats()[0][1]).toEqual({ messages: [{ from: 'user', text: '[Now talking about alpha] hello' }] })

    fireEvent.change(screen.getByLabelText('Project'), { target: { value: 'beta' } })
    expect(screen.getByText('Now talking about beta.')).toBeTruthy()
    await say('and here?', 'reply 2')
    await say('more', 'reply 3')

    expect(chats()[1][0]).toBe('/api/projects/beta/chat')
    expect(chats()[2][1].messages).toEqual([
      { from: 'user', text: '[Now talking about alpha] hello' },
      { from: 'agent', text: 'reply 1' },
      { from: 'user', text: '[Now talking about beta] and here?' },
      { from: 'agent', text: 'reply 2' },
      { from: 'user', text: 'more' },
    ])
    expect(screen.getByText('8 replies left today')).toBeTruthy()
  })

  it('a refused turn shows the reason and puts his message back to retry', async () => {
    mockApi(undefined, () => ({ ok: false, status: 429, body: { error: 'triage-cap-reached', reason: "today's replies are used up" } }))
    await open()
    fireEvent.change(screen.getByLabelText('Message the spec agent'), { target: { value: 'hello' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))

    expect((await screen.findByRole('alert')).textContent).toContain("today's replies are used up")
    expect((screen.getByLabelText('Message the spec agent') as HTMLInputElement).value).toBe('hello')
    expect(screen.queryByText(/^You:/)).toBeNull()
  })
})

describe('BEH-CHAT-2: the chat opens with suggestions', () => {
  it('suggests the next item, defining new spec and discussing the project, and a suggestion sends nothing', async () => {
    mockApi()
    await open()
    const suggestions = screen.getByTestId('chat-suggestions')
    expect(suggestions.textContent).toContain('Answer: title of A-')
    expect(suggestions.textContent).toContain('Define new spec for alpha')
    expect(suggestions.textContent).toContain('Discuss alpha')

    fireEvent.click(screen.getByRole('button', { name: 'Define new spec for alpha' }))
    expect((screen.getByLabelText('Message the spec agent') as HTMLInputElement).value).toBe('I want to add to alpha: ')
    expect(posts).toEqual([])
  })

  it('leaves the item out when nothing is waiting, and names the other project when the picked one has none', async () => {
    mockApi()
    await open()
    fireEvent.change(screen.getByLabelText('Project'), { target: { value: 'beta' } })
    expect(screen.getByTestId('chat-suggestions').textContent).toContain('(alpha)')
    vi.unstubAllGlobals()
    document.body.innerHTML = ''

    mockApi(undefined, undefined, { beta: [beh('B-DONE')] })
    await open()
    expect(screen.getByText('Now talking about beta.')).toBeTruthy()
    expect(screen.queryByRole('button', { name: /^Answer: / })).toBeNull()
    expect(screen.getByRole('button', { name: 'Discuss beta' })).toBeTruthy()
  })
})

describe('BEH-CHAT-3: an item is answered or marked off from inside the chat', () => {
  it('sends the item in hand with each turn, a reply becomes his answer, and the next is suggested', async () => {
    mockApi()
    await open()
    fireEvent.click(screen.getByRole('button', { name: /^Answer: / }))
    const card = screen.getByTestId('chat-in-hand')
    const id = card.querySelector('.id')!.textContent!

    await say('what is it asking?', 'reply 1')
    expect(chats()[0][1].behaviour).toBe(id)

    fireEvent.click(screen.getByRole('button', { name: 'Use as my words' }))
    expect((screen.getByLabelText('Your answer') as HTMLInputElement).value).toBe('reply 1')
    fireEvent.click(screen.getByRole('button', { name: 'Answer' }))

    await waitFor(() => expect(screen.queryByTestId('chat-in-hand')).toBeNull())
    expect(posts.at(-1)).toEqual(['/api/projects/alpha/notes', { text: 'reply 1', behaviour: id }])
    const nextSuggestion = screen.getByRole('button', { name: /^Answer: / }).textContent!
    expect(nextSuggestion).not.toContain(`title of ${id}`)
  })

  it('with nothing in hand, a reply is saved as a note on the project', async () => {
    mockApi()
    await open()
    await say('the board needs a done column', 'reply 1')
    fireEvent.click(screen.getByRole('button', { name: 'Save as a note on alpha' }))

    await screen.findByRole('button', { name: 'Saved as a note' })
    expect(posts.at(-1)).toEqual(['/api/projects/alpha/notes', { text: 'reply 1' }])
  })
})
