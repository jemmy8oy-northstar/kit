#!/usr/bin/env bash
# BEH-USERS-9, runner pool: one pod's loop. Instead of a Job per run, a fixed Deployment of pods asks Kit's
# public API for the next queued run, runs it with kit-run, reports how it ended, and asks again. After
# KIT_RUNS_PER_CONTAINER runs it exits 0 and the pod restarts fresh.
#
# Environment:
#   KIT_URL                 required  Kit's base URL, passed on to each run (kit-run)
#   KIT_CLAIMER_URL         optional  the claimer sidecar (kit-claimer) in this pod (default: http://127.0.0.1:8787)
#   KIT_RUNS_PER_CONTAINER  optional  runs to do before exiting (default: 20)
#   CLAUDE_CODE_OAUTH_TOKEN optional  James's subscription token; passed to each run (see kit-run), whose
#                                     usage is then sent with the `finished` report
#
# This container never holds the pool token (kit#701): a run is the same user in the same container and could
# read it from /proc/1/environ. The claimer sidecar holds it, claims ONE run when asked, and forwards the
# finished report under that run's token. No token is ever echoed. The claim answer (which holds the run token)
# lives only in a 0600 temp file and a subshell's environment, and is removed after each run.
set -euo pipefail

: "${KIT_URL:?kit-pool: KIT_URL is required}"
runs_left="${KIT_RUNS_PER_CONTAINER:-20}"
case "$runs_left" in
  ''|*[!0-9]*) echo "kit-pool: KIT_RUNS_PER_CONTAINER must be a whole number" >&2; exit 64 ;;
esac

claimer="${KIT_CLAIMER_URL:-http://127.0.0.1:8787}"
umask 077
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

# Empties the scratch disks' contents, never the mount points themselves. The KIT_*_DIR and KIT_RUN_BIN
# overrides exist only so a test can run this loop outside the image.
clean() {
  find "${KIT_WORK_DIR:-/work}" "${KIT_HOME_DIR:-/home/runner}" -mindepth 1 -delete 2>/dev/null || true
}

while [ "$runs_left" -gt 0 ]; do
  # The sidecar holds the pool token; asking it needs none. It answers 409 while the last claim is unreported.
  code="$(curl --silent --show-error --max-time 70 --request POST \
        --output "$tmp/claim.json" --write-out '%{http_code}' "$claimer/next" 2>/dev/null)" || code=000

  if [ "$code" = 204 ]; then
    continue
  fi
  if [ "$code" != 200 ]; then
    echo "kit-pool: claim answered $code, retrying in 10s" >&2
    sleep 10
    continue
  fi

  # Eight NUL-terminated fields on stdout, read back below; the JSON goes in by file, not argv.
  if ! node -e '
    const c = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8"));
    for (const k of ["id", "token", "repo", "task", "baseBranch", "model", "maxTurns", "stage"]) process.stdout.write(String(c[k] ?? "") + "\0");
  ' "$tmp/claim.json" > "$tmp/fields"; then
    echo "kit-pool: could not read the claim, retrying in 10s" >&2
    rm -f "$tmp/claim.json" "$tmp/fields"
    sleep 10
    continue
  fi
  rm -f "$tmp/claim.json"
  { read -r -d '' run_id; read -r -d '' run_token; read -r -d '' run_repo; read -r -d '' run_task; read -r -d '' run_base; read -r -d '' run_model; read -r -d '' run_turns; read -r -d '' run_stage; } < "$tmp/fields" || true
  rm -f "$tmp/fields"

  runs_left=$((runs_left - 1))
  echo "kit-pool: running $run_id on $run_repo"

  # A run never sees the pool credential: it is not in this container at all.
  status=0
  (
    export KIT_RUN_ID="$run_id" KIT_RUN_TOKEN="$run_token" KIT_REPO="$run_repo" KIT_TASK="$run_task"
    export KIT_BASE_BRANCH="$run_base" KIT_MODEL="$run_model" KIT_USAGE_FILE="$tmp/usage.json" KIT_FAILURE_FILE="$tmp/failure.txt" KIT_BUNDLES_FILE="$tmp/bundles.json"
    # BEH-ORCH-5: a pipeline stage's turn limit; a run started by hand has none, so the runner's own default applies.
    if [ -n "$run_turns" ]; then export KIT_MAX_TURNS="$run_turns"; fi
    # BEH-ORCH-12: the stage, so kit-run can check a test run's verdict itself; a run started by hand has none.
    if [ -n "$run_stage" ]; then export KIT_STAGE="$run_stage"; fi
    exec "${KIT_RUN_BIN:-/usr/local/bin/kit-run}"
  ) || status=$?

  clean

  if [ "$status" -eq 0 ]; then
    outcome=finished
  else
    outcome=failed
  fi
  echo "kit-pool: $run_id $outcome (exit $status)"
  # The usage (only written for a run on the subscription) and the bundles (only a triage pushes any) go with the
  # report. If Kit refuses it, the run is reported again without the usage, then with the status alone: ending the
  # run matters more than counting it, and its bundles matter more than its count. Bodies go by file, not argv.
  # The sidecar adds the run token and forwards the report to Kit, relaying Kit's status.
  report() {
    curl --silent --show-error --max-time 40 --request POST \
        --header 'content-type: application/json' --data-binary "@$1" \
        --output /dev/null --write-out '%{http_code}' "$claimer/finished" 2>/dev/null || echo 000
  }
  # A failed run says which step failed (the run writes "<step> exit <code>"); if it wrote nothing, only its status is known.
  failure=""
  if [ "$outcome" = failed ]; then
    failure="$(head -c 100 "$tmp/failure.txt" 2>/dev/null | head -n 1)" || failure=""
    [ -n "$failure" ] || failure="run exit $status"
  fi
  # Writes body.1 (everything), body.2 (no usage) and body.3 (status only), skipping one the same as the one before.
  node -e '
    const fs = require("fs");
    const [dir, status, failure] = process.argv.slice(1);
    const read = f => { try { return JSON.parse(fs.readFileSync(dir + "/" + f, "utf8")); } catch { return undefined; } };
    const usage = read("usage.json"), bundles = read("bundles.json");
    const bodies = [{ status, usage, bundles, failure: failure || undefined }, { status, bundles, failure: failure || undefined }, { status }].map(b => JSON.stringify(b));
    bodies.forEach((b, i) => { if (i === 0 || b !== bodies[i - 1]) fs.writeFileSync(dir + "/body." + (i + 1), b); });
  ' "$tmp" "$outcome" "$failure" || printf '{"status":"%s"}' "$outcome" > "$tmp/body.3"
  rm -f "$tmp/usage.json" "$tmp/bundles.json" "$tmp/failure.txt"
  code=000
  for n in 1 2 3; do
    [ -f "$tmp/body.$n" ] || continue
    [ "$code" = 000 ] || echo "kit-pool: Kit answered $code to $run_id's report, sending less" >&2
    code="$(report "$tmp/body.$n")"
    [ "$code" = 200 ] && break
  done
  rm -f "$tmp/body.1" "$tmp/body.2" "$tmp/body.3"
  [ "$code" = 200 ] || echo "kit-pool: could not report $run_id to Kit ($code)" >&2
  run_token=""
done
