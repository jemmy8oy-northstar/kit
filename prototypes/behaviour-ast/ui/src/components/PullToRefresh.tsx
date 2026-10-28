import { useEffect, useRef, useState } from 'react'
import { refreshAll, useLoading } from '../hooks/refresh'
import { isInstalledApp } from './installedApp'

/** How far, in px, a pull has to travel before letting go reloads. */
export const PULL_THRESHOLD = 70
/** The indicator stays at least this long, so a fast server still shows the reload happened. */
const MIN_SPIN_MS = 400

/**
 * BEH-LOADING-2: on the installed app, pulling down from the top of any page
 * re-reads that page's data from the server, without reloading the whole app.
 *
 * A pull only starts when the page is already scrolled to the top, so normal
 * scrolling never triggers it. Letting go short of PULL_THRESHOLD cancels.
 */
export default function PullToRefresh({ enabled = isInstalledApp() }: { enabled?: boolean }) {
  const [pull, setPull] = useState(0)
  const [spinning, setSpinning] = useState(false)
  const loading = useLoading()
  const startY = useRef<number | null>(null)
  const pullRef = useRef(0)
  const spunAt = useRef(0)

  useEffect(() => {
    if (!enabled) return
    const onStart = (e: TouchEvent) => {
      startY.current = window.scrollY <= 0 && e.touches.length === 1 ? e.touches[0].clientY : null
    }
    const onMove = (e: TouchEvent) => {
      if (startY.current === null) return
      const dy = e.touches[0].clientY - startY.current
      // Damped, so the indicator lags the finger the way the native one does.
      pullRef.current = dy > 0 ? Math.min(dy / 2, PULL_THRESHOLD * 1.5) : 0
      setPull(pullRef.current)
    }
    const onEnd = () => {
      if (startY.current !== null && pullRef.current >= PULL_THRESHOLD) {
        spunAt.current = Date.now()
        setSpinning(true)
        refreshAll()
      }
      startY.current = null
      pullRef.current = 0
      setPull(0)
    }
    window.addEventListener('touchstart', onStart, { passive: true })
    window.addEventListener('touchmove', onMove, { passive: true })
    window.addEventListener('touchend', onEnd)
    window.addEventListener('touchcancel', onEnd)
    return () => {
      window.removeEventListener('touchstart', onStart)
      window.removeEventListener('touchmove', onMove)
      window.removeEventListener('touchend', onEnd)
      window.removeEventListener('touchcancel', onEnd)
    }
  }, [enabled])

  // Stop spinning once the reads the pull started have all come back.
  useEffect(() => {
    if (!spinning || loading) return
    const wait = Math.max(0, MIN_SPIN_MS - (Date.now() - spunAt.current))
    const t = setTimeout(() => setSpinning(false), wait)
    return () => clearTimeout(t)
  }, [spinning, loading])

  if (!enabled || (pull === 0 && !spinning)) return null
  const ready = pull >= PULL_THRESHOLD
  return (
    <div
      role="status"
      className="pull-refresh"
      style={{ height: spinning ? PULL_THRESHOLD / 1.5 : pull }}
    >
      <span className="loading-line">
        {(spinning || ready) && <span className="spinner" aria-hidden="true" />}
        <span>{spinning ? 'Reloading…' : ready ? 'Release to reload' : 'Pull to reload'}</span>
      </span>
    </div>
  )
}
