# Professional UI: one spacing scale and a shadcn-style component set
Work-item: visual-design-system
Flag: Moving to a shadcn-style component library is a stack decision. Check the project's kit:depth line; if it is architecture, raise it as a review request before building on it.
Cards:
- BEH-LOOK-1
- BEH-LOOK-2
Notes:
- prototypes/behaviour-ast/behaviours/kit.notes.md#2026-10-16 07:25Z
Files:
- prototypes/behaviour-ast/behaviours/kit-ui.beh
- prototypes/behaviour-ast/ui/src/test/look.test.ts
Context:
The note asks for better spacing and a more professional look, and asks whether to move to a shadcn style. BEH-LOOK-1 already says one spacing scale, quiet greys and one accent. BEH-LOOK-2 says the design system's professional theme, and does not name shadcn. Whether shadcn is the right design system is the user's call, so raise it rather than pick it. The look test file already exists and should be extended, not replaced. Nothing else is needed to start.
