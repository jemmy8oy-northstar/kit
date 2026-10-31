import { BrowserRouter as Router, Link, NavLink, Routes, Route } from 'react-router-dom'
import Board from './pages/Board'
import Stages from './pages/Stages'
import Epics from './pages/Epics'
import Projects from './pages/Projects'
import Project from './pages/Project'
import BehaviourPage from './pages/BehaviourPage'
import FeaturePage from './pages/FeaturePage'
import EpicPage from './pages/EpicPage'
import Principles from './pages/Principles'
import Activity from './pages/Activity'
import Health from './pages/Health'
import Inbox from './pages/Inbox'
import Notifications from './pages/Notifications'
import Settings, { GitHubReturn } from './pages/Settings'
import SignIn from './components/SignIn'
import NoteFab from './components/NoteFab'
import PullToRefresh from './components/PullToRefresh'
import { BoardNavLink } from './components/BoardViews'
import { OpeningPage } from './components/OpeningPage'
import { FeatureGate, FlagsProvider } from './components/FeatureGate'
import { FEATURE_PAGES, useFlags } from './components/flags'
import { IDEAS } from './board'

/** BEH-FLAGS-2: a page that belongs to a feature, shown only when that feature is on for this group. */
function gated(feature: string, page: React.ReactNode) {
  return <FeatureGate feature={feature}>{page}</FeatureGate>
}

/** BEH-FLAGS-2: a navigation entry that belongs to a feature, left out when that feature is off for this group. */
function FeatureNavLink({ feature }: { feature: string }) {
  const { path, label } = FEATURE_PAGES[feature]
  return useFlags().isOn(feature) ? <NavLink to={path}>{label}</NavLink> : null
}

export default function App() {
  return (
    <SignIn>
      <FlagsProvider>
      {/*
      `basename` is kit#49's rule 8 in the router. Without it every `<Link to="/">`
      navigates to the host root — which on a shared host is a DIFFERENT APP that
      answers 200, so pressing "Kit" in the header would quietly leave Kit. Read
      from `import.meta.env.BASE_URL`, the same value the API client and the built
      asset URLs come from, because three readers of one value is the arrangement
      this rule exists to keep.

      🔴 The trailing slash is stripped, and a real browser is what proved it has
      to be. `BASE_URL` is `/kit/`, and react-router matches by `startsWith`, so a
      basename of `/kit/` does NOT match the location `/kit` — which is the URL he
      will type. The result was a WHITE PAGE with every asset and every fetch
      returning 200, because the server and the bundle were both correct and only
      the router had declined to match. `|| '/'` because stripping the slash from
      the unprefixed `/` would leave an empty basename.
      */}
      <Router basename={import.meta.env.BASE_URL.replace(/\/$/, '') || '/'}>
        <div className="app">
          <header>
            <Link to="/" className="brand">
              Kit
            </Link>
            {/*
            This said "read-only, the UI cannot write the corpus" until the
            forms landed, and it was true when written. A banner that describes
            a capability is a claim with a shelf life: it went stale the moment
            the write routes shipped, and it took a screenshot to notice. What
            replaces it states the boundary that is still true and still matters
            — Kit edits the file, Kit does not commit. `src/test/contract.test.ts` now fails if
            any of the three places that made this claim makes it again.

            Then it went stale the same way: "— never commits" was true until
            hosted write-back (kit#43, kit#144) and Commit (kit#155), after which
            a deployed Kit showed "Committed as … and pushed to kit/hosted" under
            a header saying it never commits. The header cannot know which Kit it
            is in, so it claims only what is true of both; each write's own note
            says what git did with it.
            */}
            <span className="muted">writes the corpus file</span>
            {/* NavLink sets aria-current="page" on the active link itself.
                `end` on Projects, or "/" would match every route. */}
            <nav aria-label="Main" className="nav">
              {/* One link for both board views; each page carries the switch between them (BEH-STAGE-8). */}
              <BoardNavLink />
              {/* The `# kit:idea` corpus (kit#284 slice 4): one well-known file, so one link. */}
              <NavLink to={`/projects/${IDEAS}`}>Ideas</NavLink>
              <FeatureNavLink feature="FEAT-INBOX" />
              <NavLink to="/principles">Principles</NavLink>
              <FeatureNavLink feature="FEAT-HEALTH" />
              <FeatureNavLink feature="FEAT-ACTIVITY" />
              <FeatureNavLink feature="FEAT-NOTIFICATIONS" />
              {/* BEH-SETTINGS-1: the account area, GitHub and config, on a page of its own. */}
              <NavLink to="/settings">Settings</NavLink>
              <NavLink to="/" end>
                Projects
              </NavLink>
            </nav>
            {/* A GitHub connect return lands on Kit's root; this sends it on to Settings. */}
            <GitHubReturn />
            {/* BEH-SETTINGS-2: Kit opened at its root goes on to the page chosen in Settings. */}
            <OpeningPage />
          </header>
          {/* BEH-LOADING-2: on the installed iPhone app, pull down to reload the page's data. */}
          <PullToRefresh />
          <main>
            <Routes>
              <Route path="/" element={<Projects />} />
              <Route path="/board" element={gated('FEAT-BOARD', <Board />)} />
              <Route path="/stages" element={gated('FEAT-STAGES', <Stages />)} />
              <Route path="/epics" element={gated('FEAT-EPICS', <Epics />)} />
              <Route path="/principles" element={<Principles />} />
              <Route path="/health" element={gated('FEAT-HEALTH', <Health />)} />
              <Route path="/inbox" element={gated('FEAT-INBOX', <Inbox />)} />
              <Route path="/activity" element={gated('FEAT-ACTIVITY', <Activity />)} />
              <Route path="/notifications" element={gated('FEAT-NOTIFICATIONS', <Notifications />)} />
              <Route path="/settings" element={<Settings />} />
              <Route path="/projects/:app"element={<Project />} />
              <Route path="/projects/:app/behaviours/:id" element={<BehaviourPage />} />
              <Route path="/projects/:app/features/:id" element={<FeaturePage />} />
              <Route path="/projects/:app/epics/:id" element={<EpicPage />} />
            </Routes>
          </main>
          {/* BEH-NOTE-12: a note from any page. Inside the router, because it reads the URL for its project. */}
          <NoteFab />
        </div>
      </Router>
      </FlagsProvider>
    </SignIn>
  )
}
