import { afterEach, describe, expect, it, vi } from 'vitest'
import { act, render, screen } from '@testing-library/react'
import PendingPanel from './PendingPanel'
import type { PendingView } from '../api/types'

/**
 * BEH-COMMIT-5. The server half (which commits, whose project, which pull
 * request) is scored by the C# PendingEditsTests; these pin what reaches the
 * screen, and that nothing pending shows nothing at all.
 */

function answer(views: Record<string, Partial<PendingView>>) {
  const calls: string[] = []
  vi.stubGlobal('fetch', vi.fn(async (url: string) => {
    calls.push(url)
    const app = decodeURIComponent(url.split('/')[3])
    const body: PendingView = { app, head: 'kit/hosted', base: 'dev', edits: [], pullNumber: null, pullUrl: null, reason: null, ...views[app] }
    return { ok: true, status: 200, statusText: 'OK', json: async () => body }
  }))
  return calls
}

afterEach(() => vi.unstubAllGlobals())

const edit = (sha: string, summary: string, behaviour: string | null = null) =>
  ({ sha, summary, app: 'snip-it', behaviour, when: null })

describe('PendingPanel', () => {
  it('lists the edits on kit/hosted not yet on dev, newest first, linking the pull request they wait in', async () => {
    const calls = answer({
      'snip-it': {
        edits: [edit('ccccccc', 'adjudicate BEH-ORDER-1', 'BEH-ORDER-1'), edit('aaaaaaa', 'add BEH-7', 'BEH-7')],
        pullNumber: 12,
        pullUrl: 'https://github.com/o/kit/pull/12',
      },
    })
    render(<PendingPanel apps={['snip-it']} />)

    const region = await screen.findByRole('region', { name: 'Pending' })
    expect(calls).toEqual(['/api/projects/snip-it/pending'])
    expect(region).toHaveTextContent('2 edits on kit/hosted not yet on dev')
    expect(screen.getByRole('link', { name: 'pull request #12' })).toHaveAttribute('href', 'https://github.com/o/kit/pull/12')
    const items = screen.getAllByRole('listitem').map((li) => li.textContent)
    expect(items).toEqual(['ccccccc adjudicate BEH-ORDER-1', 'aaaaaaa add BEH-7'])
  })

  it('says when the edits are not proposed yet', async () => {
    answer({ 'snip-it': { edits: [edit('aaaaaaa', 'add BEH-7')] } })
    render(<PendingPanel apps={['snip-it']} />)

    expect(await screen.findByRole('region', { name: 'Pending' })).toHaveTextContent('1 edit on kit/hosted not yet on dev, not proposed yet.')
  })

  it("a project of several files lists every file's edits", async () => {
    const calls = answer({
      kit: { edits: [edit('aaaaaaa', 'add BEH-1')] },
      'kit-ui': { edits: [edit('bbbbbbb', 'add BEH-UI-1')] },
    })
    render(<PendingPanel apps={['kit', 'kit-ui']} />)

    expect(await screen.findByRole('region', { name: 'Pending' })).toHaveTextContent('2 edits')
    expect(calls).toHaveLength(2)
  })

  it("one file that cannot be read does not hide another file's edits", async () => {
    vi.stubGlobal('fetch', vi.fn(async (url: string) => {
      if (url.includes('/gone/')) return { ok: false, status: 404, statusText: 'Not Found', json: async () => ({ error: 'no-such-project', reason: 'x' }) }
      const body: PendingView = { app: 'kit', head: 'kit/hosted', base: 'dev', edits: [edit('aaaaaaa', 'add BEH-1')], pullNumber: null, pullUrl: null, reason: null }
      return { ok: true, status: 200, statusText: 'OK', json: async () => body }
    }))
    render(<PendingPanel apps={['kit', 'gone']} />)

    expect(await screen.findByRole('region', { name: 'Pending' })).toHaveTextContent('1 edit')
  })

  it('a write elsewhere on the page (a new version) reads the list again', async () => {
    const calls = answer({ 'snip-it': { edits: [edit('aaaaaaa', 'add BEH-7')] } })
    const { rerender } = render(<PendingPanel apps={['snip-it']} version={0} />)
    await screen.findByRole('region', { name: 'Pending' })

    rerender(<PendingPanel apps={['snip-it']} version={1} />)

    await vi.waitFor(() => expect(calls).toHaveLength(2))
    expect(await screen.findByRole('region', { name: 'Pending' })).toHaveTextContent('1 edit')
  })

  it('CONTROL: nothing pending renders nothing — no heading, no empty card', async () => {
    const calls = answer({ 'snip-it': { reason: 'git write-back is off, so no edit is waiting on a branch' } })
    const { container } = render(<PendingPanel apps={['snip-it']} />)

    await vi.waitFor(() => expect(calls).toHaveLength(1))
    // Let the answer land: an empty DOM before it would prove nothing.
    await act(async () => {
      await new Promise((r) => setTimeout(r, 0))
    })
    expect(container).toBeEmptyDOMElement()
  })
})
