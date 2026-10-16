import { useCallback } from 'react'
import { useSearchParams } from 'react-router-dom'
import { LAYERS } from '../board'

/**
 * BEH-KIND-1/2: the layers chosen in the url (`?layer=ui,technical`), and a toggle that keeps
 * every other parameter. Empty means every layer; a word the corpus has none of is dropped.
 */
export function useLayers(): [string[], (layer: string) => void] {
  const [params, setParams] = useSearchParams()
  const layers = (params.get('layer') ?? '').split(',').filter((l) => LAYERS.includes(l))
  const toggle = useCallback(
    (layer: string) =>
      setParams((current) => {
        const out = new URLSearchParams(current)
        const now = (current.get('layer') ?? '').split(',').filter((l) => LAYERS.includes(l))
        const next = now.includes(layer) ? now.filter((l) => l !== layer) : [...now, layer]
        if (next.length) out.set('layer', next.join(','))
        else out.delete('layer')
        return out
      }),
    [setParams],
  )
  return [layers, toggle]
}
