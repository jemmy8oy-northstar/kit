import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { Badge, Button, Card } from '@jemmy8oy-northstar/design-system'
import { useResource } from '../hooks/useResource'
import { loadBoard } from '../boardLoad'
import BoardViews from '../components/BoardViews'
import LayerChips from '../components/LayerChips'
import ResourceView from '../components/Resource'
import { byCompletion, byPriorityAcrossProjects, CAP, columnOf, hueOf, isDenied, isIdea, isQuestion, isRung, isTrial, LAYERS, ownerOf, pausedLast, projectOf, questionsFirst, RANKED, rungFirst, tierCounts, tierOf, type Column } from '../board'

const COLUMNS: { id: Column; title: string }[] = [
  // First, beside Proposed: both are the agent waiting on him (kit#284).
  { id: 'review', title: 'Review requested' },
  { id: 'proposed', title: 'Proposed' },
  { id: 'backlog', title: 'Backlog' },
  // BEH-BOARD-18: ready to build, then waiting on him, then started.
  { id: 'queue', title: 'Queue' },
  { id: 'blocked', title: 'Blocked' },
  { id: 'in-progress', title: 'In progress' },
  { id: 'done', title: 'Done' },
  // BEH-BOARD-21: nothing is deleted by a denial, so the card stays findable here.
  { id: 'denied', title: 'Denied' },
]

/** The board (kit#237): what the corpora say about where every behaviour stands. */
export default function Board() {
  const entries = useResource(loadBoard, [])
  const [params, setParams] = useSearchParams()
  // How many cards each column shows (BEH-BOARD-7); a column not in here shows the first CAP.
  const [limits, setLimits] = useState<Partial<Record<Column, number>>>({})

  // The filter lives in the url so a view can be shared (BEH-BOARD-2).
  const selected = (params.get('project') ?? '').split(',').filter(Boolean)
  const showTrials = params.get('trials') === '1'
  // BEH-KIND-1 (his 2026-10-16 note): "filter the different types ui, ux and logic". The type is
  // the corpus's existing `layer` line, so the filter reads it rather than inventing a tag.
  const layers = (params.get('layer') ?? '').split(',').filter((l) => LAYERS.includes(l))
  // BEH-BOARD-15 (his 2026-10-22 11:30Z note): the card he last opened, and the column it was in.
  // The board is rebuilt when he comes back, and on a phone that put him on the first column.
  const back = { col: params.get('col'), card: params.get('card') }
  const board = useRef<HTMLDivElement>(null)
  const restored = useRef(false)

  // Once, when the cards are drawn: the card if it is still in that column, else the column, so a
  // card he approved out of Proposed leaves him in Proposed, not following it to Backlog.
  useEffect(() => {
    if (restored.current || entries.state !== 'ready' || !back.col) return
    restored.current = true
    const section = board.current?.querySelector<HTMLElement>(`section[data-col="${back.col}"]`)
    const card = [...(section?.querySelectorAll<HTMLElement>('[data-card]') ?? [])].find((el) => el.dataset.card === back.card)
    const target = card ?? section
    // jsdom has no scrollIntoView, and nor do some old browsers; the focus still lands.
    target?.scrollIntoView?.({ block: card ? 'center' : 'nearest', inline: 'start' })
    target?.querySelector<HTMLElement>('a, h2')?.focus({ preventScroll: true })
  }, [entries.state, back.col, back.card])

  /** Kept in the url the card's Back button returns to, replacing it so Back is not doubled. */
  function remember(col: Column, card: string) {
    setParams(
      (p) => {
        p.set('col', col)
        p.set('card', card)
        return p
      },
      { replace: true },
    )
  }

  function update(next: { project?: string[]; trials?: boolean; layer?: string[] }) {
    const project = next.project ?? selected
    const trials = next.trials ?? showTrials
    const layer = next.layer ?? layers
    const out = new URLSearchParams()
    if (project.length) out.set('project', project.join(','))
    if (layer.length) out.set('layer', layer.join(','))
    if (trials) out.set('trials', '1')
    setParams(out)
  }

  function toggleLayer(layer: string) {
    update({ layer: layers.includes(layer) ? layers.filter((l) => l !== layer) : [...layers, layer] })
  }

  function toggle(app: string) {
    update({ project: selected.includes(app) ? selected.filter((a) => a !== app) : [...selected, app] })
  }

  return (
    <>
      <h1>Board</h1>
      <BoardViews />
      <p className="muted">
        Every behaviour by where it stands: an inference nobody has adjudicated is proposed; a
        pending one is in the queue, ready to build, unless it is paused (backlog) or waiting on
        your context (blocked), and in progress at the top of its schedule; the rest is done, the
        most recently completed first. A card whose bell is rung sits at the
        top of its column until Kit answers it.
      </p>

      <ResourceView resource={entries}>
        {(all) => {
          // Ideas are never work, so they are off the board whatever the filter (kit#284 slice 4).
          const shown = all.filter(
            (e) =>
              !isIdea(e.summary) &&
              (showTrials || !isTrial(e.summary)) &&
              (selected.length === 0 || selected.includes(projectOf(e.summary))),
          )
          // One button per project, not per file: files naming one `# kit:project` share it (kit#237).
          const projects = [
            ...new Set(all.filter((e) => !isIdea(e.summary) && (showTrials || !isTrial(e.summary))).map((e) => projectOf(e.summary))),
          ]
          const failed = shown.filter((e) => e.error)
          return (
            <>
              <div className="board-controls">
                {projects.map((p) => (
                  <Button
                    key={p}
                    type="button"
                    size="sm"
                    variant={selected.includes(p) ? 'secondary' : 'ghost'}
                    aria-pressed={selected.includes(p)}
                    aria-label={`Filter to ${p}`}
                    onClick={() => toggle(p)}
                  >
                    <span className={`dot hue-${hueOf(p)}`} aria-hidden="true" />
                    {p}
                  </Button>
                ))}
                <Button
                  type="button"
                  size="sm"
                  variant={showTrials ? 'secondary' : 'ghost'}
                  aria-pressed={showTrials}
                  onClick={() => update({ trials: !showTrials })}
                >
                  Show trials
                </Button>
              </div>
              <LayerChips layers={layers} onToggle={toggleLayer} />

              {failed.map((e) => (
                <Card key={e.summary.app} elevation="flat">
                  <h2>{e.summary.app}</h2>
                  <Badge tone="danger">could not be read</Badge>
                  <p role="alert">{e.error}</p>
                </Card>
              ))}
              {shown
                .filter((e) => e.scheduleError)
                .map((e) => (
                  <Card key={`${e.summary.app}/schedule`} elevation="flat">
                    <h2>{e.summary.app}</h2>
                    <Badge tone="warning">schedule could not be read — its pending behaviours show in the queue</Badge>
                    <p role="alert">{e.scheduleError}</p>
                  </Card>
                ))}

              <div className="board" ref={board}>
                {COLUMNS.map((col) => {
                  // Done most recently completed first, across files (BEH-BOARD-4); every column before
                  // In progress by priority, across files (BEH-BOARD-5/6/18), newest first within a
                  // priority (BEH-BOARD-12); in progress in corpus order.
                  let cards = shown.flatMap((e) =>
                    e.behaviours
                      .filter(
                        (b) =>
                          (col.id === 'denied' ? isDenied(b) : columnOf(b, e.places.get(b.id)) === col.id) &&
                          (layers.length === 0 || layers.includes(b.layer)),
                      )
                      .map((b) => {
                        const rung = isRung(b, e.bells)
                        return {
                          app: e.summary.app,
                          project: projectOf(e.summary),
                          b,
                          at: e.historyMissing === null ? (col.id === 'done' ? e.completed : e.added).get(b.id) : undefined,
                          rung,
                          owner: ownerOf(b, col.id, rung),
                        }
                      }),
                  )
                  // Denied: the API gives no denial time, so it takes the order of `added` (when the card was
                  // added, newest first, as Done reads completion); a denied card has no better date.
                  if (col.id === 'done' || col.id === 'denied') cards.sort((x, y) => byCompletion(x.at, y.at))
                  // Rung cards above every priority (BEH-BELL-3), until the agent answers the bell; then the
                  // agent's questions above requests that only want a look (BEH-BOARD-11).
                  if (RANKED.includes(col.id)) cards = rungFirst(questionsFirst(pausedLast(byPriorityAcrossProjects(cards))))
                  const unordered = col.id === 'done' ? shown.filter((e) => !e.error && e.historyMissing !== null) : []
                  // Capped after sorting, so what is cut is always the bottom of the order (BEH-BOARD-7).
                  // Never cut the card he is coming back to (BEH-BOARD-15): show as many pages as reach it.
                  const at = back.col === col.id ? cards.findIndex((c) => `${c.app}/${c.b.id}` === back.card) : -1
                  const limit = Math.max(limits[col.id] ?? CAP, (Math.floor(at / CAP) + 1) * CAP)
                  const hidden = Math.max(0, cards.length - limit)
                  // A heading starts each run of a ranked column (BEH-BOARD-14), so the order can be read.
                  const tiers = RANKED.includes(col.id) ? tierCounts(cards) : null
                  return (
                    <section key={col.id} aria-labelledby={`board-${col.id}`} data-col={col.id}>
                      <h2 tabIndex={-1}>
                        <span id={`board-${col.id}`}>{col.title}</span> <span className="board-count">{cards.length}</span>
                      </h2>
                      {unordered.length > 0 && (
                        // Said, not silent: without a history these cards are in corpus order, not by date.
                        <p className="muted board-note">
                          Not ordered by date: {unordered.map((e) => `${e.summary.app} (${e.historyMissing})`).join('; ')}
                        </p>
                      )}
                      <ul className="cards">
                        {cards.slice(0, limit).flatMap(({ app, project, b, rung, owner }, i) => [
                          ...(tiers && (i === 0 || tierOf(cards[i - 1]) !== tierOf(cards[i]))
                            ? [
                                <li key={`tier/${tierOf(cards[i])}`} className="board-tier" role="none">
                                  {tierOf(cards[i])} <span className="board-count">{tiers.get(tierOf(cards[i]))}</span>
                                </li>,
                              ]
                            : []),
                          <li key={`${app}/${b.id}`} data-card={`${app}/${b.id}`}>
                            <Card className={`board-card hue-${hueOf(project)}`}>
                              <h3>
                                <Link
                                  to={`/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(b.id)}`}
                                  onClick={() => remember(col.id, `${app}/${b.id}`)}
                                >
                                  {b.title}
                                </Link>{' '}
                                <span className="muted id">{b.id}</span>
                              </h3>
                              {b.review.state === 'requested' && (
                                // What the agent wants looked at, so the card can be triaged unopened.
                                <p>{b.review.note}</p>
                              )}
                              {col.id === 'denied' && (
                                // BEH-BOARD-21: his reason, shown on the card so it can be found and revised.
                                <p data-testid="denied-reason">{b.review.note}</p>
                              )}
                              {b.needsContext && (
                                // BEH-BOARD-17: what he must add, shown not hovered: he reads the board on a phone.
                                <p data-testid="needs-context">👤 {b.needsContext}</p>
                              )}
                              {col.id === 'done' && b.onDev && (
                                // BEH-BOARD-22: built on the project's dev, so not yet something he can see live.
                                <p data-testid="on-dev">🧪 On dev, not live yet: ships with {b.onDev}</p>
                              )}
                              <div className="badges">
                                {rung &&<Badge tone="warning">🔔 rung</Badge>}
                                {isQuestion(b) && <Badge tone="primary">❓ question</Badge>}
                                {b.paused && <Badge tone="neutral">⏸ paused</Badge>}
                                {/* BEH-BOARD-17: whose move it is. */}
                                {owner === 'james' && <Badge tone="primary">👤 yours</Badge>}
                                {owner === 'agent' && <Badge tone="neutral">🤖 agent's</Badge>}
                                {b.priority !== undefined && (
                                  <Badge tone={b.priority === 1 ? 'danger' : 'neutral'}>P{b.priority}</Badge>
                                )}
                                <Badge tone="neutral">{b.layer}</Badge>
                              </div>
                              <p className="muted">{project === app ? app : `${project} · ${app}`}</p>
                            </Card>
                          </li>,
                        ])}
                      </ul>
                      {hidden > 0 && (
                        <Button
                          type="button"
                          variant="ghost"
                          onClick={() => setLimits({ ...limits, [col.id]: limit + CAP })}
                        >
                          Show {Math.min(CAP, hidden)} more ({hidden} hidden)
                        </Button>
                      )}
                    </section>
                  )
                })}
              </div>
            </>
          )
        }}
      </ResourceView>
    </>
  )
}
