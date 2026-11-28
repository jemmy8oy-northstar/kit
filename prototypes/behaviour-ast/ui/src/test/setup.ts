import '@testing-library/jest-dom/vitest'
import { afterEach, vi } from 'vitest'
import { cleanup, configure } from '@testing-library/react'

// A findBy/waitFor waits up to 3s, not the library's 1s. On a loaded pod or runner, a remount and its fetches can
// take over a second (BEH-AUTH-1's "project pages open" failed this way on 2026-11-28 and passed alone), and a red
// test that is only slow makes the pipeline's re-test fail on code it never changed. Under vitest's 5s test limit.
configure({ asyncUtilTimeout: 3000 })

afterEach(() => {
  cleanup()
  vi.restoreAllMocks()
})
