import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
// The design system's compiled stylesheet carries the tokens the components
// read. It comes from `dist/`, which npm builds at install time via the
// package's `prepare` script — see the README for why that matters.
import '@jemmy8oy-northstar/design-system/styles.css'
import './index.css'
import App from './App'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
