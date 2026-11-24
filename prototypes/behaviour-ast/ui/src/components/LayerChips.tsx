import { Badge, Button } from '@jemmy8oy-northstar/design-system'
import { LAYERS } from '../board'

/**
 * What each layer is, as a tooltip (BEH-KIND-1). The chip keeps the card's word, so the two match;
 * "technical" is the logic under both, which his 2026-10-16 note called "logic implementation".
 */
const HELP: Record<string, string> = {
  ui: 'ui: how it looks',
  ux: 'ux: how it works for the person using it',
  technical: 'technical: logic implementation, the logic under both ui and ux',
}

/** BEH-KIND-1/2: the three layer chips, the board's and a project's alike. */
export default function LayerChips({ layers, onToggle }: { layers: string[]; onToggle: (layer: string) => void }) {
  return (
    <div className="board-controls" role="group" aria-label="Layer">
      {LAYERS.map((l) => (
        <Button
          key={l}
          type="button"
          size="sm"
          variant={layers.includes(l) ? 'secondary' : 'ghost'}
          aria-pressed={layers.includes(l)}
          aria-label={`Filter to the ${l} layer`}
          title={HELP[l]}
          onClick={() => onToggle(l)}
        >
          {l}
        </Button>
      ))}
    </div>
  )
}

/**
 * BEH-KIND-1: the layers of the behaviours beneath a feature, as tags, with the same words the
 * cards carry. Renders nothing when there are none.
 */
export function LayerTags({ layers, label = 'Layers' }: { layers: string[]; label?: string }) {
  if (!layers.length) return null
  return (
    <div className="badges" role="group" aria-label={label}>
      {layers.map((l) => (
        <Badge key={l} tone="neutral" title={HELP[l]}>
          {l}
        </Badge>
      ))}
    </div>
  )
}
