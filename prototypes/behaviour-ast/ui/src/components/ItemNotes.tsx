import { citationsOf } from '../notes'
import { useResource } from '../hooks/useResource'
import type { ProjectDetail } from '../api/types'
import { filesOf } from '../projectFiles'
import NotesPanel from './NotesPanel'

/**
 * BEH-COMMENT-2: the notes on a behaviour, feature or epic page, each saying whether a card
 * cites it. Citations are read from the same files the project page reads (`filesOf`), with the
 * notes app as the home file, exactly as Project does. The page's own file is used at once, so
 * a single-file project costs no extra read. Until the files load, or if they cannot be read,
 * the panel shows no picked-up line beyond what this file says: not knowing is not "not yet".
 */
export default function ItemNotes({ project, behaviour }: { project: ProjectDetail; behaviour: string }) {
  const app = project.app
  const resource = useResource(() => filesOf(app, project), [app])
  // Not "not picked up yet" until the siblings have been looked at: a card in another file may cite it.
  const files = resource.state === 'ready' ? resource.value : resource.state === 'error' ? [project] : null
  return <NotesPanel app={app} behaviour={behaviour} citations={files ? citationsOf(app, files) : undefined} />
}
