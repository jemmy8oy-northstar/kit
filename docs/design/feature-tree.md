# Feature tree: one project per repo, specs as a folder of features

Status: **plan, in progress**. Owner's ask: claude-code-bot#151 (2026-11-24 22:27Z and 23:35Z).
Every point marked *assumption* is deniable: his newest word wins.

## Why

Nearly every ticket edits the same few files (`kit-ui.beh`: 100 commits since 11-10,
~3.8k lines), so each merge breaks the pull requests still open. Splitting the spec into many
small files, grouped by feature, spreads tickets over different files.

## The layout

```
.kit/
  project.kit              project-wide directives (kit:repo, kit:promote, kit:switch, kit:principle…)
  notes.md                 notes left in Kit (one file, append-only)
  schedule                 the schedule
  bindings.json            noun bindings
  bundles/  memory.md      pipeline hand-offs (unchanged)
  features/
    <feature>/
      README.md            what the feature IS: its concept, in prose
      <name>.kit           behaviour cards (the old .beh grammar, renamed)
      <name>.tests.json    the tests for the cards in <name>.kit
      <child-feature>/     a sub-feature, same shape
```

Rules:

1. **One project per repo.** The project is the repo; there is no `kit` / `kit-ui` / `kit-hosted`
   split. Card ids are unique across the whole project.
2. **A folder is a feature.** Its id is `FEAT-` + the folder name upper-cased, so `features/notes/`
   is `FEAT-NOTES` (*assumption*: keeps every existing `in FEAT-X` tag valid after the move).
3. **A README describes its feature's concept. It is not an index of the folder.** It changes when
   the concept changes, and not when a card is added. It needs no `.kit` file: a feature with
   only a README and sub-folders is a feature. Its first lines may carry the feature's fields
   (`stage ui`) and extra parents (`in FEAT-X`).
4. **Parents.** A folder's parent is the folder it sits in. Every `.kit` file and README in a
   nested feature must tag that parent with `in`. Any card or README may tag further features as
   parents too. The spec gate refuses a missing parent tag or a tag that names no feature
   (BEH-PARSE-5/6, kit#908).
5. **Where a card goes.** A card lives in the folder of its first `in` feature, in a `.kit` file
   named for its area (`BEH-NOTE-3` → `features/notes/note.kit`). A card with no `in` goes in
   `features/<area>/`.
6. **Tests sit beside their cards** in `<name>.tests.json`, so a ticket's test mapping lands in the
   same folder as its cards (*assumption*: `kit-ui.tests.json` had 29 commits since 11-10, the
   fourth-hottest file).

## How Kit reads it (*assumption*)

Kit builds the project's corpus by reading every `.kit` file under `.kit/features/` in path order,
plus `project.kit`, and parsing them as one project. Each card remembers the file it came from,
so a write (approve, deny, edit) goes back to that file only. Most of Kit then works unchanged,
because it already treats a project as one corpus.

## Slices (each one PR into `dev`, merged before the next starts)

1. **Validate the tree:** kit#908. The reader plus the parent-tag gate. Not yet used by Kit.
2. **Read the tree:** Kit loads a project from `.kit/features/` when it exists, else from the flat
   files as today. Writes go back to the card's own file. `kit check` reads the tree. Code only;
   no spec moves.
3. **Move Kit's own spec:** a mechanical script moves `kit.beh`, `kit-ui.beh` and `kit-hosted.beh` into
   the tree, renumbers the three ids that clash across them (`BEH-SCHED-1`, `BEH-ADJ-1`,
   `BEH-GATE-H2`) and the duplicate `FEAT-SCHEDULE`, splits the tests maps, and moves the sidecars
   into `.kit/`. CI's two gate steps become one. This happens when Kit's queue is quiet,
   because every open pull request that edits the old files will conflict with it once.
4. **Teach the pipeline:** stage tasks and `agent.md` point at the tree ("a card lives in the
   folder of its feature; add a new card to the file for its area"), not at one spec file.
5. **Other projects:** each repo's flat `.kit/<app>.beh` moves to `.kit/features/`, one repo per PR.

## Out of scope

Splitting `Pipeline.cs` / `PipelineTests.cs` (the other hot files) is a separate code tidy-up.
The owner's ask is about the spec's file structure, not the UI.
