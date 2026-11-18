# Gallery Performance (Thumbnails, Covers, Masonry, NAS Delivery) - Implementation Plan

## Status
**Current Phase:** Planning (no code written) - open questions O1-O6 answered by the user, see "Decisions Recorded"

Follow-up to `Documentation/Plans/unraid-hosting/` (PHASE_6.md D7/D8). That plan fixed hover lag and the vault mount; this plan attacks the remaining cost: **every tile downloads the full-size original through the NAS fuse mount, and the masonry hides the whole batch until all of those downloads finish.**

### Decisions Recorded (user, after first review)
| # | Decision | Consequence |
|---|----------|-------------|
| 1 | Use the **unassigned SSD** (Samsung 860 EVO 1 TB, xfs, currently unmounted) for the thumbnail cache, **not** part of the array/parity pool. Unraid reports 1 SMART warning; acceptable because the cache is fully regenerable | Mount via the Unassigned Devices plugin (installed 2026-11-12) at `/mnt/disks/scratch`; nothing irreplaceable (DB, vault data) may go on this disk. Cache path guard required (Phase 3) |
| 2 | Thumbnails are created **on demand only**. Much of the library will be cleaned up later, so no backfill and no warm-on-save by default | Phase 5 is reduced to invalidation + housekeeping; first view of an image pays the generation cost once |
| 3 | **Keep file-based ComfyUI input/output on the vault** (no switch to HTTP `/view`). The current 2 s dir-cache + app retry setup works and keeps data on the NAS | Phase 8 candidate 5 is rejected/deferred (reconsider only if the generation race reappears) |
| 4 | Cover fallback = **latest generated image of the project** | Phase 6 uses newest-image fallback, no favorite requirement |
| 5 | **Nothing is exposed outside the LAN.** Transport work is optional and LAN-only | Phase 7 rewritten: optional, LAN-only, decide after measuring Phase 4 |
| 6 | **Rich mode stays as is** | No changes to `ImageCard` visuals or its `MudRating`/`MudCheckBox`; Phase 2 only touches layout/keys/dimensions |
| 7 | **Keep writes to the SSD minimal** (SMART warning). The rclone vfs cache is **not** moved to the SSD | Only thumbnails live there (written once per viewed image, never rewritten). Phase 8 candidate 1 is rejected. Cache cap raised to **100 GB** (decision 8) |
| 8 | Thumbnail cache cap = **100 GB** (`Thumbnails:MaxCacheBytes`) of the 1 TB SSD, which will not be used for much else | Eviction rarely triggers, so deletes/rewrites stay low |

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
1 Trivial | 2 Simple | 3 Moderate | 5 Medium | 8 Complex | 13 Very complex | 21+ Epic (split it)

### Key Rules
- Each step = commitable checkpoint
- No time/date references - complexity points only
- Detours are acceptable after discussion - append to this plan
- Phase documents (`PHASE_{#}.md`) must contain enough context to resume in a new session
- UI work follows `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` (spacing tokens from `wwwroot/site.css`, no magic numbers; `.razor.css` edits follow `.github/instructions/tokens.instructions.md` and `design-language.instructions.md`)
- Cross-component notifications go through `EventService` pub/sub (see `.github/copilot-instructions.md`); do not call components/services directly to notify
- Changes must work on **both** Windows (desktop dev) and Linux (NAS container): forward-slash paths in config/URLs, case-preserving paths (Linux file lookups are case-sensitive), never lowercase a path that is later used to touch the disk
- NAS/Unraid/WSL changes are **state changes on shared infrastructure**: read-only inspection first, back up before editing, ask the user before touching anything other apps use (Stash, stashmanager, vault mount). See `infra/unraid/CLAUDE.md` and `infra/wsl/CLAUDE.md`
- Known unrelated build/test noise exists: validate with targeted builds/tests (see "Validation" below)

### Rules for the implementing model (read first)
- Read the files named in each step before editing; do not invent APIs. **Verify Magick.NET method signatures against the installed package (14.13.1)** - the code samples below are sketches and may need small signature fixes.
- One step at a time. After each step: build, run the targeted tests, stop and report.
- Keep changes surgical. Do not refactor unrelated gallery code, do not reformat files, do not touch `appsettings.json` secrets.
- Use ASCII only in code comments and docs.
- Add tests only for new behavior (cache key, path-traversal guard, fallback rules, cover resolution). Put them in `BlazorWebApp.Tests/Services/` (xunit + Moq + FluentAssertions are available).

### Validation
- Build: `dotnet build BlazorWebApp/BlazorWebApp.csproj --no-restore -v q` (expect 0 errors; ignore the known warning noise)
- Tests: `dotnet test BlazorWebApp.Tests/BlazorWebApp.Tests.csproj --filter "FullyQualifiedName~Thumbnail|FullyQualifiedName~ProjectCover"`
- Browser checks use the real gallery (`/`): Chrome DevTools Network + Performance panels

---

## Problem Statement

User report: the gallery feels sluggish on the NAS-hosted container; images take a while to load. Hover lag was already fixed (CSS-driven hover, image `2026-11-12b`).

### What the code does today (verified)
| Area | Fact | Where |
|------|------|-------|
| Tile image source | `<img src="/image/<path>">` = the **full-size original** PNG straight from the vault (generation outputs are typically ~1 MP PNGs, order of 1 MB each - measure in Phase 1) | `GalleryImageTile.razor`, `ImageCard.razor` via `IOService.GetImageStaticFile` |
| Serving | `UseStaticFiles` on `OutputDir` (rclone crypt fuse mount on the NAS) | `Program.cs` |
| Masonry gating | `layoutMasonry()` calls `waitForImages(container)` which waits for **every** `<img>`/`<video>` in the container (all items, not just new ones) before positioning anything; unpositioned items stay `visibility:hidden`; fallback timeout is 3 s | `wwwroot/js/InfiniteMasonry.js` (`layoutMasonry`, `waitForImages`) |
| Lazy images vs the gate | Every tile `<img>` has `loading="lazy"`. Off-screen lazy images do not fire `load` until scrolled near, so batches with below-the-fold tiles hit the 3 s fallback timeout. **Suspected major contributor - confirm in Phase 1** | same |
| Layout cost | Every added batch triggers a full relayout of all items (`positionedItems` only tracks hide/show; all positions are recomputed with left/top writes + `offsetHeight` reads per item) | `InfiniteMasonry.js` |
| Dimensions known but unused | `Image.Width` / `Image.Height` are in the DB, but `<img>` has no `width`/`height` so layout cannot reserve space before the bytes arrive | `Data/Entities/Image.cs` |
| Blazor render side | `InfiniteScrollMasonry.razor` loops with `@foreach` and **no `@key`**, calls `Images.Images.IndexOf(image)` per item per render (O(N^2)), `ViewerAssets` rebuilds a list on every access, and Rich-mode `ImageCard` embeds a `MudRating` + `MudCheckBox` per card (heavy component trees over SignalR). `GalleryImageTile` (Pure mode) is the lightweight variant | `InfiniteScrollMasonry.razor`, `ImageCard.razor` |
| Folder (project) covers | `ProjectCard.OnParametersSetAsync` runs `DB.GetSampleImage` and, when no cover is set, `DB.GetRandomFavorite` (`ORDER BY random()` over a 120k+ row table) **per card, per parameter set**; the cover can change on every re-render; the cover is the full-size original (or a whole `<video>`) | `ProjectCard.razor`, `DatabaseService.cs` (`GetSampleImage`, `GetRandomFavorite`), `ProjectSelectorDropdown.razor` |
| Other consumers of originals-as-thumbnails | `SelectionCard`, `ImagePickerDialog`, `Cleanup` previews, `ProjectSelectorDropdown`, `CharacterShotCard`, workshop views | grep `GetImageStaticFile` |
| Transport | Plain HTTP/1.1 to Kestrel on `http://10.0.0.11:8189` (no reverse proxy). Browsers cap HTTP/1.1 at ~6 parallel connections per origin, so a page of large images serializes | `infra/unraid/services/blazorwebapp/` |

### NAS facts (probed read-only, 2026-11-12)
| Fact | Value / consequence |
|------|--------------------|
| CPU / RAM | Intel i5-4670K, 4 cores, 23 GB RAM (17 GB page cache). Thumbnail generation competes with Stash, Navidrome, etc. -> cap concurrency (on-demand only, so load follows what the user actually browses) |
| SSD cache pool | **None** (`/mnt/cache` does not exist; `/mnt/user` is the array, 27 TB used / 9.4 TB free). `appdata` share is `prefer cache` but there is no pool, so appdata (SQLite DB, rclone vfs cache `/mnt/user/appdata/rclone-vault-cache`) lives on **parity-protected HDDs** behind the shfs FUSE layer |
| Unassigned SSD | **Prepared 2026-11-12 (Phase 3 Step 0 done, except the compose bind).** `sdb` = Samsung SSD 860 EVO 1 TB, serial `S4X6NF0M829560A`. SMART: overall PASSED, 0 reallocated/uncorrectable/program/erase failures, no error log; wear-levelling 81% remaining, ~40.9k power-on hours, ~98 TB written (rated 600 TBW). The Unraid warning is **UDMA CRC error count = 31** (attribute 199) - typically a SATA cable/connector issue rather than flash failure; reseating or replacing the cable is worthwhile. Contents were not worth keeping (user-confirmed) and the partition was **reformatted xfs, label `scratch`**. **Unassigned Devices plugin 2025.02.14 installed**; auto-mount enabled via UD's `toggle_automount` (verified: unmount + `rc.unassigned mount auto` remounts it); mounted at **`/mnt/disks/scratch`** (`rw,relatime`, 932 GB, ~914 GB free after xfs metadata). Cache dir **`/mnt/disks/scratch/blazorwebapp-thumbs/`** with marker file `.thumbs-volume` created. UD keeps its live config in RAM (`/tmp/unassigned.devices/config/`) and syncs to `/boot/config/plugins/unassigned.devices/` - edit via UD helpers, not the flash file |
| NPM | `npm` container publishes host ports 1880 (80), 1881 (81 admin), 18443 (443), 3000. It may already be port-forwarded for the personal website - cannot tell from the NAS; check the router before adding any proxy host (Phase 7) |
| Vault mount | `vault-local:` rclone crypt over `/mnt/user/vault`, `--vfs-cache-mode full --vfs-cache-max-size 100G --dir-cache-time 2s` (see `infra/wsl/CLAUDE.md`) |
| Cold/warm single-file fetch | ~42 ms cold / ~9.5 ms warm (PHASE_4 D6) - single files are fine; the pain is **many large files in parallel plus the all-images gate** |

---

## Proposed Solution

Five coordinated moves, ordered by value/effort:

1. **Measure first** (baseline numbers so we know each move paid off).
2. **Decouple the masonry from image loading** - the layout must not wait for bytes. Use known dimensions, place only new items, fade images in when ready. Pays off even before thumbnails exist.
3. **Thumbnail service**: generate small WebP derivatives **on first request only**, cache them on the **dedicated unassigned SSD**, serve via `/thumb/{tier}/{relative path}`. Originals stay untouched and are used only by the full-screen viewer.
4. **Folder (project) covers** resolved in one batch, deterministic (latest generated image), cached, and served as thumbnails.
5. **Delivery/infrastructure review**, LAN-only: optional HTTP/2, rclone/NAS/WSL tuning with evidence. File-based ComfyUI I/O stays.

### Key Decisions
| Decision | Rationale |
|----------|-----------|
| Thumbnails are a **filesystem cache**, not a DB table | No EF migration, self-healing (delete the folder and it rebuilds), easy to move between machines. Key = hash(relative path) + tier + cache version |
| Hot path touches **only the local cache file** (`File.Exists`), never the source/fuse mount | The whole point: a cache hit must cost one local read. Source is stat'ed/read only on a miss |
| **No staleness check on hit** (output files are write-once with unique names); explicit invalidation on delete/overwrite via `ImageRecordDeletedEventArgs`/`ImageRecordSavedEventArgs(IsUpdate)`; `CacheVersion` setting bumps the whole cache | Avoids a fuse `stat` per request. Browser cache TTL is 7 days |
| Output format **WebP, quality ~80**, resize with `Thumbnail` (strips profiles). Magick.NET is already a dependency | ~10-20x smaller than 1 MP PNG. AVIF is smaller but 5-10x slower to encode on a 4-core Haswell - rejected for now |
| Three tiers: `sm`=320 px, `md`=640 px, `lg`=1024 px wide (never upscale) | `sm` covers/pickers/selection cards; `md` default gallery tile (~2x for a 5-column layout); `lg` large-tile setting and instant placeholder in the viewer |
| Cache location configurable (`Thumbnails:CachePath`), pointing at a **bind mount of the unassigned SSD** (container `/data/thumbs`), guarded by a marker file | Decision 1. If the SSD is not mounted when the container starts, Docker would silently create the path on the RAM-backed root fs; the marker-file check makes the service disable caching (serve originals) instead of filling RAM |
| Generation concurrency capped (default 2), deduplicated per key, atomic publish (tmp file + rename) | 4-core NAS shared with other services; no partial files, no thundering herd when 24 tiles ask at once |
| Only raster still images are thumbnailed (`.png .jpg .jpeg .webp`, not animated); everything else falls back to the original URL | GIF/video/audio need other tooling (ffmpeg not in the image). Video posters are a deferred item |
| Masonry keeps absolute positioning but becomes **incremental** and **dimension-driven** | Keeps the existing look and `data-tile-size` behavior; removes the load gate |
| Covers: one batch query + in-memory memo + event invalidation; fallback is **deterministic** (explicit cover wins, else the project's latest generated image) | Removes N random-order queries and cover flicker (decision 4) |
| New components/services use `EventService` pub/sub for cache invalidation | Repo convention |

### Conventions
- URL shape: `/thumb/{tier}/{path under OutputDir}` mirrors `/image/{path under OutputDir}`; a helper converts one into the other.
- A failing/unsupported thumbnail request must **degrade to the original** (302 to `/image/...`), never a broken image.
- Config via `Thumbnails` section; env var form `Thumbnails__CachePath`.

---

## Implementation Phases

### Phase 1: Baseline measurement
**Objective:** Numbers before changes: how long until tiles are visible, how many bytes per page, and which of the suspected causes is real.
**Complexity:** 2 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - On the NAS app (`http://10.0.0.11:8189/`), record in Chrome DevTools for a cold load and a "next page" append: requests count, total transferred bytes, per-image sizes (avg/max), time from batch arrival to first visible tile, time to all visible. Note whether the 3 s `waitForImages` fallback fires (console warning `Only X/Y media items loaded, proceeding anyway`).
- [ ] Step 2 - Run the same page with `loading="lazy"` removed (DevTools local override) to confirm or refute the lazy-image gate theory.
- [ ] Step 3 - Read-only NAS sampling: average/max PNG size in `Text-2-Image/_samples` (sample a few hundred files with `stat`), `docker stats` while loading, `top` for rclone/shfs CPU. Optional: enable the rclone rc endpoint only if the user agrees (state change) for `core/stats`.
- [ ] Step 4 - Record results in `PHASE_1.md` (table: metric, before). These are the "before" numbers for every later phase.

#### Success Criteria
- A baseline table exists and names the dominant cause(s) (gate vs bytes vs HTTP/1.1 queueing vs fuse).

---

### Phase 2: Masonry decoupled from image loading
**Objective:** Tiles are positioned immediately from known dimensions; new batches only place new items; images fade in when decoded. Also removes the Blazor-side render waste.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Files
`BlazorWebApp/wwwroot/js/InfiniteMasonry.js`, `Components/Gallery/InfiniteScrollMasonry.razor(.css)`, `GalleryImageTile.razor(.css)`, `Components/Shared/Image/ImageCard.razor(.css)`.

#### Steps
- [ ] Step 1 - **Reserve space with `width`/`height` attributes** on tile images from `Image.Width/Height` (omit when 0 for legacy rows). Modern browsers derive the aspect ratio from those attributes when CSS says `width:100%; height:auto`, so the tile has its final height before any bytes arrive.
- [ ] Step 2 - **Remove the load gate.** In `layoutMasonry`, stop awaiting `waitForImages` for items whose image has explicit dimensions (or, in Rich mode, a fixed card height). For the few items with unknown dimensions, await only that image with a short (<= 1 s) timeout, or observe it with `ResizeObserver` and relayout when it arrives.
- [ ] Step 3 - **Incremental placement.** Keep `columnHeights`, `itemWidth`, `placedCount` in module state; on append place only items `>= placedCount`. Do a **full** relayout only on: resize / column-count change, tile-size change, item removal, or late dimension changes. This turns per-batch cost from O(total items) into O(new items).
- [ ] Step 4 - **Cheaper positioning and reveal.** Position with `transform: translate(x, y)` (no layout thrash), add a `.placed` class to reveal instead of toggling inline `opacity`/`visibility`; images get a placeholder background (token-based colour) and fade in on `load` (`img.decode()` or the `load` event), so a slow image never blocks its neighbours.
- [ ] Step 5 - **Blazor render fixes** in `InfiniteScrollMasonry.razor`: add `@key="image.Id"` to each `.gallery-item`; replace `Images.Images.IndexOf(image)` with the loop index; cache `ViewerAssets` (rebuild only when `Images.Images` changes).
- [ ] Step 6 - **Rich mode stays as is (decision 6).** Do not change `ImageCard`'s look, its `MudRating`/`MudCheckBox`, or its hover behavior. Only the shared layout/key/dimension changes above apply to it (its images still get `width`/`height` attributes and its cards keep their fixed `fixed-size` height). If a measurable Rich-mode render cost remains after Phase 1/2, record it in the phase doc for a later discussion - do not act on it here.
- [ ] Step 7 - **Optional, only if scrolling still janks after the above:** `content-visibility: auto` on tile *inner* content (NOT on `.gallery-item` - skipped items report the intrinsic size to `offsetHeight`, which breaks measurement), and DOM windowing for very long sessions (detach tiles far above the viewport, keeping their reserved height).

#### Code sketch (JS, incremental placement)
```js
const layout = { columnHeights: [], columnCount: 0, itemWidth: 0, gap: 0, placed: 0 };

function resetLayout(container) {
  layout.columnCount = getColumnCount(container);
  layout.gap = getGap(container);
  layout.itemWidth = (container.offsetWidth - layout.gap * (layout.columnCount - 1)) / layout.columnCount;
  layout.columnHeights = new Array(layout.columnCount).fill(0);
  layout.placed = 0;
}

async function placeNewItems(container) {
  const all = [...container.querySelectorAll(".gallery-item")];
  const fresh = all.slice(layout.placed);
  if (fresh.length === 0) return;

  // Items whose <img> has no width/height attributes still need their natural size.
  await Promise.all(fresh.map(waitIfSizeUnknown)); // each resolves immediately when dimensions are known or after <= 1000 ms

  for (const item of fresh) {
    item.style.width = `${layout.itemWidth}px`;                       // width first so offsetHeight is correct
    let col = 0;
    for (let i = 1; i < layout.columnCount; i++) if (layout.columnHeights[i] < layout.columnHeights[col]) col = i;
    const x = col * (layout.itemWidth + layout.gap);
    const y = layout.columnHeights[col];
    item.style.transform = `translate(${x}px, ${y}px)`;
    item.classList.add("placed");
    layout.columnHeights[col] += item.offsetHeight + layout.gap;
  }
  layout.placed = all.length;
  container.style.height = `${Math.max(...layout.columnHeights)}px`;
}

function waitIfSizeUnknown(item) {
  const img = item.querySelector("img");
  if (!img || (img.getAttribute("width") && img.getAttribute("height"))) return Promise.resolve();
  if (img.complete && img.naturalHeight) return Promise.resolve();
  return new Promise(r => { const done = () => r(); img.addEventListener("load", done, { once: true }); img.addEventListener("error", done, { once: true }); setTimeout(done, 1000); });
}
```
Triggers: MutationObserver "items added" -> `placeNewItems`; "items removed", resize, tile size change -> `resetLayout` + `placeNewItems`. Keep the public exports (`attachWindowScroll`, `refreshLayout`, `dispose`).

CSS direction (adapt to the existing rules, which currently set `.gallery-item{position:absolute!important; opacity:0; visibility:hidden}`):
```css
.gallery-item { position: absolute !important; top: 0; left: 0; opacity: 0; transition: opacity 0.25s ease-in-out; }
.gallery-item.placed { opacity: 1; }
```

#### Code sketch (Razor)
```razor
@for (var i = 0; i < Images.Images.Count; i++)
{
    var index = i;
    var image = Images.Images[i];
    <div class="gallery-item" @key="image.Id">
        ...
        <GalleryImageTile Image="@image" OnSelectImage="_ => OpenViewer(index)" ... />
    </div>
}
```
```razor
<img src="@IO.GetImageStaticFile(Image.Path)"
     width="@(Image.Width > 0 ? Image.Width : null)" height="@(Image.Height > 0 ? Image.Height : null)"
     alt="" class="gallery-tile-image" loading="lazy" decoding="async" />
```
(Phase 4 swaps the `src` for the thumbnail URL.)

#### Success Criteria
- Tiles appear immediately after a batch renders, in the right place, with no layout shift while images load (verify with DevTools Performance: no large CLS; no 3 s stall).
- "Next page" append cost is proportional to the new items.
- Removing an item, resizing the window, and changing tile size still relayout correctly.
- No visual regressions in Pure and Rich modes at all three tile sizes.

---

### Phase 3: Thumbnail service (core)
**Objective:** `IThumbnailService` + `/thumb/{tier}/{**path}` endpoint backed by a local disk cache.
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Files
New: `Services/IThumbnailService.cs`, `Services/ThumbnailService.cs`, `Models/ThumbnailOptions.cs`, `BlazorWebApp.Tests/Services/ThumbnailServiceTests.cs`. Edit: `Program.cs` (DI + endpoint), `Services/IOService.cs` + `IIOService.cs` (URL helper), `appsettings.json` (non-secret defaults only). Infra repo: `unraid/services/blazorwebapp/docker-compose.yml` + `.env.example` (new bind mount), `unraid/CLAUDE.md` (SSD facts).

#### Steps
- [x] Step 0 - **SSD prerequisite (infra) - DONE except the compose wiring** (details in "NAS facts"): SMART checked (PASSED; the warning is a CRC-error count), Unassigned Devices plugin installed, SSD reformatted xfs `scratch` (user-approved), auto-mount enabled and verified, mounted at `/mnt/disks/scratch`, cache dir + `.thumbs-volume` marker created at `/mnt/disks/scratch/blazorwebapp-thumbs/`. Remaining for this step: add the compose bind (below) to `infra/unraid/services/blazorwebapp/` and the live project when the service code is ready, and record the Unraid/UD facts in `infra/unraid/CLAUDE.md` (done in the infra repo, uncommitted).
  1. Compose bind with slave propagation so a late/remount of the SSD is seen by the container:
     ```yaml
     volumes:
       - type: bind
         source: ${THUMBS_DIR_HOST:?set THUMBS_DIR_HOST}     # /mnt/disks/scratch/blazorwebapp-thumbs
         target: /data/thumbs
         bind:
           propagation: rslave
     environment:
       Thumbnails__CachePath: /data/thumbs
       Thumbnails__MaxCacheBytes: "107374182400"            # 100 GB
     ```
  2. Boot order caveat: if the container starts before the SSD mounts, Docker creates the source dir on the RAM-backed root fs. The marker-file guard in the service (Step 3) prevents caching into it; also restart the container after boot if the guard logged a warning (same family of issue as PHASE_6 D8). Verify after any reboot: `grep scratch /proc/mounts`.
  3. Low-write policy (decision 7): write each thumbnail once via tmp file + rename on the same filesystem (no double write), no per-request writes (no last-access touches; `relatime` mount), eviction only above the 100 GB cap.
- [ ] Step 1 - **Container smoke test:** confirm Magick.NET (AnyCPU native libs) can decode/encode WebP inside `mcr.microsoft.com/dotnet/aspnet:8.0` (Debian). Quick check: build a tiny test endpoint or run the service in a throwaway container. If a native dependency is missing, add it to the infra Dockerfile (e.g. `apt-get install -y libgomp1`) and document it.
- [ ] Step 2 - Options + interface + service skeleton, DI registration as singleton.
- [ ] Step 3 - Cache key/path, **marker-file guard** (service reports `IsEnabled=false` and the endpoint redirects to the original when `<CachePath>/.thumbs-volume` is missing), source resolution with traversal guard, generation with concurrency cap + per-key dedupe + atomic publish, fallbacks.
- [ ] Step 4 - Minimal-API endpoint with conditional-request support and `Cache-Control`.
- [ ] Step 5 - `IOService.GetThumbnailUrl(path, tier)` helper.
- [ ] Step 6 - Tests: cache key stable and case-preserving; traversal (`..`, absolute paths, mixed separators) rejected; unsupported extension returns null; second call is a cache hit (generation runs once under concurrent calls); corrupted source returns null without leaving `.tmp` files; missing marker file disables caching.

#### Code sketch
```csharp
public sealed class ThumbnailOptions
{
    public const string SectionName = "Thumbnails";
    public string CachePath { get; set; } = "Data/thumbs";             // container: /data/thumbs (SSD bind mount)
    public string MarkerFileName { get; set; } = ".thumbs-volume";     // must exist in CachePath, else caching is disabled
    public long MaxCacheBytes { get; set; } = 100L * 1024 * 1024 * 1024; // housekeeping cap (Phase 5); 0 = unlimited
    public int CacheVersion { get; set; } = 1;                          // bump to invalidate everything
    public int Quality { get; set; } = 80;
    public int MaxConcurrentGenerations { get; set; } = 2;             // NAS has 4 shared cores
    public Dictionary<string, int> Tiers { get; set; } = new() { ["sm"] = 320, ["md"] = 640, ["lg"] = 1024 };
}

public sealed record ThumbnailResult(string FilePath, string ContentType);

public interface IThumbnailService
{
    /// <summary>False when the cache volume is missing/unmarked; callers must fall back to the original image.</summary>
    bool IsEnabled { get; }

    /// <summary>relativePath is the path under OutputDir using '/' separators, already URL-decoded.</summary>
    Task<ThumbnailResult?> GetOrCreateAsync(string tier, string relativePath, CancellationToken ct);
    Task InvalidateAsync(string relativePath);
}
```
```csharp
public async Task<ThumbnailResult?> GetOrCreateAsync(string tier, string relativePath, CancellationToken ct)
{
    if (!_options.Tiers.TryGetValue(tier, out var width)) return null;
    if (!IsEnabled) return null;                                              // marker file missing -> caller redirects to the original
    if (!Supported.Contains(Path.GetExtension(relativePath))) return null;   // .png .jpg .jpeg .webp

    var cacheFile = GetCacheFile(tier, relativePath);
    if (File.Exists(cacheFile)) return new ThumbnailResult(cacheFile, "image/webp");   // hot path: local disk only

    var source = ResolveSource(relativePath);                // null => outside OutputDir
    if (source is null || !File.Exists(source)) return null;

    var lazy = _inflight.GetOrAdd(cacheFile, key => new Lazy<Task>(() => GenerateAsync(source, key, width)));
    try { await lazy.Value.WaitAsync(ct); }
    catch (OperationCanceledException) { throw; }
    catch (Exception ex) { _logger.LogWarning(ex, "Thumbnail failed for {Path}", relativePath); return null; }
    finally { _inflight.TryRemove(new KeyValuePair<string, Lazy<Task>>(cacheFile, lazy)); }

    return File.Exists(cacheFile) ? new ThumbnailResult(cacheFile, "image/webp") : null;
}

private async Task GenerateAsync(string source, string cacheFile, int targetWidth)
{
    await _gate.WaitAsync();                                  // SemaphoreSlim(MaxConcurrentGenerations)
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
        var tmp = $"{cacheFile}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var image = new MagickImage(source))       // single read of the original through the fuse mount
            {
                if (image.Width > (uint)targetWidth)
                {
                    var height = (uint)Math.Max(1, Math.Round(image.Height * (double)targetWidth / image.Width));
                    image.Thumbnail((uint)targetWidth, height);    // keeps aspect, drops profiles/metadata
                }
                image.Format = MagickFormat.WebP;
                image.Quality = (uint)_options.Quality;
                await image.WriteAsync(tmp);
            }
            File.Move(tmp, cacheFile, overwrite: true);       // atomic publish
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
    finally { _gate.Release(); }
}

private string GetCacheFile(string tier, string relativePath)
{
    // Case is preserved on purpose: Linux paths are case-sensitive, two files may differ only by case.
    var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(relativePath))).ToLowerInvariant()[..40];
    return Path.Combine(_cacheRoot, $"v{_options.CacheVersion}", tier, hash[..2], hash + ".webp");
}

private string? ResolveSource(string relativePath)
{
    var root = Path.GetFullPath(_outputDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    var full = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    return full.StartsWith(root, StringComparison.Ordinal) ? full : null;   // path traversal guard
}
```
```csharp
// Program.cs (after UseRouting / MapBlazorHub)
app.MapGet("/thumb/{tier}/{**path}", async (string tier, string path, IThumbnailService thumbs, HttpContext ctx, CancellationToken ct) =>
{
    // Route values are already decoded - do NOT call Uri.UnescapeDataString again (a literal '%' in a filename would break).
    var result = await thumbs.GetOrCreateAsync(tier, path, ct);
    if (result is null)
        return Results.Redirect("/image/" + string.Join('/', path.Split('/').Select(Uri.EscapeDataString)));   // degrade to original
    ctx.Response.Headers.CacheControl = "public,max-age=604800";
    return Results.File(result.FilePath, result.ContentType, lastModified: File.GetLastWriteTimeUtc(result.FilePath));
});
```
```csharp
// IOService
public string GetThumbnailUrl(string path, string tier = "md")
{
    var url = GetImageStaticFile(path);                        // "/image/<relative>" (case preserved) or original for remote/other roots
    const string prefix = "/image/";
    return url.StartsWith(prefix, StringComparison.Ordinal) && IsThumbnailable(url)
        ? $"/thumb/{tier}/{url[prefix.Length..]}"
        : url;                                                  // video, audio, gif, http URLs: unchanged
}
```
Notes: `GetImageStaticFile` already returns URL-escaped-or-raw paths per current behavior - verify with files containing space, `%`, `#`, `+`, unicode, and uppercase/lowercase variants before relying on the round-trip.

#### Success Criteria
- First request for a thumbnail returns 200 WebP (miss, generated); second is served from the cache without touching the source (verify by logging or by making the source read-only/unreachable in a test).
- 24 simultaneous first-time requests for different files never run more than `MaxConcurrentGenerations` decodes at once; 24 requests for the same file decode once.
- Traversal attempts (`/thumb/md/../../etc/passwd`) return the redirect/404, never a file outside `OutputDir`.
- Unsupported/corrupt sources fall back to the original URL; no `.tmp` leftovers.
- With the SSD unmounted (marker file absent) the app still shows every image (originals) and logs one clear warning; nothing is written to the container filesystem.
- Works on Windows and in the Linux container.

---

### Phase 4: Wire thumbnails into the UI
**Objective:** Every grid/list that shows many images uses `sm`/`md`; originals only in the viewer.
**Complexity:** 3 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - `GalleryImageTile` and `ImageCard`: `src` -> `IO.GetThumbnailUrl(Image.Path, "md")` (use `lg` when the gallery tile size is `large`; pass the tier from `InfiniteScrollMasonry` via a parameter rather than injecting settings into each tile).
- [ ] Step 2 - Smaller consumers use `sm`: `SelectionCard`, `ImagePickerDialog`, `Cleanup` preview strips, `CharacterShotCard`, workshop thumbnails (grep `GetImageStaticFile` and decide per usage; do not touch usages that need the original, e.g. AssetViewer main image, img2img inputs, anything that reads bytes).
- [ ] Step 3 - `AssetViewer`/`ImageViewer`: show the `lg` thumbnail instantly as a placeholder behind the original (original `<img>` fades in on `load`). Prev/next preloads originals only for the neighbours.
- [ ] Step 4 - Optional `srcset` on tiles (`md 640w, lg 1024w`) with `sizes` derived from the column count, so retina/large tiles pick `lg` automatically.
- [ ] Step 5 - (Optional extension, separate small step) Apply the same mapping to `/files/resource_previews/...` and `/files/danbooru/...` via a generic root table in `ThumbnailService` (`prefix -> root dir`), since those grids also load full-size originals.

#### Success Criteria
- Gallery page transfer size drops by an order of magnitude versus the Phase 1 baseline; full-size images are only requested when the viewer opens.
- No broken images: videos/audio/GIF tiles unchanged; files that fail to thumbnail still show via redirect.
- Viewer still shows the true original after load.

---

### Phase 5: Invalidation and cache housekeeping (on-demand only)
**Objective:** Thumbnails are created only when something actually displays an image (decision 2); deletes/overwrites clean up after themselves; the SSD cache cannot grow without bound. **No backfill, no warm-on-save by default** - a large part of the library will be cleaned up, so nothing is processed ahead of need.
**Complexity:** 3 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - `ThumbnailInvalidationService` (hosted service) that subscribes through `IEventService` (pattern: `Services/Cleanup/CleanupIndexingQueue.cs`): on `ImageRecordDeletedEventArgs` call `InvalidateAsync(relativePath)` (deletes that key's file in every tier); on `ImageRecordSavedEventArgs` with `IsUpdate == true` also invalidate (file may have been overwritten). Bulk cleanup deletes (Cleanup tools) publish one event per image - keep the handler cheap (three `File.Delete` calls, no directory scans).
- [ ] Step 2 - Housekeeping: cache stats (file count, total bytes), a "clear thumbnail cache" action, and an **eviction pass** when `Thumbnails:MaxCacheBytes` (100 GB) is exceeded (delete oldest files by last-write time until under ~90% of the cap; run at startup and then at a low frequency, never on the request path). At typical thumbnail sizes the cap should almost never be reached, which keeps SSD deletes/rewrites minimal (decision 7). Surface stats/clear in `GallerySettings` or the existing maintenance area following the design language.
- [ ] Step 3 - (Optional, default **off**) `Thumbnails:WarmOnSave`: enqueue `md`/`sm` for newly generated images through a bounded channel. Only enable if first-view latency on fresh generations is noticeable; it processes images the user may later delete, which is exactly what decision 2 avoids.
- [ ] Step 4 - (Optional) Next-page prefetch is **not** needed: the browser requests tiles as they enter the viewport and the service dedupes concurrent requests.

#### Code sketch
```csharp
public sealed class ThumbnailInvalidationService : IHostedService
{
    private readonly IEventService _events;
    private readonly IThumbnailService _thumbs;
    private readonly IIOService _io;

    public Task StartAsync(CancellationToken ct)
    {
        _events.Subscribe<ImageRecordDeletedEventArgs>(OnDeleted);
        _events.Subscribe<ImageRecordSavedEventArgs>(OnSaved);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        _events.Unsubscribe<ImageRecordDeletedEventArgs>(OnDeleted);
        _events.Unsubscribe<ImageRecordSavedEventArgs>(OnSaved);
        return Task.CompletedTask;
    }

    private void OnDeleted(ImageRecordDeletedEventArgs a) => _ = _thumbs.InvalidateAsync(ToRelative(a.ImagePath));
    private void OnSaved(ImageRecordSavedEventArgs a) { if (a.IsUpdate) _ = _thumbs.InvalidateAsync(ToRelative(a.ImagePath)); }

    // Must produce exactly the key used by GET /thumb/{tier}/{path}: the part of IOService.GetImageStaticFile(path) after "/image/".
    private string ToRelative(string path) { /* ... */ }
}
```
Use the existing `IEventService.Subscribe/Unsubscribe` signatures (check `Services/EventService.cs`).

#### Success Criteria
- Nothing is generated for images that are never displayed (verify: cache file count stays flat while only the DB is browsed/cleaned).
- Deleting an image removes its thumbnails; no orphan growth after a bulk cleanup.
- With a tiny `MaxCacheBytes` in a test, eviction brings the cache under the cap without touching in-flight files.

---

### Phase 6: Folder (project) covers
**Objective:** Project/folder covers are cheap, stable and small.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Files
`Components/Shared/Project/ProjectCard.razor(.css)`, `Components/Shared/ProjectSelectorDropdown.razor`, `Services/DatabaseService.cs` / `IDatabaseService.cs`, new `Services/ProjectCoverService.cs`, `Components/Gallery/GallerySettings.razor` (cover reset at ~line 577), `Events/` (new event).

#### Steps
- [ ] Step 1 - **`IProjectCoverService`**: `GetCoverPathsAsync(IEnumerable<int> projectIds)` returns `projectId -> image path?` using at most two queries total (projects' `SampleImagePath`, then a fallback query for the remaining ids). Memoize results in a `ConcurrentDictionary`; `Invalidate(projectId?)` clears entries.
- [ ] Step 2 - **Deterministic fallback (decision 4): the project's latest generated image** (newest `Id`/`DateCreated`, not hidden; no favorite required; no more `ORDER BY random()`). Skip audio files; a video is used only when the project has no raster image. An explicit `SampleImagePath` always wins. Resolve with one indexed `ORDER BY Id DESC LIMIT 1` query per uncovered project (memoized), or one grouped query if EF Core 6 / SQLite translates it.
- [ ] Step 3 - **Invalidation by events**: add `ProjectCoverChangedEventArgs { ProjectId }` published from `DatabaseService.SetProjectCover` and from the "reset cover" action in `GallerySettings`; the cover service subscribes via `IEventService`. Because the fallback is the *latest* image, also invalidate that project's entry on `ImageRecordSavedEventArgs` / `ImageRecordDeletedEventArgs` (only when the project has no explicit cover) so a new generation becomes the cover.
- [ ] Step 4 - **`ProjectCard`**: take the cover from the service (parent lists can prefetch the whole map once via `GetCoverPathsAsync` and pass the path as a parameter to avoid N awaits), use `IO.GetThumbnailUrl(path, Compact ? "sm" : "md")`, and stop re-resolving on every `OnParametersSetAsync` (resolve only when `ProjectId` changes). Convert its `@onmouseenter/@onmouseleave` hover to CSS `:hover` (same pattern as `GalleryImageTile`, PHASE_6 D7); keep JS play/pause only for video covers.
- [ ] Step 5 - **`ProjectSelectorDropdown`**: same thumbnail URL helper (`sm`).
- [ ] Step 6 - Video covers: keep the hover-play `<video>` but use `preload="none"` until hover (avoids N metadata fetches through the fuse mount); a poster-frame thumbnail needs ffmpeg and is deferred.

#### Code sketch
```csharp
public async Task<IReadOnlyDictionary<int, string?>> GetCoverPathsAsync(IEnumerable<int> projectIds)
{
    var ids = projectIds.Distinct().ToList();
    var result = new Dictionary<int, string?>();
    var missing = ids.Where(id => !_cache.TryGetValue(id, out _)).ToList();

    if (missing.Count > 0)
    {
        await using var ctx = await _factory.CreateDbContextAsync();
        var explicitCovers = await ctx.Projects.AsNoTracking()
            .Where(p => missing.Contains(p.Id) && p.SampleImagePath != null && p.SampleImagePath != "")
            .Select(p => new { p.Id, p.SampleImagePath }).ToListAsync();
        foreach (var c in explicitCovers) _cache[c.Id] = c.SampleImagePath;

        foreach (var id in missing.Where(id => !_cache.ContainsKey(id)))
        {
            // Latest generated image of the project (decision 4). Indexed, deterministic, memoized.
            var latest = await ctx.Images.AsNoTracking()
                .Where(i => i.ProjectId == id && !i.IsHidden)
                .OrderByDescending(i => i.Id)
                .Select(i => i.Path).Take(50).ToListAsync();           // small window so audio can be skipped in memory
            _cache[id] = latest.FirstOrDefault(p => !IsAudio(p) && !IsVideo(p))
                      ?? latest.FirstOrDefault(p => IsVideo(p));        // video only when there is no raster image
        }
    }
    foreach (var id in ids) result[id] = _cache.GetValueOrDefault(id);
    return result;
}
```
(Check the `Images.ProjectId` index exists in `AppDbContextModelSnapshot.cs`; if not, adding an index needs a migration - follow `Documentation/Architecture/03-PERSISTENCE-AND-MIGRATIONS.md` and discuss first.)

#### Success Criteria
- Opening the gallery settings/project list issues a constant number of cover queries regardless of project count, and covers do not change on re-render.
- Cover images are `sm`/`md` thumbnails (verify transfer sizes); video covers do not fetch until hovered.
- Setting/resetting a cover updates the card without a page reload.

---

### Phase 7: Transport - optional LAN-only HTTP/2 (decide after Phase 4)
**Objective:** Only if measurements still show request queueing after thumbnails: remove the HTTP/1.1 six-connection limit **without exposing anything beyond the LAN**.
**Complexity:** 3 points
**Status:** [ ] Not Started - **optional**, gated on Phase 4 numbers

Background: Kestrel serves cleartext on 8189; browsers only speak HTTP/2 over TLS, so many parallel requests queue behind ~6 connections. After Phase 4 each tile is tens of KB instead of ~1 MB, so this may stop mattering - re-measure before doing anything. Nothing in this phase opens ports to the internet, adds DNS records, or sets router forwards (decision 5).

#### Options (pick at the gate)
| Option | What | LAN-only? | Notes |
|--------|------|-----------|-------|
| A (preferred if needed) | Kestrel serves HTTPS directly on a second LAN port (e.g. 8190) with a certificate from a **private local CA** installed on the devices that use the app (desktop, Steam Frame). HTTP/2 is automatic over TLS | Yes - no proxy, no new moving parts, no exposure | Needs a cert + key file mounted into the container (`ASPNETCORE_Kestrel__Certificates__Default__Path/Password`, stored outside git), and trusting the CA once per device |
| B | Nginx Proxy Manager proxy host to `10.0.0.11:8189` with HTTP/2 + WebSockets on | Only if configured so: the existing `npm` container publishes 80/443 on host ports 1880/18443 and **may already be forwarded by the router for the personal website**. Check router forwards first; use a hostname with **no public DNS record** and an NPM **Access List restricted to the LAN subnet**; never reuse the website's public host | More moving parts; shared with the website, so a mistake could expose the app |
| C | Do nothing | Yes | Valid outcome if Phase 4 numbers are good |

Coordination: the **steam-frame-vr** plan also needs HTTPS (WebXR secure context). Option A's local-CA approach would serve both; decide once.

#### Steps
- [ ] Step 1 - Re-measure after Phase 4 (requests queued/stalled in DevTools "Waiting for connection" timings). If acceptable, mark the phase "not needed".
- [ ] Step 2 - If needed: pick A or B with the user; for B first verify (read-only) router/NPM exposure; take a backup of NPM config before any change.
- [ ] Step 3 - Implement, verify circuits (SignalR on WebSockets), uploads (2 GB limit; proxy `client_max_body_size` for B), and re-measure.

#### Success Criteria
- If implemented: DevTools shows `h2` for `/thumb/...`; the app is reachable only from the LAN (verify from a non-LAN network / mobile data that nothing answers); SignalR and uploads unaffected.

---

### Phase 8: NAS / rclone / WSL review (evidence-driven)
**Objective:** Decide, with measurements, whether the vault mount and WSL/NAS data paths deserve changes now that thumbnails take the gallery off the hot path.
**Complexity:** 5 points
**Status:** [ ] Not Started

Principle: after Phases 2-4 the fuse mount serves originals only to the viewer and to generation I/O. Re-measure before tuning; every item below is a *candidate*, not a commitment.

#### Candidates
| # | Candidate | Value / trade-off |
|---|-----------|-------------------|
| 1 | ~~Storage tier for hot data / move the rclone vfs cache to the SSD~~ **Rejected (decision 7).** The thumbnail cache is the only thing on the SSD; the vfs cache is write-heavy (whole-file caching of everything read) and the drive has a SMART warning. The DB also stays on the array. Revisit only if a healthy, larger SSD is added | None - no change |
| 2 | **Separate rclone mounts per workload.** `--dir-cache-time 2s` exists for the app's write-then-read race; Stash (large sequential video) prefers long dir cache and big read-ahead. A second mount of the same crypt remote with different flags for Stash, leaving `vault-local:` at 2 s for the app | Removes a trade-off both workloads currently share. Costs one more fuse process + bind-mount changes for Stash (shared service: ask first) |
| 3 | **rclone flag review.** `--vfs-cache-max-age` (default 1 h; raise if the vfs cache disk is large and originals are re-viewed), `--vfs-cache-poll-interval`, `--buffer-size`, `--vfs-read-ahead` (for video), `--attr-timeout`; enable `--rc --rc-addr 127.0.0.1:5572` to read `vfs/stats` / `core/stats` while measuring | Only change what measurements justify. rc is localhost-only |
| 4 | **Crypt directory-name encryption cost.** With name encryption every stat re-lists and decrypts all names in the directory (see PHASE_6 D6). Large flat dirs under `Text-2-Image/_samples/<model>/<sampler>` make that expensive. Mitigation without re-encrypting: keep app hot paths off the mount (thumbnails do this); do not run recursive scans on the mount | Re-encrypting the vault is out of scope |
| 5 | ~~ComfyUI result transfer over HTTP instead of shared files~~ **Rejected for now (decision 3).** The file-based flow works, keeps all data on the NAS, and the race is handled by the 2 s dir-cache + the app's bounded retry. HTTP (`/view`, `/upload/image`) would only change *how the app obtains the bytes* (data would still be stored on the NAS); its sole advantage is removing dependence on fuse cache timing, which would allow a longer TTL. Since thumbnails take the gallery off the mount and the app does exact-path reads, a longer TTL is not needed. Reconsider only if the "file not visible yet" race returns | None - no code change |
| 6 | **WSL side.** `nas-vault:` (crypt over SMB) is write-only for ComfyUI and encrypts on the PC (good). Review CIFS options for `/mnt/nas` (models): `vers=3.1.1`, `cache=`, `rsize/wsize`, multichannel; consider keeping hot checkpoints on local NVMe. Affects model load time, not the gallery | Low priority for this plan |
| 7 | **Boot-order guard** (carried over from unraid-hosting): wait-for-mount in `rcloneMountVault` and restart consumers, or make compose services depend on the mount | Prevents the stub-dir poisoning incident (PHASE_6 D8) |
| 8 | **Duplicate storage check (read-only).** ComfyUI writes `Images/comfyui/output`, and the app also saves its own copy under `Images/StableDiffusion/...`. Verify whether both copies are kept; if so, decide retention (e.g. include `comfyui/output` in the planned library cleanup). Disk-space/tidiness item, not a speed item | Informational |

#### Steps
- [ ] Step 1 - Re-run the Phase 1 measurements after Phases 2-4/7; record fuse CPU, vfs cache hit rate, and request latency for originals.
- [ ] Step 2 - For each candidate: state expected value from the measurements, risk, rollback; present to the user; implement only approved ones (one at a time, backups first, infra notes updated in `infra/wsl/CLAUDE.md` / `infra/unraid/CLAUDE.md`).

#### Success Criteria
- A written decision log (adopted / rejected / deferred, with evidence) in `PHASE_8.md`; adopted changes verified with before/after numbers and documented in the infra repo.

---

## Open Questions
First-round questions and the SSD questions are **resolved** (see "Decisions Recorded" at the top). Remaining:
| # | Question | Needed by |
|---|----------|-----------|
| O1 | Phase 7 gate: only if Phase 4 numbers still show queueing - Option A (Kestrel HTTPS, local CA) or B (NPM, LAN-restricted)? | After Phase 4 |
| O2 | Check the router for port forwards to the NAS (NPM 1880/18443) before any proxy work - does the user know whether the website is forwarded? | Phase 7 (option B only) |
| O3 | Reseat/replace the SSD's SATA cable (CRC error count 31)? Optional hygiene, not blocking | Anytime |

---

## Stress Points & Risks
| Risk | Mitigation | Complexity |
|------|------------|------------|
| Magick.NET native WebP support missing in the Linux image | Phase 3 Step 1 smoke test first; add the needed apt package to the infra Dockerfile | 2 |
| Thumbnail generation starves the 4-core NAS | On-demand only (no backfill), concurrency cap (default 2), per-key dedupe | 2 |
| Path traversal / arbitrary file read through `/thumb` | Resolve against `OutputDir`, ordinal prefix check, tests for `..` and absolute paths; only allow whitelisted extensions | 3 |
| URL round-trip bugs (spaces, `%`, `#`, unicode, case) between `/image` and `/thumb` | Single helper in `IOService`, tests with awkward filenames, no double-decoding | 2 |
| Stale thumbnails after a file is overwritten in place | Invalidate on `ImageRecordSaved(IsUpdate)`/`Deleted`; `CacheVersion` bump; short browser TTL (7 days) | 2 |
| Cache directory growth (a few GB at most for the images actually browsed; ~7 GB at `md` if all 120k were ever viewed) | `MaxCacheBytes` cap + eviction (Phase 5), "clear cache" action, 1 TB SSD | 1 |
| SSD has a SMART warning / may fail | Cache is fully regenerable, nothing irreplaceable on it; service degrades to originals when the volume is missing or unmarked; check `smartctl` in Phase 3 Step 0 | 2 |
| SSD not mounted when the container starts (Docker would create the path on the RAM-backed root fs) | Marker-file guard disables caching; `rslave` bind propagation; restart container after boot if a warning was logged | 3 |
| Unassigned Devices plugin/mount changes touch an SSD that already has data | Read-only inspection first; never format without explicit consent | 2 |
| Proxy/TLS work accidentally exposes the app beyond the LAN (NPM may already be port-forwarded) | Phase 7 is optional; prefer Kestrel TLS on the LAN (option A); for NPM verify router forwards, use a non-public hostname and a LAN-only access list, test from a non-LAN network | 3 |
| Masonry rewrite regresses edge cases (remove item, resize, tile-size change, project switch) | Keep public exports, test each trigger manually in Pure and Rich modes, relayout fallback | 5 |
| Proxy breaks SignalR or large uploads (only if Phase 7 option B is chosen) | Enable WebSockets, raise body size, test generate/upload flows | 3 |
| rclone/NAS changes affect Stash/stashmanager (shared vault) | Read-only first, backups, user confirmation, one change at a time | 3 |

---

## Changelog
| Phase | Changes |
|-------|---------|
| Planning | Initial plan created from the post-deploy gallery investigation (hover fix shipped in unraid-hosting Phase 6). Findings: full-size originals as tiles, masonry layout gated on all images (incl. lazy ones, 3 s fallback), per-card random cover queries, HTTP/1.1 without proxy, NAS has no SSD pool (4-core i5-4670K) |
| Planning (rev 2) | User decisions recorded: dedicated unassigned SSD (Unassigned Devices plugin, marker-file guard) for the thumbnail cache; on-demand-only generation (Phase 5 reduced to invalidation + housekeeping, backfill dropped); file-based ComfyUI I/O kept (HTTP candidate rejected); covers fall back to the project's latest image; LAN-only (Phase 7 made optional, Kestrel-TLS option added, NPM exposure caveat); Rich mode untouched. SSD probed read-only: Samsung 860 EVO 1 TB, xfs, unmounted, UD plugin not installed |
| Planning (rev 3) | SSD prepared (Phase 3 Step 0): SMART checked (healthy; warning = UDMA CRC count 31), Unassigned Devices plugin installed, SSD reformatted xfs `scratch` with user approval, auto-mount enabled and verified, mounted at `/mnt/disks/scratch`, `blazorwebapp-thumbs/` + `.thumbs-volume` marker created. Cache cap raised to 100 GB; low-write policy adopted; rclone vfs cache stays on the array (candidate rejected) |

---

## References
- `Documentation/Plans/unraid-hosting/` (PHASE_4 D6, PHASE_6 D5-D8) - mount topology, dir-cache TTL, hover fix, vault incident
- `Documentation/Plans/gallery-density-optimization/` - earlier tile density work (masonry sizing)
- `Documentation/Plans/steam-frame-vr/MAIN_PLAN.md` - shares the HTTPS/auth prerequisite (Phase 7 here)
- `infra/unraid/CLAUDE.md`, `infra/wsl/CLAUDE.md` - NAS/WSL facts, remount procedure, boot-order risk
- Code: `wwwroot/js/InfiniteMasonry.js`, `Components/Gallery/InfiniteScrollMasonry.razor`, `GalleryImageTile.razor`, `Components/Shared/Image/ImageCard.razor`, `Components/Shared/Project/ProjectCard.razor`, `Services/IOService.cs`, `Services/DatabaseService.cs`, `Services/Cleanup/CleanupIndexingQueue.cs` (queue + EventService pattern), `Events/ImageRecordSavedEventArgs.cs`, `Events/ImageRecordDeletedEventArgs.cs`, `Program.cs`
