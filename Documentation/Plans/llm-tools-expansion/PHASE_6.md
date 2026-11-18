# Phase 6 - Template Engine (Mad Libs)

## Status

**Phase:** 6
**Build Status:** Not yet attempted
**Phase Status:** [ ] Not Started

---

## Objective

Ship a reusable prompt-template system where templates contain `[variable]` slots the user fills in, optionally resolved from wildcard collections. Templates are first-class DB-persisted entities; users can author, edit, save, and fill them. A **Fill Random** action randomizes all slots in one click; individual slots can be locked so Fill Random only re-randomizes the unlocked ones. Filled templates can be saved to the Prompt library or sent to Process view.

---

## Context

### Dependencies on prior phases

- **Phase 1** required.
- **Phase 3 Step 3** (per-name idempotent seeding) recommended but not required (this phase ships no seeded system-prompt templates).

### Entity design - a real EF migration

Unlike Phases 2-5, this phase adds a new relational table. The migration **must** be authored by hand following `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md` and `.github/copilot-instructions.md` (both `[DbContext]` and `[Migration]` attributes on the migration class, plus snapshot update).

```csharp
// BlazorWebApp/Data/Entities/PromptTemplate.cs
public class PromptTemplate
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Template body using [variable_name] placeholders. Placeholders may also be wildcard
    /// collections in the form [[category/collection]] or [[collection]] which resolve
    /// via WildcardService at fill time.
    /// </summary>
    [Required]
    public string Template { get; set; } = string.Empty;

    /// <summary>
    /// JSON dictionary (string -> List&lt;string&gt;) of candidate values for each plain
    /// [variable] placeholder. Wildcard [[...]] placeholders are resolved through
    /// WildcardService and do NOT appear in this dictionary.
    /// </summary>
    [Required]
    public string VariableOptionsJson { get; set; } = "{}";

    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    [NotMapped]
    public Dictionary<string, List<string>> VariableOptions
    {
        get => string.IsNullOrWhiteSpace(VariableOptionsJson)
            ? new()
            : JsonSerializer.Deserialize<Dictionary<string, List<string>>>(VariableOptionsJson) ?? new();
        set => VariableOptionsJson = JsonSerializer.Serialize(value);
    }
}
```

### Placeholder syntax contract

- `[variable]` - plain placeholder. Candidate values stored in `VariableOptions["variable"]`. Fill Random picks one uniformly at random.
- `[[collection_name]]` or `[[category/collection_name]]` - wildcard placeholder. Resolved via `WildcardService.GetRandomEntry` (or `GetRandomEntryWeighted` if a toggle in the view is enabled). NOT stored in `VariableOptions`.
- Double-bracket form is parsed first so a `[variable]` inside `[[...]]` would be treated as literal (edge case, document only).
- Unresolved placeholders remain in the output verbatim so the user can see what's missing.

### Existing infrastructure to reuse

- `IWildcardService`:
  - `Task<WildcardEntry?> GetRandomEntry(string collectionName)`
  - `Task<WildcardEntry?> GetRandomEntryWeighted(string collectionName)`
  - `Task<string> ParseWildcards(string input)` - already handles `__collection__` form. **Do NOT use this** for `[[...]]`; implement our own parser to keep the two syntaxes orthogonal.
- `IDatabaseService` for CRUD on the new entity (add methods `GetPromptTemplates`, `CreatePromptTemplate`, `UpdatePromptTemplate`, `DeletePromptTemplate`).
- `Prompt` entity + `CreatePrompt` for Save as Prompt.
- Parent `_selectedModel` (unused in this phase unless the user wants an "LLM fill" post-process - deferred to Phase 10).

### Architectural rules

- New EF migration. Follow persistence checklist strictly.
- Snapshot update mandatory.
- No events published in v1 (the template system is self-contained).

---

## Execution Checklist

### Step 1: `PromptTemplate` entity + DbContext wiring

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `BlazorWebApp/Data/Entities/PromptTemplate.cs` with the class above.
- [ ] Register in `AppDbContext`:
  ```csharp
  public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();
  ```
- [ ] Build clean before writing the migration.

#### Success Criteria

- Build passes.

---

### Step 2: Hand-authored EF migration + snapshot update

**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `BlazorWebApp/Migrations/YYYYMMDDhhmmss_Add_PromptTemplates.cs` with **both** attributes on the class (referenced in `.github/copilot-instructions.md`):

  ```csharp
  using Microsoft.EntityFrameworkCore.Infrastructure;
  using Microsoft.EntityFrameworkCore.Migrations;
  using BlazorWebApp.Data;

  #nullable disable

  namespace BlazorWebApp.Migrations
  {
      [DbContext(typeof(AppDbContext))]
      [Migration("YYYYMMDDhhmmss_Add_PromptTemplates")]
      public partial class Add_PromptTemplates : Migration
      {
          protected override void Up(MigrationBuilder migrationBuilder)
          {
              migrationBuilder.CreateTable(
                  name: "PromptTemplates",
                  columns: table => new
                  {
                      Id = table.Column<int>(type: "INTEGER", nullable: false)
                          .Annotation("Sqlite:Autoincrement", true),
                      Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                      Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                      Template = table.Column<string>(type: "TEXT", nullable: false),
                      VariableOptionsJson = table.Column<string>(type: "TEXT", nullable: false),
                      Category = table.Column<string>(type: "TEXT", nullable: true),
                      CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                      UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                  },
                  constraints: table => table.PrimaryKey("PK_PromptTemplates", x => x.Id));
          }

          protected override void Down(MigrationBuilder migrationBuilder) =>
              migrationBuilder.DropTable(name: "PromptTemplates");
      }
  }
  ```

  Replace `YYYYMMDDhhmmss` with the current UTC timestamp, e.g. `20260423120000`. The filename stem and `[Migration("...")]` id MUST match.

- [ ] Update `BlazorWebApp/Migrations/AppDbContextModelSnapshot.cs`: add a `modelBuilder.Entity("BlazorWebApp.Data.Entities.PromptTemplate", b => { ... });` block alphabetically positioned (it will sit between `Prompt` and `Resource` entries).
- [ ] Verify startup: `DatabaseService.InitializeDatabase` runs `EnsureCreatedAsync` + `MigrateAsync`. The new table should appear in the SQLite file.
- [ ] Verify `dotnet ef migrations list` lists the new migration (if the user has dotnet-ef tooling installed).

#### Success Criteria

- App starts without errors.
- `PromptTemplates` table exists in the database.
- Snapshot diff shows only the new entity.

---

### Step 3: `IDatabaseService` CRUD methods

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] Add to `IDatabaseService` + `DatabaseService`:
  ```csharp
  Task<List<PromptTemplate>> GetPromptTemplates();
  Task<PromptTemplate?> GetPromptTemplate(int id);
  Task CreatePromptTemplate(PromptTemplate entity);
  Task UpdatePromptTemplate(PromptTemplate entity);
  Task DeletePromptTemplate(int id);
  ```
- [ ] Implement with standard `using var context = await _factory.CreateDbContextAsync();` pattern used elsewhere.
- [ ] On `UpdatePromptTemplate`, set `UpdatedAt = DateTime.UtcNow`; on `CreatePromptTemplate`, set both `CreatedAt` and `UpdatedAt`.

#### Success Criteria

- All five methods round-trip correctly (manual smoke test via view or unit test).

---

### Step 4: Placeholder parser utility

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `BlazorWebApp/Services/PromptTemplateParser.cs` (static class):

  ```csharp
  public static class PromptTemplateParser
  {
      // Matches [[...]] (wildcard) or [...] (variable). Wildcards MUST be matched first.
      static readonly Regex WildcardRegex = new(@"\[\[([^\[\]]+)\]\]", RegexOptions.Compiled);
      static readonly Regex VariableRegex = new(@"\[([^\[\]]+)\]", RegexOptions.Compiled);

      public record Slot(string Name, bool IsWildcard, int Start, int Length);

      public static List<Slot> ExtractSlots(string template)
      {
          var slots = new List<Slot>();
          foreach (Match m in WildcardRegex.Matches(template))
              slots.Add(new Slot(m.Groups[1].Value.Trim(), true, m.Index, m.Length));
          // Mask wildcard spans before matching plain variables so [inner] inside [[outer]] is ignored.
          var masked = WildcardRegex.Replace(template, match => new string(' ', match.Length));
          foreach (Match m in VariableRegex.Matches(masked))
              slots.Add(new Slot(m.Groups[1].Value.Trim(), false, m.Index, m.Length));
          return slots.OrderBy(s => s.Start).ToList();
      }

      public static string ApplyFills(string template, IReadOnlyDictionary<string, string> fills)
      {
          // Replace [[...]] and [...] from fills dictionary; leave unknown slots untouched.
          string result = WildcardRegex.Replace(template, m =>
              fills.TryGetValue(m.Groups[1].Value.Trim(), out var v) ? v : m.Value);
          result = VariableRegex.Replace(result, m =>
              fills.TryGetValue(m.Groups[1].Value.Trim(), out var v) ? v : m.Value);
          return result;
      }
  }
  ```

- [ ] Unit-test the parser (happy path + nested brackets + unknown slots).

#### Success Criteria

- `ExtractSlots("a [subject] at [[roulette/location]]")` returns 2 slots in order.
- `ApplyFills` substitutes known slots and leaves unknowns intact.

---

### Step 5: `TemplateBuilderView.razor`

**Complexity:** 5
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `BlazorWebApp/Components/Prompts/LLM/Views/TemplateBuilderView.razor`.
- [ ] Two-pane layout:
  - **Left pane**: list of saved templates (`MudList`, selectable). Header has a `+` button to create a new one and a `-` button to delete the selected one.
  - **Right pane** (shown when a template is selected):
    - Template metadata: `Name`, `Description`, `Category` text fields.
    - Template body: `MudTextField` multi-line, monospace, with syntax-highlight hint for `[...]` and `[[...]]` - for v1, just use a monospace font and no highlighting.
    - "Slots" section: list of detected slots (from `PromptTemplateParser.ExtractSlots`). For each plain slot:
      - `MudAutocomplete` or `MudSelect` bound to the current fill value, seeded with `VariableOptions[slotName]` candidate list (if any).
      - A `MudIconButton` "Lock" toggle.
      - "Add candidate" button - appends to `VariableOptions[slotName]`.
    - For each wildcard slot: a read-only display of the resolved value + a refresh icon that re-rolls that specific slot.
    - Buttons: `Fill Random` (re-randomizes unlocked slots), `Save Template`, `Save as Prompt` (writes filled output to library), `Send to Process`, `Copy Filled`.
- [ ] Code-behind state:
  - `List<PromptTemplate> _templates;`
  - `PromptTemplate? _selected;`
  - `Dictionary<string, string> _fills = new();` - current resolved value per slot.
  - `HashSet<string> _lockedSlots = new();`
  - `string _filledOutput;` - result of `PromptTemplateParser.ApplyFills(_selected.Template, _fills)`.
- [ ] Fill Random algorithm:

  ```csharp
  async Task FillRandomAsync()
  {
      if (_selected == null) return;
      var slots = PromptTemplateParser.ExtractSlots(_selected.Template);
      foreach (var slot in slots)
      {
          if (_lockedSlots.Contains(slot.Name)) continue;
          if (slot.IsWildcard)
          {
              var entry = _weightedWildcards
                  ? await Wildcards.GetRandomEntryWeighted(slot.Name)
                  : await Wildcards.GetRandomEntry(slot.Name);
              _fills[slot.Name] = entry?.Value ?? $"[[{slot.Name}]]"; // leave literal on miss
          }
          else
          {
              var options = _selected.VariableOptions.TryGetValue(slot.Name, out var list) ? list : null;
              _fills[slot.Name] = options != null && options.Count > 0
                  ? options[Random.Shared.Next(options.Count)]
                  : $"[{slot.Name}]";
          }
      }
      Recompute();
  }

  void Recompute() => _filledOutput = PromptTemplateParser.ApplyFills(_selected!.Template, _fills);
  ```

- [ ] Save Template button calls `UpdatePromptTemplate` (or `CreatePromptTemplate` for new rows).
- [ ] Save as Prompt creates a `Prompt` with `Title = _selected.Name + " (filled)"`, `Positive = _filledOutput`, `Category = "Template Fills"`.
- [ ] Send to Process fires `OnSendToProcess.InvokeAsync(_filledOutput)` - parent handles via `PendingRestore`.
- [ ] Persist `_selected.Id`, `_lockedSlots`, `_fills` to `AppState.Prompts.LLM.TemplateBuilder` so the view restores its state across reloads.

#### Success Criteria

- Create template "A [subject] at [[roulette/location]] during [time_of_day]" with `subject` = ["warrior", "mage"] and `time_of_day` = ["dawn", "dusk"]. Save.
- Open it; Fill Random produces e.g. "A warrior at misty peak during dawn".
- Lock the subject slot; re-roll: subject stays the same, other slots change.
- Add a candidate value at runtime; it persists to the DB on Save Template.
- Unknown wildcard collections produce literal `[[collection]]` output so the user sees the miss.

---

### Step 6: `AppState.Prompts.LLM.TemplateBuilder` + nav + info

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] In `AppState.cs`:
  ```csharp
  public class AppStatePromptsLLMTemplateBuilder
  {
      public int? SelectedTemplateId { get; set; }
      public Dictionary<string, string> LastFills { get; set; } = new();
      public List<string> LockedSlots { get; set; } = new();
      public bool WeightedWildcards { get; set; } = true;
  }
  ```
  Add on `AppStatePromptsLLM`.
- [ ] Nav item: `new("templates", "Templates", Icons.Material.Filled.AutoFixHigh),`.
- [ ] Switch case in `LLMToolsTab`.
- [ ] Info content: title "Templates", overview "Author reusable prompt templates with `[variable]` and `[[wildcard]]` slots. Fill Random produces variations; lock slots to keep them stable across rolls."

#### Success Criteria

- State round-trips: selected template, locked slots, last fills persist.
- Nav entry works.

---

## Progress Tracking

| Step | Status | Complexity | Notes                                          |
| ---- | ------ | ---------- | ---------------------------------------------- |
| 1    | [ ]    | 1          | Entity + DbContext                             |
| 2    | [ ]    | 3          | Hand-authored migration + snapshot             |
| 3    | [ ]    | 1          | DatabaseService CRUD                           |
| 4    | [ ]    | 2          | Placeholder parser utility                     |
| 5    | [ ]    | 5          | `TemplateBuilderView` with fill/lock/save flow |
| 6    | [ ]    | 1          | AppState + nav + info                          |

**Total:** 13 points (original plan estimate: 5 - overrun from explicit parser + migration + two-pane UX).

---

## Issues & Resolutions

_None yet._

---

## Commit Checkpoints

- [ ] Entity + migration (commit together so the DB stays consistent)
- [ ] DatabaseService CRUD
- [ ] Parser utility (plus tests if we add them)
- [ ] View implementation
- [ ] AppState + nav + polish

---

## Open Risks

1. **Hand-authored migration pitfalls.** Per `.github/copilot-instructions.md`, omitting both attributes causes the migration scanner to silently skip. Mitigation: double-check both `[DbContext]` and `[Migration]` attributes exist on the class and the filename stem matches.
2. **Snapshot drift.** If the snapshot is not updated, future migrations may regenerate columns incorrectly. Mitigation: enforce in review; reference `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md`.
3. **`[...]` collides with Markdown-like syntax.** Users accustomed to Markdown may type `[link]` unintentionally. Mitigation: document syntax in info content; consider `{{variable}}` alt syntax in Phase 10 if complaints surface.
4. **Wildcard collection names with forward slashes.** Inside `[[...]]` the `/` is preserved verbatim and passed to `ResolveCollection`, which handles category syntax. No special escaping needed.
5. **Large `VariableOptions` JSON** could bloat a single row. SQLite handles it, but if a template has thousands of candidates, consider a separate `PromptTemplateVariableOption` table in a future phase.

---

## Phase Summary

_To be filled in on completion._
