import { Link } from 'react-router-dom'
import { Badge, Button, Card } from '@jemmy8oy-northstar/design-system'
import { fetchSchedule, reschedule } from '../api/client'
import { useReloadableResource } from '../hooks/useResource'
import type { ScheduleEntry, ScheduleList } from '../api/types'
import Count from './Count'
import ResourceView from './Resource'
import { useWrite } from './useWrite'
import WriteResultNote from './WriteResultNote'

/**
 * BEH-SCHED-1..3 (kit#118, his second idea): *"a scheduled ordered queue distinct
 * from an unordered backlog"*. The order lives in `<app>.schedule`, one id per
 * line beside the corpus; every pending behaviour it does not list is backlog,
 * and the top still-pending entry is NEXT — the one an agent picks up. With
 * nothing scheduled nothing is next: it never falls back to the backlog.
 *
 * A project of several files (BEH-FEAT-5) has a schedule per file, because an id
 * only means something inside its own file. They share one Schedule region and
 * one Backlog region, each file under its own name, so the page still has one of
 * each to point at.
 */
export default function SchedulePanel({ apps }: { apps: string[] }) {
  const key = apps.join('\n')
  const { resource, reload } = useReloadableResource(() => Promise.all(apps.map(fetchSchedule)), [key])
  const { write, run } = useWrite(reload)
  const busy = write.state === 'saving'
  const change = (app: string, action: 'add' | 'remove' | 'up' | 'down', id: string) => run(() => reschedule(app, action, id))
  const named = apps.length > 1

  return (
    <ResourceView resource={resource}>
      {(lists) => (
        <>
          <section aria-labelledby="schedule">
            <h2 id="schedule">Schedule</h2>
            {lists.every((l) => l.scheduled.length === 0) ? (
              // BEH-SCHED-3: says so, and does not promote the backlog to fill the gap.
              <p className="muted">Nothing is scheduled, so nothing is next. Schedule a behaviour from the backlog.</p>
            ) : (
              lists
                .filter((l) => l.scheduled.length > 0)
                .map((l) => (
                  <Scheduled key={l.app} list={l} named={named} busy={busy} onChange={(action, id) => change(l.app, action, id)} />
                ))
            )}
            {write.state === 'refused' && <p role="alert">{write.message}</p>}
            {write.state === 'wrote' && <WriteResultNote result={write.result} subject="the schedule" />}
          </section>

          <section aria-labelledby="backlog">
            <h2 id="backlog">Backlog</h2>
            {lists.every((l) => l.backlog.length === 0) ? (
              <p className="muted">No pending behaviour is waiting to be scheduled.</p>
            ) : (
              lists
                .filter((l) => l.backlog.length > 0)
                .map((l) => (
                  <div key={l.app}>
                    {named && <h3>{l.app}</h3>}
                    <div className="badges">
                      <Count n={l.backlog.length} one="pending behaviour" />
                    </div>
                    <ul className="cards">
                      {l.backlog.map((b) => (
                        <li key={b.id}>
                          <Card elevation="flat">
                            <Title app={l.app} entry={b} />
                            <Button type="button" size="sm" disabled={busy} onClick={() => change(l.app, 'add', b.id)}>
                              Schedule
                            </Button>
                          </Card>
                        </li>
                      ))}
                    </ul>
                  </div>
                ))
            )}
          </section>
        </>
      )}
    </ResourceView>
  )
}

function Scheduled({
  list,
  named,
  busy,
  onChange,
}: {
  list: ScheduleList
  named: boolean
  busy: boolean
  onChange: (action: 'remove' | 'up' | 'down', id: string) => void
}) {
  const last = list.scheduled.length - 1
  return (
    <div>
      {named && <h3>{list.app}</h3>}
      <p className="muted">
        In <code>{list.file}</code>, first first.
      </p>
      <ol className="cards">
        {list.scheduled.map((s, i) => (
          <li key={s.id}>
            <Card elevation="flat">
              <Title app={list.app} entry={s} />
              <div className="badges">
                {s.next && <Badge tone="primary">next</Badge>}
                {/* BEH-SCHED-1: shown, not dropped — he decides whether it leaves the list. */}
                {s.state === 'done' && <Badge tone="success">built</Badge>}
                {s.state === 'missing' && <Badge tone="warning">no longer in the spec</Badge>}
              </div>
              <Button type="button" variant="ghost" size="sm" disabled={busy || i === 0} onClick={() => onChange('up', s.id)}>
                Move up
              </Button>
              <Button type="button" variant="ghost" size="sm" disabled={busy || i === last} onClick={() => onChange('down', s.id)}>
                Move down
              </Button>
              <Button type="button" variant="ghost" size="sm" disabled={busy} onClick={() => onChange('remove', s.id)}>
                Unschedule
              </Button>
            </Card>
          </li>
        ))}
      </ol>
    </div>
  )
}

/** The title leads and the id follows, quieter (BEH-TITLE-1); a vanished id has only its id. */
function Title({ app, entry }: { app: string; entry: ScheduleEntry }) {
  if (entry.title === null) return <h3 className="id">{entry.id}</h3>
  return (
    <h3>
      <Link to={`/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(entry.id)}`}>{entry.title}</Link>{' '}
      <span className="muted id">{entry.id}</span>
    </h3>
  )
}
