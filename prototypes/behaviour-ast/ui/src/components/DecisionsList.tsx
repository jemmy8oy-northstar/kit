import { Badge } from '@jemmy8oy-northstar/design-system'
import type { Decision, DecisionPart } from '../api/types'

/** The order the page groups by (BEH-ARCH-1). */
const PARTS: { part: DecisionPart; label: string }[] = [
  { part: 'frontend', label: 'Frontend' },
  { part: 'backend', label: 'Backend' },
  { part: 'data', label: 'Data' },
  { part: 'deployment', label: 'Deployment' },
]

/**
 * A project's architecture decisions (BEH-ARCH-1, slice A: read-only), grouped by the part of
 * the system they belong to. A part with none gets no heading. Anything nobody has ruled on says
 * so, because a decision is my reading of the repo until he agrees.
 */
export default function DecisionsList({ decisions }: { decisions: Decision[] }) {
  return (
    <>
      {PARTS.map(({ part, label }) => {
        const own = decisions.filter((d) => d.part === part)
        if (own.length === 0) return null

        return (
          <section key={part} aria-label={label}>
            <h2>{label}</h2>
            <ul className="cards">
              {own.map((d) => (
                <li key={d.id}>
                  <strong>{d.title}</strong>
                  {d.review.state === 'unreviewed' && (
                    <>
                      {' '}
                      <Badge tone="warning">Unreviewed suggestion</Badge>
                    </>
                  )}
                  <p>Chosen: {d.chosen}</p>
                  {d.considered.length > 0 && (
                    <>
                      <p>Considered:</p>
                      <ul aria-label={`Considered for ${d.id}`}>
                        {d.considered.map((c) => (
                          <li key={c}>{c}</li>
                        ))}
                      </ul>
                    </>
                  )}
                  <p>Because:</p>
                  <ul aria-label={`Because for ${d.id}`}>
                    {d.because.map((b) => (
                      <li key={b}>{b}</li>
                    ))}
                  </ul>
                  <p className="muted">
                    {d.id} · {d.at}
                  </p>
                </li>
              ))}
            </ul>
          </section>
        )
      })}
    </>
  )
}
