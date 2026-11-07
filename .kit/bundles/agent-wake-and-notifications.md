# The agent wakes on time, shows when it is working, and reaches the phone
Work-item: agent-wake-and-notifications
Flag: The 2026-10-26 23:12Z note joins a working indicator with a backlog cost cap; the cap overlaps BEH-SETTINGS-5 and BEH-USERS-7, so the reviewer may want that half split off. The 2026-10-28 15:05Z sleep-on-usage-limit is unclear on whether the churn setting or a separate usage limit governs it.
Cards:
- BEH-NOTIFY-1
- BEH-NOTIFY-2
- BEH-NOTIFY-3
- BEH-NOTIFY-4
- BEH-NOTIFY-5
- BEH-NOTIFY-6
- BEH-SETTINGS-5
- BEH-SETTINGS-6
- BEH-HEALTH-2
- new: a pending action takes precedence over a suspend, so the agent does not sleep on work it was asked to do
- new: when the usage limit is hit, the agent does not sleep unless the idle setting allows it, and says so in the notification feed
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-24 13:59Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-24 14:00Z ("Notification feed")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-24 14:00Z ("Ai wakes after 5 minutes")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-24 22:50Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-25 12:32Z ("not getting notifications on kit as a iphone web app")
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-26 23:09Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-26 23:12Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-26 23:13Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-28 15:05Z
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-30 00:38Z
Files:
- backend/Balenthiran.Kit.Services/IdleDirective.cs
- backend/Balenthiran.Kit.DataModels/Models/IdleSetting.cs
- backend/Balenthiran.Kit.Services/PushNotifier.cs
- backend/Balenthiran.Kit.Services/NotificationReader.cs
- backend/Balenthiran.Kit.Services/KitRouter.Push.cs
- backend/Balenthiran.Kit.Database/WebPushSender.cs
- backend/Balenthiran.Kit.Tests/NotificationTests.cs
- prototypes/behaviour-ast/ui/public/manifest.json
- prototypes/behaviour-ast/ui/public/sw.js
- prototypes/behaviour-ast/ui/src/nextWake.ts
- prototypes/behaviour-ast/ui/src/pages/Notifications.tsx
Context:
Home screen and feed (13:59Z, 14:00Z): BEH-NOTIFY-2 (home screen) and BEH-NOTIFY-1 (the notifications page, newest first) already specify these. Check the built state before adding anything.
Phone notifications (2026-10-25 12:32Z): BEH-NOTIFY-3 says a new notification reaches the phone as a push. James's iPhone web app is not receiving them, so the gap is in the push path, most likely the iOS subscription in sw.js and WebPushSender, or the app not being opened from the home screen. Check the subscription before the card is changed.
Wake after five minutes (14:00Z): BEH-NOTIFY-4 says the agent wakes five minutes after James's last action. The note says that is why it acts quickly after changes.
Next wake (22:50Z): BEH-NOTIFY-5, and nextWake.ts already exists. Check the notifications page shows it.
Not waking after changes (2026-10-26 23:09Z): BEH-NOTIFY-4. The five minutes count from James's last action in Kit, so check whether a change made outside Kit counts as an action.
Working indicator and cost (23:12Z): BEH-NOTIFY-6 says Kit shows when the agent is working. The backlog cost cap is new; BEH-SETTINGS-5 says how busy the agent is when nothing is asked of it, so this may extend that card.
Suspend beats action (23:13Z): the note says the suspend currently supersedes a pending action, and it should be the other way round. IdleDirective.cs is the likely place. This is the new card above.
Churn setting (2026-10-28 15:05Z): James found the agent had slept after a usage limit without being asked, and asks for a setting for what the agent does when nothing is asked of it: wake on change, work on tech debt, suggest ideas, or sit idle until asked. BEH-SETTINGS-5 already says how busy the agent should be when nothing is asked of it; check its options against those four. BEH-HEALTH-2 is the tech-debt option. The second new card covers the usage-limit sleep, which the note says was not his choice.
Idle order (2026-10-30 00:38Z): BEH-SETTINGS-6 has him put the idle work in order and switch off what he does not want. The note separates ranking an option as not a priority from switching it off entirely. Check the contract before treating these as two states.
