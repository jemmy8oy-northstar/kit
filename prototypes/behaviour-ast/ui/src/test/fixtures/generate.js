#!/usr/bin/env node
//
//   node src/test/fixtures/generate.js            # rewrite the recorded fixtures
//   node src/test/fixtures/generate.js --check    # exit 1 if they have drifted
//
// `fixtures.test.ts` has told the reader to "regenerate the fixtures" since the
// day it was written, and until now nothing in this repo could do that — the
// three recordings were hand-copied out of a terminal. The one assertion that a
// newly onboarded corpus reliably turns red was therefore the one assertion
// whose remedy did not exist.
//
// This is deliberately NOT wired into `npm test` or CI. A generator that ran
// before the comparison could not fail: it would rewrite the fixture to match
// whatever `ui.js` now says and report green over a real regression. It is a
// thing a person runs, on purpose, and then reads the diff of.
//
// ⚠️ The diff IS the review. `--check` exists so a failure can name the command,
// never so a build can run the rewrite for you.

import fs from 'node:fs'
import path from 'node:path'
import { createRequire } from 'node:module'
import { fileURLToPath } from 'node:url'

const HERE = path.dirname(fileURLToPath(import.meta.url))
const require = createRequire(import.meta.url)

const manifest = JSON.parse(fs.readFileSync(path.join(HERE, 'manifest.json'), 'utf8'))
// src/test/fixtures -> src/test -> src -> ui -> behaviour-ast/ui.js
const ui = require(path.join(HERE, '..', '..', '..', '..', 'ui.js'))

// The fixtures are committed with two-space indent and a trailing newline. A
// generator that wrote them any other way would report a diff on every run and
// teach the reader to stop looking at diffs.
export const render = (body) => `${JSON.stringify(body, null, 2)}\n`

function record(entry) {
  const res = ui.route('GET', entry.route, {})
  if (res.status !== 200) {
    throw new Error(`${entry.route} answered ${res.status}, so there is nothing to record`)
  }
  return render(res.body)
}

export function main(argv) {
  const check = argv.includes('--check')
  const unknown = argv.filter((a) => a !== '--check')
  if (unknown.length) {
    console.error(`unknown argument: ${unknown[0]}`)
    return 2
  }

  const drifted = []
  for (const entry of manifest.generated) {
    const file = path.join(HERE, entry.file)
    const fresh = record(entry)
    const onDisk = fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : null

    if (onDisk === fresh) {
      console.log(`  unchanged  ${entry.file}`)
      continue
    }
    drifted.push(entry.file)
    if (check) {
      console.log(`  DRIFTED    ${entry.file}  (${entry.route})`)
    } else {
      fs.writeFileSync(file, fresh)
      console.log(`  ${onDisk === null ? 'created' : 'rewrote'}    ${entry.file}  (${entry.route})`)
    }
  }

  if (!drifted.length) {
    console.log(`\n${manifest.generated.length} fixture(s) already match what ui.js serves.`)
    return 0
  }
  if (check) {
    console.log(`\n${drifted.length} fixture(s) no longer match what ui.js serves.`)
    console.log('Run `node src/test/fixtures/generate.js` from ui/, then READ THE DIFF:')
    console.log('  - a corpus was added or changed  => the new rows are expected, commit them')
    console.log('  - the read API changed           => fix the components the diff implicates')
    console.log('Never edit a fixture by hand to make a component pass.')
    return 1
  }
  console.log(`\n${drifted.length} fixture(s) rewritten. Read the diff before committing it.`)
  return 0
}

export { manifest }

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) {
  process.exit(main(process.argv.slice(2)))
}
