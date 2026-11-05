#!/usr/bin/env bash
# BEH-USERS-9, runner pool: one pod's loop. Instead of a Job per run, a fixed Deployment of pods asks Kit's
# public API for the next queued run, runs it with kit-run, reports how it ended, and asks again. After
# KIT_RUNS_PER_CONTAINER runs it exits 0 and the pod restarts fresh.
#
# Environment:
#   KIT_URL                 required  Kit's base URL, e.g. https://balenthiran.co.uk/kit (no trailing slash)
#   KIT_RUNNER_POOL_TOKEN   required  proves to Kit that this is one of its pool pods; never given to a run
#   KIT_RUNS_PER_CONTAINER  optional  runs to do before exiting (default: 20)
#   CLAUDE_CODE_OAUTH_TOKEN optional  James's subscription token; passed to each run (see kit-run), whose
#                                     usage is then sent with the `finished` report
#
# No token is ever echoed. The claim answer (which holds the run token) lives only in a 0600 temp file and a
# subshell's environment, and is removed after each run.
set -euo pipefail

: "${KIT_URL:?kit-pool: KIT_URL is required}"
: "${KIT_RUNNER_POOL_TOKEN:?kit-pool: KIT_RUNNER_POOL_TOKEN is required}"
runs_left="${KIT_RUNS_PER_CONTAINER:-20}"
case "$runs_left" in
  ''|*[!0-9]*) echo "kit-pool: KIT_RUNS_PER_CONTAINER must be a whole number" >&2; exit 64 ;;
esac

base="${KIT_URL%/}"
umask 077
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

# Empties the scratch disks' contents, never the mount points themselves. The KIT_*_DIR and KIT_RUN_BIN
# overrides exist only so a test can run this loop outside the image.
clean() {
  find "${KIT_WORK_DIR:-/work}" "${KIT_HOME_DIR:-/home/runner}" -mindepth 1 -delete 2>/dev/null || true
}

while [ "$runs_left" -gt 0 ]; do
  # The pool token goes in a curl config on stdin, so it is never in argv (visible in ps).
  code="$(printf 'header = "x-kit-runner-pool: %s"\n' "$KIT_RUNNER_POOL_TOKEN" \
    | curl --silent --show-error --max-time 60 --config - --request POST \
        --output "$tmp/claim.json" --write-out '%{http_code}' "$base/api/runs/claim" 2>/dev/null)" || code=000

  if [ "$code" = 204 ]; then
    continue
  fi
  if [ "$code" != 200 ]; then
    echo "kit-pool: claim answered $code, retrying in 10s" >&2
    sleep 10
    continue
  fi

  # Six NUL-terminated fields on stdout, read back below; the JSON goes in by file, not argv.
  if ! node -e '
    const c = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8"));
    for (const k of ["id", "token", "repo", "task", "baseBranch", "model"]) process.stdout.write(String(c[k] ?? "") + "\0");
  ' "$tmp/claim.json" > "$tmp/fields"; then
    echo "kit-pool: could not read the claim, retrying in 10s" >&2
    rm -f "$tmp/claim.json" "$tmp/fields"
    sleep 10
    continue
  fi
  rm -f "$tmp/claim.json"
  { read -r -d '' run_id; read -r -d '' run_token; read -r -d '' run_repo; read -r -d '' run_task; read -r -d '' run_base; read -r -d '' run_model; } < "$tmp/fields" || true
  rm -f "$tmp/fields"

  runs_left=$((runs_left - 1))
  echo "kit-pool: running $run_id on $run_repo"

  # A run must never see the pool credential: it is unset for the child, and the child is a subshell.
  status=0
  (
    unset KIT_RUNNER_POOL_TOKEN
    export KIT_RUN_ID="$run_id" KIT_RUN_TOKEN="$run_token" KIT_REPO="$run_repo" KIT_TASK="$run_task"
    export KIT_BASE_BRANCH="$run_base" KIT_MODEL="$run_model" KIT_USAGE_FILE="$tmp/usage.json"
    exec "${KIT_RUN_BIN:-/usr/local/bin/kit-run}"
  ) || status=$?

  clean

  if [ "$status" -eq 0 ]; then
    outcome=finished
  else
    outcome=failed
  fi
  echo "kit-pool: $run_id $outcome (exit $status)"
  # The usage (only written for a run on the subscription) goes with the report. If Kit refuses the report with
  # it, the run is reported again without it: ending the run matters more than counting it.
  report() {
    printf 'header = "x-api-key: %s"\n' "$run_token" \
      | curl --silent --show-error --max-time 30 --config - --request POST \
          --header 'content-type: application/json' --data "$1" \
          --output /dev/null --write-out '%{http_code}' "$base/api/runs/finished" 2>/dev/null || echo 000
  }
  plain="{\"status\":\"$outcome\"}"
  body="$plain"
  if [ -s "$tmp/usage.json" ]; then
    body="{\"status\":\"$outcome\",\"usage\":$(cat "$tmp/usage.json")}"
  fi
  rm -f "$tmp/usage.json"
  code="$(report "$body")"
  if [ "$code" != 200 ] && [ "$body" != "$plain" ]; then
    echo "kit-pool: Kit answered $code to $run_id's usage, reporting it without" >&2
    code="$(report "$plain")"
  fi
  [ "$code" = 200 ] || echo "kit-pool: could not report $run_id to Kit ($code)" >&2
  run_token=""
done
