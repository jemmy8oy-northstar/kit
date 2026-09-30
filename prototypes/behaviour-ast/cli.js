// An unknown flag is a REFUSAL, never a silent drop.
//
// kit#67 found this at `kit.js`, which accepted every flag it did not have and
// dropped it without a word — so `node kit.js kit --dir /elsewhere` printed a
// confident report about Kit's own corpus and nothing in the output said the flag
// had been ignored. kit#68 fixed that one entry point. It did not fix the others,
// and there are six of them:
//
//   requires.js   user-facing, and `--check` is a GATE
//   project.js    user-facing — the read model the UI is built on
//   saturation.js `--check` gates a committed document
//   self-host.js  `--check` gates a committed document
//   converge.js   a measurement that gets quoted
//   prose-audit.js a measurement that gets quoted
//   writer.js     WRITES to a corpus
//
// 🔑 The reason this matters more than a usability nit: every one of those tools
// exists to answer a question about a NAMED artefact, and a dropped flag makes it
// answer about a different artefact with no change in how the answer looks. The
// measured case is `requires.js snip-it --check --dir <elsewhere>`, which reports
// 16 nouns and exits 1 about Kit's own corpus while naming yours; and
// `requires.js snip-it --dir /no/such/dir`, which exits 0 with a full report about
// a directory that cannot exist.
//
// ⚠️ This does NOT give any tool a flag it lacks. `kit.js:1187` deferred whether
// `kit.js` should GAIN `--dir` to James, on the grounds that a relocated corpus
// takes the noun namespace out of the only directory `sharedWith` can see, and
// that deferral stands here: refusing a flag makes its absence loud and pre-empts
// nothing. A tool that refuses `--dir` is telling you the truth; a tool that
// swallows it is not.
//
// Deliberately NOT a parser. `check.js:94` and `kit.js:1196` each have a full
// positional parser with this rule inside it, and folding all three into one is
// the right end state — but it re-anchors seven mutants and consolidating two
// mutation-covered parsers is an architecture call rather than a fix. So this is
// the one rule, callable, and the parsers stay where they are.

// The first token that looks like a flag and is not in `known`, else null.
//
// A value that itself starts with `-` is reported rather than accepted, which is
// the same call `check.js:99` and `kit.js:1208` make: `--dir --check` means the
// value was forgotten, and consuming the next flag as a path is how a gate ends
// up pointed somewhere nobody named.
function unknownFlag(argv, known) {
  const set = known instanceof Set ? known : new Set(known);
  for (const a of argv) {
    if (typeof a !== 'string') continue;
    if (!a.startsWith('-')) continue;
    // A lone `-` is the stdin convention, not a flag. No tool here uses it; it is
    // excluded so that adding one later does not start by tripping over this.
    if (a === '-') continue;
    if (set.has(a)) continue;
    return a;
  }
  return null;
}

// What a refusal says. One phrasing, because six tools saying it six ways is how
// the next reader concludes it is a different kind of problem in each.
// `cannot look` is `check.js:120`'s and `kit.js`'s existing wording for exit 2 —
// the three-valued rule, where a refusal is never conflated with a pass.
function refuse(flag, usage) {
  process.stderr.write(`cannot look: unknown option ${flag}\n`);
  if (usage) process.stderr.write(`${usage}\n`);
  return 2;
}

module.exports = { unknownFlag, refuse };
