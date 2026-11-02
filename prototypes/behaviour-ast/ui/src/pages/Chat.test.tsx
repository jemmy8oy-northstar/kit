import { afterEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import Chat from './Chat'
import { named, toMessages, type Line } from '../chat'
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

/** What GET .../notes answers: the notes an undo looks through. */
let notesNow: { at: string; kind: string; behaviour: string | null; resolves: null; text: string }[] = []

/** Every POST the page made, as [path, body]. */
let posts: [string, { behaviour?: string; messages?: { from: string; text: string }[]; text?: string }][] = []

function mockApi(
  triage: unknown = { configured: true, model: 'claude-haiku-5-5', remaining: 9 },
  chat?: () => { ok: boolean; status: number; body: unknown } | Promise<{ ok: boolean; status: number; body: unknown }>,
  projects: Record<string, Behaviour[]> = details,
) {
  posts = []
  notesNow = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string, init?: { method?: string; body?: string }) => {
      const ok = { ok: true, status: 200, statusText: 'OK' }
      if (init?.method === 'POST') {
        posts.push([path, JSON.parse(init.body ?? 'null')])
        if (path.endsWith('/chat')) {
          const r = (await chat?.()) ?? { ok: true, status: 200, body: { reply: `reply ${posts.filter(([p]) => p.endsWith('/chat')).length}`, remaining: 8 } }
          return { ok: r.ok, status: r.status, statusText: 'x', json: async () => r.body }
        }
        return { ...ok, json: async () => ({ ok: true }) }
      }
      if (path.endsWith('/api/triage')) return { ...ok, json: async () => triage }
      const rest = decodeURIComponent(path.split('/api/projects/')[1] ?? '')
      if (path.endsWith('/api/session')) return { ...ok, json: async () => ({ required: false, signedIn: true }) }
      if (rest.endsWith('/history')) return { ...ok, json: async () => ({ history: 'none', reason: 'no git', completed: [], added: [] }) }
      if (rest.endsWith('/notes')) return { ...ok, json: async () => ({ notes: notesNow }) }
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

const box = () => screen.getByLabelText('Message the spec agent') as HTMLTextAreaElement

async function say(text: string, reply: string) {
  fireEvent.change(box(), { target: { value: text } })
  fireEvent.click(screen.getByRole('button', { name: 'Send' }))
  await screen.findByText(reply)
}

const chats = () => posts.filter(([p]) => p.endsWith('/chat'))
const inHandId = () => screen.getByTestId('chat-in-hand').querySelector('.id')!.textContent!

afterEach(() => vi.unstubAllGlobals())

describe('BEH-CHAT-1: a Chat page talks about one project at a time', () => {
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

  it('opens on the project of the top item, and a project change says so, keeps the history, and tells the agent once', async () => {
    mockApi()
    await open()
    expect((screen.getByLabelText('Project') as HTMLSelectElement).value).toBe('alpha')

    await say('hello', 'reply 1')
    expect(chats()[0][0]).toBe('/api/projects/alpha/chat')
    const first = '[Now talking about alpha] [In view now: A-ASK "title of A-ASK", asking: Which colour?] hello'
    expect(chats()[0][1].messages).toEqual([{ from: 'user', text: first }])

    fireEvent.change(screen.getByLabelText('Project'), { target: { value: 'beta' } })
    expect(screen.getByText('Now talking about beta.')).toBeTruthy()
    await say('and here?', 'reply 2')
    await say('more', 'reply 3')

    expect(chats()[1][0]).toBe('/api/projects/beta/chat')
    expect(chats()[2][1].messages).toEqual([
      { from: 'user', text: first },
      { from: 'agent', text: 'reply 1' },
      { from: 'user', text: '[Now talking about beta] and here?' },
      { from: 'agent', text: 'reply 2' },
      { from: 'user', text: 'more' },
    ])
    expect(screen.getByText(/8 replies left today/)).toBeTruthy()
  })

  it('naming another project in a message moves the chat there before it is sent', async () => {
    mockApi()
    await open()
    await say('what does beta still need?', 'reply 1')

    expect(screen.getByText('Now talking about beta, as you named it.')).toBeTruthy()
    expect((screen.getByLabelText('Project') as HTMLSelectElement).value).toBe('beta')
    expect(chats()[0][0]).toBe('/api/projects/beta/chat')
    expect(chats()[0][1].messages).toEqual([{ from: 'user', text: '[Now talking about beta] what does beta still need?' }])
    // beta has nothing waiting, so no alpha item goes with a message about beta.
    expect(chats()[0][1].behaviour).toBeUndefined()

    await say('ok', 'reply 2')
    expect(chats()[1][1].messages!.at(-1)).toEqual({ from: 'user', text: 'ok' })
  })

  it('a message that moves the chat by naming a project is not the answer to the item waiting there', async () => {
    mockApi(undefined, undefined, { ...details, beta: [beh('B-ASK', { review: { state: 'requested', note: 'Which font?' } })] })
    await open()
    expect(inHandId()).toBe('A-ASK')
    await say('same as we did in beta', 'reply 1')

    expect(inHandId()).toBe('B-ASK')
    // He had not seen beta's item when he wrote it, so it neither went with the turn nor posts as its answer.
    expect(chats()[0][1].behaviour).toBeUndefined()
    expect(screen.queryByRole('button', { name: 'Post as my answer' })).toBeNull()

    await say('serif', 'reply 2')
    expect(chats()[1][1].behaviour).toBe('B-ASK')
    expect(screen.getAllByRole('button', { name: 'Post as my answer' })).toHaveLength(2)
  })

  it('reads a project from a message only when it names one other project, and not the current one', () => {
    const apps = [
      { app: 'kit', project: 'kit' },
      { app: 'kit-ui', project: 'kit' },
      { app: 'tracks', project: 'tracks' },
      { app: 'snip-it', project: 'snip-it' },
    ]
    expect(named('how is Tracks syncing?', apps, 'kit-ui')).toBe('tracks')
    expect(named('add a page to snip-it', apps, 'tracks')).toBe('snip-it')
    // "kit" in a kit-ui chat names its own project, so the chat stays where it is.
    expect(named('kit should show costs', apps, 'kit-ui')).toBeNull()
    expect(named('compare tracks with snip-it', apps, 'kit-ui')).toBeNull()
    expect(named('tracks and kit-ui both', apps, 'kit-ui')).toBeNull()
    // A name inside another word, or a hyphenated one, is not a mention.
    expect(named('the kitchen sink', apps, 'tracks')).toBeNull()
    expect(named('snip-it-two', apps, 'tracks')).toBeNull()
    expect(named('nothing here', apps, 'tracks')).toBeNull()
  })

  it('keeps a long chat inside the server limits, starting and ending with his', () => {
    const lines: Line[] = [{ from: 'switch', app: 'alpha' }]
    for (let i = 0; i < 30; i++)
      lines.push({ from: 'user', text: `q${i}`, sent: `q${i}`, app: 'alpha', item: null }, { from: 'agent', text: 'x'.repeat(5000), app: 'alpha', item: null })
    lines.push({ from: 'user', text: 'last', sent: 'last', app: 'alpha', item: null })

    const sent = toMessages(lines)
    expect(sent).toHaveLength(39)
    expect(sent[0].from).toBe('user')
    expect(sent.at(-1)).toEqual({ from: 'user', text: 'last' })
    expect(Math.max(...sent.map((m) => m.text.length))).toBe(4000)
  })

  it('the project cannot change while a reply is awaited', async () => {
    let answer: (r: { ok: boolean; status: number; body: unknown }) => void = () => {}
    mockApi(undefined, () => new Promise((r) => (answer = r)))
    await open()
    fireEvent.change(box(), { target: { value: 'hello' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))

    await waitFor(() => expect((screen.getByLabelText('Project') as HTMLSelectElement).disabled).toBe(true))
    answer({ ok: true, status: 200, body: { reply: 'done', remaining: 8 } })
    await screen.findByText('done')
    expect((screen.getByLabelText('Project') as HTMLSelectElement).disabled).toBe(false)
  })

  it('Enter sends and Shift+Enter does not', async () => {
    mockApi()
    await open()
    fireEvent.change(box(), { target: { value: 'line one' } })
    fireEvent.keyDown(box(), { key: 'Enter', shiftKey: true })
    expect(chats()).toEqual([])
    fireEvent.keyDown(box(), { key: 'Enter' })
    await screen.findByText('reply 1')
  })

  it('a refused turn shows the reason and puts his message back to retry', async () => {
    mockApi(undefined, () => ({ ok: false, status: 429, body: { error: 'triage-cap-reached', reason: "today's replies are used up" } }))
    await open()
    fireEvent.change(box(), { target: { value: 'hello' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))

    expect((await screen.findByRole('alert')).textContent).toContain("today's replies are used up")
    expect(box().value).toBe('hello')
    expect(screen.queryByText(/^You:/)).toBeNull()
  })
})

describe('BEH-CHAT-2: the chat opens on the top item, with no click to start', () => {
  it('shows the top item and its question from the start, and opening sends nothing', async () => {
    mockApi()
    await open()
    const card = screen.getByTestId('chat-in-hand')
    expect(card.textContent).toContain('Top of your stack')
    expect(card.textContent).toMatch(/Which colour\?|Add the API key/)
    expect(screen.queryByRole('button', { name: /^Answer: / })).toBeNull()
    expect(posts).toEqual([])
  })

  it('with nothing waiting in the project, says so and offers the next one elsewhere', async () => {
    mockApi()
    await open()
    fireEvent.change(screen.getByLabelText('Project'), { target: { value: 'beta' } })
    expect(screen.queryByTestId('chat-in-hand')).toBeNull()
    expect(screen.getByTestId('chat-nothing').textContent).toContain('Nothing is waiting on you in beta')
    // His 17:50Z note: the project comes first, so the preview is not read as being about this one.
    const next = screen.getByRole('button', { name: /next waiting is/ })
    expect(next.textContent).toBe('alpha: next waiting is “title of A-ASK”')
    fireEvent.click(next)
    expect((screen.getByLabelText('Project') as HTMLSelectElement).value).toBe('alpha')
    expect(screen.getByTestId('chat-in-hand')).toBeTruthy()
    vi.unstubAllGlobals()
    document.body.innerHTML = ''

    mockApi(undefined, undefined, { beta: [beh('B-DONE')] })
    await open()
    expect(screen.getByTestId('chat-nothing').textContent).not.toContain('next waiting')
  })
})

describe('BEH-CHAT-3: an item is answered or marked off from inside the chat', () => {
  it('sends the item in hand with each turn, a reply posts as his answer, and the next item comes up', async () => {
    mockApi()
    await open()
    const id = inHandId()

    await say('what is it asking?', 'reply 1')
    expect(chats()[0][1].behaviour).toBe(id)

    const agentLine = screen.getByText('reply 1').closest('li')!
    fireEvent.click(agentLine.querySelector('button')!)
    await waitFor(() => expect(inHandId()).not.toBe(id))
    expect(posts.at(-1)).toEqual(['/api/projects/alpha/notes', { text: 'reply 1', behaviour: id }])
    // The old messages were about the answered item, so they are no answer for the next one.
    expect(screen.queryAllByRole('button', { name: 'Post as my answer' })).toEqual([])
  })

  it('his own message can be posted as his answer', async () => {
    mockApi()
    await open()
    const id = inHandId()
    await say('blue', 'reply 1')
    const mine = screen.getByText('blue').closest('li')!
    fireEvent.click(mine.querySelector('button')!)
    await waitFor(() => expect(posts.at(-1)).toEqual(['/api/projects/alpha/notes', { text: 'blue', behaviour: id }]))
  })

  it('Approve and Skip mark the item off and bring up the next', async () => {
    mockApi()
    await open()
    const id = inHandId()
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }))
    await waitFor(() => expect(inHandId()).not.toBe(id))
    expect(posts.at(-1)?.[0]).toBe(`/api/projects/alpha/behaviours/${id}/review`)

    // His 17:48Z note: "Skip" alone read as dismissing it.
    fireEvent.click(screen.getByRole('button', { name: 'Skip to next item' }))
    expect(screen.queryByTestId('chat-in-hand')).toBeNull()
  })

  it('after he acts on an item, Kit says so in the chat and offers what comes next, and the agent is told', async () => {
    mockApi()
    await open()
    const first = inHandId()
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }))
    const kit = await screen.findByText(/^Approved “.*”\. Next up in alpha: “.*”\. Reply to answer it, or we can define new spec or discuss alpha\.$/)
    expect(kit.closest('li')!.getAttribute('data-from')).toBe('kit')

    fireEvent.click(screen.getByRole('button', { name: 'Skip to next item' }))
    expect(screen.getByText(/^Skipped for now: “.*”\. That was the last item waiting on you\. We can define new spec or discuss alpha\.$/)).toBeTruthy()

    await say('ok, what next?', 'reply 1')
    const second = chats()[0][1].messages!.at(-1)!.text
    expect(second.startsWith(`[Now talking about alpha] [He approved ${first}] [He skipped, for now,`)).toBe(true)
    expect(second.endsWith(' ok, what next?')).toBe(true)
  })

  it('the item moves into the chat where he first replies about it, and the agent reads it there', async () => {
    mockApi()
    await open()
    const id = inHandId()
    expect(screen.queryByText('Top of your stack in alpha')).toBeNull()

    await say('which colours are there?', 'reply 1')
    const order = [...screen.getByLabelText('Messages').querySelectorAll('li[data-from]')].map((li) => li.getAttribute('data-from'))
    expect(order).toEqual(['item', 'user', 'agent'])
    // The dock keeps the title and buttons; the question is in the thread now, once.
    expect(screen.getAllByText('Which colour?')).toHaveLength(1)
    expect(chats()[0][1].messages).toEqual([
      { from: 'user', text: `[Now talking about alpha] [In view now: ${id} "title of ${id}", asking: Which colour?] which colours are there?` },
    ])

    await say('blue', 'reply 2')
    expect(chats()[1][1].messages!.at(-1)).toEqual({ from: 'user', text: 'blue' })
    expect(screen.getAllByText('Top of your stack in alpha')).toHaveLength(1)
  })

  it('with nothing in hand, a reply is saved as a note on the project', async () => {
    mockApi()
    await open()
    fireEvent.change(screen.getByLabelText('Project'), { target: { value: 'beta' } })
    await say('the board needs a done column', 'reply 1')
    expect(screen.queryByRole('button', { name: 'Post as my answer' })).toBeNull()
    fireEvent.click(screen.getByRole('button', { name: 'Save as a note on beta' }))

    await screen.findByRole('button', { name: 'Saved as a note' })
    expect(posts.at(-1)).toEqual(['/api/projects/beta/notes', { text: 'reply 1' }])
  })

  it('a reply is saved on the project it was about, after he has picked another', async () => {
    mockApi()
    await open()
    await say('the board needs a done column', 'reply 1')
    fireEvent.change(screen.getByLabelText('Project'), { target: { value: 'beta' } })
    fireEvent.click(screen.getByRole('button', { name: 'Save as a note on alpha' }))

    await screen.findByRole('button', { name: 'Saved as a note' })
    expect(posts.at(-1)).toEqual(['/api/projects/alpha/notes', { text: 'reply 1' }])
  })
})

describe('BEH-CHAT-6: the chat agent acts on the item itself, and he can undo it', () => {
  const acting = (action: unknown, reply = 'Done it.') => () => ({ ok: true, status: 200, body: { reply, remaining: 8, action } })

  it('approves the item when the agent asks to, says so with an Undo, and the undo puts the review back', async () => {
    mockApi(undefined, acting({ kind: 'approve', text: null }, 'Approved it.'))
    await open()
    expect(inHandId()).toBe('A-ASK')
    await say('yes, approve it', 'Approved it.')

    await waitFor(() => expect(posts.at(-1)).toEqual(['/api/projects/alpha/behaviours/A-ASK/review', { state: 'approved', note: null }]))
    const kit = await screen.findByText(/^Approved “title of A-ASK”\./)
    await waitFor(() => expect(inHandId()).toBe('A-NEEDS'))

    fireEvent.click(kit.closest('li')!.querySelector('button')!)
    await screen.findByText('Undone: Approved “title of A-ASK”.')
    expect(posts.at(-1)).toEqual(['/api/projects/alpha/behaviours/A-ASK/review', { state: 'requested', note: 'Which colour?' }])
    expect(inHandId()).toBe('A-ASK')
    expect(screen.getByRole('button', { name: 'Undone' })).toHaveProperty('disabled', true)
  })

  it('denies and answers with his words, and an undone answer is dismissed from the notes', async () => {
    mockApi(undefined, acting({ kind: 'answer', text: 'Blue.' }))
    await open()
    await say('blue', 'Done it.')
    await waitFor(() => expect(posts.at(-1)).toEqual(['/api/projects/alpha/notes', { text: 'Blue.', behaviour: 'A-ASK' }]))

    notesNow = [
      { at: '2026-11-02 18:00Z', kind: 'note', behaviour: 'A-ASK', resolves: null, text: 'Blue.' },
      { at: '2026-11-02 18:01Z', kind: 'note', behaviour: null, resolves: null, text: 'Blue.' },
    ]
    const kit = await screen.findByText(/^Your answer is posted on “title of A-ASK”\./)
    fireEvent.click(kit.closest('li')!.querySelector('button')!)
    await screen.findByText(/^Undone: Your answer is posted on/)
    expect(posts.at(-1)).toEqual(['/api/projects/alpha/notes/dismiss', { index: 0, at: '2026-11-02 18:00Z' }])
    vi.unstubAllGlobals()
    document.body.innerHTML = ''

    mockApi(undefined, acting({ kind: 'deny', text: 'It should be green.' }))
    await open()
    await say('no, green', 'Done it.')
    await waitFor(() =>
      expect(posts.at(-1)).toEqual(['/api/projects/alpha/behaviours/A-ASK/review', { state: 'denied', note: 'It should be green.' }]),
    )
    expect(await screen.findByText(/^Denied, with your correction: “title of A-ASK”\./)).toBeTruthy()
  })

  it('asked to undo, the agent undoes the last thing done here, whoever did it', async () => {
    mockApi(undefined, acting({ kind: 'undo', text: null }, 'Undoing that.'))
    await open()
    fireEvent.click(screen.getByRole('button', { name: 'Approve' }))
    await waitFor(() => expect(inHandId()).toBe('A-NEEDS'))

    await say('wait, undo that', 'Undoing that.')
    await screen.findByText('Undone: Approved “title of A-ASK”.')
    expect(posts.at(-1)).toEqual(['/api/projects/alpha/behaviours/A-ASK/review', { state: 'requested', note: 'Which colour?' }])
    expect(inHandId()).toBe('A-ASK')
  })

  it('says so when there is nothing to undo, and a reply that only acted still takes its turn', async () => {
    mockApi(undefined, acting({ kind: 'undo', text: null }, ''))
    await open()
    await say('undo', 'Done.')
    expect(screen.getByText('There is nothing here to undo.')).toBeTruthy()
    expect(chats()[0][1].messages).toHaveLength(1)
  })
})
