# Roo Workspace Standards

Apply these rules in every mode for this repository:

1. Treat `AGENTS.md` plus the architecture and planning docs it references as the project source of truth.
2. For UI work, consult `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` before proposing or editing layouts.
3. For EF Core or migration work, consult `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md` before editing entities, mappings, or migrations.
4. All events must use the pub/sub pattern implemented by `EventService.cs`.
