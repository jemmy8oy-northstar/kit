#!/usr/bin/env node
'use strict';
/**
 * Put a corpus edit back into git, for a Kit that is not running on your laptop.
 *
 * `docs/design/ui.md` decision 2 says Kit never touches git: a write lands in
 * the working tree and the author reviews the diff themselves. That is still
 * right locally, and this module does nothing at all unless it is switched on.
 *
 * It exists because James chose git over a database for a deployed Kit
 * (kit#41): "Ok let's stick to git for now and park db". Deployed there is no
 * working tree anyone will ever look at — a pod's filesystem is wiped on the
 * next restart — so the review step decision 2 protects has nowhere to happen
 * unless the edit is committed and pushed. The flag is the difference between
 * the two deployments, not a rewrite of the local tool.
 *
 * ── the rule this module is built around ────────────────────────────────────
 * By the time anything here runs, `writer.commitToDisk` has ALREADY written the
 * file. So a failure here never means "your edit was lost"; it means "your edit
 * is on a disk nobody will ever read again". Those are different sentences and
 * the caller must be able to tell them apart, which is why every failure below
 * is a distinct `reason` and why `committed` and `pushed` are separate fields.
 * Collapsing them into one boolean would report a stranded commit as success.
 */

const { spawnSync } = require('child_process');
const path = require('path');

/** Identity used when the pod has no git config of its own. */
const DEFAULT_NAME = 'kit';
const DEFAULT_EMAIL = 'kit@users.noreply.github.com';

/**
 * Run one git command and say what happened to it.
 *
 * ⚠️ `failure` names the LAYER, and that is the whole point of this function.
 * kit#39 was three sessions spent on a mutation harness whose error branch
 * returned one fixed string and never read `status` or `signal`, so a killed
 * child, an exit 7 and an exit 3 produced character-identical output and the
 * cause could not be established from the log. A git call has the same three
 * outcomes — the binary is missing, it exited non-zero, it was signalled — and
 * they need different fixes, so they get different words here.
 */
function git(args, cwd) {
  const r = spawnSync('git', args, { cwd, encoding: 'utf8' });

  // `error` is set when the binary could not be spawned at all. A container
  // without git installed reaches here, and "git is not installed" must not be
  // reported as "your push was rejected".
  if (r.error) {
    return { ok: false, failure: `git could not be run (${r.error.code || r.error.message})`, stdout: '', stderr: '' };
  }
  if (r.signal) {
    return { ok: false, failure: `git was killed by ${r.signal}`, stdout: r.stdout || '', stderr: r.stderr || '' };
  }
  if (r.status !== 0) {
    const first = String(r.stderr || '').trim().split('\n')[0] || `exit ${r.status}`;
    return { ok: false, failure: `git ${args[0]} exited ${r.status}: ${first}`, stdout: r.stdout || '', stderr: r.stderr || '' };
  }
  return { ok: true, failure: null, stdout: r.stdout || '', stderr: r.stderr || '' };
}

/**
 * The top of the work tree containing `file`, or null if it is not in one.
 *
 * Returns null rather than throwing: "this corpus is not in a git repo" is an
 * ordinary state of the world (it is every local run), not an error.
 */
function workTreeFor(file) {
  const dir = path.dirname(path.resolve(file));
  const r = git(['rev-parse', '--show-toplevel'], dir);
  if (!r.ok) return null;
  const top = r.stdout.trim();
  return top || null;
}

/** The branch currently checked out, or null in detached HEAD. */
function currentBranch(cwd) {
  const r = git(['rev-parse', '--abbrev-ref', 'HEAD'], cwd);
  if (!r.ok) return null;
  const name = r.stdout.trim();
  return name && name !== 'HEAD' ? name : null;
}

/**
 * A commit message a person can read in `git log` without opening the diff.
 *
 * The edit is described by the CALLER, because only the caller knows whether
 * this was a new behaviour, a step, an adjudication or a binding. A generic
 * "update corpus" would make the history exactly as useless as the database
 * James rejected — his reason for choosing git was that he can review what
 * changed.
 */
function message(summary, app) {
  const what = String(summary || 'update').trim();
  return app ? `kit: ${what} (${app})` : `kit: ${what}`;
}

/**
 * Commit `file` and push it.
 *
 * Returns `{ committed, pushed, reason, commit, branch }`. `reason` is set on
 * every outcome that is not a clean push, including the benign ones — a caller
 * that only checks `pushed` should still be able to print something true.
 *
 * `opts`:
 *   enabled  — off by default. Local Kit keeps decision 2 exactly as it was.
 *   remote   — default `origin`.
 *   branch   — default: whatever is checked out. Detached HEAD is refused
 *              rather than guessed at, because pushing a detached HEAD to a
 *              guessed branch name is how you write to the wrong one.
 *   push     — commit only, without pushing. For tests and for a deployment
 *              that syncs on its own schedule.
 *   name/email — identity for the commit.
 */
function writeBack(file, opts = {}) {
  const off = { committed: false, pushed: false, commit: null, branch: null };

  if (!opts.enabled) {
    return { ...off, reason: 'git write-back is off; the edit is in the working tree only' };
  }

  const cwd = workTreeFor(file);
  if (!cwd) {
    // Switched on and pointed at something that is not a repo. Loud, because
    // the operator asked for commits and is not getting any — silence here
    // would be a deployed Kit quietly dropping every edit on restart.
    return { ...off, reason: `git write-back is on but ${path.dirname(file)} is not inside a git work tree` };
  }

  const branch = opts.branch || currentBranch(cwd);
  if (!branch) {
    return { ...off, reason: 'HEAD is detached, so there is no branch to push to; pass a branch explicitly' };
  }

  const rel = path.relative(cwd, path.resolve(file));

  const added = git(['add', '--', rel], cwd);
  if (!added.ok) return { ...off, branch, reason: added.failure };

  // Nothing staged means the write did not change the file — re-adjudicating a
  // behaviour to the state it already had, for instance. That is a success with
  // nothing to do, NOT a failure, and committing an empty change to record it
  // would fill his history with noise he specifically chose git to avoid.
  const staged = git(['diff', '--cached', '--name-only', '--', rel], cwd);
  if (!staged.ok) return { ...off, branch, reason: staged.failure };
  if (!staged.stdout.trim()) {
    return { ...off, branch, reason: 'the file is unchanged, so there was nothing to commit' };
  }

  // Identity passed with `-c` rather than written into the repo's config: this
  // runs against a clone the operator may also use, and a tool that silently
  // rewrites `user.email` in someone's checkout is a nasty surprise.
  const ident = [
    '-c', `user.name=${opts.name || DEFAULT_NAME}`,
    '-c', `user.email=${opts.email || DEFAULT_EMAIL}`,
  ];

  // The pathspec is load-bearing. A deployed pod's tree may be dirty for
  // reasons that have nothing to do with this edit, and `git commit -a` would
  // sweep all of it into his history under a message describing one behaviour.
  const committed = git([...ident, 'commit', '-m', message(opts.summary, opts.app), '--', rel], cwd);
  if (!committed.ok) return { ...off, branch, reason: committed.failure };

  const sha = git(['rev-parse', 'HEAD'], cwd);
  const commit = sha.ok ? sha.stdout.trim().slice(0, 10) : null;

  if (opts.push === false) {
    return { committed: true, pushed: false, commit, branch, reason: 'committed; pushing is switched off' };
  }

  const pushed = git(['push', opts.remote || 'origin', `HEAD:${branch}`], cwd);
  if (!pushed.ok) {
    // 🔴 The state this module exists to report honestly. The edit is committed
    // locally and did NOT reach the remote — usually because the branch moved
    // on. Not retried and not rebased: an automatic rebase-and-force here would
    // be a tool rewriting his history unattended to make its own status line
    // green. Say so and let a person decide.
    return { committed: true, pushed: false, commit, branch, reason: pushed.failure };
  }

  return { committed: true, pushed: true, commit, branch, reason: null };
}

module.exports = { writeBack, workTreeFor, currentBranch, message, git };
