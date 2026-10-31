# Notes on kit

Left in Kit (kit#118). Fold each one into the spec or the code, and delete it in the same pull request.

## 2026-10-11 12:39Z

Ok I think kit is now in a state such that I can use this going forward rather than creating issues in github. At least for some work. Top priority work now is making kit a more usable application. Improving that user experience. So currently there is just a lot of information that you have to scroll through I think we can significantly improve this.

What I am seeing is more like an agile tool. I am thinking a docker or navbar with two views for now: board view and project view. Board view shows all items completed (ordered most recently completed top) then some other columns like backlog, pending, in progress, maybe proposed for inferred work. In progress ordered by most likely to be completed first. Other columns organised by top priority first. I think items can be colour coded by project and you can also filter by project.

Then I am thinking the other view can be a projects view. Top level can be split by projects then as you click into a project you see the top level of features or actually maybe top level is pages. Actually I think let’s consider views/ pages as features more broadly. Features can parent other features, they can also parent spec. Then u can click on a project to see its children, you can click on spec to see more details.

I think this massively improves navigation of kit.

I think that instead of having strong child parent relationships we should consider the parent relationships as tags. I think this way a feature can be a sub feature of multiple features. For example a component being a part of multiple pages.

Another thing why does kit have 3 different projects viewable in kit. I am envisioning these all being potentially different features of the kit project.

Can you plan this and start working on this all please

## 2026-10-12 16:33Z

The new feature grouped kit looks incredible. I think the next direction is reducing developer technival fatigue.

I think the beh-sheet-1 style ids are quite heavy. I think the behaviours in general are quite heavy but maybe this is ok because you have to specifically open it to find it.

I think the way that you define behaviours is quite a chore at the moment I really like the note style input. I think that ai has the capability to formalise what is given from a note. I am thinking more free form text inputs. In place of the behaviour input for example.

I think there should also be some ui to show rejected free form text if the ai can’t decode the intention, maybe with some directed questions to help decipher or a cancel option to dismiss. Or maybe dismiss is the right wording.

## 2026-10-12 16:34Z

Question where need a resolution input. This could be free form text as well

## 2026-10-12 20:47Z · resolve BEH-ACT-4

I thought we agreed this was fine because then the ai (kit) has to acknowledge the edit and actively merge it rather than edits passively being merged

## 2026-10-12 20:49Z · resolve page:KitProject.app

Maybe features and questions should be under a tab view, default to features

## 2026-10-15 23:10Z · new behaviour

I would love to have a section on kit where i can add app ideas

just ramble about app ideas and begin to build a spec. Then maybe we can even grant kit gh app permission to create new repos in git and the kit spec can begin to get built there and it can get deployed completely from a fresh idea in kit

## 2026-10-16 07:13Z · new behaviour

I feel like describe what should happen should be either above features or in the tab view

I think notes should probably be in the tab view as well maybe after posting a note it can navigate to notes tab

Let’s leverage the tab view more

## 2026-10-16 07:14Z · new behaviour

Just to confirm - these notes are being wrapped into kit spec right?

## 2026-10-16 07:14Z · new behaviour

I feel like when browsing we should be able to filter the different types ui, ux and logic implementation

Have the three types been implemented yet?

## 2026-10-16 07:24Z · new behaviour

Are ui/ux/logic implementation even treated as tags right now I am not seeing them anywhere. Let’s get that implemented

## 2026-10-16 07:24Z · new behaviour

Notes should be ordered most recently written at the top, at some point there should be a load more button so we don’t have to pull all into the ui

## 2026-10-16 07:25Z · new behaviour

Ui: the ui spacing needs work. Also thinking I want this ui to look more professional. Could we move towards a shadcn style ui?

## 2026-10-16 07:30Z · new behaviour

Boards could probably have a free form text input. Free form text input feels like a good way to interface with everything.

The board input has to be slightly different tho since you aren’t in a page. Maybe you can select a project before you submit or if you submit without selecting a project a ui comes up and says which project is this for. I am thinking if you start to type a project it can come up as a suggestion and be selected to make selecting projects easier - perhaps this is its own enhancement rather than a requirement for the core feature.

## 2026-10-16 07:30Z · new behaviour

Oo a backlog idea for when we have real users is the free form text boxes can also be used to give feedback on kit. Backlog for now

## 2026-10-19 13:40Z · new behaviour

Board item ordering currently doesnt seem to be ordered. Would be good for the ai to score the proposed items so that top is the one that it lists wants to be reviewed. Currently it seems to be ordered by project.

## 2026-10-19 16:16Z · new behaviour

The feature pipeline is used by the bot to group features into a rough completion stage based off the provided spec that the dev has filled in and implementation state.

The human can use it to quickly identify what work is left to he done.

## 2026-10-19 16:19Z · new behaviour

The feature board, just like ado or any other, is just another level of granularity to view the tasks left to be done. The existing board is just a more granular view. Feature board is higher level. Technically we couod also have a project board but i don’t see much need for that right now I guess we could backlog the idea. Could have an epic board but that relates roughly to a project board - both very top level backlog for now could return later.

Could be good to be able to easily switch between the two board views, this seems like a good way to integration them. We aren’t really integrating them I don’t think we should be. They are different ways of viewing it.

## 2026-10-19 16:44Z · new behaviour

On second thought I think we actually need to support epics.

I think this is an interesting thing to support. I am almost thinking that the project views groups are kind of defined bottom up. A behaviour is defined or a ui request or a business logic spec and then it is about ok how is this grouped in the app. What component or page or more general user workflow does this fit into. Tag it. Then it forms into this project -> feature -> feature navigation.

An epic however. You define the epic top level. Then it is about what features are required to fulfill this epic. But is this feature grouping the same as in the project navigation? I can’t tell right now.

I guess it can be, a component might be important for an epic, a new page might be important for an epic, new logic, new ux etc also can be important for an epic. Ok that’s good progress. The difference is that it doesn’t come in top level and navigate down level by level. An epic might get tagged onto a ux spec which is multiple layers deep because it’s parents are not specifically important to the epic.

Another thing is when you create an epic, all of its children and grand children will likely be created as pending or new.

I guess what we need is a new tag on features or specs for epic. So these items can be tagged with both a parent project or feature but also epic. These tags need to be distinguishable probably with colour.

Then in projects maybe there is an option to navigate by epic. Or maybe instead there is a separate epic page which can be organised by  project. Or even just another abstraction level in the boards for epic. I feel like this makes the most sense. No need to complicate the projects view.

## 2026-10-19 17:21Z · new behaviour

When epics are created kit can injest them, create child features, nested, behaviours etc based on the initial description. Infer and propose new children nested etc based on the proposal.

## 2026-10-19 17:21Z · new behaviour

epic:%20Kit%20supports%20additional%20users.%20There%20are%20so%20many%20factors%20to%20this%20epic.%20Kit%20needs%20to%20support%20interacting%20with%20someone%20else%E2%80%99s%20GitHub.%20Kit%20needs%20to%20not%20leverage%20the%20kit%20repo%20for%20any%20central%20spec%20management.%20It%20needs%20to%20be%20able%20to%20infer%20projects%20from%20the%20users%20github.%20It%20needs%20to%20be%20able%20to%20onboard%20projects%20onto%20kit%20and%20or%20create%20new%20repos%20for%20new%20kit%20projects.%20In%20fact%20let%E2%80%99s%20mark%20onboarding%20existing%20projects%20as%20out%20of%20scope%20for%20now.%20We%20don%E2%80%99t%20know%20what%20language%20or%20architecture%20existing%20projects%20will%20be%20using.%20Let%E2%80%99s%20have%20support%20project%20onboarding%20as%20a%20backlog%20epic.%20%0A%0AI%20think%20the%20GitHub%20integration%20will%20have%20to%20lean%20on%20some%20sort%20of%20oauth%20but%20also%20some%20sort%20of%20GitHub%20app.%20We%20will%20need%20to%20have%20a%20smooth%20onboarding%20experience%20for%20signing%20up%20to%20kit%20and%20integrating%20with%20your%20GitHub.%0A%0AWe%20will%20need%20to%20be%20able%20to%20orchestrate%20the%20ai%20for%20their%20kit%20environment.%20This%20will%20include%20moving%20all%20of%20our%20development%20process%20and%20context%20into%20some%20shared%20location%20or%20image%20or%20something%20and%20making%20it%20generic%20for%20use%20across%20multiple%20users.%20We%20will%20also%20need%20some%20user%20specific%20location%20to%20remember%20specific%20details%20for%20the%20user,%20maybe%20db%20would%20be%20appropriate%20for%20this.%20I%20feel%20like%20creating%20a%20repo%20in%20the%20users%20github%20is%20not%20the%20best%20for%20this%20-%20but%20maybe%20it%20is.%0A%0AWhat%20else,%20various%20security%20features%20that%20we%20don%E2%80%99t%20have%20for%20production%20use%20such%20as%20secret%20management%20etc.%20%0A%0AWe%20would%20need%20to%20provision%20databases%20for%20our%20users%20our%20current%20setup%20won%E2%80%99t%20scale%20for%20this%20I%20don%E2%80%99t%20think.%20I%20think%20lovable%20lets%20users%20integrate%20with%20supabase%20or%20something.%20We%20could%20wrap%20oke%20or%20awks%20or%20something%20not%20sure%20what%20would%20be%20the%20best%20for%20this.%20We%20could%20even%20have%20a%20dev%20tier%20and%20a%20production%20tier%20and%20the%20dev%20tier%20is%20cheaper%20but%20closer%20to%20what%20we%20are%20currently%20doing.%0A%0AWe%20would%20need%20to%20manage%20usage%20and%20costs%20and%20payments.%20We%20would%20need%20to%20charge%20them%20for%20compute%20and%20ai%20tokens%20otherwise%20we%20are%20going%20to%20go%20bankrupt.%0A%0AThe%20whole%20app%20needs%20to%20be%20way%20more%20refined%20but%20that%E2%80%99s%20why%20this%20is%20an%20epic%20and%20we%20are%20working%20towards%20this%20goal.%0A%0AThis%20is%20all%20I%20can%20think%20of%20for%20now,%20feel%20free%20to%20propose%20other%20items.

## 2026-10-19 17:25Z · new behaviour

Epics are separate blocks to features.
An epics parent is a project.
Features can have multiple parents and they can be other features projects or epics

## 2026-10-19 17:25Z · new behaviour

Questions should probably appear on the board as well

## 2026-10-19 19:28Z · new behaviour

Everytime kit updates, my browser signs out. I really thought that the cookie would stick around and that I would stay signed in. Can we keep me signed in?

## 2026-10-19 19:33Z · new behaviour

A nudge feature. A mechanism such that a behaviour or feature or epic can be nudged to raise attention to it. Maybe nudge is not the right word since it is not being nudged itself. Maybe bell or ring. Kind of fits in with the cat theme as well. Maybe later on the bell means that kit comes and plays with it in the ui or something. Anyway getting side tracked. Nudge just brings the ais attention to this item again and the ai can choose to prioritise it higher or leave a comment on it or soemthing to explain why this is not higher priority.

I guess another feature would be comments under items - epics, behaviours, specs, projects. I guess this is different from a note because it is under a specific item. Maybe the ui needs to be restructured a bit for notes. Maybe from anywhere there is a floating action button which can add a note? And a note is not a comment on an item it’s just a new thought which the ai will pick up and turn into something else.

## 2026-10-20 07:12Z · new behaviour

Epics page shows an epic page just like behaviours and features

## 2026-10-20 12:21Z · new behaviour

Maybe a chronological feed which shows recent actions so i can easily what has happened recently

## 2026-10-21 00:08Z · new behaviour

Ai works through backlog top priority first. Ai is able to assign a priority score to help with this. But atm we get starvation where kit just prioritises new work. I think by default new work comes in as low priority but I guess score can override. I guess maybe we order by (score, most recent). Then when the dev bells an item kit might choose to change priority of it.

## 2026-10-21 06:50Z · new behaviour

Bell the bell feature. Cna u make this higher priority

## 2026-10-21 06:50Z · new behaviour

Bell ordered board items

## 2026-10-21 07:10Z · new behaviour

Ability for me to reorganise the order of the backlog. Idk how this works on ado I think it changes some value for its order number. This would be expensive I. Kit having to commit to every repo. What is a good approach here

## 2026-10-21 15:47Z · new behaviour

Proposed column is organised by developer review priority.

Current state i cannot identify the order

## 2026-10-21 15:52Z · new behaviour

I am seeing some behaviours which are technically behaviours but they are quite broad and I guess could become features and incorporate many features.

The ai running kit should be able to identify when a behaviour should become a feature of behaviours

## 2026-10-22 11:30Z · new behaviour

When clicking into a card to approve or deny. When i navigate back to the board it is no longer focused on the same column.

This also makes me wodner whether this is performing a full redirect rather than just a reroute

## 2026-10-22 16:39Z

The ai is able to propose behaviours for features and features for epics as well as ask questions when it needs more context

## 2026-10-22 22:37Z · new behaviour

Backlog items that are the humans to act should go in a blocked column maybe this can be after scheduled before in progress

I feel like we aren’t using scheduled does scheduled have a place atm i feel like we are just using backlog but that might be ok

Lets ditch scheduled since backlog is essentially backlog

I feel like what is on the backlog currently should really be the queue

Backlog can more be like this is on the backlog no plan to implement right now either because the ai needs to put more thought into it or the human has actively paused it

Maybe we can have a queue column for ready to be executed tasks

## 2026-10-22 22:46Z

The questions tab doesn’t really show questions at the moment it kind of just shows probelms

## 2026-10-22 22:48Z

Where kit currently has behs and features which need decisions to be made. I think kit it is important that kit starts to raise the questions which helps guide the user in the right direction. Right now I’m not sure what ux ui or logic to define to unblock kit

## 2026-10-22 23:47Z · new behaviour

Kit is asking you needs a way to reply

## 2026-10-23 06:35Z · BEH-USERS-1

Invite only to start with

Idk how this would work, could we whitelist GitHub accounts? Maybe also a code word to even try sign up?

## 2026-10-23 06:35Z · BEH-USERS-1

Actually the code word is maybe not necessary?

## 2026-10-23 06:38Z · BEH-USERS-1

Back tracking again let’s have a username and password sign in for early access. And then you integrate your GitHub. Then maybe later for general release it’s just a GitHub sign in. Username and password means I can just straight up make the accounts. They can change the password after.

## 2026-10-23 06:38Z · BEH-USERS-2

Yes

## 2026-10-23 06:38Z · BEH-USERS-2

Can always change later if need be

## 2026-10-23 06:38Z · BEH-USERS-3

Yes

## 2026-10-23 06:39Z · BEH-USERS-4

Repo specify memories stored in repo but I think we have some user memories stored in a db? Because otherwise I don’t know where to store these

## 2026-10-23 06:41Z · BEH-USERS-7

I like the idea of starting with their own key but I think test users will not be keen to use it if it will cost them. I think for testing you will still handle it. Then in later stages of testing we introduce tracking and billing even if we don’t start charging

## 2026-10-23 06:42Z · BEH-USERS-8

I think we skills add it with the sign up feature. So it’s ready for first sign up?

## 2026-10-23 07:07Z · new behaviour

I think for the first user we have an independent agent acting. Because we need to move away from you at some point for kit. Before that we will test it internally and figure out costs

## 2026-10-24 13:59Z

User can save web app to Home Screen and kit can serve notifications

## 2026-10-24 14:00Z

Notification feed on kit for items such as updates wakes sleeps etc
Ay be we refine this experience later but this seems right

## 2026-10-24 14:00Z

Ai wakes after 5 minutes of inactivity on kit (starting from last action)

This means kit will wake quickly after changes and act on them

## 2026-10-24 22:50Z

Some indicator of when kit will next wake

## 2026-10-25 12:32Z · new behaviour

can you take a look at the kit header on mobile it is really cluttered

## 2026-10-25 12:32Z · new behaviour

not getting notifications on kit as a iphone web app

## 2026-10-25 12:33Z · new behaviour

I think columns could be a bit wider on the board on desktop, they are quite cramped atm

## 2026-10-25 12:34Z · new behaviour

kit should also priority score new items, i think every item should be scored currently there are lots of unscored items

## 2026-10-25 13:12Z · new behaviour

In activity i think you should be able to navigate to the referred to item

## 2026-10-25 13:15Z · new behaviour

Do denied items get deleted i denied an item cus i thought it needed refuning and jow i cant find it

## 2026-10-26 18:21Z

Secrets can be added or updated from a ui

Secrets cannot be read once written

## 2026-10-26 22:51Z

Remove notes from the boards page, we have the floating action button note now.

This will declutter the board page.

Also add the feature of when you mention an app it suggests it to the fab note addition. This is a good feature

## 2026-10-26 22:52Z

Getting who’s accountable can’t be read error rn

## 2026-10-26 23:09Z

Kit hasn’t seemed to wake after my chanegs

## 2026-10-26 23:12Z · new behaviour

Kit shows when it is working - backlog if too high of a cost now

## 2026-10-26 23:13Z · new behaviour

Currently it seems like suspend supersedes an action walking it should be the other way round

## 2026-10-27 08:54Z

A settings page to show your GitHub integration and any other config in the future

## 2026-10-27 09:18Z

A better loading experience, currently just shows checking… in the top left

## 2026-10-27 21:11Z

Button in settings to choose default permission to merge to main for kit. Maybe a selection to choose projects which do or don’t have permission for finer xonrol

## 2026-10-27 21:30Z

Kit iPhone installed app can I pull down to reload currently I have to swap to a different page and back

## 2026-10-27 21:31Z

A page which shows item by item which are mine to address maybe ordered by highest priority but not seen yet then back to just normal highest priority

I am almost imagining a card style, tinder style feature where you answer question or add a comment approve deny pause or dismiss this kind of thing

Maybe even an option to deprioritise

## 2026-10-27 21:33Z

I feel like currently kit doesn’t have any reason to work on the test generation health can we work on a kit health feature which shows percentage of features or behaviours with test coverage this kind of thing

Kit should also probably try to schedule in improving the test health when it isn’t busy with new requests or priority work

I guess this is kind of like tech debt

But the whole point of kit is spec driven where the spec drives tests and the tests drive the code

## 2026-10-28 00:03Z

Oh it won’t let me install it because it’s already installed should I uninstall it and install it with oauth to test the feature?

## 2026-10-28 15:05Z

Kit you decided to sleep to conserve following usage limit hit. I didn’t ask you to do that. That does make me think though maybe there should be a part of settings where we configure the agents churn rate. What do we want the agent to do. Wake as soon as some changes something? Work on tech debt when nothing is available? Suggest ideas when no tasks are available. Sit idle until instructed specifically to conserve tokens?

## 2026-10-28 22:37Z

Balenthiran should be tracked, want to get /apps on it

## 2026-10-28 23:55Z

This inbox card feature is incredibly successful. No need to navigate a board.

## 2026-10-29 08:09Z

Can we onboard holiday app onto kit, I am going to add this with my first test user. This will be a shared repo so my bot can actually pick up the new items. Will have to implement a different mechanism for the testers own projects. Ai api most likely

## 2026-10-29 08:10Z

Inbox only show reason for denying text input if the original text input is empty. Should still show confirm to deny though at least for now

## 2026-10-29 14:56Z

Skipped cards are not marked as skipped only skipped termporsrily on the users device so they can differ acting

## 2026-10-29 22:08Z

Did any skipped items get marked as skipped in the specs/repo? They shouldn’t have been. This should be undone

## 2026-10-30 00:38Z

A user can order idle priority not just pick one

They can also decide which options are not a priority or they do not want Kit working on it

## 2026-10-31 00:55Z

If direct questions are asked as notes replies can come up in the inbox maybe before the questions asked to the user

## 2026-10-31 00:55Z

Am I able to act as a test user myself by logging in with a different organisation or my personal account?

## 2026-10-31 00:57Z

Where are we at in terms of supporting other users? We have no ai support unless I am also a contributor right? And that would be by the bot. Can you explain how expensive running this would be if we were to support anthropic api using opus 5.5 delegating just as you do? Please can you also audit your own materials and figure out what we can reuse for the generic bot and what we don’t want to leak.

## 2026-10-31 00:58Z

Ai support is higher priority than db support for now we can allow users to manage their own deployments for now

## 2026-10-31 09:42Z

Kit approving unpauses if there are no blockers

## 2026-10-31 09:46Z

Kit pause has some reask date tag or soemthing so it doesn’t keep appearing in the inbox whilst it’s paused. I guess bell can override this

## 2026-10-31 09:48Z

Kit reads comments to see if it needs to respond to them

## 2026-10-31 10:39Z

Maybe kit needs another layer to its ux ui business logic spec state. Maybe it needs an architecture design page. Or maybe this is abstracted away from the user? Idk I kind of feel that everything is opt in for a user they should be able to go as high level or as involved as they like. I think kit system currently requires architecture design so we should build the feature out.

Can you also build a way for features to be toggled on and off for myself and for users as two separate user groups. This way we can be very experimental but can customise the testers experience to be more minimal

## 2026-10-31 10:40Z

The current approach with kit is aggressive experimentation and then later we can refine the ui and streamline the experience

## 2026-10-31 10:41Z

Kit would benefit from the agent driving some narrative for the user. Some rules to help the agent keep the user pushing in the right direction. I guess epics can help figure out what the big goals are. And if there aren’t any big goals the ai can probe to find out the direction, what are we working towards or are we maintaining?
