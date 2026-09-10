import { Card } from '@jemmy8oy-northstar/design-system'
import type { AnyWriteResult } from '../api/types'

/**
 * What a write did, and — the part that matters — what it deliberately did not
 * do.
 *
 * `docs/design/ui.md` decision 2 stops the writer at the working tree: Kit edits
 * the file and never runs git, so the human reviews an ordinary diff and
 * commits it themselves. That boundary is only a guarantee to him if he can see
 * it holding, which is why the file path and `committed: false` are rendered on
 * every success rather than swallowed into a tick.
 *
 * The server sends `committed` on every write and it is always false. It is read
 * rather than assumed here on purpose: if a later slice ever adds option C
 * (write and commit), the page starts telling the truth about it without anyone
 * remembering to come back and change this text.
 *
 * ── Two kinds of write, and the difference is not cosmetic ──────────────────
 * A corpus write names a behaviour and touches this app's `.beh`. A BIND names
 * a noun and touches `bindings.json`, which is one flat map over every corpus —
 * so it can have changed what OTHER projects generate, and the response says
 * which. That sentence is rendered here rather than left to the caller: this is
 * the component every write path already uses to report success, and a warning
 * that only appears when someone remembers to add it is the habit the mechanism
 * was built to replace.
 */
export default function WriteResultNote({ result }: { result: AnyWriteResult }) {
  const bind = 'noun' in result ? result : null
  return (
    <Card elevation="flat">
      <p role="status">
        Wrote <strong>{bind ? bind.noun : (result as { behaviour: string }).behaviour}</strong> to{' '}
        <code>{result.file}</code>.{' '}
        {result.committed ? 'Committed.' : 'Not committed — Kit does not run git.'}
      </p>

      {bind && bind.sharedWith.length > 0 && (
        <p role="alert">
          The noun namespace is global: <code>{bind.noun}</code> is also used by{' '}
          {bind.sharedWith.join(', ')}, which now generate against this binding too.
        </p>
      )}

      {bind && bind.unreadableCorpora.length > 0 && (
        <p role="alert">
          {bind.unreadableCorpora.join(', ')} could not be parsed, so that list may be
          incomplete — this is &ldquo;could not look&rdquo;, not &ldquo;nothing else uses it&rdquo;.
        </p>
      )}

      <p className="muted">
        Review it as a working-tree diff (<code>git diff</code>) and commit it yourself.
      </p>
    </Card>
  )
}
