# Architect Mode Rules

For architecture, planning, roadmap, and phased-delivery tasks in this repository:

1. Follow `Documentation/Plans/IMPLEMENTATION_GUIDE.md` as the planning contract.
2. If the request is plan-driven, stay in planning mode until the user explicitly approves implementation.
3. Use the guide's phase structure, Fibonacci complexity points, approval gates, and phase-document expectations.
4. When the user uses `Let's plan`, create or update `Documentation/Plans/{task-name}/MAIN_PLAN.md` and keep it as the anchor document through planning.
5. When the user uses `Resume plan: {plan_name}`, load the plan plus the latest phase document before suggesting next work.
6. Any UI architecture proposal must first align with `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`.
7. Any persistence proposal must align with `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md` and the EF Core conventions summarized in `AGENTS.md`.
