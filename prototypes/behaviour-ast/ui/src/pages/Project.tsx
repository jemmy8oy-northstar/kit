import { useId, useState } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { Badge, Button, Card } from '@jemmy8oy-northstar/design-system'
import { askForBehaviour, fetchProject, fetchProjects, resolveDecision } from '../api/client'
import { useReloadableResource } from '../hooks/useResource'
import type { Behaviour, Conflict, Feature, ProjectDetail, Question } from '../api/types'
import { behavioursUnder, featureHref, topLevel, type Tagged } from '../features'
import CoverageBadge from '../components/CoverageBadge'
import Count from '../components/Count'
import NotesPanel from '../components/NotesPanel'
import PendingPanel from '../components/PendingPanel'
import PrinciplesList from '../components/PrinciplesList'
import ResourceView from '../components/Resource'
import SchedulePanel from '../components/SchedulePanel'
import { useWrite } from '../components/useWrite'
import { WriteFeedback } from './BehaviourPage'

/**
 * BEH-FEAT-5 (kit#246): the files a project page draws from. A name that is a project of
 * several files — or whose one file has another name — is every file naming it; anything
 * else is the one file, as it always was. A list that cannot be read falls back to the one
 * file: the page is still true of it, only no longer of its siblings.
 */
async function filesOf(app: string): Promise<ProjectDetail[]> {
  const rows = await fetchProjects().then(
    (r) => r.projects ?? [],
    () => [],
  )
  const names = rows.filter((r) => !r.error && (r.project ?? r.app) === app).map((r) => r.app)
  if (names.length > 1 || (names.length === 1 && names[0] !== app)) {
    return Promise.all(names.map(fetchProject))
  }
  return [await fetchProject(app)]
}

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
  const top = topLevel(project)
  const [notes, bump] = useNotesVersion()

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

      {/* Near the top on purpose (kit#118): a thought is caught before it is lost, not after scrolling past the sheet. */}
      <AskForBehaviour app={project.app} onAsked={bump} />
      <NotesPanel app={project.app} version={notes} />
      <PendingPanel apps={[project.app]} version={notes} />
      <SchedulePanel apps={[project.app]} />
      <PrinciplesList principles={project.principles ?? []} />

      <ProjectTabs
        waiting={project.conflicts.length + project.questions.length}
        features={
          <>
            {/* BEH-FEAT-1: the top level is everything with no `in` line. A corpus with no
                features has no section here and lists every behaviour, as it always did. */}
            {top.features.length > 0 && (
              <section aria-label="Features">
                <FeatureCards app={project.app} project={project} features={top.features} path={[]} heading={false} />
              </section>
            )}

            <section>
              {/* With every behaviour tagged, an empty heading would read as "nothing here". */}
              {(top.features.length === 0 || top.behaviours.length > 0) && (
                <h2>{top.features.length > 0 ? 'Behaviours in no feature' : 'Behaviours'}</h2>
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
            <Conflicts conflicts={project.conflicts} />
            <QuestionSheet sheets={[project]} notesApp={project.app} onResolved={bump} />
          </>
        }
      />
    </>
  )
}

type Tab = 'features' | 'questions'

/**
 * BEH-FEAT-6 (kit#118, his note 2026-10-12 20:49Z): "Maybe features and questions should be
 * under a tab view, default to features". What the project IS comes first; what is waiting on
 * him is one tap away, with a count so it cannot hide there.
 *
 * The tab is in the url (`?tab=questions`) so a link can open on the questions and the back
 * button undoes a switch. Both panels stay mounted and the other is `hidden`, so a resolution
 * half-typed into a decision card survives a look at the features.
 */
function ProjectTabs({ waiting, features, questions }: { waiting: number; features: React.ReactNode; questions: React.ReactNode }) {
  const [params, setParams] = useSearchParams()
  const tab: Tab = params.get('tab') === 'questions' ? 'questions' : 'features'
  const id = useId()

  function select(next: Tab) {
    const out = new URLSearchParams(params)
    if (next === 'features') out.delete('tab')
    else out.set('tab', next)
    setParams(out)
  }

  const tabs: { key: Tab; label: React.ReactNode }[] = [
    { key: 'features', label: 'Features' },
    {
      key: 'questions',
      label: (
        <>
          Questions {waiting > 0 && <Badge tone="warning">{waiting}</Badge>}
        </>
      ),
    },
  ]

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
            aria-selected={tab === t.key}
            tabIndex={tab === t.key ? 0 : -1}
            onClick={() => select(t.key)}
          >
            {t.label}
          </button>
        ))}
      </div>
      <div role="tabpanel" id={`${id}-features`} aria-labelledby={`${id}-features-tab`} hidden={tab !== 'features'}>
        {features}
      </div>
      <div role="tabpanel" id={`${id}-questions`} aria-labelledby={`${id}-questions-tab`} hidden={tab !== 'questions'}>
        {waiting > 0 ? questions : <p className="muted">Nothing is waiting on you.</p>}
      </div>
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
  const tops = files.map((f) => ({ file: f, top: topLevel(f), unreviewed: new Set(f.adjudication.unreviewed) }))
  const features = tops.flatMap(({ file, top }) => top.features.map((feature) => ({ file, feature })))
  const loose = tops.flatMap(({ file, top, unreviewed }) =>
    top.behaviours.map((behaviour) => ({ file, behaviour, unreviewed: unreviewed.has(behaviour.id) })),
  )
  const conflicts = files.flatMap((f) => f.conflicts)
  const sum = (n: (f: ProjectDetail) => number) => files.reduce((t, f) => t + n(f), 0)
  const inferred = sum((f) => f.adjudication.inferred)

  return (
    <>
      <h1>{name}</h1>
      <div className="badges">
        <Count n={sum((f) => f.behaviours.length)} one="behaviour" tone="primary" />
        <Badge tone="neutral">{sum((f) => f.adjudication.defined)} defined</Badge>
        {inferred ? <Badge tone="warning">{inferred} inferred</Badge> : null}
      </div>

      {/* His ask (kit#246): features at the top — now the default tab (BEH-FEAT-6). The notes come next (kit#118). */}
      <ProjectTabs
        waiting={conflicts.length + sum((f) => f.questions.length)}
        features={
          <>
            {features.length > 0 && (
              <section aria-label="Features">
                <ul className="cards">
                  {features.map(({ file, feature }) => (
                    <li key={`${file.app}/${feature.id}`}>
                      <FeatureCard app={file.app} project={file} feature={feature} path={[]} />
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
            <Conflicts conflicts={conflicts} />
            <QuestionSheet sheets={files} notesApp={home.app} onResolved={bump} />
          </>
        }
      />

      <AskForBehaviour app={home.app} onAsked={bump} />
      <NotesPanel app={home.app} version={notes} />
      <PendingPanel apps={files.map((f) => f.app)} version={notes} />
      <SchedulePanel apps={files.map((f) => f.app)} />
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
        value={text}
        onChange={(e) => setText(e.target.value)}
      />
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
      <h2>Question sheet</h2>
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
  heading = true,
}: {
  app: string
  project: Tagged
  features: Feature[]
  path: string[]
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
            <FeatureCard app={app} project={project} feature={f} path={path} />
          </li>
        ))}
      </ul>
    </>
  )
}

function FeatureCard({ app, project, feature, path }: { app: string; project: Tagged; feature: Feature; path: string[] }) {
  return (
    <Card interactive>
      <h3>
        <Link to={featureHref(app, feature.id, path)}>{feature.title}</Link>
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
        <Count n={behaviour.steps.length} one="step" />
        {behaviour.source.origin === 'inferred' && <Badge tone="warning">inferred</Badge>}
        {unreviewed && <Badge tone="warning">unreviewed</Badge>}
      </div>
      <p className="muted">{behaviour.at}</p>
    </Card>
  )
}
