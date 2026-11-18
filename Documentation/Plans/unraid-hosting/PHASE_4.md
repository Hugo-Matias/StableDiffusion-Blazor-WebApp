# Phase 4: Deploy to Unraid and End-to-End Verification

> **Main plan:** [MAIN_PLAN.md](./MAIN_PLAN.md)
> **Status:** [x] Complete and tested — txt2img generation round-trip deferred to user (GPU busy with LM Studio); all other success criteria met, evidence in §3
> **Complexity:** 3 points
> **Depends on:** Phase 3 (image pushed to the private registry and cached on the NAS). The live deploy runs image `10.0.0.11:5000/blazorwebapp:2026-11-11c` — a rebuild of the Phase 3 image carrying two post-deploy fixes (see §4 D3/D4).

---

## 1. Objective

App live at `http://10.0.0.11:8189` with a full generation round-trip through desktop ComfyUI, verified from a device other than the desktop.

**Result:** App is live and fully verified except for one txt2img generation, which the user will run themselves once the GPU frees up (it was busy serving LM Studio). Everything else — reachability, in-app health status, resource previews, danbooru library, DB persistence across container recreation — passed with evidence in §3.

---

## 2. Steps

### Step 1 — Port conflict check + compose project setup
- [x] Check port 8189 is free on the NAS (`ss -tlnp`, `docker ps --format '{{.Names}}\t{{.Ports}}'`) — free; kept 8189.
- [x] Verify the vault subpaths exist (PHASE_2 O2) — one guess was wrong: resources live under `/mnt/user/array/Programas/Stable-Diffusion/Models`, not `/mnt/user/Programas/...`. Corrected in the real `.env` on the NAS and in `.env.example`.
- [x] Create `/boot/config/plugins/compose.manager/projects/blazorwebapp/` with `docker-compose.yml` + a real `.env` (backups kept as `<file>.bak-*`).

### Step 2 — Desktop ComfyUI reachable from the NAS
- [x] ComfyUI running on the desktop — it runs in WSL Ubuntu via Docker (`docker compose -f /home/hugo/comfyui/docker-compose.yml up --build --force-recreate`), listening on `[::]:8188`.
- [x] Reachability verified: `ssh nas "curl http://10.0.0.2:8188/system_stats"` returned live JSON (ComfyUI 0.45.0, frontend 1.58.5). Addressing via WSL mirrored networking — see §4 D1/D2.

### Step 3 — Deploy the container
- [x] `docker compose up -d` on the NAS; container Up with clean logs. Image pinned to `2026-11-11c` after the post-deploy fixes (D3/D4).

### Step 4 — End-to-end verification
- [x] App loads from PC at `http://10.0.0.11:8189` (full prerendered page, zero console errors after the D4 fix)
- [x] ComfyUI connection status healthy in the app (Generate nav link enabled; 30 s health poll live)
- [~] One txt2img generation with previews streaming through the NAS-hosted app — **deferred to user** (GPU busy with LM Studio); all preconditions verified (health check, WS path, outputs mount)
- [x] DB state persists across `docker compose pull && up -d --force-recreate`

---

## 3. Test Evidence

### Step 1 — port + project setup
- `ss -tlnp | grep :8189` → empty (free). Port kept at 8189.
- Project directory created with the compose assets; `.env` values verified read-only against actual NAS paths before first `up`.

### Step 2 — reachability
- From the NAS: `curl http://10.0.0.2:8188/system_stats` → live JSON, ComfyUI version 0.45.0, required frontend 1.58.5, templates 0.11.106. (A PC-side curl to the same endpoint returned `http_code=000` within 10 s while the GPU was busy — transient; the NAS→WSL path is what matters and it works.)

### Step 3 — container
- `docker ps`: `blazorwebapp-blazorwebapp-1 Up`; logs clean after startup.

### Step 4 — verification
- **App load:** full page at `http://10.0.0.11:8189` renders (prerender takes ~50–110 s against the 120k+ row DB; curl needs `--max-time 180`). After the D4 fix, browser console is clean on fresh loads and all `/image/...` requests → 200.
- **ComfyUI status:** Generate nav link not disabled in the /generate snapshot (i.e., `IsBackendAvailable == true` and a workflow resolved for the current base); the backend health poller runs every 30 s against `http://10.0.0.2:8188`.
- **Resource previews:** /resources page renders fully; all `/files/resource_previews/...` requests → 200 (mp4s → 206 Partial Content, normal for range requests). ~25 preview files confirmed loading.
- **Danbooru library:** the /danbooru Library tab shows **17 cards — exactly matching the 17 `SavedDanbooruMedia` rows** in the ported DB (Explicit/Questionable/Sensitive ratings with scores, dimensions, formats JPG/PNG). All 17 `/files/danbooru/...` image requests → 200. Note: an initial "Library is empty" was a lazy-load quirk, not missing data — see §4 F5.
- **DB persistence:** `docker compose pull && up -d --force-recreate` on the NAS → container recreated cleanly; `/mnt/user/appdata/blazorwebapp/BlazorWebApp.db` intact at **551,526,400 bytes** (byte-identical to pre-recreation); app serves HTTP 200 with the full page after recreation.
- **Generation:** deferred — user will run a txt2img themselves once the GPU frees up (it was busy serving LM Studio).

---

## 4. Decisions & Findings

### D1 — Reachability via WSL mirrored networking (not Tailscale)
The plan assumed desktop addressing via Tailscale, but Tailscale was logged out on the PC at deploy time. ComfyUI runs inside WSL Ubuntu (Docker), and WSL2 in **mirrored networking** mode exposes the WSL IP (`10.0.0.2`) directly on the LAN — so `COMFYUI_BASE_URL=http://10.0.0.2:8188` works from the NAS without Tailscale. `.wslconfig` was backed up to `.wslconfig.bak-20261111` before enabling mirrored mode; a Windows firewall rule "ComfyUI WSL inbound TCP 8188" allows LAN access. **Follow-up:** re-login Tailscale on the PC and consider re-pointing `COMFYUI_BASE_URL` to the stable Tailscale address (the WSL IP can change across reboots/network changes).

### D2 — ComfyUI-in-WSL launch facts
ComfyUI is a Docker container in WSL Ubuntu: `docker compose -f /home/hugo/comfyui/docker-compose.yml up --build --force-recreate`; the process runs `/opt/conda/envs/comfy/bin/python .../comfy --skip-prompt --workspace /opt/ComfyUI launch -- --listen * --port 8188 --preview-method auto --use-sage-attention`. Version 0.45.0, required frontend 1.58.5, templates 0.11.106 (from `/system_stats`). WSL sudo requires a password (interactive). **The full Comfy/WSL setup audit is scoped as [PHASE_6.md](./PHASE_6.md).**

### D3 — Post-swap 500 fix, shipped in image `2026-11-11c`
After the DB swap (D5), some pages returned 500. Root cause: an `UncheckedColor` string literal in rating/color handling that only surfaced with ported data. Fixed and rebuilt as **`2026-11-11c`** (manifest list digest `sha256:4f01f4fc881226b21965daeab44aa32ddf999cd723816017c45afd6460d06b6e`), pushed to the registry, pinned in the NAS `.env`. Open question recorded: the MudRating/Color anomaly and why the previously published build didn't hit it (stale publish).

### D4 — Case-sensitivity fix (two-part), shipped in `2026-11-11c`
On Linux, ASP.NET Core's `PhysicalFileProvider` file lookup is case-sensitive, while `Parser.NormalizePath` lowercased paths — so DB-stored paths with on-disk casing (e.g., `Aniverse`, `LORA`) produced 404s on `/image/...`. Fix:
1. `Parser.NormalizePath(path, preserveCase: false)` — new optional parameter; the default keeps old lowercasing for existing callers.
2. `IOService.GetImageStaticFile` now normalizes twice (lowercased for prefix detection, case-preserving to build the URL) so generated URLs keep on-disk casing.

Verified in browser after deploy: all `/image/...` requests → 200, zero console errors. The DB itself was also corrected once (`BlazorWebApp.casefixed.db`) — 57 path rows remapped for case-only mismatches (`AniVerse`→`Aniverse` checkpoint samples; `LoRA`→`LORA` in three numbered LORA sample folders); the mapping is kept at `C:\Users\hugol\AppData\Local\Temp\dbport\pathfix_mapping.csv`.

### D5 — DB porting (scope change, user-requested)
The published project's DB (`E:\Documentos\GitHub Repos\Stable-Diffusion-WebUI-Publish\BlazorWebApp.db`) was ported to the NAS so all built-up data survives: **162,421 rows** rewritten with paths remapped from the desktop layout to the NAS mount layout (per-table counts 120,139 / 8,326 / 107 / 33,849); zero disk-path leftovers; 50 legacy `E:`/`H:` rows intentionally untouched; SQLite `integrity_check` ok. Deployed as `/mnt/user/appdata/blazorwebapp/BlazorWebApp.db` (551,526,400 bytes). All DB work was done with native Windows Python 3.11 (`PYTHONIOENCODING=utf-8` for CJK output) — the file lives on the Windows filesystem.

### D6 — Gallery performance: vault mount topology fix
After deploy, gallery image loads were slow: the Unraid-side rclone mount of the encrypted vault was **uncached**, while the Windows N: drive (nssm service `RCloneNasVault`) uses `--vfs-cache-mode full --vfs-cache-max-size 100G`. Fix executed and verified:
- New rclone remote **`vault-local:`** — crypt over the **local** path `/mnt/user/vault` with the same credentials as `nas-vault:` (password + password2, no salt key) — kills the SMB self-loopback (`nas:` → smb to 10.0.0.11).
- Persistent User Script updated at `/boot/config/plugins/user.scripts/scripts/rcloneMountVault/script` with VFS cache flags mirroring the Windows service: `--vfs-cache-mode full --vfs-cache-max-size 100G --cache-dir /mnt/user/appdata/rclone-vault-cache` (reboot-persistent).
- Remount performed via container stop/start; a lingering old `nas-vault` fuse process was killed after unmount.

Verified: cold image fetch ~42 ms, warm ~9.5 ms (HTTP 200, full size); browser gallery all `/image/...` → 200 with zero console errors. User confirmed "much better". rclone gotchas hit along the way: v1.76 has no `config copy`; the `config create` "found key without value" quirk was worked around by appending the section directly to the config file.

### F1 — DataProtection keys not persisted
Keys live in `/root/.aspnet/DataProtection-Keys` inside the container and are lost on recreation: active Blazor circuits break (browsers reconnect), DB is unaffected. Acceptable for v1; a bind mount for the key ring is a candidate improvement.

### F2 — Prerendering cost
Full page loads prerender server-side against the 120k+ row DB (~50–110 s). `curl` needs `--max-time 180`. Static `/image/...` and `/files/...` requests bypass prerender (fast).

### F3 — Danbooru library is DB-driven
Listing comes from the `SavedDanbooruMedia` table (`GetPagedAsync`), not a disk walk. Files live at `{SavedMediaPath}/{ratingFolder}/{scoreBucket}/{postId}.{ext}` (rating folders general/sensitive/questionable/explicit/none; score buckets score_0 <100, score_100 100–199, score_200 200–299, score_300 ≥300).

### F4 — Container env (as deployed)
`BLAZORWEBAPP_IMAGE=...:2026-11-11c`, `HTTP_PORT=8189`, `COMFYUI_BASE_URL=http://10.0.0.2:8188`, empty `COMFYUI_OUTPUTS_PATH`/`COMFYUI_INPUTS_PATH` (graceful degradation), `DANBOORU_DIR_HOST=/mnt/user/mount/vault/Images/Danbooru`. Mounts: `/app/data`←`/mnt/user/appdata/blazorwebapp`; `/data/outputs`←`/mnt/user/mount/vault/Images/StableDiffusion` (through the rclone chain); `/data/resources`←`/mnt/user/array/Programas/Stable-Diffusion/Models`; `/data/previews`←resource_previews; `/data/danbooru`←vault Danbooru folder.

### F5 — Danbooru Library lazy-load quirk (UX, not data)
`Danbooru.razor`'s `_activeTabIndex` setter only fires `LoadLibrary()` when the tab index **changes**; active-tab state is persisted, so if Library was already the active tab at page load, lazy loading never fires and "Library is empty" persists until the user clicks Filter (which calls `LoadLibrary()` directly) or switches tabs. On failure a snackbar shows and `_libraryLoaded` resets to allow retry. Candidate improvement: also trigger the load in `OnInitializedAsync` when the persisted tab is Library.

### F6 — Deferred items
- txt2img generation round-trip through the NAS-hosted app (user will run; GPU was busy with LM Studio).
- Re-login Tailscale on the PC, then consider re-pointing `COMFYUI_BASE_URL` to the Tailscale address (D1 follow-up).
- Point the desktop ComfyUI output dir at a NAS-visible folder + set `COMFYUI_OUTPUTS_PATH` (final results are read from disk; WS previews alone aren't sufficient) — part of [PHASE_6.md](./PHASE_6.md) scope.
