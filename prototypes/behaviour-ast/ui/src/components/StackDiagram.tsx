import { stackParts, type RepoFile, type StackPart } from '../stackDiagram'

/** The parts a repo declares, grouped the way the diagram draws them (BEH-ARCH-4). Layout is still open: a plain list for now. */
const GROUPS: { kind: StackPart['kind']; label: string }[] = [
  { kind: 'database', label: 'Databases' },
  { kind: 'external-api', label: 'External APIs' },
  { kind: 'helm', label: 'Helm chart' },
]

/** Nothing is drawn for a project whose files are not known; a repo that declares none says so, and draws no part. */
export default function StackDiagram({ files }: { files?: RepoFile[] }) {
  if (files === undefined) return null
  const parts = stackParts(files)
  if (parts.length === 0) return <p className="muted">The repo declares no databases, external APIs or Helm chart.</p>

  return (
    <>
      {GROUPS.map(({ kind, label }) => {
        const own = parts.filter((p) => p.kind === kind)
        if (own.length === 0) return null
        return (
          <section key={kind} aria-label={label}>
            <h2>{label}</h2>
            <ul>
              {own.map((p) => (
                <li key={`${p.kind}:${p.name}`}>
                  {p.name}
                  {p.kind === 'database' && ` (${p.engine})`}
                  {p.kind === 'external-api' && ` (${p.url})`}
                  <span className="muted"> from {p.from}</span>
                </li>
              ))}
            </ul>
          </section>
        )
      })}
    </>
  )
}
