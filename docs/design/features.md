# Features, parents as tags, and one Kit — design

_Written 2026-10-11 against James's note on
[kit#237](https://github.com/jemmy8oy-northstar/kit/issues/237):_

> Top level can be split by projects then as you click into a project you see the top level of
> features … let's consider views/pages as features more broadly. Features can parent other
> features, they can also parent spec. … instead of having strong child parent relationships we
> should consider the parent relationships as tags … a feature can be a sub feature of multiple
> features. For example a component being a part of multiple pages.
>
> Another thing why does kit have 3 different projects viewable in kit. I am envisioning these all
> being potentially different features of the kit project.

**This is planning, and it changes the corpus language, so it is his (#58).** Two decisions below
carry a default that I will act on from **2026-10-14** if he stays silent. Nothing here is built.

## Decision 1 — a `feature` declaration, and `in` as a parent tag

```
feature FEAT-BOARD "Board view"
  in FEAT-KIT-UI

feature FEAT-PROJECT-CARD "Project card"
  in FEAT-BOARD
  in FEAT-PROJECTS

behaviour BEH-BOARD-1 "The board shows every behaviour in a column for where it stands"
  in FEAT-BOARD
  ...
```

- **`feature <ID> "<title>"`** opens a block exactly as `behaviour` does. Its body takes only `in`,
  `stage` and `#` comments. A page is a feature: no separate `page` kind, because his note folds them together.
- **`stage <problem|ux|logic|development|ui>`** (FEAT-STAGES, added 2026-10-19 after he approved
  BEH-STAGE-1) puts the feature on the stages board in that column. At most one per feature; none
  keeps it off the board (BEH-STAGE-7, proposed).
- **`epic EPIC-<ID> "<title>"`** (BEH-EPIC-2, his 2026-10-19 17:25Z note) is its own block, with
  the file's project as its parent and no lines yet. A feature joins it with `in EPIC-…`, which is
  kept in `epics` apart from `in`, so the project tree never changes. A behaviour reaches an epic
  through its feature. ⚠️ A corpus may use `epic` only once the parser is on main.
- **`decision ADR-<PART>-<N> "<title>"`** (BEH-ARCH-1, slices A and B1) is its own block: one
  architecture decision. Lines: `part frontend|backend|data|deployment` and `chosen <text>` (both
  required, once each), `considered <text>` (optional, repeatable), `because <text>` (required,
  repeatable), and the `source` and `review` lines a behaviour takes. A decision with no `review`
  line is unreviewed. The project page's Architecture tab groups them by part; the tab shows only
  when the corpus has one. Each card has Approve and Deny (a denial states its correction), which
  rewrite only that block's `review` line (`POST /api/projects/<app>/decisions/<id>/review`).
  ⚠️ A corpus may use `decision` only once the parser is on main.
- **`in <FEAT-ID>`** names a parent. It can be repeated, and each line is a tag, not an ownership, so a
  card can sit under the board *and* the project page. It is valid on a feature and on a behaviour.
- **No `in` means top level** of the project. Nothing has to be tagged, so every existing corpus
  parses unchanged and shows as today: a flat list.
- **Refused at parse time**, each with a sentence naming the line: an `in` naming a feature that does
  not exist in the project, a cycle (a feature that is its own ancestor), and a duplicate feature id.
  These are the three ways a tag graph goes wrong, and silence on any of them leaves a behaviour
  drawn nowhere.
- **Feature ids are `FEAT-`, behaviour ids stay `BEH-`**, so a `serves`/`cites` line can never point
  at a feature by accident and `in` can never point at a behaviour.

**Why not reuse `serves`?** `serves` says one behaviour exists for the sake of another — a dependency,
read by the question sheet and the remove refusal. A parent tag says where something is *shown*.
Folding them would make navigation edits change what Kit refuses to delete.

**Wire shape.** Project detail gains `features: [{id, title, in: [], at}]`, and each behaviour gains
`in: []` (always written, empty when untagged, after `layer`, the same rule as `pending`).

**The projects view** (BEH specs to follow in `kit-ui.beh`): project → its top-level features and
untagged behaviours → click a feature for its child features and behaviours → click a behaviour for
the existing behaviour page. With several parents there is no single path, so the breadcrumb is the
path you came by (carried in the URL), and the feature page lists *all* its parents as tags.

## Decision 2 — Kit is one project, its three files are its top-level features

`kit.beh` (the CLI), `kit-ui.beh` (the browser UI) and `kit-hosted.beh` (the hosted service) were split
for reasons that still hold: the CLI's 0% generation score is a measurement, and the UI corpus is the
one whose generated tests must pass. Merging the files would mix those up.

So the **display** merges and the **files** stay:

```
# kit:project kit
```

- A file directive, like `# kit:layer`. Files naming the same project are shown as **one project**.
  Each file becomes one of its top-level features, titled by its file name (`kit-ui`, `kit-hosted`).
- A file with no directive is its own project, as today, so nothing else changes.
- The CLI's `check`/`report` keep working per file. Only the UI and the project list group.

## Slices, in order (each a PR, stacked)

1. `spec:` the projects-view behaviours, `pending`, in `kit-ui.beh`.
2. Parser + model: `feature`, `in`, the three refusals. Golden tests for the new wire fields.
3. The project-list grouping (`# kit:project`), server side.
4. UI: the drill-down, with the breadcrumb.
5. Tag Kit's own corpora (`# kit:project kit` on all three, and features for the UI's pages), so the
   first project shown this way is Kit itself.
