import { useState } from 'react'
import { ApiError } from '../api/client'
import type { AnyWriteResult } from '../api/types'

export type WriteState =
  | { state: 'idle' }
  | { state: 'saving' }
  | { state: 'refused'; message: string }
  | { state: 'wrote'; result: AnyWriteResult }

/**
 * One write, and the four things it can be doing.
 *
 * "refused" is its own state and is NOT folded into an error banner, because a
 * refusal from `writer.js` is the most useful thing the write path produces. Its
 * four refusals are all statements about the request — the behaviour is not in
 * this corpus, a step is one line, that id is already taken, the edit would have
 * changed a neighbouring behaviour — and each one tells him how to fix what he
 * typed. A page that rendered them as "something went wrong" would throw away
 * the only guidance the corpus can give.
 *
 * `onWrote` runs only on success, and it is where the caller re-reads the
 * project so the regenerated test appears.
 */
export function useWrite(onWrote: () => void) {
  const [write, setWrite] = useState<WriteState>({ state: 'idle' })

  async function run(send: () => Promise<AnyWriteResult>) {
    setWrite({ state: 'saving' })
    try {
      const result = await send()
      setWrite({ state: 'wrote', result })
      onWrote()
    } catch (e: unknown) {
      // An unreachable server and a corpus that will not take the edit are
      // different problems with different fixes, and `client.ts` already
      // distinguishes them: a null status means the fetch never landed. Both
      // reach the screen as a sentence either way — the one thing that must not
      // happen is a failed write looking like a successful one.
      const message = e instanceof ApiError || e instanceof Error ? e.message : String(e)
      setWrite({ state: 'refused', message })
    }
  }

  return { write, run, reset: () => setWrite({ state: 'idle' }) }
}
