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
