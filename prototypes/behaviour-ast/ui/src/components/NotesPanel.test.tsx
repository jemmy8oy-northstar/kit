import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import NotesPanel from './NotesPanel'
import type { Note } from '../api/types'

/** A fake server holding one project's notes file, so a POST really changes what the next GET reads. */
function server(start: Note[] = [], refuse?: string) {
  const notes = [...start]
  const posts: unknown[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method === 'POST') {
        const body = JSON.parse(String(init.body)) as { text: string; behaviour?: string }
        posts.push(body)
        if (refuse) return { ok: false, status: 400, statusText: 'x', json: async () => ({ reason: refuse }) }
        notes.push({ at: '2026-10-10 20:41Z', behaviour: body.behaviour ?? null, text: body.text })
        return {
          ok: true,
          status: 200,
          statusText: 'OK',
          json: async () => ({ app: 'demo', behaviour: body.behaviour ?? null, file: 'behaviours/demo.notes.md', committed: true, pushed: true, commit: 'abc1234', branch: 'kit/hosted', note: 'x' }),
        }
      }
      expect(url).toMatch(/\/api\/projects\/demo\/notes$/)
      return { ok: true, status: 200, statusText: 'OK', json: async () => ({ app: 'demo', file: 'behaviours/demo.notes.md', notes: [...notes] }) }
    }),
  )
  return posts
}

const region = () => screen.getByRole('region', { name: 'Notes' })

describe('NotesPanel (kit#118)', () => {
  it('says there are no open notes rather than showing nothing', async () => {
    server()
    render(<NotesPanel app="demo" />)
    expect(await within(region()).findByText('No open notes.')).toBeInTheDocument()
  })

  it('lists every open note on the project page, with the behaviour each one names', async () => {
    server([
      { at: '2026-10-09 08:00Z', behaviour: null, text: 'about the whole app' },
      { at: '2026-10-10 09:00Z', behaviour: 'BEH-2', text: 'line one\nline two' },
    ])
    render(<NotesPanel app="demo" />)

    expect(await screen.findByText('about the whole app')).toBeInTheDocument()
    expect(screen.getByText('2 open notes')).toBeInTheDocument()
    expect(screen.getByText('BEH-2')).toBeInTheDocument()
    // Newlines are kept: a note is prose, and a run-on paragraph is not what he typed.
    expect(screen.getByText((_, el) => el?.textContent === 'line one\nline two' && el.tagName === 'P')).toBeInTheDocument()
  })

  it('will not send an empty note', async () => {
    const posts = server()
    render(<NotesPanel app="demo" />)
    const send = await screen.findByRole('button', { name: 'Leave note' })

    expect(send).toBeDisabled()
    fireEvent.change(screen.getByLabelText('Leave a note'), { target: { value: '   \n ' } })
    expect(send).toBeDisabled()
    expect(posts).toHaveLength(0)
  })

  it('sends the text as typed, then re-reads so the new note is in the list, and says what git did', async () => {
    const posts = server()
    render(<NotesPanel app="demo" />)
    await screen.findByText('No open notes.')

    fireEvent.change(screen.getByLabelText('Leave a note'), { target: { value: 'the list should keep its order' } })
    fireEvent.click(screen.getByRole('button', { name: 'Leave note' }))

    expect(await within(region()).findByText('the list should keep its order', { selector: 'p' })).toBeInTheDocument()
    expect(posts).toEqual([{ text: 'the list should keep its order' }])
    expect(screen.getByRole('status')).toHaveTextContent('Wrote a note to behaviours/demo.notes.md. Committed as abc1234 and pushed to kit/hosted.')
    expect(screen.getByLabelText('Leave a note')).toHaveValue('')
  })

  it('on a behaviour page, tags the note with that behaviour and lists only its notes', async () => {
    const posts = server([
      { at: '2026-10-09 08:00Z', behaviour: null, text: 'about the whole app' },
      { at: '2026-10-09 09:00Z', behaviour: 'BEH-9', text: 'about another behaviour' },
    ])
    render(<NotesPanel app="demo" behaviour="BEH-2" />)
    expect(await within(region()).findByText('No open notes.')).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('Leave a note on BEH-2'), { target: { value: 'cover the empty list too' } })
    fireEvent.click(screen.getByRole('button', { name: 'Leave note' }))

    expect(await screen.findByText('cover the empty list too')).toBeInTheDocument()
    expect(posts).toEqual([{ text: 'cover the empty list too', behaviour: 'BEH-2' }])
    expect(screen.queryByText('about the whole app')).not.toBeInTheDocument()
    expect(screen.queryByText('about another behaviour')).not.toBeInTheDocument()
  })

  it('shows a refusal as the server worded it, and keeps what he typed', async () => {
    server([], 'a note is at most 20000 characters; this one is 20001')
    render(<NotesPanel app="demo" />)
    await screen.findByText('No open notes.')

    fireEvent.change(screen.getByLabelText('Leave a note'), { target: { value: 'too long, say' } })
    fireEvent.click(screen.getByRole('button', { name: 'Leave note' }))

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('a note is at most 20000 characters; this one is 20001'))
    expect(screen.getByLabelText('Leave a note')).toHaveValue('too long, say')
  })
})
