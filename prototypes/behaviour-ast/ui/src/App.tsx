import { BrowserRouter as Router, Link, Routes, Route } from 'react-router-dom'
import Projects from './pages/Projects'
import Project from './pages/Project'
import BehaviourPage from './pages/BehaviourPage'

export default function App() {
  return (
    <Router>
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
  )
}
