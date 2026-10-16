import { Button } from '@jemmy8oy-northstar/design-system'
import { LAYERS } from '../board'

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
          onClick={() => onToggle(l)}
        >
          {l}
        </Button>
      ))}
    </div>
  )
}
