import { afterEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import ProposeEdits from './ProposeEdits'
import WriteResultNote from './WriteResultNote'
import type { WriteResult } from '../api/types'

/**
 * kit#155's Commit button. The server half is scored by the C# CommitRouteTests;
 * these pin the hop it cannot see: which request the press sends, and what the
 * person who pressed it is told.
 */

function answer(status: number, body: unknown) {
  const calls: { url: string; init: RequestInit }[] = []
  vi.stubGlobal('fetch', vi.fn(async (url: string, init: RequestInit) => {
    calls.push({ url, init })
    return { ok: status < 400, status, statusText: 'x', json: async () => body }
  }))
  return calls
}

afterEach(() => vi.unstubAllGlobals())

const pushed: WriteResult = {
  ok: true, app: 'snip-it', behaviour: 'BEH-1', file: 'behaviours/snip-it.beh',
  committed: true, pushed: true, commit: 'abc1234567', branch: 'kit/hosted', note: 'pushed',
}

describe('ProposeEdits', () => {
  it('POSTs /api/commit and links the pull request it opened', async () => {
    const calls = answer(200, { opened: true, alreadyOpen: false, number: 13, url: 'https://github.com/o/kit/pull/13' })
    render(<ProposeEdits />)

    fireEvent.click(screen.getByRole('button', { name: /Propose edits/ }))

    const link = await screen.findByRole('link', { name: 'Pull request #13' })
    expect(calls).toHaveLength(1)
    expect(calls[0].url).toBe('/api/commit')
    expect(calls[0].init.method).toBe('POST')
    expect(link).toHaveAttribute('href', 'https://github.com/o/kit/pull/13')
    expect(screen.getByRole('status')).toHaveTextContent(/is open for review/)
  })

  it('a second press reads as the SAME pull request, not a new one', async () => {
    answer(200, { opened: false, alreadyOpen: true, number: 12, url: 'https://github.com/o/kit/pull/12' })
    render(<ProposeEdits />)

    fireEvent.click(screen.getByRole('button'))

    expect(await screen.findByRole('status')).toHaveTextContent(/#12 is already open and now carries this edit too/)
  })

  it("a refusal shows GitHub's own sentence, and the button stays to try again", async () => {
    answer(409, { error: 'no-pull-request', reason: 'GitHub answered 422: Validation Failed — No commits between dev and kit/hosted' })
    render(<ProposeEdits />)

    fireEvent.click(screen.getByRole('button'))

    expect(await screen.findByRole('alert')).toHaveTextContent('No commits between dev and kit/hosted')
    expect(screen.getByRole('button')).toBeEnabled()
  })
})

describe('where the button appears', () => {
  it('after a PUSHED write', () => {
    render(<WriteResultNote result={pushed} />)

    expect(screen.getByRole('button', { name: /Propose edits/ })).toBeInTheDocument()
  })

  it('CONTROL: not after a write that reached no branch (git off, or committed and not pushed)', () => {
    const { unmount } = render(<WriteResultNote result={{ ...pushed, committed: false, pushed: undefined, commit: null, branch: null }} />)
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
    unmount()

    render(<WriteResultNote result={{ ...pushed, pushed: false, warning: 'did NOT reach origin' }} />)
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })
})
