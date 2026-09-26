#!/usr/bin/env node
'use strict';
/**
 * One command that starts Kit.
 *
 *   node start.js [--port 4321] [--host 127.0.0.1] [--repos <dir>] [--dir <dir>]
 *
 * Why this file exists (kit#37): following the README you could not reach the
 * UI. `ui.js` serves the SPA out of `ui/dist`, which is gitignored, so a fresh
 * clone served a 503 — and the three commands that actually work lived in a
 * nested README you would only find after knowing to look for it.
 *
 * So this does the whole thing: install if the dependencies are missing, build
 * if the bundle is missing or stale, then serve. Everything it does, it says it
 * is doing, because the install and the build take a while and a silent pause
 * is indistinguishable from a hang.
 *
 * ⚠️ It is a plain `node` entry point rather than an npm script on purpose. A
 * `"scripts"` entry in a package.json is a packaging change and therefore
 * James's under claude-code-bot#83 — and the backend deliberately has no
 * package.json at all, so that `node ui.js` needs nothing installed.
 *
 * Exit 0 = serving. 2 = could not look — the same convention `check.js`,
 * `ui.js` and `selfhost/run.js` use, and deliberately not 0.
 */
const { execFileSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const BEH = path.join(__dirname, 'prototypes', 'behaviour-ast');
const UI = path.join(BEH, 'ui');
const DIST_INDEX = path.join(UI, 'dist', 'index.html');
const NODE_MODULES = path.join(UI, 'node_modules');

// What counts as "source" for the staleness question below. Deliberately more
// than `src/`: a vite config or a lockfile change alters the bundle just as
// surely as a component does, and a build that ignores them serves a bundle
// that does not match the tree it came from.
const SOURCE_ENTRIES = ['src', 'index.html', 'vite.config.ts', 'package.json', 'package-lock.json'];

/**
 * The newest mtime under a path, or 0 if it does not exist.
 *
 * Returns a number rather than a Date so the caller compares with `>` and an
 * absent file sorts before everything, which is the answer we want: a missing
 * bundle is maximally stale.
 */
function newestMtime(target) {
  let stat;
  try { stat = fs.statSync(target); } catch { return 0; }
  if (!stat.isDirectory()) return stat.mtimeMs;
  let newest = stat.mtimeMs;
  for (const entry of fs.readdirSync(target)) {
    const m = newestMtime(path.join(target, entry));
    if (m > newest) newest = m;
  }
  return newest;
}

/**
 * Is the built bundle missing or older than the source it was built from?
 *
 * A stale bundle is the failure this whole file exists to prevent, and it is
 * the quiet one: a fresh clone's 503 at least tells you something is wrong,
 * whereas a bundle built before your last edit serves you a Kit that silently
 * is not the one in your tree.
 */
function needsBuild(uiDir = UI, distIndex = DIST_INDEX, env = process.env) {
  const built = newestMtime(distIndex);
  if (built === 0) return true;
  // 🔑 Not only mtimes. `KIT_BASE_PATH` (kit#49) is read at BUILD time and baked
  // into the asset URLs, so changing it changes what the bundle must be while
  // touching no source file at all — every mtime test above says "fresh" and the
  // page would be blank. Compared against the prefix read back out of the bundle
  // itself rather than against a remembered value, because the bundle is the only
  // thing that knows what it was built for.
  const ui = require(path.join(BEH, 'ui.js'));
  const builtFor = ui.bundleBasePath(path.dirname(distIndex));
  if (builtFor !== null && builtFor !== ui.normaliseBasePath(env.KIT_BASE_PATH)) return true;
  return SOURCE_ENTRIES.some((e) => newestMtime(path.join(uiDir, e)) > built);
}

function needsInstall(nodeModules = NODE_MODULES) {
  // `.bin/vite` rather than the directory itself: an interrupted `npm ci`
  // leaves a node_modules that exists and cannot build anything, and "is it
  // there" is not the question — "can it build" is. vite is the binary the
  // next step actually runs, so it is the one worth asking about.
  return !fs.existsSync(path.join(nodeModules, '.bin', 'vite'));
}

const HELP = `Kit — one command to run it.

  node start.js [options]

  --port <n>       default 4321
  --host <h>       default 127.0.0.1. Writes are refused unless this is
                   loopback, so a non-loopback host serves a read-only Kit.
  --repos <dir>    where your checkouts live. Without it, coverage reports
                   "not measured" — which is not the same as "nothing is tested".
  --dir <dir>      a directory of .beh corpora. Defaults to Kit's own.
  --bindings <f>   a bindings file to write to instead of the repo's.
  --no-build       serve the existing bundle and neither install nor build.
  --help

Environment:
  KIT_PASSWORD     lock writes behind a password (kit#44). Unset = the loopback
                   rule above.
  KIT_BASE_PATH    the path Kit is served under, e.g. /kit (kit#49). Unset = the
                   root, which is every local run. ⚠️ It is read when the UI is
                   BUILT as well as when it is served, so a bundle built without
                   it cannot be served under a prefix — ui.js says so at startup
                   rather than leaving you a white page.

It installs and builds the UI the first time, and rebuilds when the bundle is
older than the source. Then it serves, and prints the URL to open.`;

function npm(args, what) {
  process.stdout.write(`kit: ${what}... `);
  const started = Date.now();
  try {
    execFileSync('npm', args, { cwd: UI, stdio: ['ignore', 'pipe', 'pipe'], encoding: 'utf8' });
  } catch (e) {
    console.log('failed');
    // The reason lives in npm's output, not in the exception's message, and a
    // person looking at a failed install needs the former.
    const out = (String(e.stdout || '') + String(e.stderr || '')).trim();
    console.error(e.code === 'ENOENT'
      ? 'kit: npm is not on the PATH — Kit needs Node and npm to build its UI'
      : `kit: \`npm ${args.join(' ')}\` failed in ${path.relative(__dirname, UI)}\n${out.split('\n').slice(-15).join('\n')}`);
    return false;
  }
  console.log(`done (${Math.round((Date.now() - started) / 1000)}s)`);
  return true;
}

async function main(argv) {
  if (argv.includes('--help') || argv.includes('-h')) {
    console.log(HELP);
    return 0;
  }

  // `--no-build` exists for the case where the bundle was built by something
  // else and this process must not spend a minute proving it — an image build,
  // where the build ran at image-build time and the container only serves.
  const skipBuild = argv.includes('--no-build');
  const forward = argv.filter((a) => a !== '--no-build');

  if (!skipBuild) {
    if (needsInstall() && !npm(['ci'], 'installing the UI’s dependencies (once)')) return 2;
    if (needsBuild() && !npm(['run', 'build'], 'building the UI')) return 2;
  } else if (!fs.existsSync(DIST_INDEX)) {
    console.error(`kit: --no-build was given and there is no bundle at ${path.relative(__dirname, DIST_INDEX)}`);
    return 2;
  }

  // Required rather than spawned: one process, so Ctrl-C does what it looks
  // like it does, and ui.js already prints the URL, the corpora and whether
  // writes are on. Repeating any of that here would let the two drift.
  return require(path.join(BEH, 'ui.js')).main(forward);
}

module.exports = { newestMtime, needsBuild, needsInstall, main, SOURCE_ENTRIES };

if (require.main === module) {
  main(process.argv.slice(2)).then((code) => {
    if (code !== 0) process.exit(code);
  });
}
