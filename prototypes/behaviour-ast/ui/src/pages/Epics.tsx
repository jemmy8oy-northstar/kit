import { Link } from 'react-router-dom'
import { Badge, Card } from '@jemmy8oy-northstar/design-system'
import { useResource } from '../hooks/useResource'
import ResourceView from '../components/Resource'
import BoardViews from '../components/BoardViews'
import { hueOf } from '../board'
import { epicCards, isOpenEpic, STATUS_STATES, ticketsByEpic, ticketStatus, type EpicCard, type TicketStatus } from '../epics'
import { epicHref, featureHref } from '../features'
import { loadRealProjects } from '../realProjects'
import { loadTickets } from '../tickets'

/** An epic card with its ticket counts; `open` is the epic still has work beneath it (BEH-TICKET-8). */
type Loaded = EpicCard & { status: TicketStatus; open: boolean }

/**
 * Every real project's epics, with what is built beneath the features in each (BEH-EPIC-4), and the state of the
 * tickets that name each one (BEH-TICKET-8). A project's goal, its first open epic, leads that project's epics.
 */
async function loadEpics() {
  const { read, failed } = await loadRealProjects()
  const perProject = await Promise.all(
    read.map(async ({ app, project, detail }) => {
      const features = detail.features ?? []
      const behaviours = detail.behaviours ?? []
      const list = await loadTickets(app)
      const byEpic = ticketsByEpic(list?.on ? list.tickets : [], features, behaviours)
      const cards: Loaded[] = epicCards(app, project, detail.epics ?? [], features, behaviours).map((card) => ({
        ...card,
        status: ticketStatus(byEpic.get(card.epic.id) ?? []),
        open: isOpenEpic(card),
      }))
      const goal = cards.findIndex((c) => c.open)
      return goal > 0 ? [cards[goal], ...cards.slice(0, goal), ...cards.slice(goal + 1)] : cards
    }),
  )
  return { cards: perProject.flat(), failed }
}

/** BEH-TICKET-8: an open epic's tickets by state, with the blocked ones beside them, not added to them. */
function TicketStatusList({ epicId, status }: { epicId: string; status: TicketStatus }) {
  const total = STATUS_STATES.reduce((n, s) => n + status.states[s], 0)
  if (total === 0) return <p className="muted">No ticket names this epic yet.</p>
  return (
    <ul aria-label={`Tickets for ${epicId}`} className="ticket-status">
      {STATUS_STATES.map((s) => (
        <li key={s}>
          <span>{s}</span> <span>{status.states[s]}</span>
        </li>
      ))}
      <li>
        <span>blocked</span> <span>{status.blocked}</span>
      </li>
    </ul>
  )
}

/**
 * BEH-EPIC-4 (his 2026-10-19 16:44Z note): "just another abstraction level in the boards for epic",
 * above the feature view. Each epic shows how much of what is tagged into it is built.
 */
export default function Epics() {
  const loaded = useResource(loadEpics, [])
  return (
    <>
      <h1>Board</h1>
      <BoardViews />
      <p className="muted">
        Every epic, with how many behaviours are built beneath the features tagged into it. A feature
        joins an epic with an <code>in EPIC-…</code> line.
      </p>
      <ResourceView resource={loaded}>
        {({ cards, failed }) => (
          <>
            {failed.map((f) => (
              <Card key={f.app} elevation="flat">
                <h2>{f.app}</h2>
                <Badge tone="danger">could not be read</Badge>
                <p role="alert">{f.error}</p>
              </Card>
            ))}
            <div role="region" aria-label="Epics">
              {cards.length === 0 && <p className="muted">No project declares an epic yet.</p>}
              <ul className="cards">
                {cards.map(({ app, project, epic, features, built, total, status, open }) => (
                  <li key={`${app}/${epic.id}`}>
                    <Card className={`board-card hue-${hueOf(project)}`}>
                      <h3>
                        <Link to={epicHref(app, epic.id)}>{epic.title}</Link> <span className="muted id">{epic.id}</span>
                      </h3>
                      <p>
                        {built} of {total} behaviours built
                      </p>
                      {features.length === 0 ? (
                        <p className="muted">No feature is in it yet.</p>
                      ) : (
                        <ul aria-label={`Features in ${epic.id}`}>
                          {features.map((f) => (
                            <li key={f.id}>
                              <Link to={featureHref(app, f.id, [])}>{f.title}</Link>
                            </li>
                          ))}
                        </ul>
                      )}
                      {open && <TicketStatusList epicId={epic.id} status={status} />}
                      <p className="muted">{project === app ? app : `${project} · ${app}`}</p>
                    </Card>
                  </li>
                ))}
              </ul>
            </div>
          </>
        )}
      </ResourceView>
    </>
  )
}
