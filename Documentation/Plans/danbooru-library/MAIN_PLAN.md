# Danbooru Library - Implementation Plan

## Status

**Current Phase:** Planning (awaiting user approval to begin Phase 1)

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow (per step)

1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update Phase Document**
   - Do NOT proceed to next step until testing is complete
   - User must explicitly approve before updating phase document
   - Build runs only after user requests or after completing all file edits for a step

### Progress Tracking Symbols

- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked/needs discussion

### Complexity Estimation (Fibonacci Points)

- **1** Trivial | **2** Simple | **3** Moderate | **5** Medium | **8** Complex | **13** Very Complex | **21+** Epic

### Key Rules

- Each step = commitable checkpoint
- No time/date references - use complexity points only
- Detours are acceptable after discussion - append to main plan
- Phase documents must contain enough context to resume in new sessions
- Minimal, focused changes - avoid over-engineering
- User permission required before moving to next phase
- All events must flow through `EventService` (pub/sub convention)
- Hand-authored EF migrations must carry both `[DbContext]` and `[Migration]` attributes and update `AppDbContextModelSnapshot.cs`
- JSON-backed entity columns use a `ValueConverter` and require `Entry(e).Property(p).IsModified = true` on in-place mutations

---

## Problem Statement

The `Library` tab on the Danbooru page (`BlazorWebApp/Pages/Danbooru.razor`) is a placeholder. Users need the ability to:

- Save images and videos fetched from the Danbooru Search tab to disk.
- Persist rich metadata from the source post (tags, rating, score, dimensions, URLs) in the database for integration (filtering, sending tags back to Img2Img, opening source, etc.).
- Browse, filter, and manage saved media in the Library tab.

Currently:

- API credentials live as flat keys (`DanbooruLogin`, `DanbooruApiKey`) in `appsettings.json`.
- The Library tab has no persistence, no service, no UI beyond a "Coming soon" message.

---

## Proposed Solution

A new `SavedDanbooruMedia` entity persists the superset of `DanbooruPost` metadata plus local file info. A `DanbooruLibraryService` downloads the original file to a configured `SavedMediaPath` root (organized by rating + score bucket), writes a DB row, and raises events through `EventService`. The Library tab reuses the masonry/infinite-scroll patterns from the Search tab but sources from the local DB. The existing flat `Danbooru*` config keys are consolidated into a strongly-typed `Danbooru` options section.

### Key Decisions

| Decision                                                                                      | Rationale                                                                                                                                                    |
| --------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Folder layout `{rating}/score_{bucket}/{postId}.{ext}` with buckets `0`, `100`, `200`, `300+` | User request: split by rating and score ranges for minimal file-system organization. Matches `ResourcesService` style of hierarchical `Saved/{type}` output. |
| Static serving via `app.UseStaticFiles` + `PhysicalFileProvider` at `/files/danbooru`         | Consistent with existing `OutputDir`, `ResourcesPath`, `ResourcePreviewsPath` mappings in `Program.cs`.                                                      |
| Synchronous download on Save click                                                            | User requested sync. Simplest UX; progress shown via snackbar.                                                                                               |
| Save both image and video original (`file_url`)                                               | User request. Size trade-off accepted.                                                                                                                       |
| Single JSON column for tag bundle                                                             | User request. DB stays simple; client-side filtering fine for expected library size.                                                                         |
| Dedicated `SavedDanbooruMedia` entity (not reuse `Image`)                                     | `Image` carries generation semantics (Project, Sampler, Mode, Selections). Danbooru media is conceptually separate.                                          |
| Dedup by unique index on `DanbooruPostId` -> toast "Already saved"                            | User request. Clear feedback, no silent no-op.                                                                                                               |
| Delete via existing `ConfirmationDialog` pattern with "Delete files from disk?" checkbox      | User request. Mirrors `Pages/Resources.razor` delete flow.                                                                                                   |
| Consolidate `DanbooruLogin` / `DanbooruApiKey` into `Danbooru` section with `SavedMediaPath`  | User request. `DanbooruOptions` bound via `IOptions` and injected into `DanbooruService`.                                                                    |

### Conventions

- Entity name: `SavedDanbooruMedia` (noun pair, consistent with `ResourceImage`, `SchedulerDraft`).
- Repository interface: `ISavedDanbooruMediaRepository`. Service: `IDanbooruLibraryService` / `DanbooruLibraryService`.
- Event args: `DanbooruMediaSavedEventArgs`, `DanbooruMediaDeletedEventArgs` under `BlazorWebApp/Events/`.
- Tag bundle type: `DanbooruTagBundle { List<string> Artist, Character, Copyright, General, Meta }` serialized via `ValueConverter`.
- Static file request path: `/files/danbooru`.
- Score buckets: `< 100` -> `score_0`, `100-199` -> `score_100`, `200-299` -> `score_200`, `>= 300` -> `score_300`.
- Rating folder: `general`, `sensitive`, `questionable`, `explicit`, `none` (full names, lowercase).

---

## Implementation Phases

### Phase 1: Config Consolidation

**Objective:** Replace flat `DanbooruLogin`/`DanbooruApiKey` with a `Danbooru` config section that also holds `SavedMediaPath`; bind via `DanbooruOptions` and refactor `DanbooruService` to use it.
**Complexity:** 3 points
**Status:** [ ] Not Started

#### Steps

- [ ] Step 1.1 - Add `BlazorWebApp/Models/DanbooruOptions.cs` (`Login`, `ApiKey`, `SavedMediaPath`). Complexity: 1
- [ ] Step 1.2 - Update `appsettings.json` to nest existing credentials under `Danbooru` and add `SavedMediaPath`. Bind options in `Program.cs` via `builder.Services.Configure<DanbooruOptions>(builder.Configuration.GetSection("Danbooru"))`. Complexity: 1
- [ ] Step 1.3 - Refactor `DanbooruService` to inject `IOptions<DanbooruOptions>` (or `IOptionsSnapshot`) instead of raw `IConfiguration["DanbooruLogin"/"DanbooruApiKey"]`. Complexity: 2

#### Success Criteria

- App builds and runs with new config layout.
- Search tab still fetches posts with authenticated requests.
- No stray references to the flat `DanbooruLogin` / `DanbooruApiKey` keys.

---

### Phase 2: Persistence Layer

**Objective:** Add the `SavedDanbooruMedia` entity, tag-bundle JSON converter, repository, hand-authored migration, and snapshot update.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps

- [ ] Step 2.1 - Add `BlazorWebApp/Data/Entities/SavedDanbooruMedia.cs` with scalar columns and a `TagsJson` string (or `DanbooruTagBundle` wrapper handled by converter). Complexity: 2
- [ ] Step 2.2 - Add `BlazorWebApp/Data/Converters/DanbooruTagBundleConverter.cs` (EF `ValueConverter`) and register on `AppDbContext.OnModelCreating`. Add unique index on `DanbooruPostId`. Complexity: 2
- [ ] Step 2.3 - Hand-author migration `{timestamp}_Add_SavedDanbooruMedia.cs` + update `AppDbContextModelSnapshot.cs` alphabetically. Verify `[DbContext]` + `[Migration]` attributes present. Complexity: 2
- [ ] Step 2.4 - Add `ISavedDanbooruMediaRepository` + `SavedDanbooruMediaRepository` with: `AddAsync`, `ExistsByPostIdAsync`, `GetByIdAsync`, `GetPagedAsync(filter, skip, take)`, `UpdateAsync`, `DeleteAsync`. Register in DI. Complexity: 3

#### Success Criteria

- App starts and runs `MigrateAsync` without error; table `SavedDanbooruMedia` exists with expected columns and unique index on `DanbooruPostId`.
- Repository CRUD works against a temporary row (verified via unit test or ad-hoc).

---

### Phase 3: Library Service

**Objective:** Service that downloads a `DanbooruPost` to disk at `SavedMediaPath/{rating}/score_{bucket}/{postId}.{ext}`, persists the entity, and raises events.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps

- [ ] Step 3.1 - Add `Events/DanbooruMediaSavedEventArgs.cs` and `DanbooruMediaDeletedEventArgs.cs` (payload: entity + whether file was deleted). Complexity: 1
- [ ] Step 3.2 - Add `IDanbooruLibraryService` + implementation with `SaveAsync(DanbooruPost)`, `DeleteAsync(int id, bool deleteFile)`, path helpers (`ResolveRatingFolder`, `ResolveScoreBucket`). Download via injected `HttpClient` (from `DanbooruService`'s pipeline or a separate named client without auth headers - confirm during Stage 1 of the step). Complexity: 3
- [ ] Step 3.3 - Register service in DI. Ensure `SavedMediaPath` directory tree is created lazily. Raise `DanbooruMediaSaved` / `DanbooruMediaDeleted` via `EventService`. Complexity: 1

#### Success Criteria

- Calling `SaveAsync` with a known post downloads the correct file bytes to the expected folder, creates a DB row, and fires `DanbooruMediaSaved`.
- Calling `DeleteAsync(id, true)` removes file and row; `DeleteAsync(id, false)` removes row only.
- Dedup: second `SaveAsync` with same `DanbooruPostId` returns a status indicating "already saved" without duplicating the file.

---

### Phase 4: Save UX (Search tab)

**Objective:** Add a "Save to Library" button to `DanbooruImageCard`'s hover overlay and wire snackbar feedback.
**Complexity:** 3 points
**Status:** [ ] Not Started

#### Steps

- [ ] Step 4.1 - Extend `DanbooruImageCard` with `OnSave` `EventCallback`; add overlay action button (bookmark / save icon) alongside existing Copy Tags / Send to Img2Img / Expand buttons. Complexity: 2
- [ ] Step 4.2 - Wire `Pages/Danbooru.razor` to call `IDanbooruLibraryService.SaveAsync`; show `Snackbar` success, "already saved" info, or error. Disable button briefly during download. Complexity: 2

#### Success Criteria

- Clicking Save on a post downloads the file and adds a library row.
- Subsequent clicks on the same post show an "Already saved" info toast.
- Error path (network / disk) surfaces a clear error toast and logs.

---

### Phase 5: Library Tab UI

**Objective:** Build the Library tab: masonry grid of saved media, filters, infinite scroll, card actions (open viewer, send tags, open source, delete).
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps

- [ ] Step 5.1 - Add `Components/Resources/SavedDanbooruMediaCard.razor` (+ css). Reuse overlay pattern from `DanbooruImageCard`. Media src uses `/files/danbooru/{relativePath}` (served in Phase 6). Actions: Copy Tags, Send to Img2Img, Open Fullscreen, Open Source (danbooru.us post page), Delete. Complexity: 3
- [ ] Step 5.2 - Replace the "Coming soon" block in the Library `MudTabPanel` with a masonry grid + topbar (search-by-tag textfield, rating filter, score-min slider, video-only toggle). Reuse `DanbooruMasonry.js` infinite scroll. Complexity: 3
- [ ] Step 5.3 - Wire delete action to `ConfirmationDialog` with `CheckboxContent = "Delete file from disk?"`, call `IDanbooruLibraryService.DeleteAsync(id, deleteFile)`, refresh list on success. Subscribe to `DanbooruMediaSaved` / `DanbooruMediaDeleted` via `EventService` for live updates when saving from the Search tab. Complexity: 2

#### Success Criteria

- Library tab lists all saved media, paginated, with working filters.
- Save on Search tab updates Library tab without manual refresh (via event subscription).
- Delete with/without file removal works; dialog matches existing resource deletion UX.
- Send Tags / Send to Img2Img still work from the Library cards.

---

### Phase 6: Static File Serving

**Objective:** Expose `SavedMediaPath` to the browser under `/files/danbooru`.
**Complexity:** 2 points
**Status:** [ ] Not Started

#### Steps

- [ ] Step 6.1 - Add `app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(danbooruOptions.SavedMediaPath), RequestPath = "/files/danbooru" })` in `Program.cs` next to existing static mappings. Guard against missing/empty path at startup. Complexity: 1
- [ ] Step 6.2 - Confirm image and video cards in the Library tab render from the mapped path; adjust any URL helper if needed. Complexity: 1

#### Success Criteria

- Saved images and videos render in the browser via `/files/danbooru/...`.
- Missing path does not crash startup; logs a clear warning if unconfigured.

---

### Phase 7: Polish & Edge Cases

**Objective:** Fix edge cases discovered during Phases 4-6 and polish UX.
**Complexity:** 3 points
**Status:** [ ] Not Started

#### Steps

- [ ] Step 7.1 - Video playback on hover in Library cards (mirror `DanbooruImageCard` behavior). Complexity: 1
- [ ] Step 7.2 - Handle posts with missing `Url` (rare on Danbooru) and oversized videos: show non-blocking error and skip. Complexity: 1
- [ ] Step 7.3 - Large-library performance pass: ensure queries use the unique index, paginate server-side, load previews lazily. Complexity: 1

#### Success Criteria

- No regressions in Search tab.
- Library tab is responsive with several hundred rows.
- All known error paths surface user-visible feedback.

---

## Stress Points & Risks

| Risk                                                                | Mitigation                                                                                                                             | Complexity |
| ------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- | ---------- |
| `SavedMediaPath` unset or unreachable at startup                    | Log warning; skip static mapping; Save action returns error toast.                                                                     | 1          |
| Large video downloads blocking the UI thread                        | Perform `HttpClient` download async; snackbar shows "Saving..." state. If proven painful, defer background queue to a follow-up phase. | 2          |
| Duplicate migration id / missed snapshot update (EF silently skips) | Follow `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md` checklist explicitly in Step 2.3.                                 | 2          |
| Static file provider exposes parent directories                     | Use a dedicated `SavedMediaPath` root; never share with `OutputDir`.                                                                   | 1          |
| Tag bundle JSON mutation not persisted                              | Apply `IsModified = true` on `TagsJson`/bundle property in `UpdateAsync`.                                                              | 1          |
| Rating/score bucket folder explosion over time                      | Flat-enough layout (4 rating x 4 score buckets = 16 leaf folders max). Acceptable.                                                     | 1          |

---

## Changelog

| Phase    | Changes                                                                                                                                                                                                                                                                  |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Planning | Initial plan created based on user requirements (rating+score folder split, sync download on Save, originals for both media, single JSON tag column, toast-on-duplicate, modal delete with file checkbox, library-only scope, config consolidation included as Phase 1). |

---

## References

- `BlazorWebApp/Pages/Danbooru.razor` - current page and Library placeholder
- `BlazorWebApp/Services/DanbooruService.cs` - API client to refactor in Phase 1
- `BlazorWebApp/Data/Dtos/DanbooruPost.cs` - source DTO to map into the new entity
- `BlazorWebApp/Components/Resources/DanbooruImageCard.razor` - card to extend with Save action
- `BlazorWebApp/Components/Resources/DanbooruSearchesDrawer.razor` - existing saved-searches UI (out of scope, for context)
- `BlazorWebApp/Program.cs` (lines 177-190) - static file mapping pattern to replicate
- `BlazorWebApp/Pages/Resources.razor` (lines 180-205) - `ConfirmationDialog` delete-with-checkbox pattern to follow in Phase 5
- `BlazorWebApp/Models/AppSettings.cs` (`DanbooruSettingsModel`) - existing `BlacklistTags`/`SavedSearches` (unchanged)
- `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md` - migration checklist
