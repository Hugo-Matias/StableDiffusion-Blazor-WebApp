# Phase 7: Polish & Edge Cases

> **Main plan:** [MAIN_PLAN.md](./MAIN_PLAN.md)
> **Status:** [ ] Not Started
> **Complexity:** 3 points
> **Depends on:** Phases 1-6 in place and working end-to-end.
> **Unblocks:** Documentation Phase (post-plan).

---

## 1. Objective

Close the loop on UX and performance details discovered during Phases 4-6: play videos on hover inside Library cards, harden `SaveAsync` against posts with missing URLs and oversized videos, and confirm the Library grid scales to several hundred rows without regressions. This phase introduces no new entities, services, or events - only targeted fixes and micro-optimizations on top of the existing surface.

---

## 2. Context & Background

The Search tab already uses `BlazorWebApp/wwwroot/js/VideoCard.js` via `DanbooruImageCard.razor` to play/pause a video on hover. `SavedDanbooruMediaCard` (Phase 5 Step 5.1) intentionally left hover playback deferred to this phase; the JS module is reusable.

Inherited conventions (verbatim from `MAIN_PLAN.md`):

- "Video playback on hover in Library cards (mirror `DanbooruImageCard` behavior)."
- "Handle posts with missing `Url` (rare on Danbooru) and oversized videos: show non-blocking error and skip."
- "Large-library performance pass: ensure queries use the unique index, paginate server-side, load previews lazily."

Phase 3 (`DanbooruLibraryService.SaveAsync`) already returns `DanbooruSaveStatus.Failed` with an error message when `Url` is missing, so Phase 7 work on that front is about UX polish (clearer toast copy, skip silently in bulk flows if any emerge).

---

## 3. Prerequisites

- **Artifacts from prior phases:**
  - `BlazorWebApp/Components/Resources/SavedDanbooruMediaCard.razor` (Phase 5)
  - `BlazorWebApp/Services/DanbooruLibraryService.cs` (Phase 3)
  - `BlazorWebApp/Data/Repositories/SavedDanbooruMediaRepository.cs` (Phase 2)
- **Files the executor must read before writing code:**
  - `BlazorWebApp/Components/Resources/DanbooruImageCard.razor` - existing hover play/pause wiring via `VideoCard.js`
  - `BlazorWebApp/wwwroot/js/VideoCard.js` - `playVideo` / `pauseVideo` JS API
  - `BlazorWebApp/wwwroot/js/DanbooruMasonry.js` - for any scroll threshold / lazy-load knobs touched in Step 7.3
- **External references:** None.

---

## 4. Files Inventory

### To Create

_None._

### To Modify

| Path                                                             | Change                                                                                                                                      |
| ---------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| `BlazorWebApp/Components/Resources/SavedDanbooruMediaCard.razor` | Mirror `DanbooruImageCard`'s video hover wiring (`VideoCard.js`, `_videoId`, play/pause on mouse enter/leave, implement `IAsyncDisposable`) |
| `BlazorWebApp/Services/DanbooruLibraryService.cs`                | Tighten error messages and optional max-file-size guard (configurable or const)                                                             |
| `BlazorWebApp/Data/Repositories/SavedDanbooruMediaRepository.cs` | Ensure `GetPagedAsync` uses the unique index; add an extra scalar index if profiling shows need (e.g., `Score`, `SavedAt`)                  |

### To Leave Untouched (but referenced)

| Path                                | Why it matters                                                                 |
| ----------------------------------- | ------------------------------------------------------------------------------ |
| `BlazorWebApp/Pages/Danbooru.razor` | No structural change to the Library tab; only behavioural through updated card |

---

## 5. Step-by-Step Execution

### Step 7.1: Video hover playback on Library cards

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] Add `@inject IJSRuntime JS` and `@implements IAsyncDisposable` to `SavedDanbooruMediaCard.razor`.
- [ ] Introduce `_videoId = $"library-video-{Guid.NewGuid():N}"` and bind `id="@_videoId"` on the `<video>` element.
- [ ] In `OnAfterRenderAsync(firstRender)`, when `Media.IsVideo && firstRender`, import `./js/VideoCard.js` into `_jsModule`.
- [ ] On mouse enter / leave, call `playVideo` / `pauseVideo` on the module using `_videoId`.
- [ ] Dispose `_jsModule` in `DisposeAsync`.

#### Code Sketch

```razor
@inject IJSRuntime JS
@implements IAsyncDisposable

@* ...existing markup, with: *@
<video id="@_videoId" src="@MediaUrl" class="card-media" muted loop playsinline preload="metadata" />

@code {
    private IJSObjectReference? _jsModule;
    private string _videoId = $"library-video-{Guid.NewGuid():N}";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && Media.IsVideo)
            _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "./js/VideoCard.js");
    }

    private async Task HandleMouseEnter()
    {
        _isHovered = true;
        if (Media.IsVideo && _jsModule != null)
            try { await _jsModule.InvokeVoidAsync("playVideo", _videoId); } catch { }
    }

    private async Task HandleMouseLeave()
    {
        _isHovered = false;
        if (Media.IsVideo && _jsModule != null)
            try { await _jsModule.InvokeVoidAsync("pauseVideo", _videoId); } catch { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_jsModule != null)
        {
            try { await _jsModule.DisposeAsync(); } catch { }
        }
    }
}
```

Replace the inline `@onmouseenter` / `@onmouseleave` lambdas from Phase 5 with `HandleMouseEnter` / `HandleMouseLeave`.

#### Validation

- Hover on a saved video in Library: plays silently; leaving pauses.
- Hover on an image: no JS invocation (module never imported).
- Navigate away: disposal runs without `JSDisconnectedException` escaping.

---

### Step 7.2: Missing-URL and large-video handling

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] In `DanbooruLibraryService.SaveAsync`, refine the "Post has no downloadable URL" error copy and return early (already behaviourally correct from Phase 3).
- [ ] Introduce a const `MaxDownloadBytes` (e.g., 200 MB) or an option on `DanbooruOptions` (see Open Clarifications). After reading response headers, inspect `response.Content.Headers.ContentLength` and return `DanbooruSaveStatus.Failed` with a user-facing message if the size exceeds the limit, without opening the output file.
- [ ] Keep the `.tmp` atomic write pattern from Phase 3 in place.
- [ ] The Save flow in `Pages/Danbooru.razor` (Phase 4) already surfaces `result.ErrorMessage` in a red snackbar - no UI change required.

#### Code Sketch

```csharp
// DanbooruLibraryService.SaveAsync (excerpt inside the download block)
using var response = await http.GetAsync(post.Url, HttpCompletionOption.ResponseHeadersRead, ct);
response.EnsureSuccessStatusCode();

var size = response.Content.Headers.ContentLength;
if (size is long bytes && bytes > MaxDownloadBytes)
{
    return new(DanbooruSaveStatus.Failed, null,
        $"File too large to save ({bytes / (1024 * 1024)} MB exceeds {MaxDownloadBytes / (1024 * 1024)} MB limit).");
}

await using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
{
    await response.Content.CopyToAsync(fs, ct);
}
```

Declaration:

```csharp
private const long MaxDownloadBytes = 200L * 1024 * 1024; // 200 MB
```

#### Validation

- Save a post where `file_url` is empty / null - error toast "Post has no downloadable URL.", no file written.
- Attempt to save an artificially oversized item - error toast with size explanation, no partial file left behind.

---

### Step 7.3: Large-library performance pass

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] Review `SavedDanbooruMediaRepository.GetPagedAsync` EF-translated SQL (log once with `EnableSensitiveDataLogging` already on). Confirm:
  - Filter by `Rating`, `Score`, `IsVideo`, `Favorite` -> scans with `ORDER BY SavedAt DESC LIMIT ...` (acceptable for a few thousand rows).
  - Tag substring -> `WHERE TagsBundle LIKE '%...%'` (full scan; acceptable v1).
- [ ] Ensure lazy image loading: verify `loading="lazy"` on the card `<img>` (already set in Phase 5). For `<video>`, keep `preload="metadata"`.
- [ ] Consider adding a non-unique index on `SavedAt` if paging feels slow on > 1000 rows. If added, bundle into a follow-up migration only (not this phase). Document the observation in "Changes Made" rather than shipping an ad-hoc migration here.
- [ ] Confirm `_library` page size (30) keeps DOM node count manageable. Reduce to 24 if masonry layout thrashes.

#### Implementation Notes

- No schema change in this phase unless profiling hard-requires it; a trailing phase / follow-up plan can add a `SavedAt` index with a proper migration (per persistence doc checklist).
- `DanbooruMasonry.js` already lazy-positions children; no JS change anticipated.

#### Validation

- Populate the library with 100+ rows (seed script or manual saves). Scroll through Library tab at 60 fps without jank.
- Filter changes respond in < 200 ms for scalar filters.
- Memory usage on the circuit does not grow unbounded during infinite scroll (test by doing multiple scroll-to-bottom passes).

---

## 6. Integration Points

- **DI registrations:** None new.
- **Events to publish / subscribe:** None new.
- **Configuration bindings:** Optional `MaxDownloadBytes` extension on `DanbooruOptions` (see Open Clarifications). Default is a constant in the service.
- **Startup side-effects:** None.

---

## 7. Testing Strategy

- **Automated tests to add/update:** If a service test harness exists, add a single test for the size-limit path using a mocked `HttpMessageHandler` that returns a large `Content-Length`. Otherwise manual-only.
- **Manual verification checklist:**
  1. Save video post -> hover in Library auto-plays, leave auto-pauses.
  2. Force a download with missing `Url` via DB tampering / debugger -> clear error toast.
  3. Save a real large video from Danbooru (>200 MB rare but possible) -> error toast, no partial file.
  4. Library scroll through 100+ items -> smooth; filters respond quickly.
- **Regression watch-list:** Search tab unchanged; existing card hover still works.

---

## 8. Stress Points Specific to This Phase

| Risk                                                 | Mitigation                                                                                                            |
| ---------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------- |
| Hover handler fires on disposed circuit              | `_jsModule == null` check + swallow exceptions inside play/pause, already the pattern in `DanbooruImageCard`.         |
| `Content-Length` absent for chunked responses        | Treat as unknown size and proceed with download (reverts to post-download size check or accept the risk; v1 accepts). |
| Index-add detours introducing schema drift mid-phase | Do not add a migration in Step 7.3; document as a follow-up plan if profiling requires it.                            |
| JS module double-import                              | `OnAfterRenderAsync(firstRender)` guards import to one per card lifetime.                                             |

---

## 9. Resolved Assumptions

- **`MaxDownloadBytes` implemented as a const in `DanbooruLibraryService`:** Moving it onto `DanbooruOptions` is a strictly-larger change; const is sufficient for v1. If the user later needs environment overrides, promote the const to an option (trivial follow-up).
- **No extra DB index in this phase:** Persistence-doc workflow for hand-authored migrations is heavy enough that it earns its own phase if profiling justifies it.
- **Continue to use `VideoCard.js`:** Same module used by `DanbooruImageCard`; keeps behaviour identical and dispose semantics familiar.

---

## 10. Open Clarifications

- **Step 7.2 - Size limit source:** Const (`200 MB`, in service) vs. option on `DanbooruOptions`. Default: const. Flip to option only if the user asks for run-time tuning.

---

## 11. Progress Tracking

| Step | Status | Complexity | Notes |
| ---- | ------ | ---------- | ----- |
| 7.1  | [ ]    | 1          |       |
| 7.2  | [ ]    | 1          |       |
| 7.3  | [ ]    | 1          |       |

---

## 12. Issues & Resolutions

_Populated during execution._

---

## 13. Commit Checkpoints

- [ ] Step 7.1 complete
- [ ] Step 7.2 complete
- [ ] Step 7.3 complete
- [ ] Phase build green; manual polish checklist passes

---

## 14. Phase Summary

_To be filled in after the phase is complete._

---

## 15. Cross-References

- Main plan section: [Phase 7: Polish & Edge Cases](./MAIN_PLAN.md)
- Prior phase: [PHASE_6.md](./PHASE_6.md)
- Next phase: N/A (final phase before Documentation)
- Related code: `BlazorWebApp/Components/Resources/DanbooruImageCard.razor` (video hover reference)
