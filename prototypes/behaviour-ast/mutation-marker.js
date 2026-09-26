'use strict';
/**
 * The shared safety marker for `mutate.js` and `mutate-ui.js`.
 *
 * Both tools deliberately write wrong code to the working tree and put it back
 * afterwards, and both announce that window with the same untracked file at the
 * repo root, so `git status` prints it as `??` right next to the files you were
 * about to stage (claude-code-bot#92). They had grown two copies of that logic;
 * this is the one copy, because the cases below are easy to get subtly
 * different and only one of the two would then be right.
 *
 * ── What both copies were missing ──────────────────────────────────────────
 *
 * `SIGINT` and `SIGTERM` are handled, but `SIGKILL` is not catchable, and
 * SIGKILL is how this pod dies when it runs out of credit. Twice in two days a
 * run was killed mid-mutant and left a deliberately-wrong file on disk for the
 * next session to find — the 210th left one in `useWrite.ts`, the 211th one in
 * `useResource.ts`. `mutate.js` called that case "a false alarm" in a comment.
 * It is not a false alarm; it is two live hazards:
 *
 *  1. A mutant on disk that the next reader may stage, which is how
 *     `index.set(k, 1)` reached a pushed branch.
 *  2. Worse and silent: the NEXT run reads its "pristine originals" from that
 *     same disk, so the mutant becomes the baseline. `restoreAll()` then
 *     restores *to the mutant* at the end, making it permanent — and every
 *     mutant whose anchor still matched is still killed, so the run reports a
 *     clean sweep while baking a defect in.
 *
 * So the marker carries the pristine originals inside it, and a stale marker
 * refuses the next run rather than being cleaned up and forgotten. Recovery is
 * then exact and mechanical: no git, and no judgement about which hunk was the
 * mutant. That matters because the obvious manual fix, `git checkout -- <file>`,
 * reverts the WHOLE file and destroys any real uncommitted work sitting beside
 * the mutant.
 *
 * ── Why this is a factory ──────────────────────────────────────────────────
 *
 * `kit.test.js` is the suite `mutate.js` runs for every mutant, so a test that
 * wrote the real marker would delete the live marker of the run executing it.
 * Tests build their own instance over a temp directory; the module's default
 * export is the real one at the repo root.
 */
const fs = require('fs');
const path = require('path');

const ORIGINALS = '--- pristine originals below (JSON) — `--recover` restores them ---';

/**
 * Is the process that armed this marker still running?
 *
 * Signal 0 asks the kernel about a pid without delivering anything. `ESRCH` is
 * the only answer that means "no such process"; `EPERM` means it exists and is
 * someone else's, which for this guard is still alive.
 *
 * Pid reuse is possible and is deliberately resolved towards refusing: a wrong
 * refusal prints a pid you can check by hand, while a wrong permission strands a
 * live run's next file with nothing left to restore it from. The originals stay
 * in the marker either way, so nothing is lost by stopping.
 */
function ownerAlive(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch (e) {
    return e.code === 'EPERM';
  }
}

function createMarker({ root, markerPath }) {
  const MARKER = markerPath || path.join(root, 'MUTATION-IN-PROGRESS');

  // The payload records the base directory it was written from, relative to the
  // repo root, so either tool can recover a run left behind by the other. They
  // mutate different trees (`behaviour-ast` and `behaviour-ast/ui`), and a
  // recovery that assumed its own base would silently write the right contents
  // to the wrong paths.
  function payload() {
    if (!fs.existsSync(MARKER)) return null;
    const text = fs.readFileSync(MARKER, 'utf8');
    const i = text.indexOf(ORIGINALS);
    if (i < 0) return null; // a marker written before the originals were carried
    try {
      const p = JSON.parse(text.slice(i + ORIGINALS.length));
      return p && p.files && typeof p.base === 'string' ? p : null;
    } catch {
      return null;
    }
  }

  function drop() {
    try {
      fs.unlinkSync(MARKER);
    } catch {
      /* already gone */
    }
  }

  /**
   * Restore the tree from a killed run. Returns a process exit code: 0 restored
   * (or nothing to do), 2 could not look — the same convention the tools use, so
   * "I could not restore it" never reads as "it was fine".
   *
   * `isAlive` is injected for the same reason `exit` and `log` are: the fixtures
   * below arm a marker from the *test's own* process, so their recorded pid is
   * genuinely alive and a real probe would refuse every one of them. Two tests
   * exercise the real `ownerAlive` in both directions so the injection cannot
   * hide a default that never worked.
   */
  function recover(log = console, isAlive = ownerAlive) {
    if (!fs.existsSync(MARKER)) {
      log.log('nothing to recover: no MUTATION-IN-PROGRESS marker.');
      return 0;
    }
    const p = payload();
    if (!p) {
      log.error('cannot look: this marker carries no originals, so the tree cannot be restored');
      log.error('  from it. Revert the mutant by hand against HEAD — do NOT use `git checkout --`,');
      log.error('  which would also destroy any real uncommitted work beside the mutant.');
      return 2;
    }
    // 🔴 THE MARKER IS THE RESTORE DATA, NOT A FLAG, so recovering while its run
    // is still going is destructive twice over: it puts back what has been
    // mutated so far, then deletes the only copy of the originals — and the live
    // run goes on to mutate a DIFFERENT file with nothing left to undo it. It
    // cost the 248th twenty minutes and a live mutant in `Project.tsx`.
    //
    // `arm()` has always written this pid into the marker, but only into the
    // prose above the payload, where nothing could read it. A value captured and
    // never read is a rule the code does not have.
    if (isAlive(p.pid)) {
      log.error(`cannot look: ${p.tool} (pid ${p.pid}) is STILL RUNNING, so this marker is live`);
      log.error('  restore data, not leftovers. Recovering now would undo what it has mutated so');
      log.error('  far and delete the originals it needs for whatever it mutates next.');
      log.error(`  Let it finish, or stop it first (\`kill ${p.pid}\` — it restores on SIGTERM).`);
      log.error(`  If \`ps -p ${p.pid} -o command=\` shows something unrelated, that pid was reused:`);
      log.error('  the originals are plain JSON inside the marker and can be applied by hand.');
      return 2;
    }
    // An older marker carries no pid. Proceeding is the lesser evil — refusing
    // would make every marker written before this change unrecoverable — but the
    // absence gets said out loud rather than read as "no run is live".
    if (p.pid === undefined) {
      log.log('note: this marker records no pid, so whether a run is still live could not be');
      log.log('  checked. Confirm no `mutate*` process is running before trusting this.');
    }
    // Only files that actually differ are written. A recovery that rewrote
    // every subject would touch mtimes across the tree and, more to the point,
    // would hide how much of the damage was real.
    const restored = [];
    for (const [rel, src] of Object.entries(p.files)) {
      const file = path.join(root, p.base, rel);
      if (fs.readFileSync(file, 'utf8') !== src) {
        fs.writeFileSync(file, src);
        restored.push(path.join(p.base, rel));
      }
    }
    drop();
    log.log(
      restored.length
        ? `recovered ${restored.length} file(s) left mutated by a killed ${p.tool} run:\n  ${restored.join('\n  ')}`
        : `marker was stale — every subject already matched its pristine original (${p.tool}). Marker removed.`,
    );
    return 0;
  }

  /**
   * Refuse to start while a marker exists. Exits rather than returning, because
   * every caller's only correct response is to stop before reading the tree.
   */
  function refuseIfStale(tool, exit = process.exit, log = console) {
    if (!fs.existsSync(MARKER)) return;
    log.error('cannot look: MUTATION-IN-PROGRESS already exists, so the tree may still hold a');
    log.error(`  mutant from a killed run. Starting ${tool} now would read that mutant as the`);
    log.error('  pristine original and restore to it at the end, making it permanent.');
    log.error(`  Run \`node ${tool} --recover\` first.`);
    return exit(2);
  }

  /**
   * Open the danger window: write the marker (carrying the originals) and
   * install the handlers for every exit path that IS catchable. A marker left
   * behind is now recoverable; a marker missing during a run is still the
   * failure it exists to prevent, so both are handled rather than assumed.
   */
  function arm({ tool, base, originals, warn, restoreAll }) {
    fs.writeFileSync(
      MARKER,
      [
        `${tool} is running and the working tree is deliberately WRONG.`,
        '',
        warn,
        '',
        'If this file is still here and no mutation run is going, the run was killed',
        `and a mutant is live on disk: run \`node ${tool} --recover\` to put the tree`,
        'back exactly. Do NOT use `git checkout --` — it reverts the whole file and',
        'would destroy any real uncommitted work sitting beside the mutant.',
        '',
        `started ${new Date().toISOString()} by pid ${process.pid}`,
        '',
        ORIGINALS,
        // The pid goes in the PAYLOAD, not just the prose line above: `recover()`
        // refuses while this process is alive, and it can only do that if the
        // value is somewhere machine-readable.
        JSON.stringify({ tool, base, pid: process.pid, files: originals }),
        '',
      ].join('\n'),
    );
    process.on('exit', drop);
    process.on('SIGINT', () => {
      restoreAll();
      drop();
      process.exit(130);
    });
    process.on('SIGTERM', () => {
      restoreAll();
      drop();
      process.exit(143);
    });
  }

  return { MARKER, arm, drop, recover, refuseIfStale, payload };
}

const ROOT = path.join(__dirname, '..', '..');
module.exports = Object.assign(createMarker({ root: ROOT }), {
  createMarker, ORIGINALS, ROOT, ownerAlive,
});
