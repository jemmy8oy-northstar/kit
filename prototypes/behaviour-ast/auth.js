#!/usr/bin/env node
'use strict';
/**
 * One password, so a write can be accepted from somewhere that is not loopback.
 *
 * `docs/design/ui.md` decision 1 says Kit's UI is a local developer tool, and
 * the write gate enforces it by refusing unless the server was STARTED on a
 * loopback address. That is a startup switch, not a check on the caller: it
 * makes a deployed Kit read-only, and it would do nothing at all for a loopback
 * Kit sitting behind a proxy. Both halves of that are measured on kit#44.
 *
 * James chose a password over GitHub sign-in and over an ingress lock (kit#44,
 * "I guess 1 will do"), so this module is the smallest thing that lets the gate
 * ask *who is calling* instead of *where did I bind*.
 *
 * ── the rule this module is built around ────────────────────────────────────
 * It is OFF unless a password is configured, and off means the loopback rule is
 * untouched. Local Kit must not change: he runs it on his laptop today and a
 * sign-in page appearing there would be this module deciding something nobody
 * asked it to decide. So `enabled()` is false for an unset, empty or
 * whitespace-only password, and every caller checks it before anything else.
 *
 * ── what this is NOT ────────────────────────────────────────────────────────
 * It is not a user system. There is one password and no accounts, because there
 * is one user (kit#25: he is Kit's first real user). Sessions are in memory: a
 * restart signs you out, which costs one re-login and — deliberately — needs no
 * storage. He parked the database on kit#41 and a session table would be that
 * decision quietly reversed by an implementation detail.
 */

const crypto = require('crypto');

/** Cookie the browser carries a session in. */
const COOKIE = 'kit_session';

/**
 * How long a session lasts. Seven days because the intended client is his
 * phone, where signing in repeatedly is the difference between a tool he uses
 * and one he doesn't — and the blast radius of a stolen cookie is one corpus in
 * a public repo, which is the same thing the password itself protects.
 */
const TTL_MS = 7 * 24 * 60 * 60 * 1000;

/**
 * Is a password configured at all?
 *
 * An empty or whitespace-only value counts as UNSET rather than as a password.
 * A Kubernetes secret that exists but holds an empty string is a real and easy
 * mistake, and treating `""` as a valid password would mean every request that
 * sent no password at all authenticated successfully — the exact inversion this
 * module exists to prevent, arriving silently.
 */
function enabled(opts = {}) {
  return typeof opts.password === 'string' && opts.password.trim().length > 0;
}

/**
 * Compare two secrets without leaking which byte differed.
 *
 * `crypto.timingSafeEqual` throws on length mismatch, and guarding that with an
 * early `a.length !== b.length` return would put the length back into the
 * timing. Hashing both to a fixed 32 bytes first removes the length from the
 * comparison entirely, so this is constant-time over inputs of any size.
 */
function secretsMatch(a, b) {
  if (typeof a !== 'string' || typeof b !== 'string') return false;
  const ha = crypto.createHash('sha256').update(a, 'utf8').digest();
  const hb = crypto.createHash('sha256').update(b, 'utf8').digest();
  return crypto.timingSafeEqual(ha, hb);
}

/**
 * Parse a `Cookie:` header into a map.
 *
 * Deliberately tolerant of the shapes a real browser sends — no cookies at all,
 * a trailing `;`, spaces after the separator, a value containing `=` (a base64
 * token from some other tool sharing the host). It never throws: a malformed
 * header is "no session", and an exception here would turn a cosmetic client
 * bug into a 500 on every request.
 */
function parseCookies(header) {
  const out = {};
  if (typeof header !== 'string' || !header) return out;
  for (const part of header.split(';')) {
    const eq = part.indexOf('=');
    if (eq < 1) continue;
    const name = part.slice(0, eq).trim();
    if (!name) continue;
    // `slice(eq + 1)` and not `split('=')[1]`: a base64 token ends in `=` and
    // splitting would silently truncate it to something that never matches.
    out[name] = part.slice(eq + 1).trim();
  }
  return out;
}

/**
 * An in-memory set of live session tokens.
 *
 * A factory rather than a module-level singleton so the suite can hold one per
 * test. Shared mutable state across tests is how a suite starts passing because
 * an earlier test signed in, which would make every assertion here meaningless.
 */
function sessions(now = () => Date.now()) {
  const live = new Map();

  /** Drop what has expired. Called on every read, so nothing accumulates. */
  function sweep() {
    const t = now();
    for (const [token, expires] of live) if (expires <= t) live.delete(token);
  }

  return {
    /**
     * Mint a token. 32 bytes from the CSPRNG — not `Math.random`, which is
     * seeded predictably enough that tokens could be guessed from each other.
     */
    create() {
      const token = crypto.randomBytes(32).toString('hex');
      live.set(token, now() + TTL_MS);
      return token;
    },
    valid(token) {
      if (typeof token !== 'string' || !token) return false;
      sweep();
      return live.has(token);
    },
    destroy(token) {
      return live.delete(token);
    },
    get size() {
      sweep();
      return live.size;
    },
  };
}

/**
 * The `Set-Cookie` value for a freshly minted session.
 *
 * Each attribute is doing a job and none is decoration:
 *   HttpOnly  — page script cannot read the token, so an XSS in the bundle
 *               cannot exfiltrate a long-lived credential.
 *   SameSite=Strict — the browser does not attach this cookie to a request
 *               originating from another site AT ALL. This is the second of the
 *               two CSRF defences (the first is the Origin check in `write`),
 *               and it is the one that holds even for a form POST that never
 *               triggers a preflight.
 *   Path=/    — the API and the bundle are served from one origin.
 *   Max-Age   — matches the server-side TTL. A cookie outliving its session
 *               means a browser that believes it is signed in and a server that
 *               disagrees, which presents as writes failing for no visible
 *               reason.
 *
 * `Secure` is CONDITIONAL, and that is not a shortcut. Deployed, Kit is behind
 * TLS and the flag must be set. Locally it is plain `http://127.0.0.1`, where a
 * `Secure` cookie is simply never sent back — so hard-coding it would make
 * sign-in silently impossible on a laptop, which is the configuration used to
 * develop this. It follows the same switch the deployment sets anyway.
 */
function cookieHeader(token, opts = {}) {
  const parts = [
    `${COOKIE}=${token}`,
    'HttpOnly',
    'SameSite=Strict',
    'Path=/',
    `Max-Age=${Math.floor(TTL_MS / 1000)}`,
  ];
  if (opts.secure) parts.push('Secure');
  return parts.join('; ');
}

/** The `Set-Cookie` value that removes a session from the browser. */
function clearCookieHeader(opts = {}) {
  const parts = [`${COOKIE}=`, 'HttpOnly', 'SameSite=Strict', 'Path=/', 'Max-Age=0'];
  if (opts.secure) parts.push('Secure');
  return parts.join('; ');
}

/**
 * Is this request carrying a live session?
 *
 * Takes the raw `Cookie` header rather than a parsed object so that the caller
 * cannot accidentally hand it something it trusts — the header is untrusted
 * input and this is the only place that decides what it means.
 */
function signedIn(store, cookieHeaderValue) {
  // No store means nobody can be signed in. This FAILS CLOSED on purpose: a
  // caller that reached here without one is misconfigured, and the two ways to
  // handle that are "refuse the write" and "throw on every request". Refusing
  // is recoverable and honest; throwing turns an auth wiring bug into a 500 on
  // the health check. It can never fail OPEN — there is no branch here that
  // returns true without a token the store minted.
  if (!store || typeof store.valid !== 'function') return false;
  const token = parseCookies(cookieHeaderValue)[COOKIE];
  return store.valid(token);
}

/** Consecutive failures tolerated before sign-in is refused for a while. */
const FREE_ATTEMPTS = 5;
/** How long the refusal lasts, doubling per failure past the allowance. */
const COOLDOWN_BASE_MS = 10 * 1000;
const COOLDOWN_MAX_MS = 15 * 60 * 1000;

/**
 * Refuse sign-in after repeated failures, so the password cannot be guessed.
 *
 * ⚠️ Deliberately GLOBAL rather than per-caller, and that is a trade with a
 * real downside worth stating. Per-IP is the usual answer, but deployed Kit
 * sits behind an ingress, so every request arrives from the proxy's address —
 * per-IP would either lock out everyone at once anyway or have to trust an
 * `X-Forwarded-For` header the client can simply write. A header the attacker
 * controls is not a key to rate-limit on; it is a free bypass.
 *
 * The cost of global is that someone who can reach the URL can keep him signed
 * out by failing on purpose. That is annoying and it is not a breach: reads are
 * unaffected, existing sessions keep working, and the cooldown is minutes. For
 * a one-user tool holding a public repo's corpus, lockout is the cheaper risk
 * than an unthrottled password.
 */
function throttle(now = () => Date.now()) {
  let failures = 0;
  let blockedUntil = 0;

  return {
    /** ms remaining before another attempt is allowed; 0 means go ahead. */
    retryAfterMs() {
      const left = blockedUntil - now();
      return left > 0 ? left : 0;
    },
    fail() {
      failures += 1;
      // `>=`, so FREE_ATTEMPTS means exactly what it says: that many attempts
      // are free and the NEXT one is refused. Written `>` first, which gave one
      // more guess than the constant advertises — a small thing, except that
      // the number in the name is the only description of this behaviour anyone
      // will read.
      if (failures >= FREE_ATTEMPTS) {
        const over = failures - FREE_ATTEMPTS + 1;
        // Capped: unbounded doubling reaches values that overflow to Infinity
        // and lock the door permanently, which turns a throttle into a denial
        // of service against its own owner.
        const wait = Math.min(COOLDOWN_BASE_MS * 2 ** (over - 1), COOLDOWN_MAX_MS);
        blockedUntil = now() + wait;
      }
    },
    /** A correct password clears the record — the guesser never got in. */
    succeed() {
      failures = 0;
      blockedUntil = 0;
    },
    get failures() {
      return failures;
    },
  };
}

module.exports = {
  COOKIE, TTL_MS, FREE_ATTEMPTS, COOLDOWN_BASE_MS, COOLDOWN_MAX_MS,
  enabled, secretsMatch, parseCookies, sessions, cookieHeader, clearCookieHeader, signedIn,
  throttle,
};
