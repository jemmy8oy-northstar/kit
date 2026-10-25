import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Badge, Card } from '@jemmy8oy-northstar/design-system'
import { fetchProjects } from '../api/client'
import { useResource } from '../hooks/useResource'
import type { GitHubProject, ProjectSummary } from '../api/types'
import CoverageBadge from '../components/CoverageBadge'
import Count from '../components/Count'
import ResourceView from '../components/Resource'
import { useSession } from '../components/session'
import { byProject } from '../features'
import { isIdea } from '../board'

/** Step 1 of his loop: see the projects Kit knows about. */
export default function Projects() {
  const projects = useResource(fetchProjects, [])

  return (
    <>
      <h1>Projects</h1>
      <p className="muted">
        Every behaviour corpus Kit can read. Counts come from the corpus itself;
        coverage needs the app's repository, which the read API is not given by
        default.
      </p>

      <ResourceView resource={projects}>
        {({ projects: rows, gitHub, gitHubError }) => (
          <>
            <ul className="cards" aria-label="Project list">
              {/* Ideas have their own page in the nav, and are not projects yet (kit#284 slice 4). */}
              {byProject(rows.filter((r) => !isIdea(r))).map((g) =>
                // BEH-FEAT-4: one file that is its own project reads as it always did.
                g.files.length === 1 && g.files[0].app === g.project ? (
                  <li key={g.project}>
                    <ProjectRow project={g.files[0]} />
                  </li>
                ) : (
                  <li key={g.project}>
                    <ProjectGroup project={g.project} files={g.files} />
                  </li>
                ),
              )}
            </ul>
            <GitHubProjects projects={gitHub} error={gitHubError} />
          </>
        )}
      </ResourceView>
    </>
  )
}

/**
 * BEH-USERS-2: the repos in the signed-in user's own GitHub that carry a corpus. They have no
 * page here yet, so each links to its repo on GitHub. A GitHub that could not be read is said,
 * never shown as an empty list.
 */
function GitHubProjects({ projects, error }: { projects?: GitHubProject[]; error?: string }) {
  if (error) {
    return <p role="alert">Your GitHub projects could not be read: {error}</p>
  }
  if (!projects) return null
  if (projects.length === 0) {
    return (
      <>
        <p className="muted">
          Kit is connected to your GitHub, but none of the repos it can see carries a <code>.beh</code> file yet.
        </p>
        <NewProject />
      </>
    )
  }
  return (
    <>
      <h2>From your GitHub</h2>
      <ul className="cards" aria-label="GitHub project list">
        {projects.map((p) => (
          <li key={p.fullName}>
            <Card>
              <h3>
                <a href={p.url} target="_blank" rel="noreferrer">
                  {p.fullName}
                </a>
              </h3>
              <p className="muted">{p.corpus.join(', ')}</p>
            </Card>
          </li>
        ))}
      </ul>
      <NewProject />
    </>
  )
}

/** A repo name GitHub accepts: whitespace runs become `-`, anything outside [A-Za-z0-9._-] is dropped. */
function repoName(raw: string): string {
  return raw.trim().replace(/\s+/g, '-').replace(/[^A-Za-z0-9._-]/g, '')
}

/** The first `.beh` file of a new project (BEH-USERS-3). */
function starterCorpus(name: string): string {
  return (
    `# ${name}, described in Kit.\n#\n` +
    `# Kit lists this repo once this file is on its default branch. Replace the behaviour below with your own.\n\n` +
    `behaviour BEH-START-1 "Someone opens ${name}"\n  pending\n  actor user\n  when opens page:Home\n  then sees region:Main\n`
  )
}

/**
 * BEH-USERS-3: a new project is a new repo in the user's own GitHub. Kit holds an installation,
 * never a user token, and an installation cannot create a repo in a personal account, so Kit
 * hands over two github.com links with the name filled in and the install picks the repo up.
 */
function NewProject() {
  const account = useSession()?.gitHub
  const [raw, setRaw] = useState('')
  const name = repoName(raw)
  // One pending behaviour, not comments alone: Kit reads a corpus of zero behaviours as one it cannot look at.
  const starter = starterCorpus(name)
  const create = `https://github.com/new?name=${encodeURIComponent(name)}&description=${encodeURIComponent('A Kit project')}`
  const addFile =
    `https://github.com/${encodeURIComponent(account ?? '')}/${encodeURIComponent(name)}/new/main` +
    `?filename=${encodeURIComponent(`behaviours/${name}.beh`)}&value=${encodeURIComponent(starter)}`
  // An anchor with no href is how a link is disabled: it is not focusable and does not navigate.
  const link = (href: string, text: string) => (
    <a href={name ? href : undefined} aria-disabled={!name} target="_blank" rel="noreferrer">
      {text}
    </a>
  )
  return (
    <>
      <h2>Start a new project</h2>
      <label>
        Project name <input value={raw} onChange={(e) => setRaw(e.target.value)} />
      </label>
      <ol>
        <li>{link(create, 'Create the repo on GitHub')}</li>
        {account && <li>{link(addFile, 'Add its first behaviour file')}</li>}
      </ol>
      <p className="muted">
        If your install of Kit&apos;s App only reaches selected repositories, add the new repo to it. The repo
        appears here once that file is on its default branch.
      </p>
    </>
  )
}

/**
 * Files that name one project with `# kit:project` (kit#237): one card with one link, to the
 * project's one page (BEH-FEAT-4, kit#246). He found a sub-card per file confusing — the files
 * are how the corpus is stored, not something he works in — so none is listed here.
 */
function ProjectGroup({ project, files }: { project: string; files: ProjectSummary[] }) {
  const sum = (n: (f: ProjectSummary) => number | undefined) => files.reduce((t, f) => t + (n(f) ?? 0), 0)
  const broken = files.filter((f) => f.error)
  const conflicts = sum((f) => f.conflicts)
  const unreviewed = sum((f) => f.unreviewed)
  return (
    <Card interactive>
      <h2>
        <Link to={`/projects/${encodeURIComponent(project)}`}>{project}</Link>
      </h2>
      <div className="badges">
        <Count n={sum((f) => f.behaviours)} one="behaviour" tone="primary" />
        {conflicts ? <Count n={conflicts} one="conflict" tone="warning" /> : null}
        {unreviewed ? <Badge tone="warning">{unreviewed} unreviewed</Badge> : null}
      </div>
      {/* A file that will not parse is still said, never summed in as zero. */}
      {broken.map((f) => (
        <p role="alert" key={f.app}>
          {f.app}: {f.error}
        </p>
      ))}
    </Card>
  )
}

function ProjectRow({ project }: { project: ProjectSummary }) {
  // A corpus that will not parse is a state of the world, not a project with
  // nothing in it. ui.js reports it as an error and the row says so, rather
  // than showing a plausible-looking "0 behaviours".
  if (project.error) {
    return (
      <Card elevation="flat">
        <h2>{project.app}</h2>
        <Badge tone="danger">corpus will not parse</Badge>
        <p role="alert">{project.error}</p>
      </Card>
    )
  }

  return (
    <Card interactive>
      <h2>
        <Link to={`/projects/${encodeURIComponent(project.app)}`}>{project.app}</Link>
      </h2>
      <p className="muted">{project.corpus}</p>
      <div className="badges">
        {/* A trial corpus describes an app that does not exist. It belongs in
            the list — hiding it would make the list lie about what corpora Kit
            reads — but a 0% score against a real app and a 0% score against
            nothing mean opposite things, so the row has to say which. `neutral`
            because this is a fact about the corpus, not an alarm. */}
        {project.notReal ? (
          <Badge tone="neutral" title="This corpus describes an app that does not exist — a trial, not a project">
            trial — no app
          </Badge>
        ) : null}
        {/* Same reasoning, different axis. This corpus describes an app that
            DOES exist and is already in the list under its own corpus, so
            `notReal` is false of it and would be a lie. Without a marker the
            trial and its subject read as two equal projects. Neutral for the
            same reason: a fact about the corpus, not an alarm. */}
        {project.duplicateOf ? (
          <Badge
            tone="neutral"
            title={`A second corpus for ${project.duplicateOf}, written forwards from a brief — not a separate project`}
          >
            trial — spec for {project.duplicateOf}
          </Badge>
        ) : null}
        <Count n={project.behaviours ?? 0} one="behaviour" tone="primary" />
        <CoverageBadge coverage={project.coverage} />
        {project.conflicts ? (
          <Count n={project.conflicts} one="conflict" tone="warning" />
        ) : null}
        {project.unreviewed ? (
          <Badge tone="warning">{project.unreviewed} unreviewed</Badge>
        ) : null}
      </div>
    </Card>
  )
}
