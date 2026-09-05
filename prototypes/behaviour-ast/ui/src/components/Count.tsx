import { Badge } from '@jemmy8oy-northstar/design-system'
import type { BadgeTone } from '@jemmy8oy-northstar/design-system'

/**
 * A count and its noun, pluralised. "1 conflicts" reads like a bug in the tool
 * that is reporting it, which is a bad thing for a tool whose whole pitch is
 * that it noticed something you did not.
 */
export default function Count({
  n,
  one,
  many,
  tone = 'neutral',
}: {
  n: number
  one: string
  many?: string
  tone?: BadgeTone
}) {
  return (
    <Badge tone={tone}>
      {n} {n === 1 ? one : (many ?? `${one}s`)}
    </Badge>
  )
}
