# Workspace Agent Guide

This repository is documentation-driven. When project-specific guidance conflicts with generic model habits, follow the repository guidance.

## Primary Sources Of Truth

Read the most relevant source before planning or editing:

1. `Documentation/Plans/IMPLEMENTATION_GUIDE.md`
   - Source of truth for planning sessions, execution phases, phase documents, Fibonacci complexity points, and approval gates.
2. `.github/copilot-instructions.md`
   - Source of truth for trigger phrases, execution conventions, architecture conventions, and persistence rules already established for this workspace.
3. `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`
   - Required for any UI, layout, tab, form, dialog, or page-shell work.
4. `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md`
   - Required for EF Core, migrations, JSON-backed entities, and snapshot updates.

## Planning And Architecture Work

- Treat `Documentation/Plans/IMPLEMENTATION_GUIDE.md` as the operating contract when the user is planning, architecting, or asking for phased implementation guidance.
- If the user starts with `Let's plan`, keep the implementation guide in context and create or update a plan under `Documentation/Plans/{task-name}/`.
- If the user starts with `Resume plan: {plan_name}`, load `Documentation/Plans/{plan_name}/MAIN_PLAN.md` plus the latest `PHASE_*.md` and continue from that documented state.
- During planning, do not write implementation code unless the user explicitly switches from planning to implementation.
- Plans should include: problem statement, proposed solution, key decisions, implementation phases, Fibonacci complexity points, stress points, and success criteria.
- UI proposals must use the documented layout system and spacing tokens rather than ad hoc layout patterns.

## Execution Standards

- This repository has known unrelated build and test noise. Prefer targeted validation for touched files and mention unrelated blockers separately.
- During plan-driven work, keep the relevant plan and phase documents synchronized with what was actually implemented.

## Workspace Conventions

### UI

- Use `TabbedPageShell` for tabbed pages. Do not render `MudTabs` directly in a page.
- Use only the documented layout variants: `TwoColumnLayout`, `TopbarLayout`, or `ContentOnlyLayout`.
- Use spacing tokens from `BlazorWebApp/wwwroot/site.css`. Do not hard-code shell spacing.
- Form controls default to `Variant.Text`; reserve `Variant.Outlined` for explicit emphasis.
- Children placed in layout slots should render flush. Do not wrap slot roots in extra `MudPaper` or add root `pa-*` padding.

### Events

- All events must use the pub/sub pattern implemented by `EventService.cs`.

### Persistence

- EF Core uses SQLite via `BlazorWebApp/Data/AppDbContext.cs`.
- JSON-backed entities are the standard aggregate pattern where already established.
- When mutating JSON-converted bodies in place, remember to mark the converted property as modified so EF re-serializes it.
- Hand-authored migrations must include both `[DbContext(typeof(AppDbContext))]` and `[Migration("<timestamp>_<name>")]`.
- Manual migrations must also update `BlazorWebApp/Migrations/AppDbContextModelSnapshot.cs` consistently.

### PowerShell And Files

- When a PowerShell script is needed, create a `.ps1` file and run that file instead of pasting multi-step PowerShell directly into the console.
- Use compatible text encodings for markdown, scripts, and source files.

## Documentation Expectations

- Keep plans and phase documents aligned with implementation progress when the task is plan-driven.
- Document blockers, detours, and scope changes in the relevant plan artifacts instead of leaving them implicit.
- Prefer concise, actionable documentation that is sufficient to resume work in a later session.
