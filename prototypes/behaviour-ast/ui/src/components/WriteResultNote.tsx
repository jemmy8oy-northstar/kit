import { Card } from '@jemmy8oy-northstar/design-system'
import type { WriteResult } from '../api/types'

/**
 * What a write did, and — the part that matters — what it deliberately did not
 * do.
 *
 * `docs/design/ui.md` decision 2 stops the writer at the working tree: Kit edits
 * the `.beh` file and never runs git, so the human reviews an ordinary diff and
 * commits it themselves. That boundary is only a guarantee to him if he can see
 * it holding, which is why the file path and `committed: false` are rendered on
 * every success rather than swallowed into a tick.
 *
 * The server sends `committed` on every write and it is always false. It is read
 * rather than assumed here on purpose: if a later slice ever adds option C
 * (write and commit), the page starts telling the truth about it without anyone
 * remembering to come back and change this text.
 */
export default function WriteResultNote({ result }: { result: WriteResult }) {
  return (
    <Card elevation="flat">
      <p role="status">
        Wrote <strong>{result.behaviour}</strong> to <code>{result.file}</code>.{' '}
        {result.committed ? 'Committed.' : 'Not committed — Kit does not run git.'}
      </p>
      <p className="muted">
        Review it as a working-tree diff (<code>git diff</code>) and commit it yourself.
      </p>
    </Card>
  )
}
