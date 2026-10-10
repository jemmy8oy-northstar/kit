#!/usr/bin/env node
//
// check-goldens.js — Node's answers for the C# `kit check` to match (kit#119, cut 2)
// ─────────────────────────────────────────────────────────────────────────────
// Records, into backend/Balenthiran.Kit.Tests/Fixtures/Check/goldens.json:
//   · `titles`: testTitles() and expectedTestCount() for each source below;
//   · `checks`: check.js run as a real process over each case — stdout, stderr
//     and exit code, byte for byte.
//
// The inputs live INSIDE the golden, as strings, and are written to a temp
// directory only for the run. Committed under their real names, a fixture
// `*.spec.ts` would be read by Kit's own Stage-0 gate (`check.js kit --repo .`
// walks the whole repo) and a fixture `*Tests.cs` would be compiled into the
// test project.
//
// The expected side never passes through C#. When the JS engine is deleted
// these goldens are frozen; that is the point of recording them now.
//
//   node prototypes/behaviour-ast/check-goldens.js            # rewrite the golden
//   node prototypes/behaviour-ast/check-goldens.js --check    # exit 1 if it would change

'use strict';
const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');
const { testTitles, expectedTestCount } = require('./kit');

const OUT = path.join(__dirname, '..', '..', 'backend', 'Balenthiran.Kit.Tests', 'Fixtures', 'Check', 'goldens.json');
const CHECK = path.join(__dirname, 'check.js');

// ── the title reader ─────────────────────────────────────────────────────────
// The cases kit.test.js already holds, plus the edges a port is likeliest to get
// wrong: line numbers, CRLF, a BOM, Unicode whitespace, a word character that is
// not ASCII, and a file that ends inside a group.
const TITLE_SOURCES = [
  ['a.spec.ts', "test('alpha', () => {});\nit('beta', async () => {});\n"],
  ['a.spec.ts', "test.only('alpha', () => {});\nit.skip('beta', () => {});\ntest.fixme(\"gamma\", () => {});\n"],
  ['T.cs', '    [Fact]\n    public void Does_A_Thing()\n    {\n    }\n'],
  ['T.cs', '    [Fact(DisplayName = "a nicer name")]\n    public void Does_A_Thing()\n'],
  ['T.cs', '    [Theory]\n    // a comment\n' + Array.from({ length: 8 }, (_, i) => `    [InlineData(${i})]\n`).join('') + '\n    public void Theory_Method(int n)\n    {\n    }\n'],
  ['T.cs', '    [Fact]\n    private readonly int _notATest = 1;\n\n    [Fact]\n    public void Real_Test()\n'],
  ['T.cs', '[Fact]\n[Theory]\n[InlineData(1)]\n'],
  ['T.cs', '    [Fact]\n    public async Task Awaits_Something()\n    [Fact]\n    internal async ValueTask<int> Generic_Return()\n    [Fact]\n    public void Tuple_Param((int, string) x)\n'],
  ['T.cs', '    [Fact(Skip = "later")]\n    /* block */\n    * continuation\n    public void After_Block_Comment()\n'],
  ['T.cs', '    [Fact(DisplayName = "escaped \\" quote")]\n    public void X()\n'],
  ['T.cs', '\r\n    [Fact]\r\n    public void Crlf_Method()\r\n    {\r\n    }\r\n'],
  ['T.cs', '    [FactAttribute]\n    public void Long_Name()\n    [Facts]\n    public void Not_Fact()\n'],
  ['T.cs', '    [Fact]\n    public void Ünïcode_Name()\n'],
  ['T.cs', '    [Fact] public void Same_Line()\n    [Fact]\n'],
  ['a.spec.ts', "it.each([[1, 'a'], [2, 'b']])('handles %s', (n, s) => {});"],
  ['a.spec.ts', "it.each([['a)]', 1], ['b((', 2]])('closes over %s', (s, n) => {});"],
  ['a.spec.ts', "it.each([[Math.max(1, 2), 'a']])('computes %s', (n, s) => {});"],
  ['a.spec.ts', 'it.each`\n  a | b\n  ${1} | ${2}\n`("$a plus $b", () => {});'],
  ['a.spec.ts', "it.each([1, 2]);\ntest('after a table with no call', () => {});"],
  ['a.spec.ts', "it.each([1, /* ] */ 2, // )\n 3])('commented %s', () => {});"],
  ['a.spec.ts', "it.each([1, 2"],
  ['a.spec.ts', "const fixture = ['test(\"[BEH-1] a\", () => {})'];\ntest('the real one', () => {});"],
  ['a.spec.ts', "// test('not this one', () => {})\ntest('the real one', () => {});"],
  ['a.spec.ts', "/* test('nor this', () => {}) */\ntest('the real one', () => {});"],
  ['a.spec.ts', 'const ok = TEST_FILE_RE.test(name);\ntest("real", () => {});'],
  ['a.spec.ts', 'beforeEach(() => {}); test("shared line", () => {});'],
  ['a.spec.ts', 'describe("g", () => {\n  it("indented", () => {});\n  await test("awaited", () => {});\n  return it("returned", () => {});\n});'],
  ['a.spec.ts', 'test(`template ${x} title`, () => {});\ntest(name, () => {});\ntest(\'esc \\\' aped\', () => {});'],
  ['a.spec.ts', '\r\ntest("crlf one", () => {});\r\n\ttest("tabbed", () => {});\r\n'],
  ['a.spec.ts', '\ufefftest("after a bom", () => {});\n'],
  ['a.spec.ts', '\u00a0test("nbsp indent", () => {});\n\u2028test("after a line separator", () => {});\n'],
  ['a.spec.ts', 'xtest("x", () => {});\n$test("y", () => {});\nmy.it("z", () => {});\nitem("w", () => {});\n'],
  ['a.spec.ts', 'it.concurrent("conc", () => {});\nit.each`a\n${1}`(\'tagged\', () => {});\ntest\n("newline before paren", () => {});\n'],
  ['a.spec.ts', 'const s = "unterminated\ntest("after a broken string", () => {});\n'],
  ['a.spec.ts', 'const t = `${ "}" } still template`; test("x", () => {});\ntest("y", () => {});\n'],
  ['a.test.js', "test('a', () => {});\n".repeat(3)],
  // JavaScript's \s has U+FEFF and not U+0085; .NET's is the other way round.
  ['a.spec.ts', 'test(\ufeff"bom before the title", () => {});\nit(\u0085"nel before the title", () => {});\n'],
];

// ── the gate ─────────────────────────────────────────────────────────────────
const CORPUS = [
  'behaviour BEH-A1 "a signed-in user saves"',
  '  when opens page:Home',
  '  then sees button:Save',
  '',
  'behaviour BEH-A2 "a second behaviour"',
  '  when opens page:Settings',
  '  then sees field:Name',
  '',
].join('\n');

const SPEC = "test('[BEH-A1] saves', () => {});\ntest('[BEH-A2] second', () => {});\n";

function corpusWith(extra) { return CORPUS + extra; }

const CASES = [
  {
    name: 'mapping-all-covered',
    files: {
      'behaviours/app.beh': CORPUS,
      'behaviours/app.tests.json': JSON.stringify({ _note: 'metadata is skipped', 'BEH-A1': [{ file: 'tests/a.spec.ts', title: '[BEH-A1] saves' }], 'BEH-A2': [{ file: 'tests/a.spec.ts', title: '[BEH-A2] second' }] }),
      'repo/tests/a.spec.ts': SPEC,
      'repo/node_modules/x.spec.ts': "test('ignored', () => {});\n",
    },
    args: ['app', '--repo', 'repo', '--dir', 'behaviours'],
  },
  {
    name: 'mapping-uncovered-and-broken-entries',
    files: {
      'behaviours/app.beh': corpusWith('behaviour BEH-A3 "a third"\n  when opens page:Home\n'),
      'behaviours/app.tests.json': JSON.stringify({
        'BEH-A1': [{ file: 'tests/a.spec.ts', title: '[BEH-A1] renamed' }],
        'BEH-A2': [{ file: 'tests/missing.spec.ts', title: 'x' }, { file: 'tests/b.spec.ts', title: 'dup' }],
        'BEH-ZZ': [{ file: 'tests/a.spec.ts', title: '[BEH-A1] saves' }],
        '7': [{ file: 'tests/a.spec.ts', title: '[BEH-A1] saves' }],
      }),
      'repo/tests/a.spec.ts': SPEC,
      'repo/tests/b.spec.ts': "test('dup', () => {});\ntest('dup', () => {});\n",
    },
    args: ['app', '--repo', 'repo', '--dir', 'behaviours'],
  },
  {
    name: 'markers-orphan',
    files: {
      'behaviours/app.beh': CORPUS,
      'repo/a/Thing.test.js': "test('[BEH-A1] saves', () => {});\ntest('[BEH-GONE] stale', () => {});\n",
      'repo/b/WidgetTests.cs': '    [Fact]\n    public void Covers_A2() { /* [BEH-A2] */ }\n',
    },
    args: ['app', '--repo', 'repo', '--via', 'markers', '--dir', 'behaviours'],
  },
  {
    name: 'markers-all-covered',
    files: {
      'behaviours/app.beh': CORPUS,
      'repo/a.spec.ts': SPEC,
    },
    args: ['--via', 'markers', 'app', '--dir', 'behaviours', '--repo', 'repo'],
  },
  {
    name: 'pending',
    files: {
      'behaviours/app.beh': corpusWith('behaviour BEH-P1 "not built yet"\n  pending\n  when opens page:Home\n\nbehaviour BEH-P2 "built but still pending"\n  pending\n  when opens page:Home\n'),
      'behaviours/app.tests.json': JSON.stringify({
        'BEH-A1': [{ file: 'a.spec.ts', title: '[BEH-A1] saves' }],
        'BEH-A2': [{ file: 'a.spec.ts', title: '[BEH-A2] second' }],
        'BEH-P2': [{ file: 'a.spec.ts', title: 'p2' }],
      }),
      'repo/a.spec.ts': SPEC + "test('p2', () => {});\n",
    },
    args: ['app', '--repo', 'repo', '--dir', 'behaviours'],
  },
  {
    name: 'pending-only-uncovered-passes',
    files: {
      'behaviours/app.beh': corpusWith('behaviour BEH-P1 "not built yet"\n  pending\n  when opens page:Home\n'),
      'behaviours/app.tests.json': JSON.stringify({
        'BEH-A1': [{ file: 'a.spec.ts', title: '[BEH-A1] saves' }],
        'BEH-A2': [{ file: 'a.spec.ts', title: '[BEH-A2] second' }],
      }),
      'repo/a.spec.ts': SPEC,
    },
    args: ['app', '--repo', 'repo', '--dir', 'behaviours'],
  },
  {
    name: 'layers',
    files: {
      'behaviours/app.beh': corpusWith(
        'behaviour BEH-U1 "a ui behaviour"\n  layer ui\n  when opens page:Home\n\n' +
        'behaviour BEH-U2 "a pending ui behaviour"\n  layer ui\n  pending\n  when opens page:Home\n\n' +
        'behaviour BEH-T1 "technical, e2e only"\n  layer technical\n  when opens page:Home\n\n' +
        'behaviour BEH-T2 "technical, unit tested"\n  layer technical\n  when opens page:Home\n\n' +
        'behaviour BEH-T3 "technical, untested"\n  layer technical\n  when opens page:Home\n'),
      'behaviours/app.tests.json': JSON.stringify({
        'BEH-A1': [{ file: 'a.spec.ts', title: '[BEH-A1] saves' }],
        'BEH-A2': [{ file: 'a.spec.ts', title: '[BEH-A2] second' }],
        'BEH-U1': [{ file: 'a.spec.ts', title: 'u1' }],
        'BEH-T1': [{ file: 'a.spec.ts', title: 't1' }],
        'BEH-T2': [{ file: 'a.spec.ts', title: 't1' }, { file: 'unit/LogicTests.cs', title: 'T2_Works' }],
      }),
      'repo/a.spec.ts': SPEC + "test('u1', () => {});\ntest('t1', () => {});\n",
      'repo/unit/LogicTests.cs': '    [Fact]\n    public void T2_Works()\n',
    },
    args: ['app', '--repo', 'repo', '--dir', 'behaviours'],
  },
  {
    name: 'layers-markers',
    files: {
      'behaviours/app.beh': '# kit:layer technical\n\n' + CORPUS,
      'repo/e2e/flow.spec.ts': "test('[BEH-A1] saves', () => {});\n",
      'repo/unit/thing.test.ts': "test('[BEH-A2] second', () => {});\n",
    },
    args: ['app', '--repo', 'repo', '--via', 'markers', '--dir', 'behaviours'],
  },
  {
    name: 'count-disagreement',
    files: {
      'behaviours/app.beh': CORPUS,
      'behaviours/app.tests.json': '{}',
      'repo/a.spec.ts': SPEC,
      'repo/b.spec.ts': 'beforeEach(() => {}); test("shared line", () => {});\n',
    },
    args: ['app', '--repo', 'repo', '--dir', 'behaviours'],
  },
  {
    name: 'count-disagreement-cs',
    files: {
      'behaviours/app.beh': CORPUS,
      'behaviours/app.tests.json': '{}',
      'repo/ATests.cs': '    [Fact]\n    private void Not_Public()\n',
    },
    args: ['app', '--repo', 'repo', '--dir', 'behaviours'],
  },
  {
    name: 'zero-test-files',
    files: { 'behaviours/app.beh': CORPUS, 'behaviours/app.tests.json': '{}', 'repo/readme.md': 'nothing' },
    args: ['app', '--repo', 'repo', '--dir', 'behaviours'],
  },
  {
    name: 'no-corpus',
    files: { 'behaviours/other.beh': CORPUS, 'repo/a.spec.ts': SPEC },
    args: ['app', '--repo', 'repo', '--dir', 'behaviours'],
  },
  {
    name: 'empty-corpus',
    files: { 'behaviours/app.beh': '# nothing here\n', 'repo/a.spec.ts': SPEC },
    args: ['app', '--repo', 'repo', '--dir', 'behaviours'],
  },
  {
    name: 'no-mapping',
    files: { 'behaviours/app.beh': CORPUS, 'repo/a.spec.ts': SPEC },
    args: ['app', '--repo', 'repo', '--dir', 'behaviours'],
  },
  {
    name: 'no-repo',
    files: { 'behaviours/app.beh': CORPUS },
    args: ['app', '--repo', 'missing', '--dir', 'behaviours'],
  },
  {
    name: 'dir-normalised',
    files: { 'behaviours/app.beh': CORPUS, 'behaviours/app.tests.json': '{}', 'repo/a.spec.ts': SPEC },
    args: ['app', '--repo', './repo/', '--dir', './x/../behaviours/'],
  },
  {
    // Written as TEXT: `JSON.stringify` of a JS object would already have put "12" first,
    // and the order JSON.parse leaves the keys in is the thing under test.
    name: 'mapping-key-order',
    files: {
      'behaviours/app.beh': CORPUS,
      'behaviours/app.tests.json': '{"BEH-A1": [{"file": "a.spec.ts", "title": "nope"}], "12": [], "BEH-QQ": [], "3": []}',
      'repo/a.spec.ts': SPEC,
    },
    args: ['app', '--repo', 'repo', '--dir', 'behaviours'],
  },
  {
    // Node's 'utf8' keeps a BOM, so a first-line test is not at a statement start — the two
    // counts disagree. A reader that strips it would read the test and pass.
    name: 'bom-test-file',
    files: { 'behaviours/app.beh': CORPUS, 'behaviours/app.tests.json': '{}', 'repo/a.spec.ts': '\ufefftest("[BEH-A1] saves", () => {});\n' },
    args: ['app', '--repo', 'repo', '--dir', 'behaviours'],
  },
  {
    // Walk order is printed: in the evidence list, and in which file a refusal names.
    name: 'walk-order',
    files: {
      'behaviours/app.beh': '# kit:layer technical\n\n' + CORPUS,
      'repo/b/z.spec.ts': "test('[BEH-A1] z', () => {});\n",
      'repo/B.spec.ts': "test('[BEH-A1] upper', () => {});\n",
      'repo/a.spec.ts': "test('[BEH-A1] lower', () => {});\n",
      'repo/a/Unit.test.ts': "test('[BEH-A2] unit', () => {});\n",
    },
    args: ['app', '--repo', 'repo', '--via', 'markers', '--dir', 'behaviours'],
  },
  {
    name: 'ui-uncovered',
    files: {
      'behaviours/app.beh': corpusWith('behaviour BEH-U3 "a ui behaviour nobody tests"\n  layer ui\n  when opens page:Home\n'),
      'behaviours/app.tests.json': JSON.stringify({ 'BEH-A1': [{ file: 'a.spec.ts', title: '[BEH-A1] saves' }], 'BEH-A2': [{ file: 'a.spec.ts', title: '[BEH-A2] second' }] }),
      'repo/a.spec.ts': SPEC,
    },
    args: ['app', '--repo', 'repo', '--dir', 'behaviours'],
  },
  { name: 'usage-no-args', files: {}, args: [] },
  { name: 'usage-no-repo', files: {}, args: ['app'] },
  { name: 'bad-via', files: {}, args: ['app', '--repo', 'repo', '--via', 'tags'] },
  { name: 'unknown-flag', files: {}, args: ['app', '--repo', 'repo', '--verbose'] },
  { name: 'help-flag', files: {}, args: ['-h'] },
  { name: 'flag-missing-value', files: {}, args: ['app', '--repo', '--dir', 'x'] },
  { name: 'flag-at-end', files: {}, args: ['app', '--dir'] },
  { name: 'two-apps', files: {}, args: ['app', 'other', '--repo', 'repo'] },
  { name: 'dash-value', files: {}, args: ['-', '--repo', 'repo'] },
];

function materialise(files) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'kit-check-golden-'));
  for (const [rel, text] of Object.entries(files)) {
    fs.mkdirSync(path.dirname(path.join(root, rel)), { recursive: true });
    fs.writeFileSync(path.join(root, rel), text);
  }
  return root;
}

function record() {
  const titles = TITLE_SOURCES.map(([file, src]) => ({
    file,
    src,
    titles: testTitles(file, src),
    expectedCount: expectedTestCount(file, src),
  }));
  const checks = CASES.map((c) => {
    const root = materialise(c.files);
    try {
      const r = spawnSync(process.execPath, [CHECK, ...c.args], { cwd: root, encoding: 'utf8' });
      return { name: c.name, files: c.files, args: c.args, exitCode: r.status, stdout: r.stdout, stderr: r.stderr };
    } finally {
      fs.rmSync(root, { recursive: true, force: true });
    }
  });
  return JSON.stringify({ _generatedBy: 'prototypes/behaviour-ast/check-goldens.js', titles, checks }, null, 2) + '\n';
}

if (require.main === module) {
  const next = record();
  if (process.argv.includes('--check')) {
    const now = fs.existsSync(OUT) ? fs.readFileSync(OUT, 'utf8') : '';
    if (now !== next) { console.error(`${path.relative(process.cwd(), OUT)} is stale — re-run without --check`); process.exit(1); }
    console.log('check goldens: current');
  } else {
    fs.mkdirSync(path.dirname(OUT), { recursive: true });
    fs.writeFileSync(OUT, next);
    console.log(`wrote ${path.relative(process.cwd(), OUT)}`);
  }
}

module.exports = { TITLE_SOURCES, CASES };
