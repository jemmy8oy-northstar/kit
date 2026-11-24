import { Fragment, useState } from 'react'
import { Link } from 'react-router-dom'
import { Badge, Button, Card } from '@jemmy8oy-northstar/design-system'
import { ApiError, fetchProject, replyOnTicket } from '../api/client'
import { useReloadableResource, useResource } from '../hooks/useResource'
import { featuresOfTicket, loadTickets, MAX_REPLY, openTickets, ticketColumns } from '../tickets'
import type { Behaviour } from '../api/types'
import ResourceView from './Resource'

/** The project's specs, for the features a ticket touches; none for someone the project is refused to. */
async function loadSpecs(app: string): Promise<Behaviour[]> {
  try {
    return (await fetchProject(app)).behaviours ?? []
  } catch (e) {
    if (e instanceof ApiError && (e.status === 401 || e.status === 403 || e.status === 404)) return []
    throw e
  }
}

/** BEH-ORCH-16 slice 3: his answer to what blocked a ticket, which wakes the first agent on it. */
function ReplyForm({ app, slug, onSent }: { app: string; slug: string; onSent: () => void }) {
  const [draft, setDraft] = useState('')
  const [state, setState] = useState<{ sending: boolean; said?: string; error?: string }>({ sending: false })

  async function send() {
    setState({ sending: true })
    try {
      const { woke } = await replyOnTicket(app, slug, draft.trim())
      setDraft('')
      setState({ sending: false, said: woke ? 'Sent. The first agent reads it next and updates the ticket.' : 'Added to the thread.' })
      onSent()
    } catch (e) {
      setState({ sending: false, error: e instanceof Error ? e.message : String(e) })
    }
  }

  return (
    <form
      className="ticket-reply"
      onSubmit={(e) => {
        e.preventDefault()
        if (draft.trim()) void send()
      }}
    >
      <textarea aria-label={`Reply on ${slug}`} rows={2} maxLength={MAX_REPLY} value={draft} onChange={(e) => setDraft(e.target.value)} />
      <Button type="submit" size="sm" disabled={state.sending || !draft.trim()}>
        Reply
      </Button>
      {state.said && <p role="status">{state.said}</p>}
      {state.error && <p role="alert">{state.error}</p>}
    </form>
  )
}

/**
 * A project's open tickets (BEH-ORCH-16 slice 4): what the pipeline is working on, which ones are blocked and why,
 * and each one's thread, where Kit asks its questions. The operator's only: anyone else is refused, and sees nothing.
 * A blocked ticket takes his reply under its thread (slice 3).
 */
export default function Tickets({ app }: { app: string }) {
  const { resource, reload } = useReloadableResource(() => loadTickets(app), [app])
  // BEH-TICKET-7: the features a ticket touches come from the project's specs, read once for the board.
  const detail = useResource(() => loadSpecs(app), [app])
  const behaviours = detail.state === 'ready' ? detail.value : []
  return (
    <ResourceView resource={resource}>
      {(list) => {
        if (!list || !list.on) return null
        const open = openTickets(list.tickets)
        const columns = ticketColumns(list.tickets)
        return (
          <section aria-labelledby="board-tickets" className="tickets">
            <h2 id="board-tickets">
              Tickets <span className="board-count">{open.length}</span>
            </h2>
            {open.length === 0 && <p className="muted">No open tickets.</p>}
            {columns.map((column) => (
              <section key={column.state} aria-label={column.state} data-state={column.state} className="ticket-column">
                <h4 className="muted">{column.state}</h4>
                <ul className="cards">
              {column.tickets.map((t) => (
                <li key={t.slug} data-ticket={t.slug}>
                  <Card className="board-card">
                    <h3>
                      {t.title} <span className="muted id">{t.slug}</span>
                    </h3>
                    {t.blockedBecause && <p data-testid="blocked-because">👤 {t.blockedBecause}</p>}
                    <div className="badges">
                      {t.blockedBecause && <Badge tone="warning">🚧 blocked</Badge>}
                      <Badge tone="neutral">{t.state}</Badge>
                      <Badge tone={t.priority === 1 ? 'danger' : 'neutral'}>P{t.priority}</Badge>
                    </div>
                    {featuresOfTicket(t, behaviours).length > 0 && (
                      <p aria-label="Features touched">
                        {featuresOfTicket(t, behaviours).map((id, i) => (
                          <Fragment key={id}>
                            {i > 0 && ', '}
                            <span>{id}</span>
                          </Fragment>
                        ))}
                      </p>
                    )}
                    {t.cards.length > 0 && (
                      <p>
                        {t.cards.map((id, i) => (
                          <span key={id}>
                            {i > 0 && ', '}
                            <Link to={`/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(id)}`}>{id}</Link>
                          </span>
                        ))}
                      </p>
                    )}
                    {t.pullRequest?.startsWith('https://') && (
                      <p>
                        <a href={t.pullRequest}>Pull request</a>
                      </p>
                    )}
                    {t.comments.length > 0 && (
                      <details open={!!t.blockedBecause}>
                        <summary>
                          {t.comments.length} comment{t.comments.length === 1 ? '' : 's'}
                        </summary>
                        <ol className="ticket-thread">
                          {t.comments.map((c, i) => (
                            <li key={i}>
                              <strong>{c.author}</strong>{' '}
                              <time className="muted" dateTime={c.at}>
                                {c.at.slice(0, 16).replace('T', ' ')} UTC
                              </time>
                              <p>{c.body}</p>
                            </li>
                          ))}
                        </ol>
                      </details>
                    )}
                    {t.blockedBecause && <ReplyForm app={app} slug={t.slug} onSent={reload} />}
                  </Card>
                </li>
              ))}
                </ul>
              </section>
            ))}
          </section>
        )
      }}
    </ResourceView>
  )
}
