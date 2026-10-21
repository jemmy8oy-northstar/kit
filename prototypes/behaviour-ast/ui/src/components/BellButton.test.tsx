import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import BellButton from './BellButton'
import type { Note } from '../api/types'

const bell = (item: string): Note => ({ at: '2026-10-21 06:50Z', kind: 'bell', behaviour: item, resolves: null, text: '', thread: [], questions: [] })

/** A fake notes file, so a ring really changes what the next read sees. */
function server(start: Note[] = []) {
  const notes = [...start]
  const posts: unknown[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      expect(url).toMatch(/\/api\/projects\/demo\/notes$/)
      if (init?.method === 'POST') {
        const body = JSON.parse(String(init.body)) as { kind: string; behaviour: string }
        posts.push(body)
        notes.push(bell(body.behaviour))
        return {
          ok: true,
          status: 200,
          statusText: 'OK',
          json: async () => ({ app: 'demo', behaviour: body.behaviour, file: 'demo.notes.md', committed: true, pushed: true, commit: 'abc1234', branch: 'kit/hosted', note: 'x' }),
        }
      }
      return { ok: true, status: 200, statusText: 'OK', json: async () => ({ app: 'demo', file: 'demo.notes.md', notes: [...notes] }) }
    }),
  )
  return posts
}

describe('BellButton (BEH-BELL-1)', () => {
  it('rings an item with a bell note and no words, then says it is waiting for Kit', async () => {
    const posts = server()
    render(<BellButton app="demo" item="FEAT-BELL" />)

    fireEvent.click(await screen.findByRole('button', { name: /Ring the bell/ }))

    expect(await screen.findByRole('button', { name: /Rung, waiting for Kit/ })).toBeDisabled()
    expect(posts).toEqual([{ kind: 'bell', behaviour: 'FEAT-BELL' }])
  })

  it("cannot be rung again while its bell is unanswered, and another item's bell does not count", async () => {
    server([bell('FEAT-BELL')])
    const { unmount } = render(<BellButton app="demo" item="FEAT-BELL" />)
    expect(await screen.findByRole('button', { name: /Rung, waiting for Kit/ })).toBeDisabled()
    unmount()

    render(<BellButton app="demo" item="BEH-1" />)
    expect(await screen.findByRole('button', { name: /Ring the bell/ })).toBeEnabled()
  })
})
