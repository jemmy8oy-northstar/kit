import { Link } from 'react-router-dom'
import { Badge, Card } from '@jemmy8oy-northstar/design-system'
import { ApiError, fetchTickets } from '../api/client'
import type { Ticket, TicketList } from '../api/types'
import { useResource } from '../hooks/useResource'
import ResourceView from './Resource'

/**
 * A project's open tickets (BEH-ORCH-16 slice 4): what the pipeline is working on, which ones are blocked and why,
 * and each one's thread, where Kit asks its questions. The operator's only: anyone else is refused, and sees nothing.
 */
export async function loadTickets(app: string): Promise<TicketList | null> {
  try {
    return await fetchTickets(app)
  } catch (e) {
    if (e instanceof ApiError && (e.status === 401 || e.status === 403)) return null
    throw e
  }
}

/** Blocked first, as they are waiting on him; then top priority first; merged ones are done, so off the list. */
export function openTickets(tickets: Ticket[]): Ticket[] {
  return tickets
    .filter((t) => t.state !== 'merged')
    .map((t, i) => ({ t, i }))
    .sort((x, y) => Number(!!y.t.blockedBecause) - Number(!!x.t.blockedBecause) || x.t.priority - y.t.priority || x.i - y.i)
    .map(({ t }) => t)
}

export default function Tickets({ app }: { app: string }) {
  const resource = useResource(() => loadTickets(app), [app])
  return (
    <ResourceView resource={resource}>
      {(list) => {
        if (!list || !list.on) return null
        const open = openTickets(list.tickets)
        return (
          <section aria-labelledby="board-tickets" className="tickets">
            <h2 id="board-tickets">
              Tickets <span className="board-count">{open.length}</span>
            </h2>
            {open.length === 0 && <p className="muted">No open tickets.</p>}
            <ul className="cards">
              {open.map((t) => (
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
                    {t.pullRequest && (
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
                  </Card>
                </li>
              ))}
            </ul>
          </section>
        )
      }}
    </ResourceView>
  )
}
