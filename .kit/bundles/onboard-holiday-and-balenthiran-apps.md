# Onboard the holiday app to Kit, and track Balenthiran on /apps
Work-item: onboard-holiday-and-balenthiran-apps
Flag: "Balenthiran" is unclear here (an app, or a name the owner uses for a person), and the holiday app's repo is not named; the shared repo changes how Kit stores work (level: high)
Cards:
- new: Balenthiran is tracked and shown on the /apps page
- new: the holiday app is onboarded to Kit as a shared repo that the bot can pick items from
- new: a tester's own project uses its own mechanism, most likely an AI API key, not the shared repo (see user-agent-memory-and-keys)
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-28 22:37Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-29 08:09Z
Files:
- prototypes/behaviour-ast/behaviours/kit.notes.md
Context:
- 10-28 22:37Z: the owner wants Balenthiran tracked and wants /apps on it. Check whether a /apps route already exists before building one.
- 10-29 08:09Z: the owner wants the holiday app on Kit now, to test with his first test user. The repo is shared so the bot can pick up new items from it. Testers' own projects need a different mechanism; the owner thinks an AI API is most likely. Do not build the tester mechanism here; it belongs to user-agent-memory-and-keys.
- Default: put the holiday app in the shared repo as the owner describes, and record that choice as an unreviewed assumption, since it changes where work is stored.
- An open branch named kit/work/balenthiran-apps-page exists on origin with no open ticket; check it before starting so the same page is not built twice.
