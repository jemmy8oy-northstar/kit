# The Kit dev process

Every user's agent follows this one process. It is written once and served by Kit
(`GET /api/process`), so a fix to it reaches every user at once.

## Where things live

- **The project** is a repo the user owns. Its behaviour corpus is the `.beh` files in it.
  The corpus is the spec: read it, do not guess past it.
- **Memory about this repo** (decisions, gotchas, what was tried) is kept in the repo, in
  `.kit/memory.md`. Read it before you start; append to it when you learn something a
  later session needs. It travels with the repo, so it is the same for anyone who works on it.
- **Memory about the user** (how they like to work, what they have said no to), tied to no
  repo, is kept by Kit: `GET /api/me/memory` reads it and `POST /api/me/memory` with
  `{"body": "..."}` replaces it. It is the signed-in user's alone. Never copy it into a repo.

## The loop

1. Read `.kit/memory.md`, the user's memory, and the project's corpus.
2. Take the top of the project's Queue (its schedule). If it is empty, stop and say so.
3. Build that one behaviour on its own branch. Write the tests first or beside it, run the
   project's own checks, and keep the change small enough to review.
4. Open a pull request into the project's integration branch. Never push to the default
   branch and never merge your own work unless the project's corpus says you may.
5. Record every assumption you made as an unreviewed contract in the corpus
   (`contract my assumption: ...`), so the user can deny it later. Do not bury a guess in code.
6. When something blocks you, put the question in the corpus where the user will see it, with
   the default you will act on if they stay silent. Then take the next item.
7. Update `.kit/memory.md`, and the user's memory if you learned something about them.

## Rules

- The corpus and the user's newest feedback win over this file and over your own notes.
- Anything that spends money, touches secrets, or deletes a lot is the user's decision.
- Say what you did in the pull request in a few lines a person will read on a phone.
- Never read one user's memory into another user's session.
