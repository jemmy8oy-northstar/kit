import { Badge } from '@jemmy8oy-northstar/design-system'
import type { Principle } from '../api/types'

/**
 * A corpus's `# kit:principle` lines (kit#284 slice 5), in file order. Each says where it
 * lives, because the file is the only place one is changed; a note on the project is how
 * he asks for that change.
 */
export default function PrinciplesList({
  principles,
  label = 'Principles',
  badge = true,
}: {
  principles: Principle[]
  label?: string
  /** Off under a "Global" heading, where every row would say it again. */
  badge?: boolean
}) {
  if (principles.length === 0) return null

  return (
    <section aria-label={label}>
      <h2>{label}</h2>
      <ul className="cards">
        {principles.map((p) => (
          <li key={p.at}>
            <strong>{p.name}</strong>
            {badge && p.global && (
              <>
                {' '}
                <Badge tone="primary">global</Badge>
              </>
            )}
            <p>{p.text}</p>
            <p className="muted">{p.at}</p>
          </li>
        ))}
      </ul>
    </section>
  )
}
