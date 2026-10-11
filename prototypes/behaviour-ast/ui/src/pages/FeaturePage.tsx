import { Fragment } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { Badge } from '@jemmy8oy-northstar/design-system'
import { fetchProject } from '../api/client'
import { useResource } from '../hooks/useResource'
import type { ProjectDetail } from '../api/types'
import ResourceView from '../components/Resource'
import { childrenOf, featureHref, pathFrom } from '../features'
import { BehaviourRow, FeatureCards } from './Project'

/** One feature (kit#237): what is tagged into it, and every feature it is tagged into. */
export default function FeaturePage() {
  const { app = '', id = '' } = useParams()
  const resource = useResource(() => fetchProject(app), [app])

  return (
    <ResourceView resource={resource}>
      {(project) => <Detail project={project} id={id} />}
    </ResourceView>
  )
}

function Detail({ project, id }: { project: ProjectDetail; id: string }) {
  const [params] = useSearchParams()
  const app = project.app
  const byId = new Map(project.features.map((f) => [f.id, f]))
  const feature = byId.get(id)
  const path = pathFrom(params.get('path'), project, id)
  const unreviewed = new Set(project.adjudication.unreviewed)

  if (!feature) {
    return (
      <>
        <Crumbs app={app} path={[]} titles={byId} />
        <p role="alert">No feature {id} in {app}.</p>
      </>
    )
  }

  const children = childrenOf(project, id)
  const here = [...path, id]

  return (
    <>
      <Crumbs app={app} path={path} titles={byId} current={feature.title} />
      <h1>{feature.title}</h1>
      <p className="muted">
        {feature.id} · {feature.at}
      </p>

      {/* BEH-FEAT-2: every parent, as a tag. None of them is the owner, so none is
          singled out — the crumbs above are only the way you happened to come. */}
      <section aria-label="In">
        {feature.in.length ? (
          <div className="badges">
            <span className="muted">In</span>
            {feature.in.map((p) => (
              <Link key={p} to={featureHref(app, p, [])}>
                <Badge tone="neutral">{byId.get(p)?.title ?? p}</Badge>
              </Link>
            ))}
          </div>
        ) : (
          <p className="muted">Top level of {app}.</p>
        )}
      </section>

      <section aria-label="Contents">
        <FeatureCards app={app} project={project} features={children.features} path={here} />
        {children.behaviours.length > 0 && (
          <>
            <h2>Behaviours</h2>
            <ul className="cards">
              {children.behaviours.map((b) => (
                <li key={b.id}>
                  <BehaviourRow app={app} behaviour={b} unreviewed={unreviewed.has(b.id)} />
                </li>
              ))}
            </ul>
          </>
        )}
        {children.features.length === 0 && children.behaviours.length === 0 && (
          <p className="muted">Nothing is tagged into this feature yet.</p>
        )}
      </section>
    </>
  )
}

/** BEH-FEAT-3: Projects / app / the path you came by / here. */
function Crumbs({
  app,
  path,
  titles,
  current,
}: {
  app: string
  path: string[]
  titles: Map<string, { title: string }>
  current?: string
}) {
  return (
    <nav aria-label="Breadcrumb" className="crumbs">
      <Link to="/">Projects</Link> / <Link to={`/projects/${encodeURIComponent(app)}`}>{app}</Link>
      {path.map((p, i) => (
        <Fragment key={p}>
          {' / '}
          <Link to={featureHref(app, p, path.slice(0, i))}>{titles.get(p)?.title ?? p}</Link>
        </Fragment>
      ))}
      {current && <> / {current}</>}
    </nav>
  )
}
