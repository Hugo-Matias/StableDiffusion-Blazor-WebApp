# Phase 6: WSL ↔ Unraid Connectivity and Comfy Setup Audit

> **Main plan:** [MAIN_PLAN.md](./MAIN_PLAN.md)
> **Status:** Complete — Steps 1–4 ✅. Step 3 (vault I/O) deployed and verified end-to-end 2026-11-12: migration integrity-checked, race fix shipped in image `2026-11-12`, fuse dir-cache root cause fixed (`--dir-cache-time 2s`), generation test passed. Step 4 (gallery responsiveness) shipped in image `2026-11-12b` the same day, including recovery of a dead vault host mount (D8).
> **Complexity:** 5 points
> **Depends on:** Phase 4 findings ([PHASE_4.md](./PHASE_4.md) D1/D2/F6). Independent of Phase 5 — either order works.

---

## 1. Objective

A broad, read-first investigation of the desktop Comfy/WSL setup and its connectivity to the Unraid NAS, so future Comfy-related work is informed:

- Report on the **overall ComfyUI setup** (Docker compose in WSL Ubuntu, networking mode, versions, workspace/model layout).
- Establish how the NAS-hosted app can reach ComfyUI **input/output images** — both live inside WSL; the desktop connects fine, but the NAS↔WSL file path is the missing link for final-result retrieval (`COMFYUI_OUTPUTS_PATH` was empty and degraded gracefully).
- Add **WSL as a new device in the infra repo** (`infra/wsl/` + its own CLAUDE.md) with verified facts, per that repo's one-subfolder-per-device convention.

## 2. Verified Facts (audited 2026-11-11)

### WSL / ComfyUI side
- Distro: Ubuntu on WSL2 in **mirrored networking** mode; `.wslconfig` sets memory=64GB, swap=32GB (backup `.wslconfig.bak-20261111`). Mirrored mode shares the host's network namespace → container ports are reachable at the **PC's LAN IP** (currently `10.0.0.2`, DHCP Ethernet 4).
- Docker is the **native Docker Engine inside WSL** (`dockerd` under systemd, v28.4.0) with NVIDIA Container Toolkit — NOT Docker Desktop. GPU via `capabilities: [gpu]` reservation in compose.
- ComfyUI 0.45.0 (frontend 1.58.5, templates 0.11.106 per `/system_stats`) via comfy-cli: `comfy --skip-prompt --workspace /opt/ComfyUI launch -- --listen * --port 8188 --preview-method auto --use-sage-attention`; conda env in a named volume; torch cu130.
- Compose file `/home/hugo/comfyui/docker-compose.yml` (repo origin `https://github.com/Hugo-Matias/comfyui-docker-wsl.git`). Key volumes: core from `/mnt/e/Programas/ComfyUI`, user data + input from `/mnt/e/Programas/ComfyUI_Data`, models via CIFS mount `/mnt/nas` → `//NAS/array`, **output = `./output` (WSL ext4 — not NAS-visible)**, conda named volume.
- Windows firewall rule "ComfyUI WSL inbound TCP 8188" allows LAN access. Tailscale is logged out on the PC (NoState) — re-login remains the durable fix for IP stability.

### NAS / vault side (corrects Phase 4 notes)
- **Plaintext vault** = `vault-local:` rclone crypt mount, persistently mounted by a fuse.rclone process at `/mnt/user/mount/vault` (`--vfs-cache-mode full --cache-dir /mnt/user/appdata/rclone-vault-cache`). Share "mount" contains gdrive, gsuite, gsuite-vault, vault.
- **Ciphertext** lives in share `vault` (`/mnt/user/vault`) — random-named dirs only; never edit there directly.
- Windows N: drive = separate nssm service mounting the same remote (`nas-vault:`). So `N:\Images\...` == `/mnt/user/mount/vault/Images/...`. The `.env.example` paths were already correct; Phase 4 notes had conflated the two locations (see D4).

### App path semantics (BlazorWebApp code, verified)
- `ComfyUIService.cs:1096/1134`: input uploads go to `Path.Combine(_comfyInputsPath, "input", filename)` → **`COMFYUI_INPUTS_PATH` must be the ComfyUI workspace ROOT** (the app appends `/input/<filename>` itself).
- `ComfyUIHistoryImageOutputCollector.cs:56-77`: empty OutputsPath → `FullPath = relativePath` resolved against container CWD (`/app`) — disk reads fail silently; previews still stream over WS. Non-empty → `Path.Combine(comfyOutputsPath, subfolder/filename)` — must point **directly at ComfyUI's output dir**.
- `OutputPathsOptions.cs:17/22/27` defaults use **Windows backslashes** (`"Text-2-Image\\_samples"` etc.); the options bind from config section `"OutputPaths"` (`BackendService.cs:53`) so env vars like `OutputPaths__Txt2ImgSamples=Text-2-Image/_samples` override them without a rebuild.

## 3. Steps

### Step 1 — Audit and document the Comfy/WSL setup ✅
- [x] Inspect the WSL Docker compose file, image, volumes, and networking; record versions, workspace layout (`/opt/ComfyUI`), model locations, GPU passthrough config → §2 above.
- [x] Determine how inputs/outputs currently reach what the desktop app expects: output dir is WSL ext4 (`./output`) and input dir is on E: — **neither NAS-visible**; with `COMFYUI_OUTPUTS_PATH`/`INPUTS_PATH` empty, final results are not readable from disk by the NAS container (previews still stream over WS).

### Step 2 — Add WSL as an infra device ✅ (infra `045a4c3`)
- [x] Create `infra/wsl/CLAUDE.md` with verified facts (§2) + transient-mounts gotcha.
- [x] Update infra root CLAUDE.md: add WSL row to the device index; note the ComfyUI I/O cross-device workflow.

### Step 3 — Vault I/O for ComfyUI (design agreed with user: **both input and output** on the vault) ✅
Target layout: `/mnt/user/mount/vault/Images/comfyui/{input,output}` (= `N:\Images\comfyui\{input,output}`).
- [x] NAS: create the two folders.
- [x] WSL: make mounts persistent via systemd units — CIFS `//NAS/array` → `/mnt/nas`, rclone crypt `nas-vault:` → `/home/hugo/mnt/vault`. Both are currently manual/transient (F1).
- [x] WSL: edit `docker-compose.yml` volumes — replace `/mnt/e/Programas/ComfyUI_Data/input:/opt/ComfyUI/input` and `./output:/opt/ComfyUI/output` with the vault paths (back up compose file as `.bak-YYYYMMDD` first).
- [x] NAS app: add bind mount `${COMFYUI_DIR_HOST}:/data/comfyui`; set in live `.env`: `COMFYUI_OUTPUTS_PATH=/data/comfyui/output`, `COMFYUI_INPUTS_PATH=/data/comfyui` (workspace root, D3), plus the three `OutputPaths__*` forward-slash overrides (D1). Back up `.env` first.
- [x] Recreate comfyui container + restart app container; verify end-to-end: generate → file appears in vault output dir under correct `Text-2-Image/_samples/...` layout → app serves it via `/image/...`. **Passed 2026-11-12** (user-run generation test, input uploads verified too).

### Step 4 — Gallery responsiveness on the NAS container ✅ (image `2026-11-12b`, app commits `5246e63`, `4429f85`)
User report: gallery feels sluggish; the hover panel on image cards took 1–2 s to appear/dissolve on the NAS-hosted container.
- [x] Root-cause the hover lag (D7) and move hover visuals to pure CSS in `ImageCard` and `GalleryImageTile`.
- [x] Stop every tile re-rendering on `StateChangedEventArgs` (published on each state save and orchestrator update); `ImageCard` re-renders on selection events only when its own state changed.
- [x] `Cache-Control: public,max-age=86400` on the `/image` static files (`Program.cs`) so revisits skip the NAS fuse mount.
- [x] Commit the previously uncommitted `2026-11-11c` fixes (case-preserving `/image` URLs, `@Color.*` bindings — PHASE_4.md D3/D4) so git matches what is deployed.
- [x] Build + push `10.0.0.11:5000/blazorwebapp:2026-11-12b`, deploy (backup `.env.bak-20261112b`, pin bumped, `compose pull && up -d --force-recreate`).
- [x] Recover the NAS vault host mount, found missing during the deploy (D8).

## 4. Open Questions

- Is the WSL IP (`10.0.0.2`) stable enough for `COMFYUI_BASE_URL`, or should we re-login Tailscale on the PC and use that address? — Accepted DHCP IP for now; Tailscale re-login remains a pending follow-up (PC currently logged out).
- ~~Do inputs need NAS visibility too, or only outputs (v1 scope)?~~ **Resolved:** both input and output go to vault folders (user decision 2026-11-11).
- Should ComfyUI itself be documented as a service in the infra repo? — Covered by `infra/wsl/CLAUDE.md` (it lives in WSL, not Unraid); no separate `unraid/services/` entry.

## 5. Test Evidence

- **NAS→WSL reachability:** from the NAS, `curl http://10.0.0.2:8188/system_stats` returns ComfyUI stats (version 0.45.0, frontend 1.58.5, templates 0.11.106).
- **Stray-folder forensics** (user's failed test): `ls /mnt/user/mount/vault/Images/StableDiffusion` shows both `Text-2-Image/` and a folder literally named `Text-2-Image\_samples`; `find ... -name '*Text-2-Image*'` returns exactly those two paths; `ls -laR` of the stray tree: all dirs size 0, created Nov 11 19:28, **no files** — `CreateDirectory` succeeded, image save failed (output dir not NAS-visible).
- **Vault mount verification:** `mount | grep vault` → fuse.rclone `vault-local:` at `/mnt/user/mount/vault`; rclone remotes include both `nas-vault:` and `vault-local:`.
- **Migration integrity (Step 3):** input (154 files) + output (5901 files) moved old→vault; `rclone check` → 0 differences. Stray empty `Text-2-Image\_samples` tree removed during cleanup. WSL container recreated with vault volumes, verified in-container.
- **Race fix deployed (image `2026-11-12`):** bounded retry + null guard on image load (`ComfyUIService.GetBase64FromFileWithRetryAsync`, 20 × 500 ms) — a file not yet visible to the container no longer crashes generation with null base64.
- **Dir-cache diagnosis:** primed-cache probe (prime listing from inside the container, write via WSL/SMB, poll stat at 500 ms) → new file invisible for **277 s** under rclone's default `--dir-cache-time` of 5 min; ground-truth oracle (`rclone lsf -F pts "vault-local:..."`, bypasses the fuse cache) proved files were on disk *before* the app's retry window started — not an SMB flush delay. Fix: `--dir-cache-time 2s` in the User Scripts plugin script → probe **1788 ms** (within TTL + poll granularity).
- **Gallery regression root cause:** at TTL=0, crypt `DirectoryNames` forces a re-list + decrypt of *all* names on every stat — an orphaned diagnostic `find /data/outputs | wc -l` (left running after its ssh session timed out) drove the daemon to ~4 cores and stalled all other clients' fuse ops. Final value **2s**: race window ≤2 s (absorbed by the app's retry loop), cached listings make repeat lookups free. Verified: daemon CPU 0 ticks/5 s, `ls /data/outputs` 81 ms, full-tree find over 352,331 files in real 50.4 s with a count exactly matching the rclone oracle (linear behavior).
- **End-to-end generation test (user-run, 2026-11-12):** generate via app UI → file appears in vault output under `Text-2-Image/_samples/...` and is served by the app; input uploads verified working. All three vault-mounting apps (blazorwebapp gallery, stashmanager, Stash) confirmed healthy after the TTL change.
- **Step 4 deploy (2026-11-12, image `2026-11-12b`, manifest digest `sha256:5f03f842…`):** build (~33 s, layers cached) + push to `10.0.0.11:5000` OK; NAS pull + `up -d --force-recreate` OK, logs clean, `WS connected`. `curl -D -` on a real `/image/Text-2-Image/_samples/...png` → `200`, `Cache-Control: public,max-age=86400`, 1,015,784 bytes; `/` → 200. Playwright against `http://10.0.0.11:8189/`: gallery rendered 10 `ImageCard`s, card markup has no `onmouseenter`; hovering the first card flipped its `.overlay-top/-center/-bottom` computed opacity to `1` within 400 ms while its neighbour stayed `0` and the card's Blazor class list was unchanged (i.e. purely client-side).
- **Vault host-mount recovery (D8):** before — `grep -c vault /proc/mounts` → 0, `ls /data/outputs` in the fresh container → only `resource_previews`, rclone vault pid alive with last log line `Unmounted rclone mount`; `Stash`/`stashmanager` (started 16:08:52/54) still saw the real tree. After — stopped the 3 consumers (stale rclone exited on its own), `find … -type f | wc -l` = 0 on the bare share, `rmdir`'d 5 Docker-created stub dirs, started `rcloneMountVault/script` detached → `vault-local: /mnt/user/mount/vault fuse.rclone` in `/proc/mounts`; restarted the consumers; all three list real content (`/data/outputs` → Audio, ComfyUI, Image-2-Image, Text-2-Image…; `/data/comfyui` → input, output).

## 6. Decisions & Findings

- **D1 — Stray-folder root cause (backslash bug).** `OutputPathsOptions` defaults use Windows backslashes; on Linux, `Path.Combine("/data/outputs", "Text-2-Image\\_samples")` creates a single directory literally named `Text-2-Image\_samples`. The app's ComfyUI save flow (`ImageService.cs:417` → `_io.CreateDirectory(GetCurrentSaveFolder(...))`) built the empty tree; the image itself never landed because ComfyUI's output dir was WSL ext4. **Fix now (no rebuild):** env overrides in the NAS `.env`: `OutputPaths__Txt2ImgSamples=Text-2-Image/_samples`, `OutputPaths__Img2ImgSamples=Image-2-Image/_samples`, `OutputPaths__Img2VidSamples=Image-2-Video/_samples` (forward slashes are valid on both platforms). **Follow-up (needs image rebuild):** change the defaults in `OutputPathsOptions.cs` to forward slashes.
- **D2 — I/O location.** User decision: both input and output live on the vault at `/mnt/user/mount/vault/Images/comfyui/{input,output}`.
- **D3 — `COMFYUI_INPUTS_PATH` semantics.** Must be the workspace root (`/data/comfyui`), not `.../input` — the app appends `/input/<filename>` itself (`ComfyUIService.cs:1096`).
- **D4 — Vault path correction.** Plaintext vault is `/mnt/user/mount/vault` (persistent `vault-local:` rclone mount on the NAS); `/mnt/user/vault` is the ciphertext cryptdir. `.env.example` was already correct; Phase 4 notes conflated them.
- **D5 — Generation race root cause.** The null-base64 crash after generation was a fuse dir-cache visibility delay (rclone default `--dir-cache-time` 5 min, negative lookups cached), not an SMB flush issue. Fixed two ways: app-side bounded retry + null guard (image `2026-11-12`) and the NAS mount TTL lowered to 2 s.
- **D6 — `--dir-cache-time 2s`, not 0.** With crypt `DirectoryNames` every stat re-lists + decrypts all names in the directory; at TTL=0 one `find` over the tree is O(N²) (observed: an orphaned diagnostic find pinned ~4 cores and stalled the gallery). 2 s caps the race window while cached listings keep repeat lookups free.
- **F1 — WSL mounts are transient.** The CIFS mount (`//NAS/array` → `/mnt/nas`) and the rclone vault mount in WSL are manual; both are lost on WSL reboot. systemd units (Step 3) must land before container volumes depend on them.
- **D7 — Hover lag root cause (Blazor Server round-trips).** `ImageCard`/`GalleryImageTile` toggled their hover panel through `@onmouseenter`/`@onmouseleave` handlers setting `_isHovered`. In Blazor Server each event is a SignalR round-trip, rendered server-side and diffed back, so any server/connection latency (or queueing behind the per-tile re-renders triggered by every `StateChangedEventArgs`) shows as a 1–2 s delay. Fix: visuals use `:hover`/`:focus-within` in the component CSS; only the open "more actions" menu stays server state, and its `mouseleave` handler is bound only while the menu is open (null `Action` ⇒ no event registered). Tiles no longer subscribe to `StateChangedEventArgs` (they render nothing from it). Image bytes are the other half of "sluggish": `/image` now sends `Cache-Control: public,max-age=86400` (filenames are write-once; ETag revalidation remains for stale entries).
- **D8 — Vault host mount was silently dead; deploy surfaced it.** The rclone `vault-local:` host mount had vanished (log: `Unmounted rclone mount` 16:08:53, right when the Phase 6 TTL change restarted the consumers) but `Stash`, `stashmanager` and the old app container kept a private reference (`rprivate` bind) to the old superblock, so nothing looked wrong for ~5 h. Recreating the app container bound the empty array-share dir, and Docker created stub dirs on it that block the next `rclone mount` (`"… is not empty"`). Recovery and lessons are in `infra/wsl/CLAUDE.md` ("Remount gotchas"): always verify `grep vault-local /proc/mounts` after a remount, never recreate consumers while it's missing.
- **Follow-ups (not done):**
  - **Boot ordering is a real risk (partially verified 2026-11-12):** `rcloneMountVault` is scheduled at array start, while `blazorwebapp`/`stashmanager` are compose-managed (`unless-stopped`, started by the Docker daemon, absent from the dockerMan autostart list), so they can start before the fuse mount exists and bind the empty dir (D8 poisoning). Not reproduced with an actual reboot. After a NAS reboot: check `/proc/mounts`, then restart the three consumers. Pending: a wait-for-mount guard (documented in `infra/unraid/CLAUDE.md`).
  - **Thumbnails:** tiles still load full-size originals through the crypt fuse mount on first view; a cached thumbnail endpoint would be the next real win (not scoped).
  - **`VideoCard` / `AudioCard` still use server hover events** (they drive media playback through them); convert via JS/CSS if their lag is noticeable.
  - **Other cards** (`ProjectCard`, `ResourceCard`, `CivitaiImageCard`, `DanbooruImageCard`, `SavedDanbooruMediaCard`, …) share the same `@onmouseenter` pattern.
