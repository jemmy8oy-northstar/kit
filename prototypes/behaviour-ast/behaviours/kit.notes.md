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

## 2026-10-31 16:23Z

kit you should be able to filter projects when going through your inbox

## 2026-10-31 16:26Z

When I click already installed? connect it i get - This GitHub App must be configured with a callback URL

also slightly confused what this is acc for - on my account it is already installed everything is working fine what’s left?

## 2026-10-31 16:43Z

UX - probably a feature or an epic or maybe even makes up a selection of all

Just talking through the envisioned user experience so that we can ensure that we are working in the right direction to get there.

As a kit user I want to be able to work on a project start start to finish through designing spec. I want to worry about implementation details as little as possible and i want to focus on UX and then later UI.

The onboarding process takes me through setting integrating my git account. It shows any projects that I already have setup with kit and their test health. If there are no existing projects it asks if I would like to create a new project to get started or something along these lines. 

Creating a new project in kit creates a new web template project - do we need to make a new web template for kit users? or is the existing one appropriate.

It sets up the branches appropriately, it adds branch rules and actions if possible. Kit takes me through the initial spec design of my project with a real time non code agent, we discuss what is the overrall vision for the project, together we determine what the data dependencies of the project are, whether there is a database dependency, whether there is a login requirement. This all begins to get recorded in kit as spec. The kit agent starts to ask me about specific user workflows and determines whether there are different classes of users so that it can group workflows by user classes and document this all explicitly as spec. As the spec is created and the agent marks it as ready for implementation the feature gets created and a worker agent picks up the agent and gets cracking. This worker agent is a coding agent it has a coding harness such as claude code (is this sufficient / necessary?) and an additional kit agent (is this sufficient / necessary?) In the background the agent implements the features and merges it into the dev branch (preprod).

From kit I am able to spin up a preprod version of my app and use the app and play around and evaluate the success of the agents. If I find any gaps I can discuss with the realtime spec agent what the gaps are and it helps me ensure that it has all the details so that the feature branch can be created and the coder agent can work on this and get it implemented. I get notified when a feature is implemented.

When I have technical questions I am able to ask the non coder agent and it is able to delegate to a coder agent to answer - i think to start with this just gets posted to an open questions board or something and the answer becomes available when the coder agent picks it up and answers it - later this can be picked up instantly and answered instantly.

When the agent encounters unforeseen blockers it asks its own questions to its own open question board. The inbox can be used to address questions from the coder agent. The next step is instead to have the non coder agent discuss these open questions with the human to get these resolves. Maybe we need towards this next. Having a page where all of this spec work is triaged or consulted via a non coder agent. Non coder agent is much cheaper right? But this would create great value because then the user can discuss just like I can with gemini as a first step before the coder agent takes a look. But this agent will have way more context than gemini.

I also want to say improving kit is our no1 priority and focus. I get that when kit is blocked you can tick on with other things but we need to be working towards the kit dream.

## 2026-10-31 16:47Z

the feature board gets a done column, each column represents what spec needs to be provided, development represents in development, done represents done

## 2026-10-31 23:53Z

I have desk time tomorrow, if u need secrets or any desk work prepare it for me

## 2026-11-02 12:04Z

I have added a kit-anthropic secret with the key Anthropic__ApiKey to unblock the creation of the triage chat. Let’s use haiku for this

## 2026-11-02 12:12Z

I have the kit Anthropic api $10 credit with no auto credit. Let’s see how long this lasts for personal use

## 2026-11-02 12:56Z

I like talk it through on the inbox but it is getting quite cluttered. I don’t want to mess with talk it through right now or inbox I want them to stay as they r so let’s try new ux with a new feature. Instead could we add a new chat feature. And the chat has some suggestions. You can either define new spec, discuss a project or answer inbox items. I think the way we do this is the chat by default suggests an item that needs answering as part of this suggestion it says or we can define new spec or discuss a project. Make sure to either name or select the project that you are talking about. I think there should be a select box to select the project. When you select it says. Now talking about x project. Do you think it keeps the history after a project change I think it does, we can leverage that cheap cache. We can think about discarding context let’s put it on the backlog as a cost saver. Then you can get on with discussing or marking off items as we go. If the user seems off topic we can say are you talking about a different project and suggest some projects that seem appropriate maybe prompt them to select a different project or maybe define specific for a new project

## 2026-11-02 14:12Z

I don’t like the initial entry point for the chat. Acting is behind an initial click. I think the context should be available showing the default item to be addressed. Maybe with some context of address the top item off the stack or discuss other work or something.

Then the user can just get on with chatting. The ai will then infer which route they are taking. Maybe start with defaulted to x project. Tap to change or explicitly mention the project that you are talking about in your message. Also this doesn’t really look like a chat at the moment. Think chat gpt think Claude.

## 2026-11-02 17:41Z

Can you enable some features for testers. I guess we will need them working as well but I think the projects page the boards the inbox and settings is a good place to start right?

## 2026-11-02 17:47Z

We need to start improving the robustness of kit, flagging when features conflict and being able to resolve this. I think a good non blocking conflict is building the feature anyway when specs conflict and then letting the user confirm or deny when approving.

Maybe the settings page needs some way of configuring conflicting behaviour. Whether it merges anyway asks for confirmation or doesn’t work until the conflict is resolved. The code agent can provide context to the triage/chat agent which can help resolve with the user. I think it is important to be able to configure it not to implement before resolution because that would obviously incur more spend which the user might not want.

Backlog ideas, priority is still getting ready for users. The triage agent is able to identify conflicts. With smaller scale this is easier right the agent can identify compared to the top level readme or soemthing or a kit overview or something. But with scale this is harder, have to think of some way to index the spec well so that we can identify similar features quickly. Or maybe verb matching or semantic meaning checks. Maybe some vectorisation of the spec happens for each dev snapshot and it is stored in the repo so the agent can easily identify similar behaviour. Idk needs more design but you see the direction I am heading with this.

The chat agent being able to identify conflicts is ideal, this is the least disruptive means the user can identify and unblock during proposal rather than having to do an entirely separate resolution round then potentially waiting again before it gets merged.

## 2026-11-02 17:48Z

In chat, skip is not the clearest maybe it needs to be skip to next item. Skip might get confused with a dismiss action as it currently is.

## 2026-11-02 17:50Z

In next waiting maybe it needs to show the project first and then preview: then the preview I was a bit confused what I was reading I thought it was about kit

## 2026-11-02 17:52Z

When replying to the default action or just when replying in general this item should move up in the chat since this is chronological context. This should also be reflected in the way that it sees it

## 2026-11-02 17:53Z

Kit chat needs the capability to be able to approve answer deny respond etc rather than just suggest the user buttons I feel this is more useful. Maybe there should then be an undo button or you can ask chat to undo the last actuon

## 2026-11-02 17:54Z

Once acted on an item a similar prompt should come up from kit, here’s the next item or we can discuss new spec blah balh

## 2026-11-02 18:54Z

Does the chat get logged or saved at all when chatting with Kit? I am thinking this could have great value with the kit chat videos, however it could blow up the repo size. What do you think? Good idea/ bad idea? Do you think it would be good to dedicate a volume to this for personal chat saving. Probably no need to save chats for users
Spec agent:
The spec doesn't say whether chats are logged or stored, so I can't tell you what happens today. The question is worth settling on its own, so it could become a note for the project.

On the idea: saving transcripts for your own videos sounds useful. Keeping them out of the repo is the right instinct, since chat logs would grow it quickly. A separate volume or store that only you can read, with nothing saved for other users, fits what you describe. Whether it's worth building depends on how often you'd actually pull from it for videos.
You:
If I click save as a note will this save the whole chat as a note or some custom text that you decide?
Spec agent:
The spec doesn't say what "save as a note" does, so I can't tell you whether it saves the whole chat or text I choose. I also don't write notes myself: my only actions are approving, denying, or answering an item in hand. To find out, try it on a throwaway chat and check what gets saved, or tell me what you want it to do and I'll add that to the spec

Chat with chat

## 2026-11-02 18:54Z

Chat should be well aware of its own capabailities

## 2026-11-02 18:55Z

Maybe backlog of this is technically difficult but chat should be able to read the spec of the project that it is answering for

## 2026-11-02 18:57Z

Kit backlog feature - a security agent. An agent that can be switched on and off in settings which can do an audit on the code base and flag any security issues and create tickets to address them.

On this point a later backlog feature could be various custom agents. Kit health agent, QA agent with configurable objectives and goals.

## 2026-11-02 18:59Z

Another later backlog feature would be maybe hooks where kit can trigger external integrations and even some way that external integrations can trigger kit and maybe even some way for kit to integrate with external data sources. I am thinking logs or log triggers.

Anyway this is probably three different features please separate and file for me.

## 2026-11-02 19:04Z

Top of ur stack should appear in the messages order. Upon request kit can spit it out again. Currently I don’t see kit actioning approve for me. Also the buttons post as answer or post as note under it’s not clear what this is doing is this just that one chat bubble? Can we just delegate this responsibility straight to kit, maybe with a quick button which is something along the lines of ask kit to add the context of this chat as a note. Or maybe Kit can figure out when this appropriate and prompt the user.

## 2026-11-02 19:05Z

I was thinking of getting rid of notes in favour of chat but I think notes still olds great value. It allows a user to quickly just brain dump and let the coding agent pick it up and file it.

## 2026-11-02 19:28Z

Might be good to give the chat a help function which explains what kit is explains that the chat does not make changes for you. You discuss and define specific with the chat and a coding agent works in the background to implement features and keep your app up to date with the spec you write.

## 2026-11-02 19:28Z

Before we go to production we should think about an onboarding experience as well that shows you around but let’s backlog this for now

## 2026-11-02 20:15Z

We should probably have a splash page for the production kit, with a register interest page.

Should I point the new domain to the same public ip as balenthiran.co.uk? Or will this have a different public ip from the cluster?

We can just use the preprod cluster for now then later decide whether we want a separate cluster for production kit.

## 2026-11-02 20:22Z

Kit backlog: some integration page or config page for projects where users can configure secrets and integrations such as db or api keys.

With this we will need to think about migration strategies when hooked up to users production databases, maybe at first we should only handle migrations on preprod servers. Or maybe we should have some strong disclosure or something where we heavily recommend that users manage their own production dbs and we take no responsibility if migrations cause data loss or db corruption or something.

Also thinking about whether we could host some really light version of there app maybe call it a lite version where there are no integrations, external dependencies mostly return bogus faker data or something but of the correct shape, and maybe these pods spin up with an in memory db which is injected with some start data to enable basic use and testing. This would allow us to really cheaply host testable versions of the apps for users. Here we wouldnt need a block volume or anything just a really cheap dotnet and react pod. Maybe they can have the option to setup a test db and test integrations so that they get more accurate ux for testing and also data persistence between pod restarts. But this would be a good cheap and lite experience for them before they set up the db.

## 2026-11-02 20:27Z

Backlog kit: I am thinking more and more that there might be value in restructuring kit such that everything is organised by project. I think maybe the inventory might be worth having globally maybe with a take to chat button to take you to the chat within that project. But I kind of feel like each project should be quite isolated in kit. Kind of like in git how there is a entry page, there is an inbox, there is global settings and configuration and then there is a repo with the repo code, with the config, the gh actions etc. I feel like it would be good if the chat was organise by space, maybe we can still have a message when you enter which says no new actions need your attention on this project jump to x project to respond to most recent item.

But this feels like a natural way to do it.

Just thinking about whether it is still worth having a global board, maybe we can have project boards and have some button to take you to the global board, still feel like it is somewhat useful to see everything. But I think starting to introduce some project isolation would be valuable for the user experience of kit.

## 2026-11-02 23:50Z

Kitstudio should currently show a splash page with a register interest flow, maybe this flow is powered by balenthiran backend

## 2026-11-03 10:19Z

Kit Kit as part of initial analysis kit helps you with market research and assessing what the competitors do

## 2026-11-03 11:17Z

Does reading the queue need to built into claudemd?

## 2026-11-04 00:52Z

<!-- from: note-button -->

Just thinking about housing the users coding and pricing models. What would the coding agent actually require to be able to run. Would it need its own volume in order to clone the repo. Should we instead be pricing the user with a volume so that an agent can clone a repo. Maybe we can have some base subscription which supports x number of projects or something. Let’s say £15pm subscription. Then some of this can go towards the volume. Maybe we can give them £15 worth of token credits. But we somehow add a premium on the token cost into the credits such that of the £15 credit we only end up spending £10 on tokens meaning £5 is ours to fund the volume and pod and floating costs. Maybe this £15 can be the base subscription and it gives them monthly credits which expire every month if they don’t use them. Hm I guess the only issue would be this would encourage them to cancel if they aren’t using it. Anyway I am thinking they could also purchase additional credits which don’t expire. But they need to be paying the subscription to use them still. I guess we would have to support some refund system as well.
Spec agent:
The coding agent needs a runtime, a checkout of the user's repo, the dev process as its instructions, Kit's commands as tools, and per-user mem
You:
Please continue I think you got cut off
Spec agent:
Sorry, I cut off there. Here is the rest.

On the volume question, the repo doesn't need a permanent volume. The agent can clone it into a temporary workspace when a job starts, work there, push the result, and throw the clone away. That costs only compute for the


Sometimes the chat gets cut off, see above.

## 2026-11-04 00:57Z

<!-- from: note-button -->

Just thinking about hosting the users coding and pricing models. What would the coding agent actually require to be able to run. Would it need its own volume in order to clone the repo. Should we instead be pricing the user with a volume so that an agent can clone a repo. Maybe we can have some base subscription which supports x number of projects or something. Let’s say £15pm subscription. Then some of this can go towards the volume. Maybe we can give them £15 worth of token credits. But we somehow add a premium on the token cost into the credits such that of the £15 credit we only end up spending £10 on tokens meaning £5 is ours to fund the volume and pod and floating costs. Maybe this £15 can be the base subscription and it gives them monthly credits which expire every month if they don’t use them. Hm I guess the only issue would be this would encourage them to cancel if they aren’t using it. Anyway I am thinking they could also purchase additional credits which don’t expire. But they need to be paying the subscription to use them still. I guess we would have to support some refund system as well.

Just thoughts, with making a note as we start to design this

## 2026-11-04 10:58Z

<!-- from: note-button -->

To help us with cost analysis we should start recording the cost that Claude code bot incurs from kit sessions. Apprentice this should be easy to extract and we can even look at previous sessions by looking inside ~/.Claude/ this should help us with figuring out pricing models and whether this is a viable product.

I am worried because people have said that api incurs a much lower cost than if you were using api. So we can use the cost outputs to figure out weekly cost historically? And compare to subscription cost?

## 2026-11-06 18:07Z

<!-- from: note-button -->

This is just a test please discard it

## 2026-11-06 18:16Z

<!-- from: note-button -->

This is a test (I need Claude code bot to wake)

## 2026-11-06 18:20Z

<!-- from: note-button -->

Test

## 2026-11-06 18:34Z

<!-- from: note-button -->

Test

## 2026-11-06 18:37Z

<!-- from: note-button -->

Test

## 2026-11-06 23:53Z

<!-- from: note-button -->

This is a test

## 2026-11-06 23:54Z

<!-- from: note-button -->

Test

## 2026-11-07 01:23Z

<!-- from: note-button -->

test

## 2026-11-07 01:50Z

<!-- from: note-button -->

Kit pipeline needs to quickly (and cheaply) identify when something is just for the backlog

## 2026-11-07 01:51Z

<!-- from: note-button -->

The opus agent needs to be able to identify what backlog items can be scheduled for action and kit pipeline needs to be able to wake for these items - these items probably can skip a few items, if they are already backlog they probably dont need a triage

## 2026-11-07 02:10Z

<!-- from: note-button -->

A kit pipeline uncertainty flag from haiku picks a model by complexity level can pick opus review or sonnet review - should cut costs

## 2026-11-07 02:11Z

<!-- from: note-button -->

Running kit pipeline with a node script. Surely this is perfect application for csharp async

## 2026-11-07 10:50Z

<!-- from: note-button -->

Some page that shows visibility into agents running, what stage of the pipeline they are at, whether they flagged for a review and by which agent. Maybe you can also view the pipeline run of historic runs and see which stage costed the money, what reviews were requested for. This is purely an internal feature for now

## 2026-11-07 10:56Z

<!-- from: note-button -->

The note and chart inputs do not get lost when subscription credits run out and the pipeline cannot activate

## 2026-11-07 11:28Z

<!-- from: note-button -->

Pipeline can raise and complete PRs for completed work

## 2026-11-07 14:22Z

<!-- from: note-button -->

www.kitstudio.dev seems to be broken what do i need to change

## 2026-11-07 21:13Z

<!-- from: note-button -->

I think it would be worth changing the .beh extension to .kit or .spec or something. .beh is not intuitive it’s not easy to say i don’t really like it. Currently im leaning towards .kit

## 2026-11-07 21:14Z

<!-- from: note-button -->

Backlog: I want to do some proper work on designing the kit spec structure I think one file dump per project is not structured well at all

## 2026-11-07 21:21Z

<!-- from: note-button -->

First tester can see my proposed projects when they should not be able to, tester should only see work from there own space. Proposed projects should be per user, maybe this gets stored in the db until they create a fit repo for it

## 2026-11-07 21:21Z

<!-- from: note-button -->

tester can’t see their own project - holiday planner, they are a contributor to holiday planner  - I feel like maybe we haven’t moved the spec files into this repo yet so we don’t see it. We also need to make sure that kit finds projects where are users are added contributors

## 2026-11-07 21:21Z

<!-- from: note-button -->

A new user as part of the onboarding has to sign in with git before they can use the app - currently a user can look around without having signed into git

## 2026-11-08 22:06Z

<!-- from: note-button -->

Ok so v1 of the pipeline had some queuing issues I have some ideas for a rework. The main issue I am seeing is that I don’t think a note should immediately trigger a feature build. Instead I think just like everything else it should go on the backlog. Then we have a few different jobs monitoring different status’ and waking agents to act on the tasks in various states. Rough idea:
1. chat adds notes
2. Hook to backend haiku agent which triages ensures there are no similar tickets and creates a ticket and or updates existing backlog tickets in new states
3. Another agent which goes through tickets in new state and adds more context and loves state to ready or something
4. Another one which runs through all items and plans them
5. Another one which runs through implements and raises the pr
6. Another one which is in charge of resolving conflicts and merging PRs
There are a few things I’m not sure on. Like could/ should any of the stages be merged? Should 3&4 be merged for example. 3 will have brought all the context in already for 4 but then also will likely have extra context. I am thinking we start with them all as separate and then evaluate. But you see the benefit here right. Existing tickets will still get actioned currently only new tickets are getting actioned (and quite poorly). And also actioning will take place based off priority not based off a hook.

In terms of hosting I kinda of feel that we should host a cluster local csharp backend to handle this and then have jobs for everything that handles the border tasks  but the triage stays acting on a hook. Seem reasonable?

## 2026-11-12 08:08Z

<!-- from: note-button -->

Ah my minutes r used up. I feel like if we swap to paid we will still run out it’s only 50% increase right?. Are there any other options. Is self hosting viable? Maybe we should switch back to free tier.

## 2026-11-12 08:37Z

<!-- from: note-button -->

Maybe to cut costs we can use the kit pipeline for deployment - it can run tests as well but only use it for those completed merge promotions. Maybe we should have some promotion pipeline as well like deploy from main rather than develop so that promotions can be purposeful. Then you can just make sure that tests are passing locally otherwise build will fail

## 2026-11-14 08:27Z

<!-- from: note-button -->

Made kit public temporarily to unblock us but we still need to look at minimising credit usage in our private usage

Later when we r making money I can pay for github

## 2026-11-14 12:50Z

<!-- from: note-button -->

Can I get a button to flag for a ccb wake for my personal kit only. Sometimes I want it to wake but have to wait or write a note

## 2026-11-14 13:44Z

<!-- from: note-button -->

Please continue with the pipeline enhancements!

## 2026-11-14 17:25Z

<!-- from: note-button -->

I thought you were going to get the pipeline running Howcome u paused until 8

## 2026-11-15 12:47Z

<!-- from: note-button -->

Kit projects are separated just like GitHub repos. You have to navigate to the project to see the views for a r project. There is also a global board and a global settings. Pipelines are run on projects independently. Pipeline can be switched on and off for individual projects. Let’s start with all off but on for kit

## 2026-11-15 12:47Z

<!-- from: note-button -->

Kit .kit specs live in the project repo not centrally in kit

## 2026-11-15 12:50Z

<!-- from: note-button -->

When in chat you should see the state of the top of stack item that you are being shown

## 2026-11-15 13:00Z

<!-- from: note-button -->

Too priority atm is splitting out the projects in kit and moving notes and specs into the projects repos

## 2026-11-15 15:20Z

<!-- from: note-button -->

Just wanted to write a large note to explain my current target goal for kit pipeline so that we can continue on the correct path.

So a few input mechanisms, writing a note, speaking to chat, leaving a comment, approving rejecting. This is the input surface.

Then we have the first agent in the pipeline which picks up all of these loose items and inspects the existing spec and figures out what needs to change / by deleted / be created to accomadate for the new inputs. This bot will create new spec, update existing tickets and ask questions about spec and even removing spec that does not align with the new behaviour. These updates go in a new feature branch. A ticket needs to be created to represent the change (this ticket will be in the new state) and this is where questions will be posted if this agent wants to clarify anything with the user. If there are any questions this ticket get marked as blocked for the user to act on. This agent will also handle blocked items where there is extra discussion until there is no extra discussion and this ticket gets moved to ready. There is a distinction here, previously spec was a ticket. Now a ticket is different from spec. I think this is a strong improvement. Tickets appear on the board, spec is part of the feature. Thoughts?

The next agent takes the ready ticket and begins technical design.

Then onto an agent that performs the implementation.

These agents can also ask questions maybe we have blocked as a tag rather than a ticket state - this way an item can be new but blocked or ready but blocked or planned but blocked or implemented but blocked.

Etc.

The agents pick up top priority work from features in the appropriate state. The job of ccb atm is to monitor the pipeline and get it into this working state. Ccb does not need to wake and respond to all new notes, ccb can be awoken by a button that I can click to wake it early, or it wakes on a schedule as normal.

Maybe it is worth having a priority agents which wakes when the bell gets hit and it can reprioritise work or something like this, maybe the first agent can prioritise the new and ready backlog including prioritisation. This was ccb doesnt need to wake to a bell either.

Get to this state where the pipeline is handling new and backlog tasks by itself and ccb is just a watcher.

## 2026-11-16 22:56Z

<!-- from: note-button -->

Kit pipeline has a component that monitors blocked items and untags them if a blocker changes

## 2026-11-16 22:58Z

<!-- from: note-button -->

The pipeline stages are scheduled via separate jobs, they wake and deterministicly check for changes and then sleep if no changes. Overtime we can fine tune the wake frequencies

## 2026-11-18 00:10Z

<!-- from: note-button -->

There should be an option to disconnect your GitHub

## 2026-11-18 00:13Z

<!-- from: note-button -->

Each pipeline stage independently runs on a schedule

## 2026-11-18 00:14Z

<!-- from: note-button -->

Maybe kit should show hints when it isn’t taking action of what the user needs to do. Eg. Pipeline not running items in states: new, ready, planning, development need action. Or something like this

## 2026-11-18 00:23Z

<!-- from: note-button -->

Can we have an agents page which lets you pick the agent and see what it has done recently, what’s unblocked in its queue and what’s in its queue?

## 2026-11-18 08:12Z

<!-- from: note-button -->

Pipeline runs page should show most recent runs first

## 2026-11-18 08:14Z

<!-- from: note-button -->

Pipeline stage agents should as agents or workers instead of as pipeline stages. The workers should be woken using a Kubernetes job schedule, kind of like ccb. Idk whether we should have one backend which just gets pinged when the job wakes.

## 2026-11-18 08:14Z

<!-- from: note-button -->

The merge agent should be merging open PRs into dev and also should manage for the allowed repos promotion to main

## 2026-11-18 08:17Z

<!-- from: note-button -->

Triage and merge agent run sequentially. I feel like for now plan and implement agents can run in parallel

## 2026-11-18 18:27Z

<!-- from: note-button -->

A setting to configure whether the developer gets assigned to all PRs or not (do they appear on inbox)

## 2026-11-18 21:10Z

<!-- from: note-button -->

Tickets still have states and should still have state columns. Tickets also have priority and are organised in their column board by priority. Within priority blocked tickets appear at the top

## 2026-11-18 22:51Z

<!-- from: note-button -->

Blocked: the build run left the bundle unchanged

If this is intentional I guess we need to know why or if it’s not it needs to rerun

## 2026-11-19 16:55Z

<!-- from: note-button -->

Kit pipeline agents need to be able to make assumptions in the same way that ccb was able to. It can merge features tied to behaviour which have not been approved. This indicates to future agents that this was an assumed behaviour and can be quietly superseded by new owner defined behaviour

## 2026-11-19 16:57Z

<!-- from: note-button -->

Important: the merge agent needs to be merging I don’t see it merging so there are lots of PRs hanging
