# Phase 2: Persistence Layer

> **Main plan:** [MAIN_PLAN.md](./MAIN_PLAN.md)
> **Status:** [ ] Not Started
> **Complexity:** 5 points
> **Depends on:** `DanbooruOptions` (Phase 1) - only indirectly (not imported in Phase 2 code, but `SavedMediaPath` is implied by the feature context).
> **Unblocks:** Phase 3 consumes `ISavedDanbooruMediaRepository`; Phase 5 queries it for the Library grid.

---

## 1. Objective

Introduce a dedicated `SavedDanbooruMedia` entity with a unique index on `DanbooruPostId`, a JSON column for the tag bundle, a hand-authored EF Core migration, and a repository (`ISavedDanbooruMediaRepository` / `SavedDanbooruMediaRepository`) following the `JobRepository` pattern. After this phase, running the app applies the migration cleanly and CRUD against `SavedDanbooruMedia` works end-to-end through the repository.

---

## 2. Context & Background

The codebase persists JSON-backed aggregates via `ValueConverter` (see `JobEntity.Body`, `SchedulerDraft.Body`, `State.AppState`). The tag bundle is exactly that shape: a small complex object serialized into a single `TEXT` column. Scalar columns denormalize the fields used for filtering / sorting (`DanbooruPostId`, `Rating`, `Score`, `IsVideo`) so `GetPagedAsync` does not have to parse JSON per row.

Inherited conventions (verbatim from `MAIN_PLAN.md`):

- "Single JSON column for tag bundle. DB stays simple; client-side filtering fine for expected library size."
- "Dedicated `SavedDanbooruMedia` entity (not reuse `Image`). `Image` carries generation semantics (Project, Sampler, Mode, Selections). Danbooru media is conceptually separate."
- "Dedup by unique index on `DanbooruPostId` -> toast 'Already saved'."
- "Tag bundle type: `DanbooruTagBundle { List<string> Artist, Character, Copyright, General, Meta }` serialized via `ValueConverter`."

Convention document that must be followed end-to-end: `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md`. Key rules distilled:

- Hand-authored migrations MUST carry BOTH `[DbContext(typeof(AppDbContext))]` and `[Migration("<id>")]` on the class, or `MigrateAsync()` silently skips them.
- `AppDbContextModelSnapshot.cs` must be updated with a new `modelBuilder.Entity("BlazorWebApp.Data.Entities.SavedDanbooruMedia", b => { ... })` block inserted alphabetically.
- JSON-column in-place mutations require `context.Entry(row).Property(e => e.TagsBundle).IsModified = true;` on update.
- Repositories inject `IDbContextFactory<AppDbContext>` and create a fresh context per operation.

---

## 3. Prerequisites

- **Artifacts from prior phases:** None strictly required; `DanbooruOptions.SavedMediaPath` exists but Phase 2 does not read it.
- **Files the executor must read before writing code:**
  - `BlazorWebApp/Data/AppDbContext.cs` - where to register the converter and index
  - `BlazorWebApp/Data/Entities/JobEntity.cs` - pattern for a scalar-plus-JSON entity
  - `BlazorWebApp/Data/Entities/SchedulerDraft.cs` - minimal JSON-backed entity shape
  - `BlazorWebApp/Scheduler/Persistence/JobRepository.cs` - reference repository pattern (context factory, in-place mutation + `IsModified = true`)
  - `BlazorWebApp/Migrations/20260421000000_Add_SchedulerDraft.cs` - hand-authored migration template with both attributes
  - `BlazorWebApp/Migrations/AppDbContextModelSnapshot.cs` - snapshot insertion ordering
  - `BlazorWebApp/Data/Dtos/DanbooruPost.cs` - source DTO whose fields map onto the entity
- **External references:** `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md`.

---

## 4. Files Inventory

### To Create

| Path                                                              | Purpose                                                               |
| ----------------------------------------------------------------- | --------------------------------------------------------------------- |
| `BlazorWebApp/Data/Entities/SavedDanbooruMedia.cs`                | EF entity: scalar columns + `DanbooruTagBundle` JSON property         |
| `BlazorWebApp/Data/Entities/DanbooruTagBundle.cs`                 | Plain POCO holding the five tag lists                                 |
| `BlazorWebApp/Data/Converters/DanbooruTagBundleConverter.cs`      | `ValueConverter<DanbooruTagBundle,string>` + matching `ValueComparer` |
| `BlazorWebApp/Data/Repositories/ISavedDanbooruMediaRepository.cs` | Repository contract                                                   |
| `BlazorWebApp/Data/Repositories/SavedDanbooruMediaRepository.cs`  | Implementation using `IDbContextFactory<AppDbContext>`                |
| `BlazorWebApp/Data/Repositories/SavedDanbooruMediaFilter.cs`      | Simple record/DTO holding paged-query filters                         |
| `BlazorWebApp/Migrations/{timestamp}_Add_SavedDanbooruMedia.cs`   | Hand-authored migration (both attributes, `yyyyMMddHHmmss`)           |

> **Note on repository folder:** No `BlazorWebApp/Data/Repositories/` folder exists today (`JobRepository` lives under `Scheduler/Persistence`). Creating a new `Data/Repositories/` folder is acceptable and keeps domain repositories co-located with entities; confirm during Stage 1 of Step 2.4 (see Open Clarifications) or place the repo under `BlazorWebApp/Services/` if the user prefers that location.

### To Modify

| Path                                                   | Change                                                                                                                                                      |
| ------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `BlazorWebApp/Data/AppDbContext.cs`                    | Register the tag-bundle converter, unique index on `DanbooruPostId`, add `DbSet<SavedDanbooruMedia> SavedDanbooruMedia`                                     |
| `BlazorWebApp/Migrations/AppDbContextModelSnapshot.cs` | Insert a `modelBuilder.Entity("BlazorWebApp.Data.Entities.SavedDanbooruMedia", ...)` block alphabetically (between `ResourceTemplate` and `SchedulerDraft`) |
| `BlazorWebApp/Program.cs`                              | Register `ISavedDanbooruMediaRepository` with `AddSingleton`                                                                                                |

### To Leave Untouched (but referenced)

| Path                                  | Why it matters                                                                          |
| ------------------------------------- | --------------------------------------------------------------------------------------- |
| `BlazorWebApp/Data/Entities/Image.cs` | Shows why reuse was rejected (generation semantics: Project, Sampler, Mode, Selections) |
| `BlazorWebApp/Data/Converters/*.cs`   | Converter naming and placement reference                                                |

---

## 5. Step-by-Step Execution

### Step 2.1: Add `SavedDanbooruMedia` entity and `DanbooruTagBundle`

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Add `BlazorWebApp/Data/Entities/DanbooruTagBundle.cs` with five `List<string>` properties (defaulted to empty lists).
- [ ] Add `BlazorWebApp/Data/Entities/SavedDanbooruMedia.cs` with scalar columns plus a `TagsBundle` navigation-style property of type `DanbooruTagBundle`.
- [ ] Provide a convenience constructor that maps from `DanbooruPost` (mirror the existing `Image(DanbooruPost post)` pattern in `BlazorWebApp/Data/Entities/Image.cs`).

#### Implementation Notes

- Do not mark any list as nullable; default to `new()` to avoid EF materializing `null`.
- Keep `RelativePath` as the canonical on-disk locator (relative to `DanbooruOptions.SavedMediaPath`). Absolute path is reconstructable and avoids invalidating rows if `SavedMediaPath` moves.
- `Extension` is lowercased and stored without the leading dot (match `DanbooruPost.Extension` usage).

#### Code Sketch

```csharp
// BlazorWebApp/Data/Entities/DanbooruTagBundle.cs
namespace BlazorWebApp.Data.Entities
{
    public class DanbooruTagBundle
    {
        public List<string> Artist { get; set; } = new();
        public List<string> Character { get; set; } = new();
        public List<string> Copyright { get; set; } = new();
        public List<string> General { get; set; } = new();
        public List<string> Meta { get; set; } = new();
    }
}
```

```csharp
// BlazorWebApp/Data/Entities/SavedDanbooruMedia.cs
using BlazorWebApp.Data.Dtos;

namespace BlazorWebApp.Data.Entities
{
    /// <summary>
    /// A Danbooru post that has been downloaded to the local library.
    /// Scalar columns carry filter/sort keys; <see cref="TagsBundle"/> holds the full
    /// tag graph serialized as JSON through <c>DanbooruTagBundleConverter</c>.
    /// </summary>
    public class SavedDanbooruMedia
    {
        public int Id { get; set; }

        /// <summary>Source Danbooru post id. Unique index on this column enforces dedup.</summary>
        public int DanbooruPostId { get; set; }

        /// <summary>Path relative to <c>DanbooruOptions.SavedMediaPath</c> (no leading slash).</summary>
        public string RelativePath { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public bool IsVideo { get; set; }

        public int Width { get; set; }
        public int Height { get; set; }
        public int Score { get; set; }
        public string Rating { get; set; } = string.Empty; // "g" | "s" | "q" | "e" | ""

        public string SourceUrl { get; set; } = string.Empty;  // post.Url
        public string SampleUrl { get; set; } = string.Empty;  // post.SampleUrl
        public string PreviewUrl { get; set; } = string.Empty; // post.PreviewUrl

        public DateTime SavedAt { get; set; } = DateTime.UtcNow;
        public bool Favorite { get; set; }
        public string? Notes { get; set; }

        public DanbooruTagBundle TagsBundle { get; set; } = new();

        public SavedDanbooruMedia() { }

        public SavedDanbooruMedia(DanbooruPost post, string relativePath, string fileName)
        {
            DanbooruPostId = post.Id;
            RelativePath = relativePath;
            FileName = fileName;
            Extension = post.Extension ?? string.Empty;
            IsVideo = post.IsVideo;
            Width = post.Width;
            Height = post.Height;
            Score = post.Score;
            Rating = post.Rating ?? string.Empty;
            SourceUrl = post.Url ?? string.Empty;
            SampleUrl = post.SampleUrl ?? string.Empty;
            PreviewUrl = post.PreviewUrl ?? string.Empty;
            TagsBundle = new DanbooruTagBundle
            {
                Artist = post.TagsArtist ?? new(),
                Character = post.TagsCharacter ?? new(),
                Copyright = post.TagsCopyright ?? new(),
                General = post.TagsGeneral ?? new(),
                Meta = post.TagsMeta ?? new(),
            };
        }
    }
}
```

#### Conventions to Respect

- Entity POCO, no EF attributes - all configuration in `OnModelCreating`.
- Default all non-nullable `string` to `string.Empty`.

#### Validation

- File compiles.
- Unit inspection: all fields from `DanbooruPost` that affect display / filtering are represented.

#### Changes Made

_To be filled in after the step is implemented._

---

### Step 2.2: Tag-bundle JSON converter and model configuration

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Add `BlazorWebApp/Data/Converters/DanbooruTagBundleConverter.cs` with a static `Converter` (`ValueConverter<DanbooruTagBundle,string>`) and `Comparer` (`ValueComparer<DanbooruTagBundle>`).
- [ ] In `AppDbContext.OnModelCreating`, register the converter on `SavedDanbooruMedia.TagsBundle` and add a unique index on `DanbooruPostId`.
- [ ] Add `DbSet<SavedDanbooruMedia> SavedDanbooruMedia { get; set; }` at the bottom of `AppDbContext`.

#### Implementation Notes

- Reuse the `JsonSerializerOptions` pattern already in `AppDbContext` (`DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`).
- The comparer uses JSON-round-trip equality like `loraListComparer`. Cheap and correct for this data size.

#### Code Sketch

```csharp
// BlazorWebApp/Data/Converters/DanbooruTagBundleConverter.cs
using BlazorWebApp.Data.Entities;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Converters
{
    public static class DanbooruTagBundleConverter
    {
        private static readonly JsonSerializerOptions _json = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static readonly ValueConverter<DanbooruTagBundle, string> Converter = new(
            v => JsonSerializer.Serialize(v ?? new DanbooruTagBundle(), _json),
            v => string.IsNullOrWhiteSpace(v)
                ? new DanbooruTagBundle()
                : JsonSerializer.Deserialize<DanbooruTagBundle>(v, _json) ?? new DanbooruTagBundle());

        public static readonly ValueComparer<DanbooruTagBundle> Comparer = new(
            (c1, c2) => JsonSerializer.Serialize(c1, _json) == JsonSerializer.Serialize(c2, _json),
            c => JsonSerializer.Serialize(c, _json).GetHashCode(),
            c => JsonSerializer.Deserialize<DanbooruTagBundle>(JsonSerializer.Serialize(c, _json), _json) ?? new DanbooruTagBundle());
    }
}
```

```csharp
// AppDbContext.OnModelCreating (insert near the SchedulerDraft block)
modelBuilder.Entity<SavedDanbooruMedia>()
    .HasIndex(s => s.DanbooruPostId)
    .IsUnique();

modelBuilder.Entity<SavedDanbooruMedia>()
    .Property(s => s.TagsBundle)
    .HasConversion(DanbooruTagBundleConverter.Converter, DanbooruTagBundleConverter.Comparer);
```

```csharp
// AppDbContext (bottom of class)
public DbSet<SavedDanbooruMedia> SavedDanbooruMedia { get; set; }
```

#### Conventions to Respect

- Converter class is `static` with `public static readonly` fields, to match `SchedulerJsonOptions` style.
- Unique index declared explicitly with `.IsUnique()`.

#### Validation

- `dotnet build` succeeds.
- No error about missing `ValueComparer` when EF compares tracked entities.

#### Changes Made

_To be filled in after the step is implemented._

---

### Step 2.3: Hand-authored migration + snapshot update

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Pick a timestamp strictly greater than the latest migration (current latest is `20260421000000_Add_SchedulerDraft`). Propose `20260423000000_Add_SavedDanbooruMedia` (see Open Clarifications for confirmation of the exact timestamp).
- [ ] Create `BlazorWebApp/Migrations/<timestamp>_Add_SavedDanbooruMedia.cs` with BOTH `[DbContext(typeof(AppDbContext))]` and `[Migration("<timestamp>_Add_SavedDanbooruMedia")]`.
- [ ] `Up` creates table `SavedDanbooruMedia` with all scalar columns (SQLite `INTEGER` / `TEXT` / `REAL` per the repo convention table) and the unique index `IX_SavedDanbooruMedia_DanbooruPostId`. `Down` drops the table.
- [ ] Update `AppDbContextModelSnapshot.cs` inserting the block alphabetically between `ResourceTemplate` and `SchedulerDraft`.

#### Implementation Notes

Follow `20260421000000_Add_SchedulerDraft.cs` and `20260420203413_Add_JobEntity.cs` as templates. Column order inside `CreateTable` matches the snapshot block for clarity.

#### Code Sketch

```csharp
// BlazorWebApp/Migrations/20260423000000_Add_SavedDanbooruMedia.cs
using System;
using BlazorWebApp.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260423000000_Add_SavedDanbooruMedia")]
    public partial class Add_SavedDanbooruMedia : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedDanbooruMedia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DanbooruPostId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    Extension = table.Column<string>(type: "TEXT", nullable: false),
                    IsVideo = table.Column<bool>(type: "INTEGER", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<int>(type: "INTEGER", nullable: false),
                    Rating = table.Column<string>(type: "TEXT", nullable: false),
                    SourceUrl = table.Column<string>(type: "TEXT", nullable: false),
                    SampleUrl = table.Column<string>(type: "TEXT", nullable: false),
                    PreviewUrl = table.Column<string>(type: "TEXT", nullable: false),
                    SavedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Favorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    TagsBundle = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_SavedDanbooruMedia", x => x.Id));

            migrationBuilder.CreateIndex(
                name: "IX_SavedDanbooruMedia_DanbooruPostId",
                table: "SavedDanbooruMedia",
                column: "DanbooruPostId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SavedDanbooruMedia");
        }
    }
}
```

```csharp
// AppDbContextModelSnapshot.cs - insert alphabetically (between ResourceTemplate and SchedulerDraft)
modelBuilder.Entity("BlazorWebApp.Data.Entities.SavedDanbooruMedia", b =>
{
    b.Property<int>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
    b.Property<int>("DanbooruPostId").HasColumnType("INTEGER");
    b.Property<string>("Extension").IsRequired().HasColumnType("TEXT");
    b.Property<bool>("Favorite").HasColumnType("INTEGER");
    b.Property<string>("FileName").IsRequired().HasColumnType("TEXT");
    b.Property<int>("Height").HasColumnType("INTEGER");
    b.Property<bool>("IsVideo").HasColumnType("INTEGER");
    b.Property<string>("Notes").HasColumnType("TEXT");
    b.Property<string>("PreviewUrl").IsRequired().HasColumnType("TEXT");
    b.Property<string>("Rating").IsRequired().HasColumnType("TEXT");
    b.Property<string>("RelativePath").IsRequired().HasColumnType("TEXT");
    b.Property<string>("SampleUrl").IsRequired().HasColumnType("TEXT");
    b.Property<DateTime>("SavedAt").HasColumnType("TEXT");
    b.Property<int>("Score").HasColumnType("INTEGER");
    b.Property<string>("SourceUrl").IsRequired().HasColumnType("TEXT");
    b.Property<string>("TagsBundle").IsRequired().HasColumnType("TEXT");
    b.Property<int>("Width").HasColumnType("INTEGER");
    b.HasKey("Id");
    b.HasIndex("DanbooruPostId").IsUnique();
    b.ToTable("SavedDanbooruMedia");
});
```

#### Conventions to Respect

Verbatim from the persistence doc:

- BOTH `[DbContext]` and `[Migration]` attributes on the migration class.
- Migration id matches filename stem exactly.
- Timestamp strictly greater than every existing migration.
- Snapshot block alphabetical by entity full name.

#### Validation

- App starts; `DatabaseService.InitializeDatabase()` runs without error.
- SQLite shell or EF inspector shows table `SavedDanbooruMedia` with the expected columns and unique index.
- `__EFMigrationsHistory` contains the new migration id.

#### Changes Made

_To be filled in after the step is implemented._

---

### Step 2.4: Repository interface + implementation + DI

**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks

- [ ] Add `SavedDanbooruMediaFilter` record with `string? Tag`, `string? Rating`, `int? MinScore`, `bool? VideoOnly`, `bool? FavoriteOnly`.
- [ ] Add `ISavedDanbooruMediaRepository` with: `AddAsync`, `ExistsByPostIdAsync`, `GetByIdAsync`, `GetByPostIdAsync`, `GetPagedAsync`, `CountAsync`, `UpdateAsync`, `DeleteAsync`.
- [ ] Implement `SavedDanbooruMediaRepository` using `IDbContextFactory<AppDbContext>`, following `JobRepository` patterns (fresh context per call, `AsNoTracking` on reads, `IsModified = true` on `TagsBundle` mutations).
- [ ] Register in `Program.cs` next to existing repository registrations as `AddSingleton<ISavedDanbooruMediaRepository, SavedDanbooruMediaRepository>();`.

#### Implementation Notes

- `GetPagedAsync` applies filters server-side for scalar columns (`Rating`, `Score`, `IsVideo`, `Favorite`). Tag filtering is client-side after materialization (main-plan decision: "client-side filtering fine for expected library size"); do a cheap prefilter by searching the raw `TagsBundle` JSON for the substring before deserialization to avoid materializing the full list.
- Return ordered by `SavedAt DESC` by default for Library-tab "most recent first" feel.
- `AddAsync` returns the saved entity (with assigned `Id`). The caller (Phase 3 service) is responsible for raising events.

#### Code Sketch

```csharp
// BlazorWebApp/Data/Repositories/SavedDanbooruMediaFilter.cs
namespace BlazorWebApp.Data.Repositories
{
    public record SavedDanbooruMediaFilter(
        string? Tag = null,
        string? Rating = null,
        int? MinScore = null,
        bool? VideoOnly = null,
        bool? FavoriteOnly = null);
}
```

```csharp
// BlazorWebApp/Data/Repositories/ISavedDanbooruMediaRepository.cs
using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Data.Repositories
{
    public interface ISavedDanbooruMediaRepository
    {
        Task<SavedDanbooruMedia> AddAsync(SavedDanbooruMedia entity, CancellationToken ct = default);
        Task<bool> ExistsByPostIdAsync(int danbooruPostId, CancellationToken ct = default);
        Task<SavedDanbooruMedia?> GetByIdAsync(int id, CancellationToken ct = default);
        Task<SavedDanbooruMedia?> GetByPostIdAsync(int danbooruPostId, CancellationToken ct = default);
        Task<List<SavedDanbooruMedia>> GetPagedAsync(SavedDanbooruMediaFilter filter, int skip, int take, CancellationToken ct = default);
        Task<int> CountAsync(SavedDanbooruMediaFilter filter, CancellationToken ct = default);
        Task UpdateAsync(SavedDanbooruMedia entity, CancellationToken ct = default);
        Task DeleteAsync(int id, CancellationToken ct = default);
    }
}
```

```csharp
// BlazorWebApp/Data/Repositories/SavedDanbooruMediaRepository.cs (shape)
using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Data.Repositories
{
    public class SavedDanbooruMediaRepository : ISavedDanbooruMediaRepository
    {
        private readonly IDbContextFactory<AppDbContext> _factory;
        private readonly ILogger<SavedDanbooruMediaRepository> _logger;

        public SavedDanbooruMediaRepository(IDbContextFactory<AppDbContext> factory, ILogger<SavedDanbooruMediaRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<SavedDanbooruMedia> AddAsync(SavedDanbooruMedia entity, CancellationToken ct = default)
        {
            await using var ctx = await _factory.CreateDbContextAsync(ct);
            ctx.SavedDanbooruMedia.Add(entity);
            await ctx.SaveChangesAsync(ct);
            return entity;
        }

        public async Task<bool> ExistsByPostIdAsync(int postId, CancellationToken ct = default)
        {
            await using var ctx = await _factory.CreateDbContextAsync(ct);
            return await ctx.SavedDanbooruMedia.AsNoTracking().AnyAsync(e => e.DanbooruPostId == postId, ct);
        }

        public async Task<List<SavedDanbooruMedia>> GetPagedAsync(SavedDanbooruMediaFilter filter, int skip, int take, CancellationToken ct = default)
        {
            await using var ctx = await _factory.CreateDbContextAsync(ct);
            var query = ctx.SavedDanbooruMedia.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(filter.Rating)) query = query.Where(e => e.Rating == filter.Rating);
            if (filter.MinScore is int min) query = query.Where(e => e.Score >= min);
            if (filter.VideoOnly == true) query = query.Where(e => e.IsVideo);
            if (filter.FavoriteOnly == true) query = query.Where(e => e.Favorite);
            // Cheap JSON prefilter on tag substring; exact match happens client-side if needed.
            if (!string.IsNullOrWhiteSpace(filter.Tag)) query = query.Where(e => EF.Property<string>(e, "TagsBundle").Contains(filter.Tag));
            return await query.OrderByDescending(e => e.SavedAt).Skip(skip).Take(take).ToListAsync(ct);
        }

        public async Task UpdateAsync(SavedDanbooruMedia entity, CancellationToken ct = default)
        {
            await using var ctx = await _factory.CreateDbContextAsync(ct);
            ctx.SavedDanbooruMedia.Update(entity);
            ctx.Entry(entity).Property(e => e.TagsBundle).IsModified = true;
            await ctx.SaveChangesAsync(ct);
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            await using var ctx = await _factory.CreateDbContextAsync(ct);
            var row = await ctx.SavedDanbooruMedia.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (row == null) return;
            ctx.SavedDanbooruMedia.Remove(row);
            await ctx.SaveChangesAsync(ct);
        }

        // GetByIdAsync, GetByPostIdAsync, CountAsync follow the same shape.
    }
}
```

```csharp
// Program.cs (insert near the Scheduler persistence block)
builder.Services.AddSingleton<
    BlazorWebApp.Data.Repositories.ISavedDanbooruMediaRepository,
    BlazorWebApp.Data.Repositories.SavedDanbooruMediaRepository>();
```

#### Conventions to Respect

- "Repositories are registered in `Program.cs` ... Use `AddSingleton<TInterface, TImpl>()` for stateless repositories that operate through the factory." (persistence doc)
- `AsNoTracking()` on all read paths.
- `IsModified = true` on `TagsBundle` for any in-place mutation path (update existing row's tag lists).

#### Validation

- Build passes.
- Ad-hoc verification via a throwaway console call or a test: `AddAsync` -> `ExistsByPostIdAsync` -> `GetByPostIdAsync` -> `DeleteAsync` round-trip succeeds.

#### Changes Made

_To be filled in after the step is implemented._

---

## 6. Integration Points

- **DI registrations (exact line to add to `Program.cs`):**

  ```csharp
  builder.Services.AddSingleton<
      BlazorWebApp.Data.Repositories.ISavedDanbooruMediaRepository,
      BlazorWebApp.Data.Repositories.SavedDanbooruMediaRepository>();
  ```

  Place it near the other singleton repository-style registrations (below `IWorkflowStateService`, above the Scheduler block).

- **Events to publish / subscribe:** _Not applicable for this phase. Phase 3 introduces events._
- **Configuration bindings:** None added.
- **Startup side-effects:** `MigrateAsync()` applies the new migration. If this is a fresh DB, `EnsureCreatedAsync()` creates the table directly; either path must result in the same schema.

---

## 7. Testing Strategy

- **Automated tests to add/update:** `BlazorWebApp.Tests/` has a SQLite-based integration pattern (see existing scheduler / repository tests). Add a minimal repository round-trip test (add -> exists -> get -> delete). If unavailable in scope, defer to Phase 3 where the library service exercises the repository end-to-end.
- **Manual verification checklist:**
  1. Delete `BlazorWebApp/bin/Debug/net6.0/BlazorWebApp.db` (if present) and run the app. Confirm schema is created.
  2. Keep an existing DB and run the app. Confirm the migration applies without duplicate-table errors.
  3. Inspect the DB: table exists, unique index `IX_SavedDanbooruMedia_DanbooruPostId` present.
- **Regression watch-list:** Other migrations continue to run (`Add_JobEntity`, `Add_SchedulerDraft` history rows intact).

---

## 8. Stress Points Specific to This Phase

| Risk                                                                | Mitigation                                                                           |
| ------------------------------------------------------------------- | ------------------------------------------------------------------------------------ |
| Duplicate migration id / missed snapshot update (EF silently skips) | Checklist in Step 2.3 + grep `__EFMigrationsHistory` post-run.                       |
| Tag bundle JSON mutation not persisted                              | `Entry(entity).Property(e => e.TagsBundle).IsModified = true;` inside `UpdateAsync`. |
| `ValueComparer` missing causes EF to treat every load as "changed"  | Register the `Comparer` alongside the `Converter` in `OnModelCreating`.              |
| JSON `Contains` filter is case-sensitive on SQLite by default       | Acceptable for v1 (Danbooru tags are lowercase). Note in the repository XML docs.    |

---

## 9. Resolved Assumptions

- **Tag storage shape:** `DanbooruTagBundle` as a POCO rather than five separate columns. The main plan specifies single JSON column.
- **Repository location:** `BlazorWebApp/Data/Repositories/` (new folder) - keeps domain repositories with their entities. `JobRepository` lives elsewhere because it is Scheduler-feature-scoped; Danbooru's repo is DB-scoped.
- **Default ordering:** `SavedAt DESC`. Newest-first is the natural UX for a library feed and costs nothing (index not required because the page size is small).
- **Case sensitivity on tag filter:** Left case-sensitive (SQLite default) since Danbooru tags are canonically lowercase.
- **Migration timestamp convention:** `yyyyMMddHHmmss` strictly greater than `20260421000000`. Using a nominal `20260423000000` during planning; executor may use the current day's timestamp when creating the file.

---

## 10. Open Clarifications

- **Step 2.3 - Exact migration timestamp:** Use the current `yyyyMMddHHmmss` at authoring time (must be greater than `20260421000000`). Default: `20260423000000`.
- **Step 2.4 - Repository folder:** `BlazorWebApp/Data/Repositories/` is proposed. Alternative: `BlazorWebApp/Services/` (next to `ResourcesService`). Confirm with the user if they prefer one over the other before creating the folder.

---

## 11. Progress Tracking

| Step | Status | Complexity | Notes |
| ---- | ------ | ---------- | ----- |
| 2.1  | [ ]    | 2          |       |
| 2.2  | [ ]    | 2          |       |
| 2.3  | [ ]    | 2          |       |
| 2.4  | [ ]    | 3          |       |

---

## 12. Issues & Resolutions

_Populated during execution._

---

## 13. Commit Checkpoints

- [ ] Step 2.1 complete
- [ ] Step 2.2 complete
- [ ] Step 2.3 complete
- [ ] Step 2.4 complete
- [ ] Phase build green (migration applies on a fresh DB AND on an existing DB)

---

## 14. Phase Summary

_To be filled in after the phase is complete._

---

## 15. Cross-References

- Main plan section: [Phase 2: Persistence Layer](./MAIN_PLAN.md)
- Prior phase: [PHASE_1.md](./PHASE_1.md)
- Next phase: [PHASE_3.md](./PHASE_3.md)
- Related docs: `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md`
