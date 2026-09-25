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
 * The server sends `committed` on every write. It is read rather than assumed
 * here on purpose — and that has now paid off: kit#43 added the commit-and-push
 * James chose over a database (kit#41), and this line started telling the truth
 * about it on its own.
 *
 * 🔴 The state worth rendering loudly is neither success nor failure but the one
 * between: committed locally and NOT pushed. The edit is safe on a disk that is
 * about to be thrown away, and nothing else on the page would ever say so.
 *
 * ── Two kinds of write, and the difference is not cosmetic ──────────────────
 * A corpus write names a behaviour and touches this app's `.beh`. A BIND names
 * a noun and touches this app's `<app>.bindings.json`, beside it.
 *
 * ⚠️ THE SENTENCE BELOW USED TO BE AN ALARM AND IS NOW A FACT (kit#66). Bindings
 * were one flat map over every corpus, so a bind really did change what OTHER
 * projects generate, and this was the only place anyone would find out. James
 * ended that: *"Imagine scaled to 1000 projects and 1000 project owners no need
 * to share nouns."* A bind now reaches one corpus, so the same list means
 * something weaker and still worth reading — these other projects use the NAME,
 * and bind it for themselves.
 *
 * So it is a `status` rather than an `alert`. An alert for a thing that cannot
 * happen is how people learn to ignore alerts, and this component's whole job is
 * that the ones which remain are worth reading. ⚠️ Retiring `sharedWith` outright
 * would be a bigger change than the one he made — the information still helps
 * while you are naming things — so the wording moved and the mechanism did not.
 */
export default function WriteResultNote({ result }: { result: AnyWriteResult }) {
  const bind = 'noun' in result ? result : null
  return (
    <Card elevation="flat">
      <p role="status">
        Wrote <strong>{bind ? bind.noun : (result as { behaviour: string }).behaviour}</strong> to{' '}
        <code>{result.file}</code>.{' '}
        {result.committed
          ? result.pushed
            ? `Committed as ${result.commit} and pushed to ${result.branch}.`
            : `Committed as ${result.commit}.`
          : 'Not committed — Kit does not run git.'}
      </p>

      {result.warning && (
        <p role="alert">
          {result.warning} Your edit is safe in this Kit&rsquo;s working tree, but it has
          not reached the repository — it will be lost if this Kit restarts.
        </p>
      )}

      {bind && bind.sharedWith.length > 0 && (
        <p role="status">
          <code>{bind.noun}</code> is a name also used by {bind.sharedWith.join(', ')}, which bind
          it separately — this write reaches only this project.
        </p>
      )}

      {bind && bind.unreadableCorpora.length > 0 && (
        <p role="alert">
          {bind.unreadableCorpora.join(', ')} could not be parsed, so that list may be
          incomplete — this is &ldquo;could not look&rdquo;, not &ldquo;nothing else uses it&rdquo;.
        </p>
      )}

      {!result.committed && (
        <p className="muted">
          Review it as a working-tree diff (<code>git diff</code>) and commit it yourself.
        </p>
      )}
    </Card>
  )
}
