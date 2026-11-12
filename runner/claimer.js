#!/usr/bin/env node
// BEH-USERS-9, runner pool: the claimer sidecar (kit#701). The ONLY process in a runner pod that holds the pool
// token. pool.sh, in the runner container, never sees it: it asks this process for the next run on localhost, and
// reports how the run ended through it. A run executes as the same user as pool.sh, so anything pool.sh could read
// (its environment, /proc/1/environ) a run can read too; the token therefore lives in another container.
//
//   POST /next      claims ONE run from Kit with the pool token. 200 + Kit's claim body, or 204 (nothing queued).
//                   409 while a claim is still outstanding, so asking again can never collect a second run.
//   POST /finished  forwards the body to Kit's /api/runs/finished under the outstanding run's token and relays
//                   Kit's status. Only a 200 from Kit frees the pod to claim again.
//
// Anyone in the pod can reach this port, a run included. What a run can get from it is bounded: nothing while its
// own claim is outstanding, and the pool token never. A claim that is never reported (the runner container died)
// is reported failed after KIT_CLAIM_MAX_SECONDS, and on SIGTERM, so its project is not held until its token expires.
//
// Environment: KIT_URL, KIT_RUNNER_POOL_TOKEN (required); KIT_CLAIMER_PORT (8787), KIT_CLAIMER_HOST (127.0.0.1),
// KIT_CLAIM_MAX_SECONDS (7200).
"use strict";
const http = require("node:http");

const base = (process.env.KIT_URL || "").replace(/\/+$/, "");
const poolToken = process.env.KIT_RUNNER_POOL_TOKEN || "";
if (!base || !poolToken) {
  console.error("kit-claimer: KIT_URL and KIT_RUNNER_POOL_TOKEN are required");
  process.exit(64);
}
const port = Number(process.env.KIT_CLAIMER_PORT || 8787);
const host = process.env.KIT_CLAIMER_HOST || "127.0.0.1";
const maxAgeMs = Number(process.env.KIT_CLAIM_MAX_SECONDS || 7200) * 1000;
const maxBody = 256 * 1024;

/** The one claim handed out and not yet reported: { id, token, at }. */
let outstanding = null;
let claiming = false;

const send = (res, status, body, type = "application/json") => {
  res.writeHead(status, { "content-type": type, "cache-control": "no-store" });
  res.end(body);
};

const readBody = req => new Promise((resolve, reject) => {
  const chunks = [];
  let size = 0;
  req.on("data", c => {
    size += c.length;
    if (size > maxBody) { reject(new Error("body too large")); req.destroy(); return; }
    chunks.push(c);
  });
  req.on("end", () => resolve(Buffer.concat(chunks)));
  req.on("error", reject);
});

const postFinished = async (token, body) => {
  const r = await fetch(`${base}/api/runs/finished`, {
    method: "POST",
    headers: { "x-api-key": token, "content-type": "application/json" },
    body,
    signal: AbortSignal.timeout(30000),
  });
  await r.arrayBuffer();
  return r.status;
};

/** Ends a claim the runner never reported, so Kit releases the project now rather than when the token expires. */
const abandon = async reason => {
  const held = outstanding;
  if (!held) return;
  try {
    const status = await postFinished(held.token, JSON.stringify({ status: "failed", failure: reason }));
    if (status === 200) outstanding = null;
    console.error(`kit-claimer: ${held.id} reported failed (${reason}), Kit answered ${status}`);
  } catch (e) {
    console.error(`kit-claimer: could not report ${held.id} (${reason}): ${e.message}`);
  }
};

const server = http.createServer(async (req, res) => {
  try {
    if (req.method !== "POST") return send(res, 405, "");
    const path = (req.url || "").split("?")[0];

    if (path === "/next") {
      if (outstanding && Date.now() - outstanding.at > maxAgeMs) await abandon("pod stopped reporting");
      if (outstanding || claiming) return send(res, 409, '{"error":"a claim is outstanding"}');
      claiming = true;
      try {
        const r = await fetch(`${base}/api/runs/claim`, {
          method: "POST",
          headers: { "x-kit-runner-pool": poolToken },
          signal: AbortSignal.timeout(60000),
        });
        const text = await r.text();
        if (r.status === 204) return send(res, 204, "");
        if (r.status !== 200) return send(res, 502, JSON.stringify({ error: "kit answered " + r.status }));
        let claim;
        try { claim = JSON.parse(text); } catch { return send(res, 502, '{"error":"unreadable claim"}'); }
        if (typeof claim.token !== "string" || typeof claim.id !== "string") return send(res, 502, '{"error":"unreadable claim"}');
        outstanding = { id: claim.id, token: claim.token, at: Date.now() };
        return send(res, 200, text);
      } finally {
        claiming = false;
      }
    }

    if (path === "/finished") {
      if (!outstanding) return send(res, 409, '{"error":"no claim is outstanding"}');
      const body = await readBody(req);
      const held = outstanding;
      const status = await postFinished(held.token, body);
      if (status === 200 && outstanding === held) outstanding = null;
      return send(res, status, "");
    }

    return send(res, 404, "");
  } catch (e) {
    console.error("kit-claimer: " + e.message);
    if (!res.headersSent) send(res, 502, '{"error":"claimer failed"}');
  }
});

const stop = async () => {
  await abandon("pod stopped");
  process.exit(0);
};
process.on("SIGTERM", stop);
process.on("SIGINT", stop);

server.listen(port, host, () => console.log(`kit-claimer: listening on ${host}:${port}`));
