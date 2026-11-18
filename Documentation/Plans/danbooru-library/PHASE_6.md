# Phase 6: Static File Serving

> **Main plan:** [MAIN_PLAN.md](./MAIN_PLAN.md)
> **Status:** [ ] Not Started
> **Complexity:** 2 points
> **Depends on:** `DanbooruOptions.SavedMediaPath` (Phase 1)
> **Unblocks:** The Library UI (Phase 5) renders images and videos from `/files/danbooru/{RelativePath}`. The app will function end-to-end only once this phase is also in.

---

## 1. Objective

Map the physical directory pointed to by `DanbooruOptions.SavedMediaPath` to the HTTP request path `/files/danbooru` via `app.UseStaticFiles`, following the exact pattern used for the other media roots (`OutputDir`, `ResourcesPath`, `ResourcePreviewsPath`). Guard against unset / missing paths so startup never throws and emits a clear warning when the mapping is skipped.

---

## 2. Context & Background

`BlazorWebApp/Program.cs` already declares three `UseStaticFiles` blocks (lines ~177-190) that wrap `PhysicalFileProvider(builder.Configuration["<key>"])`. The Danbooru library mapping sits naturally at the end of that sequence, reading from the bound `DanbooruOptions` instead of indexing configuration directly (aligned with Phase 1).

Inherited convention (verbatim from `MAIN_PLAN.md`):

- "Static serving via `app.UseStaticFiles` + `PhysicalFileProvider` at `/files/danbooru`. Consistent with existing `OutputDir`, `ResourcesPath`, `ResourcePreviewsPath` mappings in `Program.cs`."
- "Static file provider exposes parent directories. Use a dedicated `SavedMediaPath` root; never share with `OutputDir`."

---

## 3. Prerequisites

- **Artifacts from prior phases:**
  - `DanbooruOptions.SavedMediaPath` (Phase 1)
  - Existing saved files under the configured path, produced by Phase 3 / 4 saves
- **Files the executor must read before writing code:**
  - `BlazorWebApp/Program.cs` (lines ~170-195) - existing `UseStaticFiles` block to replicate
- **External references:** `Microsoft.Extensions.FileProviders.PhysicalFileProvider` constructor (throws on nonexistent path in .NET 6 - must guard).

---

## 4. Files Inventory

### To Create

_None._

### To Modify

| Path                      | Change                                                                                                       |
| ------------------------- | ------------------------------------------------------------------------------------------------------------ |
| `BlazorWebApp/Program.cs` | Add a fourth `UseStaticFiles(new StaticFileOptions { ... })` mapping for Danbooru, guarded by path existence |

### To Leave Untouched (but referenced)

| Path                                              | Why it matters                                                                                                   |
| ------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| `BlazorWebApp/Services/DanbooruLibraryService.cs` | `RelativePath` is already normalized with forward slashes, so URL assembly in cards works without helper changes |

---

## 5. Step-by-Step Execution

### Step 6.1: Add the static file mapping

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] In `BlazorWebApp/Program.cs`, near the existing `UseStaticFiles` block for `ResourcePreviewsPath`, resolve `DanbooruOptions` from the container (`app.Services.GetRequiredService<IOptions<DanbooruOptions>>().Value`) or reuse the `danbooruOpts` local variable introduced by Phase 3 Step 3.3.
- [ ] Only call `UseStaticFiles` when `!string.IsNullOrWhiteSpace(danbooruOpts.SavedMediaPath) && Directory.Exists(danbooruOpts.SavedMediaPath)`.
- [ ] When skipping, log a warning with the reason.

#### Implementation Notes

- `PhysicalFileProvider` throws `DirectoryNotFoundException` if the path does not exist; the `Directory.Exists` guard avoids this at startup.
- `RequestPath` must start with a single slash: `"/files/danbooru"` (mirrors the two other `/files/...` mappings).
- Order matters only for precedence on overlapping request paths; no overlap with other mappings here.

#### Code Sketch

```csharp
// BlazorWebApp/Program.cs (insert after the ResourcePreviewsPath mapping)
using BlazorWebApp.Models; // if not already imported

// ...existing mappings...
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(builder.Configuration["ResourcePreviewsPath"]),
    RequestPath = "/files/resource_previews"
});

// Danbooru library (Phase 6)
var danbooruOpts = app.Services.GetRequiredService<IOptions<DanbooruOptions>>().Value;
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
if (!string.IsNullOrWhiteSpace(danbooruOpts.SavedMediaPath) && Directory.Exists(danbooruOpts.SavedMediaPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(danbooruOpts.SavedMediaPath),
        RequestPath = "/files/danbooru"
    });
}
else
{
    startupLogger.LogWarning(
        "Danbooru:SavedMediaPath '{Path}' is empty or missing; /files/danbooru static mapping disabled.",
        danbooruOpts.SavedMediaPath);
}
```

If Phase 3 Step 3.3 already introduced `danbooruOpts` / `startupLogger` locals, reuse them instead of re-resolving.

#### Conventions to Respect

- Reuse the existing `using Microsoft.Extensions.FileProviders;` already at the top of `Program.cs`.
- Do not read `builder.Configuration["Danbooru:SavedMediaPath"]` - always go through `IOptions<DanbooruOptions>` so the section-name constant stays the single source of truth.

#### Validation

- Start the app with a valid `SavedMediaPath` - no warning, requests to `/files/danbooru/<rating>/<bucket>/<id>.png` succeed.
- Start the app with an empty or missing path - warning logged, app still starts, Library tab shows broken images (expected; Phase 7 may refine).

#### Changes Made

_To be filled in after the step is implemented._

---

### Step 6.2: Verify card rendering

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] Confirm `SavedDanbooruMediaCard.razor`'s `MediaUrl` resolves to a fetchable URL on a running instance.
- [ ] Spot-check a saved mp4/webm video - ensures correct MIME is served by the static middleware (`FileExtensionContentTypeProvider` defaults include these).
- [ ] Confirm `RelativePath` stored in DB uses forward slashes (Phase 3 enforces this) and that the URL builder does not accidentally URL-encode them.

#### Implementation Notes

- If a custom content type is missing for an exotic extension (e.g., `.avif` on some environments), extend `StaticFileOptions.ContentTypeProvider` with a configured `FileExtensionContentTypeProvider`. Do not add unless a specific extension is confirmed failing - the default provider already covers png/jpg/gif/webp/mp4/webm/avif on modern .NET.
- Browser console network tab is the fastest way to diagnose 404 or 415 on a specific item.

#### Validation

- Images render without browser console errors.
- Videos load their poster frame and play on interaction (hover play is Phase 7).

#### Changes Made

_To be filled in after the step is implemented._

---

## 6. Integration Points

- **DI registrations:** None added.
- **Events to publish / subscribe:** None.
- **Configuration bindings:** Consumes `DanbooruOptions.SavedMediaPath` (Phase 1).
- **Startup side-effects:**
  - Adds a fourth `UseStaticFiles` middleware entry when the path is valid.
  - Emits a warning log otherwise.

---

## 7. Testing Strategy

- **Automated tests to add/update:** Not applicable - pipeline wiring.
- **Manual verification checklist:**
  1. Browser request to `/files/danbooru/general/score_0/<id>.png` returns 200 + the image.
  2. Library tab renders all previously saved items once Phase 5 is in.
  3. Set `SavedMediaPath` to an empty string in `appsettings.Development.json`, restart - warning appears, app still boots, no crash when navigating to Library.
- **Regression watch-list:** Other `/files/...` and `/image` mappings continue to serve their respective content.

---

## 8. Stress Points Specific to This Phase

| Risk                                                                   | Mitigation                                                                                                                                |
| ---------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| `PhysicalFileProvider` throws on non-existent directory                | `Directory.Exists` guard; log warning and skip.                                                                                           |
| Static file provider exposes parent directories                        | Dedicated root; never nest `SavedMediaPath` inside `OutputDir`. Document the expectation in the warning message when misconfigured.       |
| MIME type missing for obscure extensions                               | Default provider covers common cases; add `FileExtensionContentTypeProvider` only if a specific ext is confirmed failing.                 |
| Case-sensitive filesystems (Linux) expose different paths than Windows | `RelativePath` uses forward slashes and preserves case as authored; no action required on Windows; document for future Linux deployments. |

---

## 9. Resolved Assumptions

- **Path existence guard:** `Directory.Exists` before `new PhysicalFileProvider(...)`. `PhysicalFileProvider` will otherwise throw at middleware registration time.
- **`IOptions<DanbooruOptions>` over raw `IConfiguration` indexing:** Keeps the section binding consistent with Phase 1.
- **Fourth block placement:** After `ResourcePreviewsPath`, before `UseRouting`. Matches the existing ordering convention.

---

## 10. Open Clarifications

_None - phase is fully specified._

---

## 11. Progress Tracking

| Step | Status | Complexity | Notes |
| ---- | ------ | ---------- | ----- |
| 6.1  | [ ]    | 1          |       |
| 6.2  | [ ]    | 1          |       |

---

## 12. Issues & Resolutions

_Populated during execution._

---

## 13. Commit Checkpoints

- [ ] Step 6.1 complete
- [ ] Step 6.2 complete
- [ ] Phase build green

---

## 14. Phase Summary

_To be filled in after the phase is complete._

---

## 15. Cross-References

- Main plan section: [Phase 6: Static File Serving](./MAIN_PLAN.md)
- Prior phase: [PHASE_5.md](./PHASE_5.md)
- Next phase: [PHASE_7.md](./PHASE_7.md)
- Related code: `BlazorWebApp/Program.cs` (existing `UseStaticFiles` mappings)
