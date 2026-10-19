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
