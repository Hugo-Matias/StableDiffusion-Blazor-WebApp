# Phase 1: Config Consolidation

> **Main plan:** [MAIN_PLAN.md](./MAIN_PLAN.md)
> **Status:** [ ] Not Started
> **Complexity:** 3 points
> **Depends on:** None (first phase)
> **Unblocks:** Phase 2 and Phase 6 both read `DanbooruOptions.SavedMediaPath`; Phase 3 reuses the same options object for downloads.

---

## 1. Objective

Replace the flat `DanbooruLogin` / `DanbooruApiKey` string keys currently read via `_configuration["DanbooruLogin"]` with a strongly-typed options section `Danbooru` that also exposes a new `SavedMediaPath`. After this phase the app still authenticates against the Danbooru API in the Search tab, but `DanbooruService` consumes `IOptions<DanbooruOptions>` instead of indexing `IConfiguration`, and the rest of the plan can depend on `DanbooruOptions.SavedMediaPath` being available through DI.

---

## 2. Context & Background

`BlazorWebApp/appsettings.json` currently stores Danbooru credentials as two flat keys next to unrelated settings. `BlazorWebApp/Services/DanbooruService.cs` reads them inline inside the constructor:

```csharp
_httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
    "Basic",
    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_configuration["DanbooruLogin"]}:{_configuration["DanbooruApiKey"]}")));
```

The rest of the Library feature needs a third setting (`SavedMediaPath`) plus a structured place to grow. Workspace convention: options classes live under `BlazorWebApp/Models/` and are bound in `Program.cs` via `builder.Services.Configure<T>(builder.Configuration.GetSection("<Name>"))`. `DanbooruService` is registered as a typed `HttpClient` via `builder.Services.AddHttpClient<DanbooruService>()` (see [BlazorWebApp/Program.cs](../../../BlazorWebApp/Program.cs) line ~27), so it already participates in DI and can take `IOptions<DanbooruOptions>` in its constructor.

Inherited conventions (verbatim from `MAIN_PLAN.md`):

- "Consolidate `DanbooruLogin` / `DanbooruApiKey` into `Danbooru` section with `SavedMediaPath`. `DanbooruOptions` bound via `IOptions` and injected into `DanbooruService`."
- "No stray references to the flat `DanbooruLogin` / `DanbooruApiKey` keys."

---

## 3. Prerequisites

- **Artifacts from prior phases:** _None._
- **Files the executor must read before writing code:**
  - `BlazorWebApp/Services/DanbooruService.cs` - current constructor to refactor
  - `BlazorWebApp/appsettings.json` - current flat keys to migrate
  - `BlazorWebApp/Program.cs` (top of file, service-registration block) - to locate where to call `Configure<DanbooruOptions>`
  - `BlazorWebApp/Models/OutputPathsOptions.cs` - shape reference for an existing options POCO that is bound the same way
- **External references:** Microsoft docs for `Microsoft.Extensions.Options.IOptions<T>` (standard .NET 6 options pattern).

---

## 4. Files Inventory

### To Create

| Path                                     | Purpose                                                            |
| ---------------------------------------- | ------------------------------------------------------------------ |
| `BlazorWebApp/Models/DanbooruOptions.cs` | Strongly-typed options POCO (`Login`, `ApiKey`, `SavedMediaPath`). |

### To Modify

| Path                                       | Change                                                                                                                                                                               |
| ------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `BlazorWebApp/appsettings.json`            | Remove top-level `DanbooruLogin` / `DanbooruApiKey`; add nested `"Danbooru": { "Login": "...", "ApiKey": "...", "SavedMediaPath": "..." }`.                                          |
| `BlazorWebApp/Program.cs`                  | Register `builder.Services.Configure<DanbooruOptions>(builder.Configuration.GetSection("Danbooru"));` near existing service registrations (before `AddHttpClient<DanbooruService>`). |
| `BlazorWebApp/Services/DanbooruService.cs` | Replace `IConfiguration` dependency with `IOptions<DanbooruOptions>`; read `Login` / `ApiKey` from it.                                                                               |

### To Leave Untouched (but referenced)

| Path                                                           | Why it matters                                                                                              |
| -------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| `BlazorWebApp/Models/AppSettings.cs` (`DanbooruSettingsModel`) | Unrelated per-user settings (`BlacklistTags`, `SavedSearches`). Do not merge with `DanbooruOptions`.        |
| `BlazorWebApp/appsettings.Development.json`                    | If it contains overrides for the old flat keys, they must be migrated the same way - check during Step 1.2. |

---

## 5. Step-by-Step Execution

### Step 1.1: Add `DanbooruOptions`

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] Create `BlazorWebApp/Models/DanbooruOptions.cs` with three public string properties.
- [ ] Expose a `const string SectionName = "Danbooru";` for the binding key.

#### Implementation Notes

Plain POCO, default values safe for missing configuration (empty string, not `null!`). No validation attributes required for this phase; the service can decide how to handle an empty `SavedMediaPath` later.

#### Code Sketch

```csharp
// BlazorWebApp/Models/DanbooruOptions.cs
namespace BlazorWebApp.Models
{
    public class DanbooruOptions
    {
        public const string SectionName = "Danbooru";

        public string Login { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string SavedMediaPath { get; set; } = string.Empty;
    }
}
```

#### Conventions to Respect

- Options classes live under `BlazorWebApp/Models/`.
- Non-nullable `string` properties default to `string.Empty` (match `OutputPathsOptions` style).

#### Validation

- File compiles standalone (`dotnet build BlazorWebApp/BlazorWebApp.csproj`).

#### Changes Made

_To be filled in after the step is implemented._

---

### Step 1.2: Update `appsettings.json` and bind options

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] Remove the top-level `"DanbooruLogin"` and `"DanbooruApiKey"` keys from `BlazorWebApp/appsettings.json`.
- [ ] Add a `"Danbooru"` object with `Login`, `ApiKey` (using the existing values), and a new `SavedMediaPath` placeholder.
- [ ] Check `BlazorWebApp/appsettings.Development.json` for the same flat keys and migrate them.
- [ ] In `BlazorWebApp/Program.cs`, call `builder.Services.Configure<DanbooruOptions>(builder.Configuration.GetSection(DanbooruOptions.SectionName));` before `builder.Services.AddHttpClient<DanbooruService>();`.

#### Implementation Notes

Pick a sensible default for `SavedMediaPath` that matches the existing "close to other media folders" conventions - the user's current machine uses paths like `N:\Images\StableDiffusion\`. A good default is `N:\Images\Danbooru\`, but the executor must confirm the exact path with the user during Stage 1 of this step (see Open Clarifications).

#### Code Sketch

```jsonc
// BlazorWebApp/appsettings.json (excerpt)
{
  // ... existing keys ...
  "Danbooru": {
    "Login": "Nysalie",
    "ApiKey": "PbtiyYVGEtzeCcKtvKRS3MEA",
    "SavedMediaPath": "N:\\Images\\Danbooru",
  },
  // note: remove the old "DanbooruLogin" / "DanbooruApiKey" top-level keys
}
```

```csharp
// BlazorWebApp/Program.cs (insertion just before AddHttpClient<DanbooruService>)
builder.Services.Configure<DanbooruOptions>(
    builder.Configuration.GetSection(DanbooruOptions.SectionName));
builder.Services.AddHttpClient<DanbooruService>();
```

#### Conventions to Respect

- JSON uses escaped backslashes on Windows paths (consistent with existing `ResourcesPath`, `ResourcePreviewsPath`, `OutputDir`).
- Register `Configure<T>` adjacent to the typed `HttpClient` it feeds, to preserve locality.

#### Validation

- App starts; no configuration binding exception on first request.
- A quick grep for `DanbooruLogin` / `DanbooruApiKey` returns zero matches across the solution.

#### Changes Made

_To be filled in after the step is implemented._

---

### Step 1.3: Refactor `DanbooruService` to use `IOptions<DanbooruOptions>`

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Replace the `IConfiguration` constructor parameter with `IOptions<DanbooruOptions> options`.
- [ ] Read `Login` / `ApiKey` from `options.Value`; compute the `Basic` auth header the same way.
- [ ] Remove the `_configuration` field if it is no longer used.
- [ ] Confirm no other code path depends on `IConfiguration` being held by `DanbooruService`.

#### Implementation Notes

Keep the constructor shape otherwise unchanged. The `HttpClient` still gets its `BaseAddress`, `User-Agent`, and `Authorization` header assigned once per instance. Use `IOptions<T>` (singleton) rather than `IOptionsSnapshot<T>` because `AddHttpClient<T>` resolves the typed client as transient from a singleton handler pipeline - `IOptions` is appropriate.

#### Code Sketch

```csharp
// BlazorWebApp/Services/DanbooruService.cs
using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;

namespace BlazorWebApp.Services
{
    public class DanbooruService
    {
        private readonly HttpClient _httpClient;
        private readonly string[] _videoExtensions = new[] { "mp4", "webm" };
        private readonly string[] _imageExtensions = new[] { "avif", "jpg", "png", "gif" };
        private readonly string[] _otherExtentions = new[] { "swf", "zip" };

        public DanbooruService(HttpClient httpClient, IOptions<DanbooruOptions> options)
        {
            var opts = options.Value;
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://hijiribe.donmai.us");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{opts.Login}:{opts.ApiKey}")));
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SDBlazor");
        }

        // GetPosts(...) unchanged
    }
}
```

#### Conventions to Respect

- "All events must use the pub/sub pattern via `EventService.cs`" - not applicable here, but listed to reinforce.
- No behavioural change to `GetPosts` in this step.

#### Validation

- `dotnet build BlazorWebApp/BlazorWebApp.csproj` succeeds.
- Manual: open Danbooru page, perform a search, verify posts load (same behaviour as before).

#### Changes Made

_To be filled in after the step is implemented._

---

## 6. Integration Points

- **DI registrations (exact line to add in `Program.cs` just before `AddHttpClient<DanbooruService>`):**

  ```csharp
  builder.Services.Configure<DanbooruOptions>(
      builder.Configuration.GetSection(DanbooruOptions.SectionName));
  ```

- **Events to publish / subscribe:** _Not applicable for this phase._
- **Configuration bindings:** Section `"Danbooru"` -> `BlazorWebApp.Models.DanbooruOptions`.
- **Startup side-effects:** None. `SavedMediaPath` directory creation is deferred to Phase 3.

---

## 7. Testing Strategy

- **Automated tests to add/update:** None required for this phase (refactor preserves behaviour).
- **Manual verification checklist:**
  1. App starts without configuration binding errors.
  2. Navigate to `/danbooru` and run a search - posts load authenticated (no 401).
  3. Solution-wide text search for `"DanbooruLogin"` and `"DanbooruApiKey"` returns zero hits.
- **Regression watch-list:** Any other service that may have been reading `_configuration["DanbooruLogin"]` indirectly - none expected, confirm via search.

---

## 8. Stress Points Specific to This Phase

| Risk                                                                                  | Mitigation                                                                                 |
| ------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| Empty or misspelled section name binds to all-default options (silent 401 at runtime) | Use `DanbooruOptions.SectionName` constant; verify manually with a search after step 1.3.  |
| `appsettings.Development.json` keeps the old flat keys, masking the migration in Dev  | Grep both files during step 1.2 and migrate together.                                      |
| `SavedMediaPath` wrong at startup causes later phases to fail                         | Out of scope here; Phase 6 handles startup guard; Phase 3 handles lazy directory creation. |

---

## 9. Resolved Assumptions

- **Options lifetime:** `IOptions<DanbooruOptions>` (singleton) rather than `IOptionsSnapshot<DanbooruOptions>`. `DanbooruService` is registered as a typed `HttpClient` with a singleton handler pipeline, and the values are credentials that do not need per-request reload.
- **Section name constant:** Exposed as `DanbooruOptions.SectionName = "Danbooru"` to mirror the pattern the executor should adopt for all later option classes.
- **`BlacklistTags` / `SavedSearches` not merged:** They already live on `AppSettings.Resources.Danbooru` (user-editable runtime settings persisted via `ISettingsService`), which is a separate concern from app-level `IOptions`. Main plan explicitly lists `DanbooruSettingsModel` under "To Leave Untouched".

---

## 10. Open Clarifications

- **Step 1.2 - `SavedMediaPath` default:** The new key needs a concrete value in `appsettings.json`. Proposed default: `"N:\\Images\\Danbooru"` because the user mentioned the target folder will be "close to the already setup media folders" and `OutputDir` is `N:\Images\StableDiffusion\`. Confirm the exact path with the user during Stage 1 of this step.

---

## 11. Progress Tracking

| Step | Status | Complexity | Notes |
| ---- | ------ | ---------- | ----- |
| 1.1  | [ ]    | 1          |       |
| 1.2  | [ ]    | 1          |       |
| 1.3  | [ ]    | 2          |       |

---

## 12. Issues & Resolutions

_Populated during execution._

---

## 13. Commit Checkpoints

- [ ] Step 1.1 complete
- [ ] Step 1.2 complete
- [ ] Step 1.3 complete
- [ ] Phase build green

---

## 14. Phase Summary

_To be filled in after the phase is complete._

- **Accomplishments:**
- **Deferred to later phase:**
- **Lessons learned:**

---

## 15. Cross-References

- Main plan section: [Phase 1: Config Consolidation](./MAIN_PLAN.md)
- Prior phase: N/A
- Next phase: [PHASE_2.md](./PHASE_2.md)
- Related docs: `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md` (applies to Phase 2 onwards)
