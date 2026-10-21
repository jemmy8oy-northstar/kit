import { describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import NoteFab, { NOTE_WRITTEN } from './NoteFab'

/** Records every note posted, by URL, and lists two projects (one of which does not parse). */
function server() {
  const posts: { url: string; body: unknown }[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method === 'POST') {
        posts.push({ url, body: JSON.parse(String(init.body)) })
        return {
          ok: true,
          status: 200,
          statusText: 'OK',
          json: async () => ({ app: 'demo', behaviour: null, file: 'demo.notes.md', committed: true, pushed: true, commit: 'abc1234', branch: 'kit/hosted', note: 'x' }),
        }
      }
      expect(url).toMatch(/\/api\/projects$/)
      return { ok: true, status: 200, statusText: 'OK', json: async () => ({ projects: [{ app: 'demo' }, { app: 'other' }, { app: 'broken', error: 'no parse' }] }) }
    }),
  )
  return posts
}

function at(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <NoteFab />
    </MemoryRouter>,
  )
}

async function leave(text: string) {
  fireEvent.change(screen.getByRole('textbox'), { target: { value: text } })
  fireEvent.click(screen.getByRole('button', { name: 'Leave note' }))
  expect(await screen.findByText(/Committed|committed/)).toBeInTheDocument()
}

describe('NoteFab (BEH-NOTE-12)', () => {
  it("leaves a note on the project of the page it was opened from, and tells the page's notes to re-read", async () => {
    const posts = server()
    const heard = vi.fn()
    window.addEventListener(NOTE_WRITTEN, heard)
    at('/projects/demo/features/FEAT-X')

    fireEvent.click(screen.getByRole('button', { name: /Note/ }))
    expect(screen.getByLabelText('Note on demo')).toBeInTheDocument()
    await leave('the header is too tall')

    expect(posts).toEqual([{ url: expect.stringMatching(/\/api\/projects\/demo\/notes$/), body: { text: 'the header is too tall' } }])
    expect(heard).toHaveBeenCalledTimes(1)
    window.removeEventListener(NOTE_WRITTEN, heard)
  })

  it("tags the note with the behaviour on a behaviour's page", async () => {
    const posts = server()
    at('/projects/demo/behaviours/BEH-1')

    fireEvent.click(screen.getByRole('button', { name: /Note/ }))
    await leave('wrong word')

    expect(posts[0].body).toEqual({ text: 'wrong word', behaviour: 'BEH-1' })
  })

  it('asks which project on a page that has none, and sends nothing until one is chosen', async () => {
    const posts = server()
    at('/board')

    fireEvent.click(screen.getByRole('button', { name: /Note/ }))
    const picker = screen.getByLabelText('Which project is this about?')
    expect(await screen.findByRole('option', { name: 'other' })).toBeInTheDocument()
    expect(screen.queryByRole('option', { name: 'broken' })).toBeNull()

    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'an idea' } })
    expect(screen.getByRole('button', { name: 'Leave note' })).toBeDisabled()

    fireEvent.change(picker, { target: { value: 'other' } })
    fireEvent.click(screen.getByRole('button', { name: 'Leave note' }))
    expect(await screen.findByText(/Committed|committed/)).toBeInTheDocument()
    expect(posts).toEqual([{ url: expect.stringMatching(/\/api\/projects\/other\/notes$/), body: { text: 'an idea' } }])
  })

  it('says, at the box, that a note is published before anything is sent (BEH-GATE-H2)', () => {
    server()
    at('/projects/demo')
    fireEvent.click(screen.getByRole('button', { name: /Note/ }))
    expect(screen.getByRole('textbox')).toHaveAccessibleDescription(/anyone can read/)
  })
})
