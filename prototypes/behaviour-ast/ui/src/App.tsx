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
          <span className="muted">read-only — the UI cannot write the corpus</span>
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
