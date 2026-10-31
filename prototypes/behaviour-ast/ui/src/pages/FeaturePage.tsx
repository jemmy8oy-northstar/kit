import { Fragment } from 'react'
import { Link, useParams, useSearchParams } from 'react-router-dom'
import { Badge } from '@jemmy8oy-northstar/design-system'
import { fetchProject } from '../api/client'
import { useResource } from '../hooks/useResource'
import type { ProjectDetail } from '../api/types'
import BellButton from '../components/BellButton'
import ItemNotes from '../components/ItemNotes'
import LayerChips from '../components/LayerChips'
import { useLayers } from '../hooks/useLayers'
import ResourceView from '../components/Resource'
import { childrenOf, epicHref, featureHref, ofLayers, pathFrom, withAnyUnder } from '../features'
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
  const [layers, toggleLayer] = useLayers()

  if (!feature) {
    return (
      <>
        <Crumbs app={app} path={[]} titles={byId} />
        <p role="alert">No feature {id} in {app}.</p>
      </>
    )
  }

  // BEH-KIND-2: the project page's layer filter, carried in the url, narrows this page the same way.
  const shown = ofLayers(project, layers)
  const children = childrenOf(shown, id)
  const subFeatures = withAnyUnder(shown, children.features, layers)
  const here = [...path, id]

  return (
    <>
      <Crumbs app={app} path={path} titles={byId} current={feature.title} />
      <h1>{feature.title}</h1>
      <p className="muted">
        {feature.id} · {feature.at}
      </p>
      <BellButton app={app} item={feature.id} />

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
        {/* BEH-EPIC-3: an epic tag is coloured apart from feature and project tags, and opens the epic's own page (BEH-EPIC-8). */}
        {feature.epics?.length ? (
          <div className="badges">
            <span className="muted">Epic</span>
            {feature.epics.map((e) => (
              <Link key={e} to={epicHref(app, e)} className="epic-tag">
                {project.epics?.find((x) => x.id === e)?.title ?? e}
              </Link>
            ))}
          </div>
        ) : null}
      </section>

      <section aria-label="Contents">
        <LayerChips layers={layers} onToggle={toggleLayer} />
        <FeatureCards app={app} project={shown} features={subFeatures} path={here} layers={layers} />
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
        {subFeatures.length === 0 && children.behaviours.length === 0 && (
          <p className="muted">
            {layers.length ? `Nothing of the ${layers.join(' or ')} layer is in this feature.` : 'Nothing is tagged into this feature yet.'}
          </p>
        )}
      </section>

      {/* BEH-COMMENT-1: a feature carries its own comments, which are the notes left on it. */}
      <ItemNotes project={project} behaviour={feature.id} />
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
