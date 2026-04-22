# Persistence & EF Core Migrations

This document captures the conventions and pitfalls for working with the SQLite-backed persistence layer in `BlazorWebApp`. It is intentionally short and checklist-driven so it can be followed verbatim during implementation.

---

## Stack summary

| Piece              | Value                                                                                          |
| ------------------ | ---------------------------------------------------------------------------------------------- |
| ORM                | Entity Framework Core 6 (see `ProductVersion = 6.0.11` in `AppDbContextModelSnapshot.cs`)      |
| Provider           | SQLite (`Microsoft.EntityFrameworkCore.Sqlite`)                                                |
| Context            | `BlazorWebApp/Data/AppDbContext.cs`                                                            |
| Factory            | `IDbContextFactory<AppDbContext>` (registered in `Program.cs`)                                 |
| Startup bootstrap  | `DatabaseService.InitializeDatabase()` -> `EnsureCreatedAsync()` then `MigrateAsync()`         |
| Migrations folder  | `BlazorWebApp/Migrations/`                                                                     |
| Snapshot file      | `BlazorWebApp/Migrations/AppDbContextModelSnapshot.cs`                                         |

All repositories follow the pattern: create a fresh scoped `AppDbContext` via the factory per operation, swallow exceptions on read paths (log + return null/empty), surface exceptions on write paths.

Reference implementations:

- `BlazorWebApp/Scheduler/Persistence/JobRepository.cs`
- `BlazorWebApp/Scheduler/Persistence/SchedulerDraftStore.cs`
- `BlazorWebApp/Services/WorkflowStateService.cs`

---

## JSON-backed entities (standard pattern)

Aggregate roots that are either polymorphic or rapidly evolving are persisted as a **single JSON column** through a `ValueConverter`. Keeps the schema stable when the in-memory model changes.

### Examples in the codebase

| Entity               | Column                                   | Converter / Options                                    |
| -------------------- | ---------------------------------------- | ------------------------------------------------------ |
| `JobEntity`          | `Body` (the whole `Job` graph)           | `SchedulerJsonOptions.Compact`                         |
| `SchedulerDraft`     | `Body` (the draft `Job`)                 | `SchedulerJsonOptions.Compact`                         |
| `State`              | `AppState`, `GenerationParameters`       | App-level `JsonSerializerOptions` + `GenerationParametersJsonConverter` |
| `WorkflowState`      | `Parameters` (`GenerationParameters`)    | `GenerationParametersJsonConverter`                    |

### How to add a new JSON-backed entity

1. Define the POCO under `BlazorWebApp/Data/Entities/` with a plain `Id` primary key and a single complex property to be serialized (e.g., `public Job Body { get; set; }`).
2. In `AppDbContext.OnModelCreating`, register the converter:

   ```csharp
   var bodyConverter = new ValueConverter<MyAggregate, string>(
       v => JsonSerializer.Serialize(v, MyJsonOptions),
       v => JsonSerializer.Deserialize<MyAggregate>(v, MyJsonOptions) ?? new MyAggregate());

   modelBuilder.Entity<MyEntity>()
       .Property(e => e.Body)
       .HasConversion(bodyConverter);
   ```

3. Add a `DbSet<MyEntity>` property at the bottom of `AppDbContext`.
4. Add a migration (see next section).

### Gotcha: in-place mutations must be flagged

EF only re-serializes a JSON-converted property when it believes the value changed. When repositories mutate the existing tracked entity's graph in place (instead of assigning a new instance), EF's change tracker may not detect the change. Always force the property as modified on write:

```csharp
var row = await context.Set<MyEntity>().FirstOrDefaultAsync(...);
// ...mutate row.Body...
context.Entry(row).Property(e => e.Body).IsModified = true;
await context.SaveChangesAsync();
```

See `JobRepository.UpdateAsync` and `SchedulerDraftStore.SaveAsync` for reference.

### Single-slot / singleton-row tables

Tables that only ever hold one row (e.g., `SchedulerDraft`) use a fixed primary key constant:

```csharp
private const int SingletonId = 1;

var row = await context.SchedulerDrafts.FirstOrDefaultAsync(d => d.Id == SingletonId, ct);
```

Keep the constant co-located with the repository and default the entity's `Id` to the same value.

---

## Writing a migration without `dotnet ef`

The repository has historically generated migrations through the EF CLI, but it is common (and fully supported) to hand-author migrations. Follow this checklist exactly; skipping any step results in silent failure at runtime.

### 1. File placement and naming

- Path: `BlazorWebApp/Migrations/<timestamp>_<Descriptive_Name>.cs`
- `<timestamp>` format: `yyyyMMddHHmmss`, strictly greater than the latest existing migration timestamp.
- The class name must match the file stem (without the timestamp), and the `[Migration("...")]` id must match the full file stem.

### 2. Required attributes on the migration class

EF Core 6 discovers migrations by scanning the migrations assembly for classes decorated with BOTH of these attributes. Missing either attribute means `Database.MigrateAsync()` will silently skip the migration (the symptom is a runtime `SQLite Error 1: 'no such table: X'` when the repository first queries the new table).

```csharp
using BlazorWebApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BlazorWebApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260421000000_Add_SchedulerDraft")]
    public partial class Add_SchedulerDraft : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ...
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ...
        }
    }
}
```

Why both:

- `[Migration]` supplies the migration identifier used against the `__EFMigrationsHistory` table.
- `[DbContext(typeof(AppDbContext))]` associates the migration with the right context. `AppDbContext.Database.MigrateAsync()` only applies migrations whose `[DbContext]` matches the current context type.

### 3. SQL column types (SQLite conventions used in this repo)

| .NET type       | SQLite column type |
| --------------- | ------------------ |
| `int`, `long`   | `INTEGER`          |
| `bool`          | `INTEGER`          |
| `string`        | `TEXT`             |
| `Guid`, `Guid?` | `TEXT`             |
| `DateTime?`     | `TEXT`             |
| `float`, `double` | `REAL`           |

Serialized JSON columns are always `TEXT`.

### 4. Update `AppDbContextModelSnapshot.cs`

Insert a new `modelBuilder.Entity("BlazorWebApp.Data.Entities.<Name>", b => { ... });` block alphabetically ordered by entity full name. This is what `dotnet ef migrations add` would do automatically; failing to update it means future scaffolded migrations will diff against a stale model and produce duplicate `CreateTable` operations.

Minimal template for a JSON-backed singleton row:

```csharp
modelBuilder.Entity("BlazorWebApp.Data.Entities.SchedulerDraft", b =>
{
    b.Property<int>("Id")
        .ValueGeneratedOnAdd()
        .HasColumnType("INTEGER");

    b.Property<string>("Body")
        .IsRequired()
        .HasColumnType("TEXT");

    b.Property<Guid?>("EditingJobId")
        .HasColumnType("TEXT");

    b.Property<DateTime>("UpdatedAt")
        .HasColumnType("TEXT");

    b.HasKey("Id");

    b.ToTable("SchedulerDrafts");
});
```

### 5. Designer file (optional at runtime, recommended for tooling)

Older migrations in this repo ship with `.Designer.cs` files that carry a copy of the full model snapshot and the `[Migration]` / `[DbContext]` attributes on a second `partial class`. At runtime these files are redundant once the attributes are on the migration class itself. They are only required by the EF CLI for operations like `dotnet ef migrations remove`.

Guidance:

- Preferred: put both attributes on the migration class (sections 2 above) and skip the Designer file.
- Only create a Designer file if you know you will need CLI-driven removal/rescaffolding; otherwise it is 500+ lines of duplicated snapshot.

---

## Registering services

- Repositories are registered in `Program.cs` under the "Scheduler job persistence" / equivalent block.
- Use `AddSingleton<TInterface, TImpl>()` for stateless repositories that operate through the factory (e.g., `JobRepository`).
- Use `AddScoped<TInterface, TImpl>()` for per-circuit state holders (e.g., `SchedulerDraftStore`, `SchedulerEditorState`).
- Always depend on `IDbContextFactory<AppDbContext>` inside repositories; never inject `AppDbContext` directly.

---

## Runtime debugging checklist (when migrations misbehave)

Symptom: `SQLite Error 1: 'no such table: X'` at repository read time.

1. Did the migration class get BOTH `[DbContext(typeof(AppDbContext))]` and `[Migration("<id>")]`? Missing either -> migration is silently skipped.
2. Does the `[Migration]` id match the filename stem exactly?
3. Is the timestamp strictly greater than every existing migration in `Migrations/`?
4. Did the snapshot get updated? Missing snapshot changes will not break startup directly but will corrupt future scaffolded migrations.
5. Inspect `__EFMigrationsHistory` in the SQLite database (the file path is set in `appsettings.json` connection string). A missing row for the new migration id confirms it never ran.
6. If the user's DB was originally created via `EnsureCreatedAsync()` without migrations, their `__EFMigrationsHistory` may be empty - in that case deleting and recreating the dev DB is the pragmatic fix. Do not apply this to production data without backup.

---

## Related documents

- `.github/copilot-instructions.md` - short-form rules surfaced to all agents.
- `BlazorWebApp/Data/AppDbContext.cs` - central model configuration.
- `BlazorWebApp/Scheduler/SchedulerJsonOptions.cs` - polymorphic JSON serialization options used by Scheduler aggregates.
- `Documentation/Plans/generation-scheduler/PHASE_14.md` - Scheduler Editor draft persistence; first entity in the codebase that was authored by-hand without the `dotnet ef` CLI.
