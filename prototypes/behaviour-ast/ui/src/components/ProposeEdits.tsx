import { useState } from 'react'
import { Button } from '@jemmy8oy-northstar/design-system'
import { proposeEdits } from '../api/client'
import type { ProposeResult } from '../api/types'

type State =
  | { state: 'idle' }
  | { state: 'sending' }
  | { state: 'refused'; message: string }
  | { state: 'done'; result: ProposeResult }

/**
 * The Commit button (kit#155): propose every pushed edit for merging, as one
 * pull request from the edits branch.
 *
 * Shown only after a write that was PUSHED, because that is the one state in
 * which there is something on the edits branch to propose. With write-back off
 * the server would refuse anyway, and a button that can only ever refuse is how
 * people learn to ignore buttons.
 *
 * Pressing it again is safe and says so: the server finds the pull request that
 * is already open instead of opening a second one, so "already open" renders as
 * a link to the same place, not as an error.
 *
 * A refusal is GitHub's own sentence where GitHub spoke ("No commits between dev
 * and kit/hosted"), and it reaches the screen whole, the same way a corpus
 * refusal does in `useWrite`.
 */
export default function ProposeEdits() {
  const [s, setS] = useState<State>({ state: 'idle' })

  async function send() {
    setS({ state: 'sending' })
    try {
      setS({ state: 'done', result: await proposeEdits() })
    } catch (e: unknown) {
      setS({ state: 'refused', message: e instanceof Error ? e.message : String(e) })
    }
  }

  if (s.state === 'done') {
    const { result } = s
    const what = result.alreadyOpen ? 'is already open and now carries this edit too' : 'is open for review'
    return (
      <p role="status">
        {result.url
          ? <a href={result.url}>Pull request #{result.number}</a>
          : <>Pull request #{result.number}</>}{' '}
        {what}.
      </p>
    )
  }

  return (
    <>
      <Button type="button" onClick={send} disabled={s.state === 'sending'}>
        Propose edits for merging
      </Button>
      {s.state === 'refused' && <p role="alert">{s.message}</p>}
    </>
  )
}
