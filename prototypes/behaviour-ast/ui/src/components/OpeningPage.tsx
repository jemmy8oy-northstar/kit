import { useEffect, useRef, useState } from 'react'
import { useLocation, useNavigate } from 'react-router-dom'
import { OPENING_PAGES, readOpeningPage, saveOpeningPage } from './openingPageStore'

/**
 * BEH-SETTINGS-2: sends Kit, opened at its root, on to the chosen page. Only once: after Kit is
 * open, the Projects link (also `/`) must still go to Projects. A root carrying a query is a
 * GitHub connect return (BEH-SETTINGS-1's GitHubReturn), and that wins.
 */
export function OpeningPage() {
  const { pathname, search } = useLocation()
  const navigate = useNavigate()
  const opened = useRef<{ pathname: string; search: string } | null>(null)
  opened.current ??= { pathname, search }
  const done = useRef(false)
  useEffect(() => {
    if (done.current) return
    done.current = true
    if (opened.current?.pathname !== '/' || opened.current.search !== '') return
    const target = readOpeningPage()
    if (target !== '/') navigate(target, { replace: true })
  }, [navigate])
  return null
}

/** The Settings section: which page Kit opens on, remembered in this browser. */
export function OpeningPageSetting() {
  const [page, setPage] = useState(readOpeningPage)
  const [saved, setSaved] = useState(false)

  return (
    <section aria-labelledby="opening-page">
      <h2 id="opening-page">Opening page</h2>
      <label>
        Kit opens on{' '}
        <select
          value={page}
          onChange={(e) => {
            setPage(e.target.value)
            saveOpeningPage(e.target.value)
            setSaved(true)
          }}
        >
          {OPENING_PAGES.map((p) => <option key={p.path} value={p.path}>{p.label}</option>)}
        </select>
      </label>
      {saved && <p role="status">Saved. Kit opens on {OPENING_PAGES.find((p) => p.path === page)?.label} in this browser.</p>}
      <p className="muted">Remembered in this browser only. A link to any other page still opens that page.</p>
    </section>
  )
}
