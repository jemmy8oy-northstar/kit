#!/usr/bin/env bash
# BEH-USERS-9, slice B: one agent run. Clone, let Claude Code work headless, push, exit.
#
# Env contract (defined here; the launcher in slice A, IAgentRunLauncher, hands over only a
# run and its token, so slice C's Job must set these):
#   KIT_RUN_ID        required  Kit's id for the run (a branch name and log label)
#   KIT_URL           required  Kit's base URL, e.g. https://balenthiran.co.uk/kit (no trailing slash)
#   KIT_RUN_TOKEN     required  the per-run token; sent as x-api-key to Kit's model proxy
#   KIT_REPO          required  owner/repo to work on
#   KIT_TASK          required  what the user asked for
#   GITHUB_TOKEN      optional  a fixed token that can push to KIT_REPO; when absent (the pool case) git
#                               asks Kit's POST /api/runs/github-token each time it needs one
#                               (this script itself uses no gh CLI)
#   KIT_BASE_BRANCH   optional  branch to start from (default: the repo's default branch)
#   KIT_MODEL         optional  model for the run (default: sonnet)
#   KIT_MAX_TURNS     optional  turn limit (default: 100)
#   KIT_RUN_MAX_SECONDS optional  wall-clock limit on the agent, in seconds (default: 6600, under the claimer's 7200)
#   KIT_STAGE         optional  the pipeline stage; "test" (BEH-ORCH-12) makes the script check the agent's verdict line and never push
#   CLAUDE_CODE_OAUTH_TOKEN optional  James's subscription token (`claude setup-token`); see below
#   KIT_USAGE_FILE    optional  where to write Claude Code's usage JSON when the run is on the subscription
#   KIT_BUNDLES_FILE  optional  where to write the bundles of the kit/work/* branches the run pushed (BEH-ORCH-3)
#
# Two ways to reach the model. Without CLAUDE_CODE_OAUTH_TOKEN, only through Kit: ANTHROPIC_BASE_URL
# is <KIT_URL>/api/agent and the run token stands in for the API key, so Kit counts and caps every
# call. With it, the run uses James's subscription and goes straight to Anthropic (his kit#708: no
# pipeline until it can use his subscription). Kit's proxy then counts nothing, so the run's usage is
# written to KIT_USAGE_FILE for the pool to report with `finished`. The token is readable inside the
# run, which James accepted on kit#708; Kit's per-run dollar cap does not apply to it, the
# subscription's own limits and --max-turns do.
# Tokens are never echoed; git gets the GitHub token through a credential helper reading env.
#   KIT_FAILURE_FILE  optional  where to write "<step> exit <code>" when the run fails; only a fixed step word and a number
set -euo pipefail

# The step the run is in, so a failure can say where it stopped. Only this word and the exit code are ever
# written out: no stderr text, URL or token.
step=setup
work=""
helper=""
finish() {
  local code=$?
  if [ "$code" -ne 0 ] && [ -n "${KIT_FAILURE_FILE:-}" ]; then
    printf '%s exit %s\n' "$step" "$code" > "$KIT_FAILURE_FILE" 2>/dev/null || true
  fi
  [ -z "$work" ] || rm -rf "$work"
  [ -z "$helper" ] || rm -rf "$helper"
}
trap finish EXIT

missing=()
for v in KIT_RUN_ID KIT_URL KIT_RUN_TOKEN KIT_REPO KIT_TASK; do
  [ -n "${!v:-}" ] || missing+=("$v")
done
if [ "${#missing[@]}" -gt 0 ]; then
  echo "kit-run: missing required env: ${missing[*]}" >&2
  exit 64
fi

case "$KIT_REPO" in
  */*) ;;
  *) echo "kit-run: KIT_REPO must be owner/repo" >&2; exit 64 ;;
esac
case "$KIT_RUN_ID" in
  *[!A-Za-z0-9._-]*) echo "kit-run: KIT_RUN_ID has characters a branch name cannot" >&2; exit 64 ;;
esac
max_seconds="${KIT_RUN_MAX_SECONDS:-6600}"
case "$max_seconds" in
  ''|*[!0-9]*|0*) echo "kit-run: KIT_RUN_MAX_SECONDS must be a whole number of seconds above zero" >&2; exit 64 ;;
esac

unset ANTHROPIC_AUTH_TOKEN
if [ -n "${CLAUDE_CODE_OAUTH_TOKEN:-}" ]; then
  # An API key would win over the OAuth token, so none may be set.
  unset ANTHROPIC_BASE_URL ANTHROPIC_API_KEY
  echo "kit-run[$KIT_RUN_ID]: on the subscription"
else
  unset CLAUDE_CODE_OAUTH_TOKEN
  export ANTHROPIC_BASE_URL="${KIT_URL%/}/api/agent"
  export ANTHROPIC_API_KEY="$KIT_RUN_TOKEN"
fi
export CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC=1

# git authenticates at call time, so the token is in no URL, config or log.
# Without GITHUB_TOKEN (the normal case: a run carries no GitHub token) the credential helper asks Kit
# each time git needs one, with the run token; Kit mints a token for this ONE repo (contents and
# pull_requests write only) and refuses it once the run is over. The run token goes to curl in a
# config on stdin, so it is never in argv (visible in ps).
export GIT_TERMINAL_PROMPT=0
export KIT_URL KIT_RUN_TOKEN
scratch="${KIT_WORK_DIR:-/work}" # overridable only so a test can run this script outside the image
helper="$(mktemp "$scratch/credential.XXXXXX")"
cat > "$helper" <<'HELPER'
#!/usr/bin/env bash
# git credential helper: `get` prints a fresh repo-scoped token from Kit; anything else is a no-op.
[ "${1:-}" = get ] || exit 0
if [ -n "${GITHUB_TOKEN:-}" ]; then
  echo username=x-access-token
  echo "password=$GITHUB_TOKEN"
  exit 0
fi
# --fail-with-body keeps Kit's refusal, whose error and reason name no secret, so the log says WHICH one (kit#714).
if ! body="$(printf 'header = "x-api-key: %s"\n' "$KIT_RUN_TOKEN" \
  | curl --fail-with-body --silent --show-error --max-time 30 --config - --request POST "${KIT_URL%/}/api/runs/github-token")"; then
  printf '%s' "$body" | node -e '
    let s = "";
    process.stdin.on("data", (d) => (s += d)).on("end", () => {
      let j = {};
      try { j = JSON.parse(s); } catch {}
      const say = (v) => (typeof v === "string" ? v.replace(/[^\x20-\x7e]/g, "").slice(0, 300) : "");
      process.stderr.write(`kit-run: Kit refused a GitHub token: ${say(j.error) || "no reason given"}${j.reason ? ` (${say(j.reason)})` : ""}\n`);
    });'
  exit 1
fi
# Parsed as JSON: Kit writes it indented ("token": "..."), which a sed pattern without the space missed (kit#714).
token="$(printf '%s' "$body" | node -e '
  let s = "";
  process.stdin.on("data", (d) => (s += d)).on("end", () => {
    try { const t = JSON.parse(s).token; if (typeof t === "string") process.stdout.write(t); } catch {}
  });')"
if [ -z "$token" ]; then
  echo "kit-run: Kit's github-token answer held no token" >&2
  exit 1
fi
echo username=x-access-token
echo "password=$token"
HELPER
chmod 700 "$helper"
export GIT_CONFIG_COUNT=1
export GIT_CONFIG_KEY_0="credential.helper"
export GIT_CONFIG_VALUE_0="!$helper"

work="$(mktemp -d "$scratch/run.XXXXXX")"

step=clone
echo "kit-run[$KIT_RUN_ID]: cloning $KIT_REPO"
git clone --quiet "https://github.com/${KIT_REPO}.git" "$work/repo"
cd "$work/repo"
git config user.name "Kit agent"
git config user.email "agent@kit.invalid"
step=checkout
if [ -n "${KIT_BASE_BRANCH:-}" ]; then
  git checkout --quiet "$KIT_BASE_BRANCH"
fi
branch="kit/run-${KIT_RUN_ID}"
git checkout --quiet -b "$branch"

step=process
# The process is served by Kit so a fix reaches every run at once. The route is public, so
# the run token is not sent: it goes only to the model proxy.
process="$work/process.md"
curl --fail --silent --show-error --location "${KIT_URL%/}/api/process" -o "$process"

step=claude
echo "kit-run[$KIT_RUN_ID]: working"
# bypassPermissions, so the agent can run the repo's tests: acceptEdits refuses every command.
# The Job is the sandbox (scratch disk, no Kubernetes token, a capped run token, a GitHub token
# for one repo), which is the default proposed on kit#684 and met with silence.
# JSON, so the usage can be read; the agent's final message is still printed for the log.
result="$work/result.json"
status=0
# BEH-ORCH-14: nothing else bounds a run's time. The claimer's limit acts only when pool.sh asks for the next run, which a
# hung agent never lets it do, and a subscription run never meets the proxy's token age. timeout signals the agent's whole
# process group, so a test server it left running dies with it rather than holding the pod.
started=$SECONDS
kill_after="${KIT_RUN_KILL_AFTER:-30}" # overridable only so a test can reach the SIGKILL path quickly
timeout --kill-after="$kill_after" "$max_seconds" claude -p "$KIT_TASK" \
  --append-system-prompt "$(cat "$process")" \
  --model "${KIT_MODEL:-sonnet}" \
  --max-turns "${KIT_MAX_TURNS:-100}" \
  --permission-mode bypassPermissions \
  --output-format json > "$result" || status=$?
# A failed run spent tokens too, so its usage is written before the exit status is acted on.
node -e '
  const [file, usageFile] = process.argv.slice(1);
  let r;
  try { r = JSON.parse(require("fs").readFileSync(file, "utf8")); } catch { process.exit(0); }
  if (typeof r.result === "string") console.log(r.result);
  if (usageFile && r.usage && typeof r.usage === "object") {
    const u = {};
    for (const k of ["input_tokens", "output_tokens", "cache_creation_input_tokens", "cache_read_input_tokens"]) {
      u[k] = Number.isSafeInteger(r.usage[k]) && r.usage[k] >= 0 ? r.usage[k] : 0;
    }
    require("fs").writeFileSync(usageFile, JSON.stringify(u));
  }
' "$result" "${CLAUDE_CODE_OAUTH_TOKEN:+${KIT_USAGE_FILE:-}}"
# 137 is also a kill from elsewhere (out of memory), so it counts as the limit only once the limit has passed.
if [ "$status" -eq 124 ] || { [ "$status" -eq 137 ] && [ $((SECONDS - started)) -ge "$max_seconds" ]; }; then
  step=time
  echo "kit-run[$KIT_RUN_ID]: claude ran out of time (${max_seconds}s)" >&2
  exit 124
fi
if [ "$status" -ne 0 ]; then
  echo "kit-run[$KIT_RUN_ID]: claude exited $status" >&2
  exit "$status"
fi

# BEH-ORCH-1: an error result (running out of --max-turns above all) can still exit 0, so the run would end finished with
# nothing pushed and nothing said. Its subtype is read instead, and only a fixed step word goes out: "turns" or "result".
ended="$(node -e '
  let r;
  try { r = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8")); } catch { process.exit(0); }
  const sub = typeof r.subtype === "string" ? r.subtype : "";
  if (sub === "error_max_turns") process.stdout.write("turns");
  else if (r.is_error === true || sub.startsWith("error")) process.stdout.write("result");
' "$result")" || ended=""
if [ -n "$ended" ]; then
  step="$ended"
  echo "kit-run[$KIT_RUN_ID]: claude ended with an error result ($ended, max turns ${KIT_MAX_TURNS:-100})" >&2
  exit 3
fi

# BEH-ORCH-12: a test run's verdict is checked here, not taken from claude's exit code (an agent that saw failures and
# said so in prose still exits 0). Fail-closed: it passes only when the last non-empty line of the agent's final message
# is exactly "KIT-TEST: PASS <40 lowercase hex>", that commit is the one the task named, and HEAD is that commit.
# A test run never reports bundles and never pushes, whatever the agent left in the tree.
if [ "${KIT_STAGE:-}" = test ]; then
  step=verdict
  want="$(node -e '
    let r;
    try { r = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8")); } catch { process.exit(0); }
    if (typeof r.result !== "string") process.exit(0);
    const lines = r.result.split(/\r?\n/).filter((l) => l.trim() !== "");
    const m = /^KIT-TEST: PASS ([0-9a-f]{40})$/.exec(lines[lines.length - 1] ?? "");
    if (m) process.stdout.write(m[1]);
  ' "$result")" || want=""
  [ -n "$want" ] || exit 1
  case "$KIT_TASK" in *"$want"*) ;; *) exit 1 ;; esac
  head="$(git rev-parse HEAD)" || exit 1
  [ "$head" = "$want" ] || exit 1
  echo "kit-run[$KIT_RUN_ID]: tests passed at $want; nothing pushed"
  exit 0
fi

step=bundles
# BEH-ORCH-3: the bundles a triage pushed go back to Kit with the pool's finished report. A work branch counts only
# when origin has exactly what the run has, i.e. it was pushed. Each bundle is sent whole or not at all (text null),
# so the report stays inside Kit's 64KB body limit; Kit stops an item whose text is null and says why.
if [ -n "${KIT_BUNDLES_FILE:-}" ]; then
  node -e '
    const { execFileSync } = require("child_process");
    const git = (...a) => execFileSync("git", a, { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] });
    const tryGit = (...a) => { try { return git(...a); } catch { return null; } };
    const [file, maxEach, maxAll, maxCount] = [process.argv[1], 8000, 48000, 50];
    const prefix = "refs/heads/kit/work/";
    const out = [];
    let room = maxAll;
    for (const ref of (tryGit("for-each-ref", "--format=%(refname)", prefix) || "").split("\n").filter(Boolean)) {
      const slug = ref.slice(prefix.length);
      if (out.length >= maxCount || slug.length > 60 || !/^[a-z0-9]+(-[a-z0-9]+)*$/.test(slug)) continue;
      const local = tryGit("rev-parse", ref);
      if (!local || local !== tryGit("rev-parse", "refs/remotes/origin/kit/work/" + slug)) continue;
      let text = tryGit("show", ref + ":.kit/bundles/" + slug + ".md") ?? "";
      // Measured as it will be sent: JSON-escaped UTF-8 bytes.
      const size = Buffer.byteLength(JSON.stringify(text)) + slug.length + 24;
      if (size > maxEach || size > room) text = null;
      else room -= size;
      out.push({ slug, text });
    }
    require("fs").writeFileSync(file, JSON.stringify(out));
  ' "$KIT_BUNDLES_FILE"
fi

# Push only if the agent left something behind; an empty run is a success with nothing to push.
if [ "$(git rev-list --count "HEAD" --not --remotes)" -eq 0 ] && git diff --quiet && [ -z "$(git status --porcelain)" ]; then
  echo "kit-run[$KIT_RUN_ID]: nothing to push"
  exit 0
fi
step=push
if [ -n "$(git status --porcelain)" ]; then
  git add -A
  git commit --quiet -m "kit run ${KIT_RUN_ID}: ${KIT_TASK:0:60}"
fi
git push --quiet origin "$branch"
echo "kit-run[$KIT_RUN_ID]: pushed $branch"
