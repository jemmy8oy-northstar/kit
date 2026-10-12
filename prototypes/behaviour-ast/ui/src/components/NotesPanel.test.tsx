import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import NotesPanel from './NotesPanel'
import type { Note } from '../api/types'

function note(at: string, behaviour: string | null, text: string, more: Partial<Note> = {}): Note {
  return { at, kind: 'note', behaviour, resolves: null, text, thread: [], questions: [], ...more }
}

type Post = { url: string; body: { text?: string; behaviour?: string; index?: number; at?: string } }

/** A fake server holding one project's notes file, so a POST really changes what the next GET reads. */
function server(start: Note[] = [], refuse?: string) {
  const notes = [...start]
  const posts: Post[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method === 'POST') {
        const body = JSON.parse(String(init.body)) as Post['body']
        posts.push({ url, body })
        if (refuse) return { ok: false, status: 400, statusText: 'x', json: async () => ({ reason: refuse }) }
        if (url.endsWith('/notes/dismiss')) notes.splice(body.index!, 1)
        else if (url.endsWith('/notes/reply')) {
          const n = notes[body.index!]
          notes[body.index!] = { ...n, thread: [...n.thread, { kind: 'answer', at: '2026-10-10 20:41Z', text: body.text! }], questions: [] }
        } else notes.push(note('2026-10-10 20:41Z', body.behaviour ?? null, body.text!))
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
const asked = () =>
  note('2026-10-11 09:00Z', null, 'keep the order', {
    kind: 'request',
    thread: [{ kind: 'questions', at: null, text: '1. Which list?\n2. Per device?' }],
    questions: ['Which list?', 'Per device?'],
  })

describe('NotesPanel (kit#118)', () => {
  it('says there are no open notes rather than showing nothing', async () => {
    server()
    render(<NotesPanel app="demo" />)
    expect(await within(region()).findByText('No open notes.')).toBeInTheDocument()
  })

  it('lists every open note on the project page, with the behaviour each one names', async () => {
    server([
      note('2026-10-09 08:00Z', null, 'about the whole app'),
      note('2026-10-10 09:00Z', 'BEH-2', 'line one\nline two'),
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
    expect(posts.map((p) => p.body)).toEqual([{ text: 'the list should keep its order' }])
    expect(screen.getByRole('status')).toHaveTextContent('Wrote a note to behaviours/demo.notes.md. Committed as abc1234 and pushed to kit/hosted.')
    expect(screen.getByLabelText('Leave a note')).toHaveValue('')
  })

  it('on a behaviour page, tags the note with that behaviour and lists only its notes', async () => {
    const posts = server([
      note('2026-10-09 08:00Z', null, 'about the whole app'),
      note('2026-10-09 09:00Z', 'BEH-9', 'about another behaviour'),
    ])
    render(<NotesPanel app="demo" behaviour="BEH-2" />)
    expect(await within(region()).findByText('No open notes.')).toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('Leave a note on BEH-2'), { target: { value: 'cover the empty list too' } })
    fireEvent.click(screen.getByRole('button', { name: 'Leave note' }))

    expect(await screen.findByText('cover the empty list too')).toBeInTheDocument()
    expect(posts.map((p) => p.body)).toEqual([{ text: 'cover the empty list too', behaviour: 'BEH-2' }])
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

  // ---- BEH-NOTE-5..7 ----------------------------------------------------------------------

  it('puts a note with questions under "Needs your answer", numbered, and not in the open list too', async () => {
    server([note('2026-10-09 08:00Z', null, 'plain'), asked()])
    render(<NotesPanel app="demo" />)

    const needs = await screen.findByRole('region', { name: 'Needs your answer' })
    expect(within(needs).getByText('keep the order')).toBeInTheDocument()
    expect(within(needs).getByText('asks for a behaviour')).toBeInTheDocument()
    expect(within(needs).getAllByRole('listitem').map((li) => li.textContent)).toContain('Per device?')
    expect(within(region()).queryByText('keep the order')).not.toBeInTheDocument()
    expect(within(region()).getByText('1 open note')).toBeInTheDocument()
  })

  it('shows no "Needs your answer" at all when nothing is waiting', async () => {
    server([note('2026-10-09 08:00Z', null, 'plain')])
    render(<NotesPanel app="demo" />)
    await screen.findByText('plain')
    expect(screen.queryByRole('region', { name: 'Needs your answer' })).not.toBeInTheDocument()
  })

  it('answers a note by its place in the FILE and its date, then it leaves "Needs your answer"', async () => {
    // On a behaviour page the list is filtered, so the index on screen is not the index in the file.
    const posts = server([note('2026-10-09 08:00Z', null, 'about the app'), { ...asked(), behaviour: 'BEH-2' }])
    render(<NotesPanel app="demo" behaviour="BEH-2" />)

    const needs = await screen.findByRole('region', { name: 'Needs your answer' })
    const reply = within(needs).getByRole('button', { name: 'Reply' })
    expect(reply).toBeDisabled()
    fireEvent.change(within(needs).getByLabelText('Your answer'), { target: { value: 'today only' } })
    fireEvent.click(reply)

    await waitFor(() => expect(screen.queryByRole('region', { name: 'Needs your answer' })).not.toBeInTheDocument())
    expect(posts).toEqual([{ url: '/api/projects/demo/notes/reply', body: { index: 1, at: '2026-10-11 09:00Z', text: 'today only' } }])
    expect(within(region()).getByText(/You answered: today only/)).toBeInTheDocument()
  })

  it('dismisses a note in one tap, by its place and date', async () => {
    const posts = server([note('2026-10-09 08:00Z', null, 'first'), note('2026-10-09 09:00Z', null, 'second')])
    render(<NotesPanel app="demo" />)
    await screen.findByText('second')

    const card = screen.getByText('second').closest('li')!
    fireEvent.click(within(card).getByRole('button', { name: 'Dismiss' }))

    await waitFor(() => expect(screen.queryByText('second')).not.toBeInTheDocument())
    expect(screen.getByText('first')).toBeInTheDocument()
    expect(posts).toEqual([{ url: '/api/projects/demo/notes/dismiss', body: { index: 1, at: '2026-10-09 09:00Z' } }])
  })

  it('says why a dismissal was refused, on the card it was pressed on', async () => {
    server([note('2026-10-09 08:00Z', null, 'gone already')], 'note 0 left at 2026-10-09 08:00Z is no longer in demo.notes.md')
    render(<NotesPanel app="demo" />)
    await screen.findByText('gone already')

    fireEvent.click(screen.getByRole('button', { name: 'Dismiss' }))

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('is no longer in demo.notes.md'))
  })
})
