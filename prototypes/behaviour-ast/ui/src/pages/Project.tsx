import { useCallback, useId, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { Badge, Button, Card } from '@jemmy8oy-northstar/design-system'
import { askForBehaviour, fetchNotes, leaveNote, resolveDecision } from '../api/client'
import { filesOf } from '../projectFiles'
import { useReloadableResource } from '../hooks/useResource'
import type { Behaviour, Conflict, Feature, Note, ProjectDetail, Question } from '../api/types'
import { behavioursUnder, featureHref, ofLayers, topLevel, withAnyUnder, type Tagged } from '../features'
import { citationsOf } from '../notes'
import { asksOf, type Ask } from '../asks'
import CoverageBadge from '../components/CoverageBadge'
import LayerChips from '../components/LayerChips'
import { useLayers } from '../hooks/useLayers'
import Count from '../components/Count'
import NotesPanel from '../components/NotesPanel'
import PendingPanel from '../components/PendingPanel'
import DecisionsList from '../components/DecisionsList'
import PrinciplesList from '../components/PrinciplesList'
import PublishedHint from '../components/PublishedHint'
import ResourceView from '../components/Resource'
import SchedulePanel from '../components/SchedulePanel'
import { useWrite } from '../components/useWrite'
import { WriteFeedback } from './BehaviourPage'

/** Step 1 of his loop, one level down: the corpus, what conflicts, what nobody reviewed. */
export default function Project() {
  const { app = '' } = useParams()
  const { resource } = useReloadableResource(() => filesOf(app), [app])

  return (
    <>
      <p className="crumbs">
        <Link to="/">Projects</Link> / {app}
      </p>

      <ResourceView resource={resource}>
        {(files) =>
          files.length === 1 && files[0].app === app ? (
            <Detail project={files[0]} />
          ) : (
            <OneProject name={app} files={files} />
          )
        }
      </ResourceView>
    </>
  )
}

function Detail({ project }: { project: ProjectDetail }) {
  const unreviewed = new Set(project.adjudication.unreviewed)
  const [layers, toggleLayer] = useLayers()
  const shown = ofLayers(project, layers)
  const top = topLevel(shown)
  const features = withAnyUnder(shown, top.features, layers)
  const [notes, bump] = useNotesVersion()
  const [tab, select] = useTab()
  const [open, setOpen] = useState<number | null>(null)

  return (
    <>
      <h1>{project.app}</h1>
      <p className="muted">{project.corpus}</p>
      <div className="badges">
        <Count n={project.behaviours.length} one="behaviour" tone="primary" />
        <CoverageBadge coverage={project.coverage} />
        <Badge tone="neutral">{project.adjudication.defined} defined</Badge>
        {project.adjudication.inferred ? (
          <Badge tone="warning">{project.adjudication.inferred} inferred</Badge>
        ) : null}
      </div>

      {/* Above the tabs on purpose (kit#118, BEH-FEAT-7): a thought is caught before it is lost, not after picking a tab. */}
      <AskForBehaviour app={project.app} onAsked={() => (bump(), select('notes'))} />

      <ProjectTabs
        tab={tab}
        select={select}
        waiting={asksOf([project]).length + project.conflicts.length + project.questions.length}
        open={open}
        notes={
          <>
            <NotesPanel app={project.app} version={notes} citations={citationsOf(project.app, [project])} onOpenCount={setOpen} />
            <PendingPanel apps={[project.app]} version={notes} />
          </>
        }
        schedule={<SchedulePanel apps={[project.app]} />}
        principles={project.principles?.length ? <PrinciplesList principles={project.principles} /> : null}
        architecture={project.decisions?.length ? <DecisionsList decisions={project.decisions.map((d) => ({ ...d, app: project.app }))} behaviours={project.behaviours.map((b) => ({ ...b, app: project.app }))} /> : null}
        features={
          <>
            <LayerChips layers={layers} onToggle={toggleLayer} />
            <NoneOfLayers layers={layers} empty={shown.behaviours.length === 0} />
            {/* BEH-FEAT-1: the top level is everything with no `in` line. A corpus with no
                features has no section here and lists every behaviour, as it always did. */}
            {features.length > 0 && (
              <section aria-label="Features">
                <FeatureCards app={project.app} project={shown} features={features} path={[]} layers={layers} heading={false} />
              </section>
            )}

            <section>
              {/* With every behaviour tagged, an empty heading would read as "nothing here". */}
              {(features.length === 0 || top.behaviours.length > 0) && shown.behaviours.length > 0 && (
                <h2>{features.length > 0 ? 'Behaviours in no feature' : 'Behaviours'}</h2>
              )}
              <ul className="cards">
                {top.behaviours.map((b) => (
                  <li key={b.id}>
                    <BehaviourRow app={project.app} behaviour={b} unreviewed={unreviewed.has(b.id)} />
                  </li>
                ))}
              </ul>
            </section>
          </>
        }
        questions={
          <>
            <KitAsks asks={asksOf([project])} notesApp={project.app} version={notes} onAnswered={bump} />
            <Conflicts conflicts={project.conflicts} />
            <QuestionSheet sheets={[project]} notesApp={project.app} onResolved={bump} />
          </>
        }
      />
    </>
  )
}

type Tab = 'features' | 'questions' | 'notes' | 'schedule' | 'principles' | 'architecture'
const TABS: Tab[] = ['features', 'questions', 'notes', 'schedule', 'principles', 'architecture']

/**
 * The tab is in the url (`?tab=questions`) so a link can open on it and the back button
 * undoes a switch; features, the default, keeps the url bare. Shared by the page and its
 * tabs so a form above them can open one (BEH-FEAT-7: asking opens Notes).
 */
function useTab(): [Tab, (next: Tab) => void] {
  const [params, setParams] = useSearchParams()
  const asked = params.get('tab') as Tab | null
  const tab: Tab = asked && TABS.includes(asked) ? asked : 'features'
  const select = useCallback(
    (next: Tab) => {
      setParams((current) => {
        const out = new URLSearchParams(current)
        if (next === 'features') out.delete('tab')
        else out.set('tab', next)
        return out
      })
    },
    [setParams],
  )
  return [tab, select]
}

/**
 * BEH-FEAT-6 (kit#118, his note 2026-10-12 20:49Z): "Maybe features and questions should be
 * under a tab view, default to features". BEH-FEAT-7 (his note 2026-10-16 07:13Z): "Let's
 * leverage the tab view more" — so notes, the schedule and principles are tabs too, and above
 * them is only the title and the describe box. What the project IS comes first; what is
 * waiting on him carries a count so it cannot hide behind a tab.
 *
 * Every panel stays mounted and the others are `hidden`, so a resolution half-typed into a
 * decision card survives a look at the features. A project with no principles has no tab.
 */
function ProjectTabs({
  tab,
  select,
  waiting,
  open,
  features,
  questions,
  notes,
  schedule,
  principles,
  architecture,
}: {
  tab: Tab
  select: (next: Tab) => void
  waiting: number
  /** Open notes, once the list has loaded (null until then). */
  open: number | null
  features: React.ReactNode
  questions: React.ReactNode
  notes: React.ReactNode
  schedule: React.ReactNode
  principles: React.ReactNode | null
  /** BEH-ARCH-1: a project with no `decision` blocks has no tab. */
  architecture: React.ReactNode | null
}) {
  const id = useId()

  const tabs: { key: Tab; label: React.ReactNode; panel: React.ReactNode }[] = [
    { key: 'features', label: 'Features', panel: features },
    {
      key: 'questions',
      label: (
        <>
          Questions {waiting > 0 && <Badge tone="warning">{waiting}</Badge>}
        </>
      ),
      panel: waiting > 0 ? questions : <p className="muted">Nothing is waiting on you.</p>,
    },
    {
      key: 'notes',
      label: (
        <>
          Notes {open ? <Badge tone="neutral">{open}</Badge> : null}
        </>
      ),
      panel: notes,
    },
    { key: 'schedule', label: 'Schedule', panel: schedule },
    ...(principles ? [{ key: 'principles' as Tab, label: 'Principles', panel: principles }] : []),
    ...(architecture ? [{ key: 'architecture' as Tab, label: 'Architecture', panel: architecture }] : []),
  ]
  const shown = tabs.some((t) => t.key === tab) ? tab : 'features'

  return (
    <>
      <div role="tablist" aria-label="Project" className="tabs">
        {tabs.map((t) => (
          <button
            key={t.key}
            type="button"
            role="tab"
            id={`${id}-${t.key}-tab`}
            aria-controls={`${id}-${t.key}`}
            aria-selected={shown === t.key}
            tabIndex={shown === t.key ? 0 : -1}
            onClick={() => select(t.key)}
          >
            {t.label}
          </button>
        ))}
      </div>
      {tabs.map((t) => (
        <div key={t.key} role="tabpanel" id={`${id}-${t.key}`} aria-labelledby={`${id}-${t.key}-tab`} hidden={shown !== t.key}>
          {t.panel}
        </div>
      ))}
    </>
  )
}

function Conflicts({ conflicts }: { conflicts: Conflict[] }) {
  if (!conflicts.length) return null
  return (
    <section>
      <h2>Conflicts</h2>
      <ul className="cards">
        {conflicts.map((c) => (
          <li key={c.key}>
            <ConflictCard conflict={c} />
          </li>
        ))}
      </ul>
    </section>
  )
}

/**
 * BEH-SHEET-2 (his 2026-10-22 22:48Z note: "kit starts to raise the questions which helps guide the
 * user… Right now I'm not sure what ux ui or logic to define to unblock kit"): first on the tab, what
 * the agent is asking him, each question above the card it is about, so he can answer without
 * opening anything. The conflicts and the sheet below it are problems Kit found, not questions.
 */
function KitAsks({
  asks,
  notesApp,
  version,
  onAnswered,
}: {
  asks: Ask[]
  notesApp: string
  version: number
  onAnswered: () => void
}) {
  // BEH-SHEET-3: an open note on the card is his reply, still waiting for the agent to fold it.
  const { resource } = useReloadableResource(() => fetchNotes(notesApp), [notesApp, version])
  if (!asks.length) return null
  const notes = resource.state === 'ready' ? resource.value.notes : []
  return (
    <section>
      <h2>Kit is asking you</h2>
      <p className="muted">Reply under a question. Each one says what Kit does if you never answer.</p>
      <ul className="cards">
        {asks.map((ask) => (
          <li key={`${ask.app}/${ask.b.id}`}>
            <AskCard
              ask={ask}
              notesApp={notesApp}
              replies={notes.filter((n) => n.kind === 'note' && n.behaviour === ask.b.id)}
              onAnswered={onAnswered}
            />
          </li>
        ))}
      </ul>
    </section>
  )
}

/**
 * BEH-SHEET-3 (his 2026-10-22 23:47Z note: "Kit is asking you needs a way to reply"): the answer box
 * sits under the question. A reply is a plain note on the card, in the notes the page shows, so the
 * agent folds it like any other note; until then the card shows what he wrote, and he can add more.
 */
function AskCard({
  ask: { app, b, text },
  notesApp,
  replies,
  onAnswered,
}: {
  ask: Ask
  notesApp: string
  replies: Note[]
  onAnswered: () => void
}) {
  const [reply, setReply] = useState('')
  const { write, run } = useWrite(onAnswered)
  const fieldId = useId()

  async function send(e: React.FormEvent) {
    e.preventDefault()
    await run(async () => {
      const result = await leaveNote(notesApp, reply, b.id)
      setReply('')
      return result
    })
  }

  return (
    <Card elevation="flat">
      <p className="ask">{text}</p>
      <p className="muted">
        <Link to={`/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(b.id)}`}>{b.id}</Link> {b.title}
      </p>
      {replies.map((n) => (
        <p key={n.at} className="reply">
          <Badge tone="neutral">You replied</Badge> {n.text} <span className="muted">({n.at}, waiting for Kit)</span>
        </p>
      ))}
      <form onSubmit={send} className="write">
        <label htmlFor={fieldId}>{replies.length ? 'Add to your answer' : 'Your answer'}</label>
        <textarea id={fieldId} className="note-text" value={reply} onChange={(e) => setReply(e.target.value)} />
        <Button type="submit" disabled={write.state === 'saving' || reply.trim() === ''}>
          {write.state === 'saving' ? 'Sending…' : 'Reply'}
        </Button>
        <WriteFeedback write={write} />
      </form>
    </Card>
  )
}

/** BEH-KIND-2: a filter that leaves nothing says so, or it reads as a page that failed to load. */
function NoneOfLayers({ layers, empty }: { layers: string[]; empty: boolean }) {
  if (!layers.length || !empty) return null
  return <p className="muted">Nothing in this project is of the {layers.join(' or ')} layer.</p>
}

/** A form elsewhere on the page writes a note; bumping the version makes the notes list re-read. */
function useNotesVersion(): [number, () => void] {
  const [version, setVersion] = useState(0)
  return [version, () => setVersion((v) => v + 1)]
}

/**
 * BEH-FEAT-5 (kit#246): a project of several files as ONE page. James: "is kit ui not just
 * part of kit and the pages would be the features". So every file's top level is drawn
 * together and no heading names a file; each card still links into the file it lives in,
 * because that is where its id means something. The notes and the new-behaviour form are
 * the file named like the project (Kit's command line), which is what this url showed
 * before it was merged — so neither moves under him.
 */
function OneProject({ name, files }: { name: string; files: ProjectDetail[] }) {
  const home = files.find((f) => f.app === name) ?? files[0]
  const [notes, bump] = useNotesVersion()
  const [layers, toggleLayer] = useLayers()
  const tops = files.map((f) => {
    const shown = ofLayers(f, layers)
    return { file: f, shown, top: topLevel(shown), unreviewed: new Set(f.adjudication.unreviewed) }
  })
  const features = tops.flatMap(({ file, shown, top }) =>
    withAnyUnder(shown, top.features, layers).map((feature) => ({ file, shown, feature })),
  )
  const loose = tops.flatMap(({ file, top, unreviewed }) =>
    top.behaviours.map((behaviour) => ({ file, behaviour, unreviewed: unreviewed.has(behaviour.id) })),
  )
  const conflicts = files.flatMap((f) => f.conflicts)
  const sum = (n: (f: ProjectDetail) => number) => files.reduce((t, f) => t + n(f), 0)
  const inferred = sum((f) => f.adjudication.inferred)
  const [tab, select] = useTab()
  const [open, setOpen] = useState<number | null>(null)
  const principles = files.flatMap((f) => f.principles ?? [])
  // Each carries the file it was read from: that is the corpus an approval is written to.
  const decisions = files.flatMap((f) => (f.decisions ?? []).map((d) => ({ ...d, app: f.app })))

  return (
    <>
      <h1>{name}</h1>
      <div className="badges">
        <Count n={sum((f) => f.behaviours.length)} one="behaviour" tone="primary" />
        <Badge tone="neutral">{sum((f) => f.adjudication.defined)} defined</Badge>
        {inferred ? <Badge tone="warning">{inferred} inferred</Badge> : null}
      </div>

      {/* Above the tabs (BEH-FEAT-7); features are the default tab (kit#246, BEH-FEAT-6), and notes a tab of their own. */}
      <AskForBehaviour app={home.app} onAsked={() => (bump(), select('notes'))} />

      <ProjectTabs
        tab={tab}
        select={select}
        waiting={asksOf(files).length + conflicts.length + sum((f) => f.questions.length)}
        open={open}
        notes={
          <>
            <NotesPanel app={home.app} version={notes} citations={citationsOf(home.app, files)} onOpenCount={setOpen} />
            <PendingPanel apps={files.map((f) => f.app)} version={notes} />
          </>
        }
        schedule={<SchedulePanel apps={files.map((f) => f.app)} />}
        principles={principles.length ? <PrinciplesList principles={principles} /> : null}
        architecture={decisions.length ? <DecisionsList decisions={decisions} behaviours={files.flatMap((f) => f.behaviours.map((b) => ({ ...b, app: f.app })))} /> : null}
        features={
          <>
            <LayerChips layers={layers} onToggle={toggleLayer} />
            <NoneOfLayers layers={layers} empty={tops.every(({ shown }) => shown.behaviours.length === 0)} />
            {features.length > 0 && (
              <section aria-label="Features">
                <ul className="cards">
                  {features.map(({ file, shown, feature }) => (
                    <li key={`${file.app}/${feature.id}`}>
                      <FeatureCard app={file.app} project={shown} feature={feature} path={[]} layers={layers} />
                    </li>
                  ))}
                </ul>
              </section>
            )}

            {loose.length > 0 && (
              <section>
                <h2>{features.length > 0 ? 'Behaviours in no feature' : 'Behaviours'}</h2>
                <ul className="cards">
                  {loose.map(({ file, behaviour, unreviewed }) => (
                    <li key={`${file.app}/${behaviour.id}`}>
                      <BehaviourRow app={file.app} behaviour={behaviour} unreviewed={unreviewed} />
                    </li>
                  ))}
                </ul>
              </section>
            )}
          </>
        }
        questions={
          <>
            <KitAsks asks={asksOf(files)} notesApp={home.app} version={notes} onAnswered={bump} />
            <Conflicts conflicts={conflicts} />
            <QuestionSheet sheets={files} notesApp={home.app} onResolved={bump} />
          </>
        }
      />
    </>
  )
}

/**
 * His step 4, "what the desired behaviour really is", in prose (BEH-NOTE-4,
 * kit#118 2026-10-12): *"the way that you define behaviours is quite a chore …
 * I am thinking more free form text inputs. In place of the behaviour input."*
 *
 * It replaces the Id / Title / Actor form. What it writes is a `request` note,
 * not a behaviour: whoever folds it writes the id, title, actor and steps, and
 * the corpus is untouched until then. That keeps the one property the old form
 * was careful of — a machine never writes `source defined` on his behalf —
 * because nothing here writes the corpus at all.
 */
function AskForBehaviour({ app, onAsked }: { app: string; onAsked: () => void }) {
  const [text, setText] = useState('')
  const { write, run } = useWrite(onAsked)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    await run(async () => {
      const result = await askForBehaviour(app, text)
      setText('')
      return result
    })
  }

  return (
    <form onSubmit={submit} className="write">
      <label htmlFor="ask-behaviour">Describe what should happen</label>
      <textarea
        id="ask-behaviour"
        className="note-text"
        placeholder="e.g. dragging a habit reorders the list, and it stays that way"
        aria-describedby="ask-behaviour-published"
        value={text}
        onChange={(e) => setText(e.target.value)}
      />
      <PublishedHint id="ask-behaviour-published" />
      <Button type="submit" disabled={write.state === 'saving' || text.trim() === ''}>
        {write.state === 'saving' ? 'Sending…' : 'Ask for behaviour'}
      </Button>
      <WriteFeedback write={write} />
    </form>
  )
}

/**
 * The question sheet, on the screen at last.
 *
 * `kit.js` has always built this — `questions()` + `renderSheet()` — and
 * `docs/design/ui.md` promised it as "the sheet, made clickable". `ui.js` has
 * been sending it in every project payload since the read model shipped; this UI
 * typed it `unknown[]` and rendered none of it. The material it was throwing
 * away is the most considered thing Kit produces: the question, both options
 * with their consequences, and a recommendation with its reasoning.
 *
 * ⚠️ **The two tiers do NOT get the same treatment, and the asymmetry is the
 * point.** A `review` entry is answered in a vocabulary that already exists, so
 * the queue links to where it can be answered. A `decision` entry — two
 * behaviours disagreeing — has no answer Kit can record: there is no `supersede`
 * keyword in the corpus grammar, no writer function for one, and adding either
 * is a change to the LANGUAGE rather than a feature on top of it.
 *
 * So a decision is resolved in prose (BEH-NOTE-8, his "Question where need a
 * resolution input. This could be free form text as well"): the answer is a note
 * naming the decision's key, and the fold is what edits the behaviours. Still no
 * syntax invented; the conflict stays on the sheet until the note is folded.
 * Resolutions go to `notesApp`, the notes the page shows, whichever file the
 * decision came from — a note in a file nobody lists is a note nobody folds.
 */
function QuestionSheet({
  sheets,
  notesApp,
  onResolved,
}: {
  sheets: { app: string; questions: Question[] }[]
  notesApp: string
  onResolved: () => void
}) {
  // One sheet over every file of the project; each question keeps the file it links into.
  const questions = sheets.flatMap((s) => s.questions.map((q) => ({ ...q, app: s.app })))
  if (!questions.length) return null

  const decisions = questions.filter((q) => q.tier === 'decision')
  const reviews = questions.filter((q) => q.tier === 'review')

  return (
    <section>
      {/* BEH-SHEET-2: what Kit found wrong with the spec, which he called "problems", not questions. */}
      <h2>Problems in the spec</h2>
      <div className="badges">
        <Count n={decisions.length} one="decision" tone="warning" />
        <Count n={reviews.length} one="review" />
      </div>

      {decisions.map((q) => (
        <DecisionCard key={q.key} question={q} notesApp={notesApp} onResolved={onResolved} />
      ))}

      {reviews.length > 0 && (
        <>
          <h3>Never adjudicated</h3>
          <p className="muted">
            Kit inferred each of these from code and marked it unreviewed. Open one to approve it,
            or to deny it with what is actually true — the corpus is what gets edited.
          </p>
          <ul className="cards">
            {reviews.map((q) => (
              <li key={q.key}>
                <Card interactive>
                  <h4>
                    <Link
                      to={`/projects/${encodeURIComponent(q.app)}/behaviours/${encodeURIComponent(q.id ?? q.key)}`}
                    >
                      {q.id ?? q.key}
                    </Link>{' '}
                    {q.title}
                  </h4>
                  {/* In a <code>, which is not decoration: a source ref is one
                      unbroken token and as plain text it pushes the card wider
                      than a phone, clipping the title beside it. */}
                  {q.source?.ref && (
                    <p className="muted">
                      inferred from <code>{q.source.ref}</code>
                    </p>
                  )}
                </Card>
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  )
}

function DecisionCard({ question, notesApp, onResolved }: { question: Question; notesApp: string; onResolved: () => void }) {
  const [text, setText] = useState('')
  const { write, run } = useWrite(onResolved)
  const fieldId = useId()

  async function resolve(e: React.FormEvent) {
    e.preventDefault()
    await run(async () => {
      const result = await resolveDecision(notesApp, question.key, text)
      setText('')
      return result
    })
  }

  return (
    <Card elevation="flat">
      <h3>{question.title}</h3>
      {question.sides && (
        <ul>
          {question.sides.map((s) => (
            <li key={s.id}>
              <strong>{s.id}</strong> — {s.title}: <code>{s.value.join(', ')}</code>{' '}
              {s.ref && <span className="muted">({s.ref})</span>}
            </li>
          ))}
        </ul>
      )}
      {question.asks && <p>{question.asks}</p>}
      {question.options.length > 0 && (
        <ol>
          {question.options.map((o) => (
            <li key={o.label}>
              <strong>{o.label}</strong> — {o.consequence}
            </li>
          ))}
        </ol>
      )}
      {question.recommend && (
        <p>
          <Badge tone="primary">recommended</Badge> {question.recommend.label} —{' '}
          {question.recommend.why}
        </p>
      )}
      {/* The argument AGAINST the recommendation, kept next to it rather than
          folded away: a recommendation shown without its own counter-case is how
          a suggestion becomes a decision nobody made. */}
      {question.against && <p className="muted">Against: {question.against}</p>}
      <form onSubmit={resolve} className="write">
        <label htmlFor={fieldId}>How should this be resolved?</label>
        <textarea id={fieldId} className="note-text" value={text} onChange={(e) => setText(e.target.value)} />
        <Button type="submit" disabled={write.state === 'saving' || text.trim() === ''}>
          {write.state === 'saving' ? 'Sending…' : 'Resolve'}
        </Button>
        <WriteFeedback write={write} />
      </form>
    </Card>
  )
}

function ConflictCard({ conflict }: { conflict: Conflict }) {
  return (
    <Card elevation="flat">
      <h3>{conflict.key}</h3>
      <p>
        held at <strong>{conflict.held.join(', ')}</strong> by {conflict.holders.join(', ')}
      </p>
      <ul>
        {conflict.challengers.map((ch) => (
          <li key={`${ch.from}:${ch.at}`}>
            {ch.from} says <strong>{ch.value.join(', ')}</strong> <span className="muted">({ch.at})</span>
          </li>
        ))}
      </ul>
    </Card>
  )
}

/**
 * Feature cards (BEH-FEAT-1/2): a title, and how many behaviours sit anywhere beneath it, each
 * counted once. `path` is how the reader got here, carried into the link for the crumbs.
 */
export function FeatureCards({
  app,
  project,
  features,
  path,
  layers = [],
  heading = true,
}: {
  app: string
  project: Tagged
  features: Feature[]
  path: string[]
  /** The layer filter, carried into each feature's link (BEH-KIND-2). */
  layers?: string[]
  /** Off under the project page's Features tab, which already says it (BEH-FEAT-6). */
  heading?: boolean
}) {
  if (!features.length) return null
  return (
    <>
      {heading && <h2>Features</h2>}
      <ul className="cards">
        {features.map((f) => (
          <li key={f.id}>
            <FeatureCard app={app} project={project} feature={f} path={path} layers={layers} />
          </li>
        ))}
      </ul>
    </>
  )
}

function FeatureCard({
  app,
  project,
  feature,
  path,
  layers = [],
}: {
  app: string
  project: Tagged
  feature: Feature
  path: string[]
  layers?: string[]
}) {
  return (
    <Card interactive>
      <h3>
        <Link to={featureHref(app, feature.id, path, layers)}>{feature.title}</Link>
      </h3>
      <div className="badges">
        <Count n={behavioursUnder(project, feature.id).size} one="behaviour" tone="primary" />
        <span className="muted id">{feature.id}</span>
      </div>
    </Card>
  )
}

export function BehaviourRow({
  app,
  behaviour,
  unreviewed,
}: {
  app: string
  behaviour: Behaviour
  unreviewed: boolean
}) {
  return (
    <Card interactive>
      <h3>
        <Link to={`/projects/${encodeURIComponent(app)}/behaviours/${encodeURIComponent(behaviour.id)}`}>
          {behaviour.title}
        </Link>{' '}
        {/* BEH-TITLE-1: the title leads; the id stays, quieter, to search and link by. */}
        <span className="muted id">{behaviour.id}</span>
      </h3>
      <div className="badges">
        <Badge tone="neutral">{behaviour.actor}</Badge>
        {/* BEH-KIND-1: the layer the corpus gives it — ux unless a `layer` line or the file says otherwise. */}
        <Badge tone="neutral">{behaviour.layer}</Badge>
        <Count n={behaviour.steps.length} one="step" />
        {behaviour.source.origin === 'inferred' && <Badge tone="warning">inferred</Badge>}
        {unreviewed && <Badge tone="warning">unreviewed</Badge>}
      </div>
      <p className="muted">{behaviour.at}</p>
    </Card>
  )
}
