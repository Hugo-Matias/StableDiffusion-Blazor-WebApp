# Follow-up Analysis Report

## Scope

- Plan analysed: [Documentation/Plans/danbooru-library/MAIN_PLAN.md](MAIN_PLAN.md)
- Baseline report: [Documentation/Plans/danbooru-library/ANALYSIS_REPORT_2.md](ANALYSIS_REPORT_2.md)
  (round 2 report; round 1 is [ANALYSIS_REPORT.md](ANALYSIS_REPORT.md))
- Phase documents reviewed: [PHASE_1.md](PHASE_1.md), [PHASE_2.md](PHASE_2.md), [PHASE_3.md](PHASE_3.md), [PHASE_4.md](PHASE_4.md), [PHASE_5.md](PHASE_5.md), [PHASE_6.md](PHASE_6.md), [PHASE_7.md](PHASE_7.md)
- Git scope reviewed (working tree vs `HEAD` on branch `comfyui`):
  - Modified: [BlazorWebApp/Components/Resources/DanbooruImageCard.razor](../../../BlazorWebApp/Components/Resources/DanbooruImageCard.razor), [BlazorWebApp/Data/AppDbContext.cs](../../../BlazorWebApp/Data/AppDbContext.cs), [BlazorWebApp/Migrations/AppDbContextModelSnapshot.cs](../../../BlazorWebApp/Migrations/AppDbContextModelSnapshot.cs), [BlazorWebApp/Pages/Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor), [BlazorWebApp/Program.cs](../../../BlazorWebApp/Program.cs), [BlazorWebApp/Services/DanbooruService.cs](../../../BlazorWebApp/Services/DanbooruService.cs)
  - Untracked (new in this pass): [SavedDanbooruMediaCard.razor](../../../BlazorWebApp/Components/Resources/SavedDanbooruMediaCard.razor), [SavedDanbooruMediaCard.razor.css](../../../BlazorWebApp/Components/Resources/SavedDanbooruMediaCard.razor.css), [DanbooruTagBundleConverter.cs](../../../BlazorWebApp/Data/Converters/DanbooruTagBundleConverter.cs), [DanbooruTagBundle.cs](../../../BlazorWebApp/Data/Entities/DanbooruTagBundle.cs), [SavedDanbooruMedia.cs](../../../BlazorWebApp/Data/Entities/SavedDanbooruMedia.cs), [Data/Repositories/](../../../BlazorWebApp/Data/Repositories/), [DanbooruMediaDeletedEventArgs.cs](../../../BlazorWebApp/Events/DanbooruMediaDeletedEventArgs.cs), [DanbooruMediaSavedEventArgs.cs](../../../BlazorWebApp/Events/DanbooruMediaSavedEventArgs.cs), [20260424000000_Add_SavedDanbooruMedia.cs](../../../BlazorWebApp/Migrations/20260424000000_Add_SavedDanbooruMedia.cs), [DanbooruOptions.cs](../../../BlazorWebApp/Models/DanbooruOptions.cs), [DanbooruLibraryService.cs](../../../BlazorWebApp/Services/DanbooruLibraryService.cs), [IDanbooruLibraryService.cs](../../../BlazorWebApp/Services/IDanbooruLibraryService.cs)
- Analysis mode: Documentation only; no code changes performed

## Executive Summary

The round-2 follow-up resolved every P0 item from [ANALYSIS_REPORT_2.md](ANALYSIS_REPORT_2.md)
and most of the P1 items. Library now receives events via `IEventService`, the
`HttpClient` registration is no longer captive, `UpdateAsync` persists scalar changes,
`GetPagedAsync` uses SQL-side `LIMIT/OFFSET` on the fast path, the library lazy-load
is render-safe, `WriteLibraryTags` reaches parity with `WriteDanbooruTags`, and the
"none" rating option was removed from the filter dropdown. Several P2 items remain
open (orphan-file cleanup, streaming download, `_libraryMinScore` typing, library
pagination / infinite scroll, startup logging, save-button in-flight state, whitespace
noise). One new correctness issue was introduced in the event handlers: the
`OnMediaSaved` subscription inserts new rows regardless of the currently active
filter, producing results that do not match the user's filter state. A separate
pre-existing concern surfaced: the unrelated `DanbooruService` migration to
`IOptions<DanbooruOptions>` left real API credentials committed to `appsettings.json`
(out of plan scope, but worth flagging).

## Prior Findings Validation

### Finding 1: 5.B - EventService subscription missing (pub/sub convention)

**Previous Severity:** High (P0)
**Current Status:** Fixed
**Plan Reference:** [MAIN_PLAN.md](MAIN_PLAN.md) - "all events through EventService"; [PHASE_5.md](PHASE_5.md) Step 5.3
**Files Reviewed:** [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor)

**Original Concern**
UI never subscribed to `DanbooruMediaSaved/Deleted`; save/delete paths called
`await LoadLibrary()` directly, violating the workspace pub/sub convention.

**Validation Result**
`OnInitializedAsync` now creates `_onMediaSavedHandler` / `_onMediaDeletedHandler`
delegates and calls `EventService.Subscribe<DanbooruMediaSavedEventArgs>` /
`Subscribe<DanbooruMediaDeletedEventArgs>`. `DisposeAsync` unsubscribes both.
`SaveToLibrary` and `DeleteFromLibrary` no longer call `LoadLibrary()` - they rely
on the event bus.

**Remaining Gap**
_None for the convention itself._ A secondary correctness issue in the handler
implementation is documented as [New Finding 1](#new-finding-1-onmediasaved-bypasses-active-library-filters).

**Notes**
Subscribe order looks correct: handlers are stored in fields so that
`Unsubscribe` receives the same delegate instance.

---

### Finding 2: R2-2 - Lazy-load path ignores Blazor's render cycle

**Previous Severity:** High (P0)
**Current Status:** Fixed
**Plan Reference:** [PHASE_5.md](PHASE_5.md) Step 5.1
**Files Reviewed:** [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L171-L200)

**Original Concern**
`_ = LoadLibrary()` fired outside `InvokeAsync`, so no re-render occurred on
completion; the spinner stuck forever on first tab activation and
`_libraryLoaded = true` was set before the load completed, blocking retries.

**Validation Result**
The setter now wraps the bootstrap in `InvokeAsync(async () => ...)`, uses
`try/catch/finally`, resets `_libraryLoaded = false` on failure, surfaces the error
via `Snackbar`, and calls `StateHasChanged()` in `finally`. `LoadLibrary` itself now
calls `await InvokeAsync(StateHasChanged)` before and after the DB query so the
spinner transitions are observed.

**Remaining Gap**
_None._

**Notes**
The retry-on-failure reset happens before `StateHasChanged()` in `finally`, which
is correct.

---

### Finding 3: R2-1 - `GetPagedAsync` tag filter applied after full materialisation

**Previous Severity:** High (P0)
**Current Status:** Partially Fixed
**Plan Reference:** [PHASE_2.md](PHASE_2.md) Step 2.4
**Files Reviewed:** [SavedDanbooruMediaRepository.cs](../../../BlazorWebApp/Data/Repositories/SavedDanbooruMediaRepository.cs#L49-L95)

**Original Concern**
`Skip/Take` always applied in memory; rating filter used `EF.Functions.Like` without
wildcards (exploitable if callers supplied `%`/`_`).

**Validation Result**

- Fast path (no `TagSearch`): now uses `query.OrderByDescending(...).Skip(...).Take(...).ToListAsync()`.
  SQL `LIMIT/OFFSET` is emitted. Good.
- Rating filter: replaced with `m.Rating != null && m.Rating.ToLower() == ratingLower`.
  EF Core 6 translates `ToLower()` to SQL; LIKE wildcard risk removed.

**Remaining Gap**
The tag-search path still materialises the entire scalar-filtered set into memory
(`await query.OrderByDescending(...).ToListAsync(cancellationToken)`) with no upper
bound. Round-2 explicitly recommended an explicit `.Take(5_000)` (or configurable)
cap plus a warning log when the cap is reached. Acceptable for today's library size
but a silent scaling hazard.

**Notes**
A `Contains` on a denormalised tag column (or an FTS index) would eliminate the
materialisation entirely. Deferring that is defensible, but the unbounded
materialisation should at minimum be documented in the phase and bounded in code.

---

### Finding 4: 3.B - Captive `HttpClient`

**Previous Severity:** High (P1)
**Current Status:** Fixed
**Plan Reference:** [PHASE_3.md](PHASE_3.md) Step 3.1
**Files Reviewed:** [Program.cs](../../../BlazorWebApp/Program.cs#L96-L99)

**Original Concern**
`AddHttpClient<DanbooruLibraryService>(...)` followed by
`AddSingleton<IDanbooruLibraryService>(sp => sp.GetRequiredService<DanbooruLibraryService>())`
froze a transient handler at singleton lifetime.

**Validation Result**
Now:
```csharp
builder.Services.AddHttpClient<IDanbooruLibraryService, DanbooruLibraryService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});
```
This registers the typed client with the interface directly, using the correct
`HttpMessageHandler` lifecycle. The singleton shim is gone.

**Remaining Gap**
_None._

---

### Finding 5: 2.C - `UpdateAsync` drops scalar changes

**Previous Severity:** Medium (P1)
**Current Status:** Fixed
**Plan Reference:** [PHASE_2.md](PHASE_2.md)
**Files Reviewed:** [SavedDanbooruMediaRepository.cs](../../../BlazorWebApp/Data/Repositories/SavedDanbooruMediaRepository.cs#L97-L104)

**Original Concern**
`Attach` + `IsModified(TagsBundle)` only persisted the JSON column; scalar edits
were silently dropped.

**Validation Result**
Now `context.Update(entity)` followed by
`context.Entry(entity).Property(e => e.TagsBundle).IsModified = true;`.
`Update` marks all scalars modified, and the explicit flag ensures the JSON
converter re-serializes in-place mutations of `TagsBundle` - matching the
[Persistence conventions checklist](../../../.github/copilot-instructions.md).

**Remaining Gap**
_None._

---

### Finding 6: `WriteLibraryTags` parity with `WriteDanbooruTags`

**Previous Severity:** Medium (P1)
**Current Status:** Fixed
**Plan Reference:** [PHASE_5.md](PHASE_5.md) Step 5.2
**Files Reviewed:** [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L437-L462)

**Original Concern**
`WriteLibraryTags` dropped `OnSendTags.InvokeAsync` and `Settings.SaveSettings()`.

**Validation Result**
Handler now mutates the positive-prompt fragment identically to
`WriteDanbooruTags`, invokes `OnSendTags` when wired, and calls
`Settings.SaveSettings()`. Shared helper extraction is still advisable for DRY
but is not required by the plan.

**Remaining Gap**
_Minor._ Both handlers raise `OnSendTags`, but `Danbooru.razor` is a top-level
`@page` so `OnSendTags` has no parent to assign it - the callback is effectively
dead regardless. Pre-existing; parity is achieved.

---

### Finding 7: Rating dropdown "none" option

**Previous Severity:** Low (P1)
**Current Status:** Fixed
**Plan Reference:** [PHASE_5.md](PHASE_5.md) Step 5.2
**Files Reviewed:** [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L86-L94)

**Validation Result**
The dropdown now lists only `g/s/q/e`. `ResetValue=null` + `Clearable` give users
an explicit "no rating filter" state. Good.

**Remaining Gap**
_None._

---

### Finding 8: `_libraryMinScore` typing

**Previous Severity:** Low (P1)
**Current Status:** Not Fixed
**Plan Reference:** [PHASE_5.md](PHASE_5.md) Step 5.2
**Files Reviewed:** [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L180, #L412)

**Original Concern**
`int` default 0 forces "no filter" to coincide with score = 0. Should be `int?`.

**Validation Result**
Field remains `private int _libraryMinScore;`. `LoadLibrary` compensates with
`_libraryMinScore > 0 ? _libraryMinScore : (int?)null`, which means the user
cannot filter "score >= 0" explicitly. Functional work-around, not a correct fix.
Also, negative values (MudTextField allows them) silently disable the filter.

**Remaining Gap**
Change to `int?`, bind with `T="int?"`, and update `LoadLibrary` to pass the
nullable value directly.

---

### Finding 9: Library pagination / infinite scroll

**Previous Severity:** Medium (P1)
**Current Status:** Not Fixed
**Plan Reference:** [PHASE_5.md](PHASE_5.md) Step 5.2
**Files Reviewed:** [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L458)

**Original Concern**
Library page hard-coded to `Take = 100`. No `CountAsync` in repository, no
infinite scroll / "load more" UI. Users with >100 saved items silently see only
the most recent 100.

**Validation Result**
Still `Take = 100` with no user-visible indicator of truncation. No `CountAsync`
was added to [ISavedDanbooruMediaRepository](../../../BlazorWebApp/Data/Repositories/ISavedDanbooruMediaRepository.cs).

**Remaining Gap**
Either wire the infinite-scroll module already used by the Search tab to the
Library grid, or document the scope reduction in the [MAIN_PLAN](MAIN_PLAN.md)
changelog and show a "showing first N results" hint in the UI.

---

### Finding 10: 3.C - Orphan file on DB insert failure

**Previous Severity:** Medium (P2)
**Current Status:** Not Fixed
**Plan Reference:** [PHASE_3.md](PHASE_3.md)
**Files Reviewed:** [DanbooruLibraryService.cs](../../../BlazorWebApp/Services/DanbooruLibraryService.cs#L58-L93)

**Original Concern**
If `_repository.AddAsync` throws after the file is written, the file is
orphaned on disk and no corresponding row exists; next save attempt can overwrite
the orphan if the same post is retried but the path is deterministic per post id,
so this is only a disk-leak bug, not a data-safety bug.

**Validation Result**
Unchanged. The outer `try/catch` logs and returns `Error`, but does not unlink
`fullPath` on failure.

**Remaining Gap**
Wrap the DB insert in an inner try and `File.Delete(fullPath)` on exception
before rethrowing.

---

### Finding 11: 3.D - Full in-memory buffer for large videos

**Previous Severity:** Medium (P2)
**Current Status:** Not Fixed
**Plan Reference:** [PHASE_3.md](PHASE_3.md)
**Files Reviewed:** [DanbooruLibraryService.cs](../../../BlazorWebApp/Services/DanbooruLibraryService.cs#L79-L81)

**Original Concern**
`GetByteArrayAsync` + `WriteAllBytesAsync` buffers the full payload in memory.
A 100 MB webm temporarily doubles server RAM.

**Validation Result**
Unchanged.

**Remaining Gap**
Use `GetStreamAsync` + `File.Create(...)` + `CopyToAsync`, guarded by a
`using` to avoid handle leaks.

---

### Finding 12: Startup log for unconfigured `SavedMediaPath`

**Previous Severity:** Low (P2)
**Current Status:** Not Fixed
**Plan Reference:** [PHASE_3.md](PHASE_3.md)
**Files Reviewed:** [Program.cs](../../../BlazorWebApp/Program.cs#L207-L216)

**Original Concern**
If `SavedMediaPath` is empty or missing, the static file endpoint silently
skips registration, and the runtime error only surfaces when a user tries to
view an item.

**Validation Result**
The `if (!string.IsNullOrWhiteSpace(...) && Directory.Exists(...))` guard is
still silent on the negative branch.

**Remaining Gap**
Emit `logger.LogWarning` (or `LogError`) describing which case failed
(missing path vs. non-existent directory) so operators can see it at startup.

---

### Finding 13: Save button not disabled during in-flight save

**Previous Severity:** Low (P2)
**Current Status:** Not Fixed
**Plan Reference:** [PHASE_5.md](PHASE_5.md)
**Files Reviewed:** [DanbooruImageCard.razor](../../../BlazorWebApp/Components/Resources/DanbooruImageCard.razor#L46-L49, #L133)

**Original Concern**
User can double-click the bookmark and trigger two `SaveAsync` calls. The second
is swallowed by `ExistsByPostIdAsync`, but the first might still be downloading.

**Validation Result**
The diff adds the bookmark button + `OnSave` callback but no `_isSaving` flag,
no `disabled` binding, and no `StateHasChanged` gating.

**Remaining Gap**
Add a private `_isSaving` bool, toggle it around `HandleSave`, and bind
`disabled="@_isSaving"` on the bookmark button. Or debounce at the page level.

---

### Finding 14: Whitespace-only reformatting in unrelated hunks

**Previous Severity:** Low (P2)
**Current Status:** Not Fixed
**Plan Reference:** Repository hygiene
**Files Reviewed:** [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L107-L108) and [DanbooruImageCard.razor](../../../BlazorWebApp/Components/Resources/DanbooruImageCard.razor)

**Validation Result**
The multi-hundred-space indentation on the `SavedDanbooruMediaCard` callback
attributes persists. The `DanbooruImageCard.razor` change also absorbed several
whitespace-only reflows (multi-line `<video>` / `<img>` collapsed to single lines,
unrelated `title=` reformatting).

**Remaining Gap**
Revert the whitespace-only hunks to keep this change set reviewable.

---

### Finding 15: 2.A / 5.A / 5.C / 5.D / 5.E (already fixed round 2)

No regression observed for any of the round-2 fixes:
- `DanbooruTagBundleConverter` is registered in `OnModelCreating` via
  `new Converters.DanbooruTagBundleConverter()`.
- [SavedDanbooruMediaCard.razor.css](../../../BlazorWebApp/Components/Resources/SavedDanbooruMediaCard.razor.css) still present.
- Delete dialog still respects the "Delete file from disk?" checkbox via
  `result.Data is bool`.
- Library viewer still resolves `/files/danbooru/{FilePath}`.
- Lazy-load bootstrap now works end-to-end (see Finding 2).

---

## New Findings

### New Finding 1: `OnMediaSaved` bypasses active library filters

**Severity:** Medium
**Files:** [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L205-L213)

**Problem**
When a media item is saved from the Search tab while the Library tab has an
active filter (rating, min score, video-only, or tag search), the handler
unconditionally inserts the new item at `_libraryItems[0]`:

```csharp
private void OnMediaSaved(DanbooruMediaSavedEventArgs e)
{
    _ = InvokeAsync(() =>
    {
        _libraryItems.Insert(0, e.Media);
        StateHasChanged();
    });
}
```

If the current filter would exclude the item (e.g., user is filtering to
`Rating = "e"` but the saved item is rated `g`), the item appears anyway until
the user clicks Filter or reloads. This is a silent UX inconsistency and can
also mislead the user ("why is a General post showing up in my Explicit filter?").

**Why This Matters**
It violates the invariant that the grid reflects the active filter set. It
also makes the next paginated `LoadLibrary` call potentially drop this item,
creating a race where insert/delete flicker on repeated saves.

**Recommended Follow-Up Change**
Re-apply the in-memory filter predicate before inserting, or - simpler - trigger
a targeted reload instead of mutating the list:

```csharp
private void OnMediaSaved(DanbooruMediaSavedEventArgs e)
{
    if (!_libraryLoaded) return; // list is not yet populated; nothing to update

    _ = InvokeAsync(async () =>
    {
        if (MatchesActiveFilter(e.Media))
        {
            _libraryItems.Insert(0, e.Media);
            StateHasChanged();
        }
    });
}

private bool MatchesActiveFilter(SavedDanbooruMedia m)
{
    if (!string.IsNullOrWhiteSpace(_libraryRatingFilter)
        && !string.Equals(m.Rating, _libraryRatingFilter, StringComparison.OrdinalIgnoreCase))
        return false;
    if (_libraryMinScore > 0 && m.Score < _libraryMinScore) return false;
    if (_libraryVideoOnly && !m.IsVideo) return false;
    if (!string.IsNullOrWhiteSpace(_libraryTagSearch))
    {
        var needle = _libraryTagSearch.Trim();
        if (!m.TagsBundle.General.Concat(m.TagsBundle.Artist)
            .Concat(m.TagsBundle.Character).Concat(m.TagsBundle.Copyright)
            .Concat(m.TagsBundle.Meta)
            .Any(t => t.Contains(needle, StringComparison.OrdinalIgnoreCase)))
            return false;
    }
    return true;
}
```

The early return on `!_libraryLoaded` also prevents quietly growing
`_libraryItems` with events that happened before the tab was ever opened (the
list is replaced wholesale on first load, so the growth is harmless but
wasteful).

---

### New Finding 2: Delete dialog still uses obsolete `DialogResult.Cancelled`

**Severity:** Low
**Files:** [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L489)

**Problem**
```csharp
if (result is not null && !result.Cancelled)
```
`DialogResult.Cancelled` is obsolete in the current MudBlazor version (compile
warning `CS0618: 'DialogResult.Cancelled' is obsolete: 'Use Canceled instead'`).

**Why This Matters**
Warning noise today; breakage on the next MudBlazor major. Other call sites in
the codebase likely already use `.Canceled` - this one was copied from an
older snippet.

**Recommended Follow-Up Change**
Replace `.Cancelled` with `.Canceled`.

---

### New Finding 3: Tag-search path still materialises the full scalar-filtered table

**Severity:** Medium
**Files:** [SavedDanbooruMediaRepository.cs](../../../BlazorWebApp/Data/Repositories/SavedDanbooruMediaRepository.cs#L81-L95)

**Problem**
When `TagSearch` is non-empty, the repository runs
`await query.OrderByDescending(...).ToListAsync(cancellationToken)` **without** a
`Take` ceiling, then filters and pages in memory. For the target scale this is
acceptable, but there is no safety rail - a year from now a 50k-row library will
happily be fully loaded on every tag query.

**Why This Matters**
Silent memory / latency regression as the library grows. Round-2 explicitly
called out adding a hard cap.

**Recommended Follow-Up Change**
Add a cap + warn log:
```csharp
const int TagSearchScanLimit = 5000;
var scanned = await query
    .OrderByDescending(m => m.DateCreated)
    .Take(TagSearchScanLimit + 1)
    .ToListAsync(cancellationToken);

if (scanned.Count > TagSearchScanLimit)
{
    // log + drop the sentinel extra row
}
```
Longer term, denormalise tags into a searchable column or add SQLite FTS.

---

### New Finding 4: `OnSendTags` on `Danbooru.razor` is a dead callback

**Severity:** Low
**Files:** [Danbooru.razor](../../../BlazorWebApp/Pages/Danbooru.razor#L142, #L451-L455)

**Problem**
`Danbooru.razor` is routed via `@page "/danbooru"`, so nothing instantiates it
as a child component. Its `[Parameter] public EventCallback<(string, bool)> OnSendTags`
is never assigned. Both `WriteDanbooruTags` and the new `WriteLibraryTags` raise
it conditionally, so nothing observably breaks - but the "Send to Img2Img" affordance
in the Library row silently does nothing beyond the shared GenerationParameters
write, which is indistinguishable from "Copy Tags".

**Why This Matters**
The UI exposes two buttons with identical effective behaviour. Users will
eventually file a bug about the duplicate.

**Recommended Follow-Up Change**
Either:

1. Resolve `IImageSendToService` (already in DI) in the page and invoke the
   Img2Img routing directly when `isImg2Img` is true; or
2. Collapse the two library buttons into one "Copy Tags" action and drop the
   dead `OnSendTags` parameter.

---

### New Finding 5: `DanbooruImageCard.razor` reformatting is out of plan scope

**Severity:** Low
**Files:** [DanbooruImageCard.razor](../../../BlazorWebApp/Components/Resources/DanbooruImageCard.razor)

**Problem**
The diff collapses multi-line `<video>` / `<img>` declarations and reformats
several `title="..."` attributes alongside the intentional "add Save button"
change. This is a recurring pattern in the Danbooru-library change set and makes
focused review harder.

**Why This Matters**
Blurs the meaningful delta (new `OnSave` parameter, new button) with noise.

**Recommended Follow-Up Change**
Revert whitespace-only hunks; keep only the parameter, markup, and handler
additions.

---

### New Finding 6: Credentials checked into `appsettings.json` (out-of-plan, but worth flagging)

**Severity:** Medium (security hygiene, pre-existing)
**Files:** [appsettings.json](../../../BlazorWebApp/appsettings.json)

**Problem**
The `Danbooru.Login` / `Danbooru.ApiKey` values are committed to the repo with
what appear to be real credentials (`Nysalie` / hex API key). The
`DanbooruService.cs` refactor to `IOptions<DanbooruOptions>` is fine, but it
highlights the pre-existing secrets leak rather than moving them to user secrets
or environment variables.

**Why This Matters**
Secrets in source control. If this repo is (or becomes) public or shared,
those credentials are exposed.

**Recommended Follow-Up Change**
Move `Danbooru.Login` / `Danbooru.ApiKey` to `dotnet user-secrets` for dev and
env vars / secret store for prod. Rotate the exposed key. Keep only
`SavedMediaPath` and non-sensitive keys in `appsettings.json`.

---

## Coverage Snapshot

- **Fully fixed from prior report:**
  - Finding 1 (5.B - EventService subscription)
  - Finding 2 (R2-2 - render-safe lazy load)
  - Finding 4 (3.B - captive `HttpClient`)
  - Finding 5 (2.C - `UpdateAsync` scalar persistence)
  - Finding 6 (`WriteLibraryTags` parity)
  - Finding 7 ("none" rating option)
  - Finding 15 (2.A / 5.A / 5.C / 5.D / 5.E - all round-2 fixes still intact)
- **Partially fixed:**
  - Finding 3 (R2-1 - SQL pagination fixed, but tag-path materialisation still
    unbounded; see also New Finding 3)
- **Still open from prior report:**
  - Finding 8 (`_libraryMinScore` typing)
  - Finding 9 (library pagination / infinite scroll / `CountAsync`)
  - Finding 10 (3.C - orphan file on DB failure)
  - Finding 11 (3.D - streaming download)
  - Finding 12 (startup log for unconfigured `SavedMediaPath`)
  - Finding 13 (save button in-flight state)
  - Finding 14 (whitespace-only reformatting)
- **New issues discovered:**
  - New Finding 1 (`OnMediaSaved` ignores active filters)
  - New Finding 2 (obsolete `DialogResult.Cancelled`)
  - New Finding 3 (unbounded tag-search materialisation)
  - New Finding 4 (dead `OnSendTags` callback)
  - New Finding 5 (whitespace reformatting in `DanbooruImageCard.razor`)
  - New Finding 6 (real credentials in `appsettings.json` - pre-existing, surfaced by the `IOptions` migration)
- **Original report items now believed unnecessary or superseded:** _None._

## Recommended Next Steps

1. Filter `OnMediaSaved` insertions by the active library filter (New Finding 1)
   to prevent stale items appearing outside the current view.
2. Replace `DialogResult.Cancelled` with `Canceled` (New Finding 2) - 1-line fix,
   clears a compile warning.
3. Bound the tag-search materialisation with a `Take(n)` cap and log when the cap
   is hit (Finding 3 partial + New Finding 3).
4. Change `_libraryMinScore` to `int?` and bind `T="int?"` (Finding 8). Verify
   the MudTextField renders a clearable empty state.
5. Add library pagination (infinite scroll or "load more") + `ISavedDanbooruMediaRepository.CountAsync`
   (Finding 9). If descoping, update [MAIN_PLAN.md](MAIN_PLAN.md) and show a
   "first N results" hint in the empty-filter UI.
6. Clean up the file-write path: wrap `AddAsync` in an inner try/catch and
   `File.Delete(fullPath)` on exception (Finding 10); switch to
   `GetStreamAsync` + `CopyToAsync` (Finding 11).
7. Log a warning at startup when `SavedMediaPath` is empty or the directory does
   not exist (Finding 12).
8. Add an in-flight flag to the Save button in `DanbooruImageCard` (Finding 13).
9. Resolve the dead `OnSendTags` parameter: wire `IImageSendToService` or
   remove the duplicate button (New Finding 4).
10. Revert whitespace-only hunks in `Danbooru.razor` and `DanbooruImageCard.razor`
    (Finding 14 + New Finding 5).
11. Move Danbooru credentials out of `appsettings.json` into user secrets / env
    vars and rotate the exposed API key (New Finding 6).
12. Add unit tests for `ResolveRatingFolder`, `ResolveScoreBucket`, the
    `SavedDanbooruMediaFilter` translation (both paths in `GetPagedAsync`),
    and duplicate-save behaviour (`AlreadySaved` result) - none exist yet under
    [BlazorWebApp.Tests/](../../../BlazorWebApp.Tests).
