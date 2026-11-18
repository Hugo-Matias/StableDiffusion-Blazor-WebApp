---
name: persistence-migration-playbook
description: "Implement persistence changes the repo way. Use when adding a JSON-backed entity, ValueConverter, repository, singleton-row table, EF Core migration, AppDbContext model update, or debugging a missing SQLite table after startup migration."
user-invocable: false
---

# Persistence Migration Playbook

Use this skill when a feature needs schema or repository changes.

## When To Use

- Add a new persistent aggregate
- Add a JSON-backed entity
- Hand-author or update an EF Core migration
- Modify `AppDbContext`
- Debug `SQLite Error 1: 'no such table: X'`

## Procedure

1. Decide whether the feature fits the repo's JSON-backed aggregate pattern.
2. Add the entity under `BlazorWebApp/Data/Entities/`.
3. Register the converter in `AppDbContext.OnModelCreating`.
4. Add a `DbSet<T>` in `AppDbContext`.
5. In repositories that mutate a converted aggregate in place, force EF to reserialize the JSON column with:

   ```csharp
   context.Entry(row).Property(e => e.Body).IsModified = true;
   ```

6. For singleton-row tables, use a fixed id constant and load the row by that id.
7. If you hand-author a migration, include both required attributes on the migration class:
   - `[DbContext(typeof(AppDbContext))]`
   - `[Migration("<timestamp>_<name>")]`
8. Keep the migration filename stem, class name, and `[Migration]` id aligned.
9. Update `AppDbContextModelSnapshot.cs` alphabetically for the new entity.
10. Register repositories and stores through `IDbContextFactory<AppDbContext>` patterns, not direct `AppDbContext` injection.
11. Validate with the narrowest relevant repository or scheduler test before falling back to a full build.

## Guardrails

- Missing either migration attribute can make startup silently skip the migration.
- Do not forget snapshot updates when authoring migrations manually.
- Do not assume EF will detect in-place mutations to JSON-converted bodies.

## Key Anchors

- `../../../Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md`
- `../../../BlazorWebApp/Data/AppDbContext.cs`
- `../../../BlazorWebApp/Migrations/AppDbContextModelSnapshot.cs`
- `../../../BlazorWebApp/Scheduler/Persistence/JobRepository.cs`
- `../../../BlazorWebApp/Scheduler/Persistence/SchedulerDraftStore.cs`
- `../../../BlazorWebApp/Services/WorkflowStateService.cs`
