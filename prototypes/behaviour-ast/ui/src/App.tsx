import { BrowserRouter as Router, Link, Routes, Route } from 'react-router-dom'
import Projects from './pages/Projects'
import Project from './pages/Project'
import BehaviourPage from './pages/BehaviourPage'
import SignIn from './components/SignIn'

export default function App() {
  return (
    <SignIn>
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
            — Kit edits the file, Kit does not commit. `kit.test.js` now fails if
            any of the three places that made this claim makes it again.
            */}
            <span className="muted">writes the corpus file — never commits</span>
          </header>
          <main>
            <Routes>
              <Route path="/" element={<Projects />} />
              <Route path="/projects/:app" element={<Project />} />
              <Route path="/projects/:app/behaviours/:id" element={<BehaviourPage />} />
            </Routes>
          </main>
        </div>
      </Router>
    </SignIn>
  )
}
