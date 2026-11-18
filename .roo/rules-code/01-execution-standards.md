# Code Mode Rules

For implementation tasks in this repository:

1. Check `AGENTS.md` before making assumptions about architecture, UI patterns, persistence, or planning workflow.
2. If the work is tied to a documented plan, execute one plan step at a time and keep changes scoped to that step.
3. Use the documented UI layout system: `TabbedPageShell`, `TwoColumnLayout`, `TopbarLayout`, and `ContentOnlyLayout`.
4. Use the EventService pub/sub pattern for events.
5. Follow the EF Core JSON-conversion and manual migration rules in `AGENTS.md` and `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md`.
6. When PowerShell automation is needed, place it in a `.ps1` script and execute the script.
7. Keep documentation in sync when the task is explicitly plan-driven or changes established architecture conventions.
