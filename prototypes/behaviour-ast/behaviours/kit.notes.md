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
