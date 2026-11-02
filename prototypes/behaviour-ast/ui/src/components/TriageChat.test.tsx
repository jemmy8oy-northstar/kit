import { afterEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import TriageChat from './TriageChat'

/** Every turn the chat POSTed, as [path, body]. */
let turns: [string, { behaviour: string; messages: { from: string; text: string }[] }][] = []

function mockApi(status: unknown, reply: (n: number) => { ok: boolean; status: number; body: unknown }) {
  turns = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (path: string, init?: { method?: string; body?: string }) => {
      if (init?.method === 'POST') {
        turns.push([path, JSON.parse(init.body ?? 'null')])
        const r = reply(turns.length)
        return { ok: r.ok, status: r.status, statusText: 'x', json: async () => r.body }
      }
      if (path.endsWith('/api/triage')) {
        return status === 401
          ? { ok: false, status: 401, statusText: 'Unauthorized', json: async () => ({ error: 'not-signed-in' }) }
          : { ok: true, status: 200, statusText: 'OK', json: async () => status }
      }
      throw new Error(`unexpected fetch ${path}`)
    }),
  )
}

const on = { configured: true, model: 'claude-haiku-5-5', remaining: 5 }

afterEach(() => vi.unstubAllGlobals())

describe('BEH-SPECAGENT-4: an Inbox card is talked through with the spec agent', () => {
  it('offers nothing when the server has no model key, or the session cannot ask', async () => {
    mockApi({ configured: false, model: null, remaining: null }, () => ({ ok: true, status: 200, body: {} }))
    const { unmount } = render(<TriageChat app="kit" behaviour="BEH-1" onUse={() => {}} />)
    await waitFor(() => expect(fetch).toHaveBeenCalled())
    expect(screen.queryByRole('button', { name: 'Talk it through' })).toBeNull()
    unmount()

    mockApi(401, () => ({ ok: true, status: 200, body: {} }))
    render(<TriageChat app="kit" behaviour="BEH-1" onUse={() => {}} />)
    await waitFor(() => expect(fetch).toHaveBeenCalled())
    expect(screen.queryByRole('button', { name: 'Talk it through' })).toBeNull()
  })

  it('sends the whole chat each turn, shows the reply, and a reply becomes his words', async () => {
    mockApi(on, (n) => ({ ok: true, status: 200, body: { reply: `reply ${n}`, remaining: 5 - n } }))
    const used: string[] = []
    render(<TriageChat app="my app" behaviour="BEH-7" onUse={(t) => used.push(t)} />)

    fireEvent.click(await screen.findByRole('button', { name: 'Talk it through' }))
    expect(screen.getByText(/claude-haiku-5-5/)).toBeTruthy()
    fireEvent.change(screen.getByLabelText('Ask the spec agent'), { target: { value: 'what is it asking?' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))
    await screen.findByText('reply 1')

    fireEvent.change(screen.getByLabelText('Ask the spec agent'), { target: { value: 'then deny it' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))
    await screen.findByText('reply 2')

    expect(turns[0][0]).toBe('/api/projects/my%20app/triage')
    expect(turns[1][1]).toEqual({
      behaviour: 'BEH-7',
      messages: [
        { from: 'user', text: 'what is it asking?' },
        { from: 'agent', text: 'reply 1' },
        { from: 'user', text: 'then deny it' },
      ],
    })
    expect(screen.getByText('3 replies left today')).toBeTruthy()

    fireEvent.click(screen.getAllByRole('button', { name: 'Use as my words' })[1])
    expect(used).toEqual(['reply 2'])
  })

  it('a refused turn shows the reason and puts his message back to retry', async () => {
    mockApi(on, () => ({ ok: false, status: 429, body: { error: 'triage-cap-reached', reason: "today's replies are used up" } }))
    render(<TriageChat app="kit" behaviour="BEH-1" onUse={() => {}} />)

    fireEvent.click(await screen.findByRole('button', { name: 'Talk it through' }))
    fireEvent.change(screen.getByLabelText('Ask the spec agent'), { target: { value: 'hello' } })
    fireEvent.click(screen.getByRole('button', { name: 'Send' }))

    expect((await screen.findByRole('alert')).textContent).toContain("today's replies are used up")
    expect((screen.getByLabelText('Ask the spec agent') as HTMLInputElement).value).toBe('hello')
    expect(screen.queryByText(/^You:/)).toBeNull()
  })
})
