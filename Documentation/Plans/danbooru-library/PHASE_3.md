# Phase 3: Library Service

> **Main plan:** [MAIN_PLAN.md](./MAIN_PLAN.md)
> **Status:** [ ] Not Started
> **Complexity:** 5 points
> **Depends on:**
>
> - `DanbooruOptions.SavedMediaPath` (Phase 1)
> - `SavedDanbooruMedia` entity + `ISavedDanbooruMediaRepository` (Phase 2)
> - `IEventService` (pre-existing, `BlazorWebApp/Services/EventService.cs`)
>   **Unblocks:** Phase 4 (Save UX) calls `IDanbooruLibraryService.SaveAsync`; Phase 5 (Library tab) calls `DeleteAsync` and subscribes to the two new events.

---

## 1. Objective

Add `IDanbooruLibraryService` / `DanbooruLibraryService` that turns a `DanbooruPost` into a file on disk plus a DB row, and removes the file and/or the row on demand. The service handles deduplication (unique index on `DanbooruPostId`), directory creation under `{SavedMediaPath}/{rating}/score_{bucket}/`, and publishes `DanbooruMediaSavedEventArgs` / `DanbooruMediaDeletedEventArgs` through `EventService`.

---

## 2. Context & Background

The service sits between the UI (`DanbooruImageCard` in Phase 4, Library tab in Phase 5) and the persistence layer built in Phase 2. Events are the only way the Search tab communicates "something was saved" to the Library tab without shared UI state (workspace convention: "All events must flow through `EventService` (pub/sub convention)"). Event-arg types live under `BlazorWebApp/Events/` (see siblings like `DownloadCompletedEventArgs.cs` for the conventional shape).

Inherited conventions (verbatim from `MAIN_PLAN.md`):

- "Folder layout `{rating}/score_{bucket}/{postId}.{ext}` with buckets `0`, `100`, `200`, `300+`."
- "Score buckets: `< 100` -> `score_0`, `100-199` -> `score_100`, `200-299` -> `score_200`, `>= 300` -> `score_300`."
- "Rating folder: `general`, `sensitive`, `questionable`, `explicit`, `none` (full names, lowercase)."
- "Synchronous download on Save click. Simplest UX; progress shown via snackbar."
- "Save both image and video original (`file_url`)."
- "Dedup: second `SaveAsync` with same `DanbooruPostId` returns a status indicating 'already saved' without duplicating the file."

Download source: `DanbooruPost.Url` (JSON property `file_url`) per DTO in `BlazorWebApp/Data/Dtos/DanbooruPost.cs`. This is an absolute URL to `https://cdn.donmai.us/...` and is independent of the authenticated API host, so no `Authorization` header is needed for the download.

---

## 3. Prerequisites

- **Artifacts from prior phases:**
  - `BlazorWebApp/Models/DanbooruOptions.cs` - `SavedMediaPath` is read here
  - `BlazorWebApp/Data/Entities/SavedDanbooruMedia.cs` - constructed from the `DanbooruPost`
  - `BlazorWebApp/Data/Repositories/ISavedDanbooruMediaRepository.cs` - called for `AddAsync`, `ExistsByPostIdAsync`, `GetByIdAsync`, `DeleteAsync`
- **Files the executor must read before writing code:**
  - `BlazorWebApp/Services/EventService.cs` - `IEventService.Publish<T>` signature
  - `BlazorWebApp/Events/DownloadCompletedEventArgs.cs` - pattern for a small event-args class
  - `BlazorWebApp/Services/DanbooruService.cs` - already-registered typed `HttpClient` (Phase 1 state). Do **not** reuse its authenticated client for file downloads.
- **External references:**
  - Danbooru CDN URL format (returned by `DanbooruPost.Url` / `file_url`).

---

## 4. Files Inventory

### To Create

| Path                                                   | Purpose                                                                          |
| ------------------------------------------------------ | -------------------------------------------------------------------------------- |
| `BlazorWebApp/Events/DanbooruMediaSavedEventArgs.cs`   | Published after a post is persisted                                              |
| `BlazorWebApp/Events/DanbooruMediaDeletedEventArgs.cs` | Published after a library row is removed                                         |
| `BlazorWebApp/Services/IDanbooruLibraryService.cs`     | Service contract                                                                 |
| `BlazorWebApp/Services/DanbooruLibraryService.cs`      | Implementation                                                                   |
| `BlazorWebApp/Services/DanbooruLibraryPaths.cs`        | Static helpers: `ResolveRatingFolder`, `ResolveScoreBucket`, `BuildRelativePath` |
| `BlazorWebApp/Services/DanbooruSaveResult.cs`          | Result enum + record returned by `SaveAsync`                                     |

### To Modify

| Path                      | Change                                                                                         |
| ------------------------- | ---------------------------------------------------------------------------------------------- |
| `BlazorWebApp/Program.cs` | Register `HttpClient` for downloads (plain, un-auth'd) and `IDanbooruLibraryService` singleton |

### To Leave Untouched (but referenced)

| Path                                       | Why it matters                                                          |
| ------------------------------------------ | ----------------------------------------------------------------------- |
| `BlazorWebApp/Services/DanbooruService.cs` | Phase 1 authenticated client - must **not** be reused for CDN downloads |
| `BlazorWebApp/Services/EventService.cs`    | Publish surface we call into                                            |

---

## 5. Step-by-Step Execution

### Step 3.1: Event args

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] Add `DanbooruMediaSavedEventArgs(SavedDanbooruMedia media)`.
- [ ] Add `DanbooruMediaDeletedEventArgs(int id, int danbooruPostId, bool fileDeleted)`.

#### Code Sketch

```csharp
// BlazorWebApp/Events/DanbooruMediaSavedEventArgs.cs
using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Events
{
    public class DanbooruMediaSavedEventArgs : EventArgs
    {
        public SavedDanbooruMedia Media { get; }
        public DanbooruMediaSavedEventArgs(SavedDanbooruMedia media) => Media = media;
    }
}
```

```csharp
// BlazorWebApp/Events/DanbooruMediaDeletedEventArgs.cs
namespace BlazorWebApp.Events
{
    public class DanbooruMediaDeletedEventArgs : EventArgs
    {
        public int Id { get; }
        public int DanbooruPostId { get; }
        public bool FileDeleted { get; }

        public DanbooruMediaDeletedEventArgs(int id, int danbooruPostId, bool fileDeleted)
        {
            Id = id;
            DanbooruPostId = danbooruPostId;
            FileDeleted = fileDeleted;
        }
    }
}
```

#### Validation

- Both files compile and follow the naming convention of neighbours in `BlazorWebApp/Events/`.

---

### Step 3.2: Paths helper, result type, service contract & implementation

**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks

- [ ] `DanbooruLibraryPaths` static class with the two resolver methods and a `BuildRelativePath(DanbooruPost)` that returns `{rating}/score_{bucket}/{postId}.{ext}`.
- [ ] `DanbooruSaveStatus` enum (`Saved`, `AlreadySaved`, `Failed`) + `DanbooruSaveResult` record.
- [ ] `IDanbooruLibraryService` with `SaveAsync(DanbooruPost, CancellationToken)` and `DeleteAsync(int id, bool deleteFile, CancellationToken)`.
- [ ] Implementation injecting:
  - `ISavedDanbooruMediaRepository`
  - `IOptions<DanbooruOptions>`
  - `IEventService`
  - `IHttpClientFactory` (named client `"danbooru-download"` registered in Step 3.3)
  - `ILogger<DanbooruLibraryService>`

#### Implementation Notes

- Dedup: call `ExistsByPostIdAsync` first. If true, return `DanbooruSaveResult(DanbooruSaveStatus.AlreadySaved, existing)`.
- Folder creation: `Directory.CreateDirectory(absoluteFolder)` is idempotent; call once per `SaveAsync`.
- Download: use `HttpCompletionOption.ResponseHeadersRead` + `CopyToAsync` on a `FileStream` to stream large files without buffering. Wrap in `try/finally` and delete partial files on failure.
- Atomicity: download to a `.tmp` file and `File.Move` to the final name on success, so a crashed save never leaves a half-file that dedup would then consider "present".
- Entity creation: use the `SavedDanbooruMedia(DanbooruPost, relativePath, fileName)` constructor from Phase 2.
- Events: publish `DanbooruMediaSavedEventArgs` only after `AddAsync` returns successfully.
- `DeleteAsync(id, deleteFile)`:
  - Load row via `GetByIdAsync`.
  - If `deleteFile` and file exists, delete it (log and continue on `IOException`).
  - Call `repository.DeleteAsync(id)`.
  - Publish `DanbooruMediaDeletedEventArgs(row.Id, row.DanbooruPostId, actualFileDeleted)`.

#### Code Sketch

```csharp
// BlazorWebApp/Services/DanbooruLibraryPaths.cs
using BlazorWebApp.Data.Dtos;

namespace BlazorWebApp.Services
{
    public static class DanbooruLibraryPaths
    {
        public static string ResolveRatingFolder(string? rating) => rating switch
        {
            "g" => "general",
            "s" => "sensitive",
            "q" => "questionable",
            "e" => "explicit",
            _   => "none"
        };

        public static string ResolveScoreBucket(int score) => score switch
        {
            >= 300 => "score_300",
            >= 200 => "score_200",
            >= 100 => "score_100",
            _      => "score_0"
        };

        public static string BuildRelativePath(DanbooruPost post)
        {
            var ext = string.IsNullOrWhiteSpace(post.Extension) ? "bin" : post.Extension;
            var rating = ResolveRatingFolder(post.Rating);
            var bucket = ResolveScoreBucket(post.Score);
            return Path.Combine(rating, bucket, $"{post.Id}.{ext}");
        }
    }
}
```

```csharp
// BlazorWebApp/Services/DanbooruSaveResult.cs
using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Services
{
    public enum DanbooruSaveStatus { Saved, AlreadySaved, Failed }

    public record DanbooruSaveResult(
        DanbooruSaveStatus Status,
        SavedDanbooruMedia? Media = null,
        string? ErrorMessage = null);
}
```

```csharp
// BlazorWebApp/Services/IDanbooruLibraryService.cs
using BlazorWebApp.Data.Dtos;

namespace BlazorWebApp.Services
{
    public interface IDanbooruLibraryService
    {
        Task<DanbooruSaveResult> SaveAsync(DanbooruPost post, CancellationToken ct = default);
        Task<bool> DeleteAsync(int id, bool deleteFile, CancellationToken ct = default);
    }
}
```

```csharp
// BlazorWebApp/Services/DanbooruLibraryService.cs (shape)
using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Data.Repositories;
using BlazorWebApp.Events;
using BlazorWebApp.Models;
using Microsoft.Extensions.Options;

namespace BlazorWebApp.Services
{
    public class DanbooruLibraryService : IDanbooruLibraryService
    {
        public const string DownloadClientName = "danbooru-download";

        private readonly ISavedDanbooruMediaRepository _repo;
        private readonly IOptions<DanbooruOptions> _options;
        private readonly IEventService _events;
        private readonly IHttpClientFactory _httpFactory;
        private readonly ILogger<DanbooruLibraryService> _logger;

        public DanbooruLibraryService(
            ISavedDanbooruMediaRepository repo,
            IOptions<DanbooruOptions> options,
            IEventService events,
            IHttpClientFactory httpFactory,
            ILogger<DanbooruLibraryService> logger)
        {
            _repo = repo;
            _options = options;
            _events = events;
            _httpFactory = httpFactory;
            _logger = logger;
        }

        public async Task<DanbooruSaveResult> SaveAsync(DanbooruPost post, CancellationToken ct = default)
        {
            if (post == null) throw new ArgumentNullException(nameof(post));
            if (string.IsNullOrWhiteSpace(post.Url))
                return new(DanbooruSaveStatus.Failed, null, "Post has no downloadable URL.");

            if (await _repo.ExistsByPostIdAsync(post.Id, ct))
            {
                var existing = await _repo.GetByPostIdAsync(post.Id, ct);
                return new(DanbooruSaveStatus.AlreadySaved, existing);
            }

            var savedRoot = _options.Value.SavedMediaPath;
            if (string.IsNullOrWhiteSpace(savedRoot))
                return new(DanbooruSaveStatus.Failed, null, "SavedMediaPath is not configured.");

            var relativePath = DanbooruLibraryPaths.BuildRelativePath(post);
            var absolutePath = Path.Combine(savedRoot, relativePath);
            var directory = Path.GetDirectoryName(absolutePath)!;
            Directory.CreateDirectory(directory);

            var tmpPath = absolutePath + ".tmp";
            try
            {
                var http = _httpFactory.CreateClient(DownloadClientName);
                using var response = await http.GetAsync(post.Url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
                await using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs, ct);
                }
                if (File.Exists(absolutePath)) File.Delete(absolutePath);
                File.Move(tmpPath, absolutePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download Danbooru post {PostId} from {Url}", post.Id, post.Url);
                if (File.Exists(tmpPath)) { try { File.Delete(tmpPath); } catch { } }
                return new(DanbooruSaveStatus.Failed, null, ex.Message);
            }

            var fileName = Path.GetFileName(absolutePath);
            var entity = new SavedDanbooruMedia(post, relativePath.Replace('\\', '/'), fileName);
            await _repo.AddAsync(entity, ct);

            _events.Publish(new DanbooruMediaSavedEventArgs(entity));
            _logger.LogInformation("Saved Danbooru post {PostId} -> {RelativePath}", post.Id, relativePath);
            return new(DanbooruSaveStatus.Saved, entity);
        }

        public async Task<bool> DeleteAsync(int id, bool deleteFile, CancellationToken ct = default)
        {
            var row = await _repo.GetByIdAsync(id, ct);
            if (row == null) return false;

            var actualFileDeleted = false;
            if (deleteFile)
            {
                var savedRoot = _options.Value.SavedMediaPath;
                if (!string.IsNullOrWhiteSpace(savedRoot))
                {
                    var full = Path.Combine(savedRoot, row.RelativePath);
                    try
                    {
                        if (File.Exists(full)) { File.Delete(full); actualFileDeleted = true; }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete file {Path} for Danbooru media {Id}", full, id);
                    }
                }
            }

            await _repo.DeleteAsync(id, ct);
            _events.Publish(new DanbooruMediaDeletedEventArgs(id, row.DanbooruPostId, actualFileDeleted));
            return true;
        }
    }
}
```

#### Conventions to Respect

- "All events must flow through `EventService` (pub/sub convention)" - `Publish<T>` is the only way saved/deleted notifications leave this service.
- Store `RelativePath` with forward-slash separators so it composes safely into URLs in Phase 6 (`/files/danbooru/{relativePath}`). The physical filesystem uses `Path.Combine` which respects OS separators.
- Repositories are singleton; this service is singleton too (no per-request state).

#### Validation

- Manual: call `SaveAsync` with a real `DanbooruPost` from the Search tab; verify file appears under the correct `{rating}/score_{bucket}/` folder and DB row exists.
- Manual: call `SaveAsync` again with the same post; returns `AlreadySaved`, no second file.
- Manual: `DeleteAsync(id, true)` removes both row and file; `DeleteAsync(id, false)` removes only the row.

#### Changes Made

_To be filled in after the step is implemented._

---

### Step 3.3: DI registration + startup guard

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] In `Program.cs`, register a named `HttpClient` for downloads:
  ```csharp
  builder.Services.AddHttpClient(DanbooruLibraryService.DownloadClientName, c =>
  {
      c.DefaultRequestHeaders.Add("User-Agent", "SDBlazor");
      c.Timeout = TimeSpan.FromMinutes(5);
  });
  ```
- [ ] Register the service:
  ```csharp
  builder.Services.AddSingleton<IDanbooruLibraryService, DanbooruLibraryService>();
  ```
- [ ] Add a startup log warning (after `var app = builder.Build();`) if `DanbooruOptions.SavedMediaPath` is empty OR does not exist (log only, do not crash):
  ```csharp
  var danbooruOpts = app.Services.GetRequiredService<IOptions<DanbooruOptions>>().Value;
  var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
  if (string.IsNullOrWhiteSpace(danbooruOpts.SavedMediaPath))
      startupLogger.LogWarning("Danbooru:SavedMediaPath is not configured. Library Save will fail.");
  else if (!Directory.Exists(danbooruOpts.SavedMediaPath))
      startupLogger.LogWarning("Danbooru:SavedMediaPath '{Path}' does not exist. It will be created on first Save.", danbooruOpts.SavedMediaPath);
  ```

#### Implementation Notes

- Named `HttpClient` avoids polluting `DanbooruService`'s authenticated pipeline with download calls to the CDN.
- Startup guard is a warning only - Phase 6 reuses the same options value to skip static file mapping when empty.

#### Conventions to Respect

- Registrations grouped near other Danbooru-related lines in `Program.cs` (`AddHttpClient<DanbooruService>()`, `Configure<DanbooruOptions>()`).

#### Validation

- App starts; no warning logged when `SavedMediaPath` is valid.
- App starts with empty `SavedMediaPath`; warning appears in console; no crash.

#### Changes Made

_To be filled in after the step is implemented._

---

## 6. Integration Points

- **DI registrations:**
  ```csharp
  builder.Services.AddHttpClient(DanbooruLibraryService.DownloadClientName, c =>
  {
      c.DefaultRequestHeaders.Add("User-Agent", "SDBlazor");
      c.Timeout = TimeSpan.FromMinutes(5);
  });
  builder.Services.AddSingleton<IDanbooruLibraryService, DanbooruLibraryService>();
  ```
- **Events published (none subscribed here):**
  - `DanbooruMediaSavedEventArgs(SavedDanbooruMedia media)` - raised after a successful `AddAsync`.
  - `DanbooruMediaDeletedEventArgs(int id, int danbooruPostId, bool fileDeleted)` - raised after the row is deleted.
- **Configuration bindings:** Reads `DanbooruOptions.SavedMediaPath` (Phase 1).
- **Startup side-effects:** Log warning if path is empty / missing; `Directory.CreateDirectory` called lazily inside `SaveAsync`.

---

## 7. Testing Strategy

- **Automated tests to add/update:** If `BlazorWebApp.Tests` has an existing service-test pattern, add a happy-path test using an in-memory / temp-folder `SavedMediaPath` and a mocked `ISavedDanbooruMediaRepository`. Defer if the pattern is not readily available; Phase 4 exercises the service via the UI.
- **Manual verification checklist:**
  1. From the Search tab (once Phase 4 wires it) click Save on an image post - file appears at `{SavedMediaPath}/{rating}/score_{bucket}/{postId}.{ext}`, row in DB.
  2. Repeat on the same post - `AlreadySaved` result, no duplicate file or row.
  3. Save a video post - mp4/webm downloads fully and plays in Phase 5.
  4. Delete with file removal - file gone, row gone, `DanbooruMediaDeletedEventArgs.FileDeleted == true`.
  5. Delete without file removal - file remains, row gone, `FileDeleted == false`.
- **Regression watch-list:** Authenticated Danbooru API requests (Search tab) unaffected; `DanbooruService`'s `HttpClient` pipeline is separate from the download client.

---

## 8. Stress Points Specific to This Phase

| Risk                                                    | Mitigation                                                                                                                                                                                                  |
| ------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Large video downloads block the UI thread               | `HttpCompletionOption.ResponseHeadersRead` + streamed copy; Snackbar in Phase 4 shows progress feel. If proven painful, defer to a background queue (follow-up phase).                                      |
| Partial file left after a crash passes dedup check      | Download to `.tmp` + `File.Move` on success; `.tmp` deleted on failure.                                                                                                                                     |
| `SavedMediaPath` unset at startup                       | Warning logged; `SaveAsync` returns `Failed` with clear message; Phase 6 also guards static mapping.                                                                                                        |
| Concurrent `SaveAsync` for the same post (double-click) | Unique index on `DanbooruPostId` throws on second `AddAsync`; catch `DbUpdateException` and translate to `AlreadySaved` to be safe (optional hardening; acceptable to leave as a rare visible error in v1). |
| Event raised before the DB write flushes                | `AddAsync` awaits `SaveChangesAsync`; publish happens after - no issue.                                                                                                                                     |

---

## 9. Resolved Assumptions

- **Separate named `HttpClient` for downloads:** Avoids sending the `Basic` auth header to the CDN and isolates the 5-minute timeout from search requests. (Answered the Phase 3 micro-decision raised during planning.)
- **Atomicity via `.tmp` + `File.Move`:** Prevents dedup from being fooled by half-downloaded files.
- **`RelativePath` stored with forward slashes:** Keeps URL assembly in Phase 6 trivial (`"/files/danbooru/" + relativePath`) on Windows.
- **Publish-after-commit ordering:** Events fire only after `AddAsync` / `DeleteAsync` complete, so subscribers always see the row when querying.

---

## 10. Open Clarifications

- **Step 3.2 - Snackbar progress feel:** Phase 3 does not expose progress callbacks (the UI shows a single "Saving..." toast in Phase 4). If the user later asks for per-byte progress, the service would need an `IProgress<long>` parameter. Not in scope now.

---

## 11. Progress Tracking

| Step | Status | Complexity | Notes |
| ---- | ------ | ---------- | ----- |
| 3.1  | [ ]    | 1          |       |
| 3.2  | [ ]    | 3          |       |
| 3.3  | [ ]    | 1          |       |

---

## 12. Issues & Resolutions

_Populated during execution._

---

## 13. Commit Checkpoints

- [ ] Step 3.1 complete
- [ ] Step 3.2 complete
- [ ] Step 3.3 complete
- [ ] Phase build green; manual round-trip (save -> dedup -> delete) confirmed

---

## 14. Phase Summary

_To be filled in after the phase is complete._

---

## 15. Cross-References

- Main plan section: [Phase 3: Library Service](./MAIN_PLAN.md)
- Prior phase: [PHASE_2.md](./PHASE_2.md)
- Next phase: [PHASE_4.md](./PHASE_4.md)
- Related docs: `BlazorWebApp/Services/EventService.cs` (pub/sub contract)
