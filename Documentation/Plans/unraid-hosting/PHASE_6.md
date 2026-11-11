# Phase 6: WSL ↔ Unraid Connectivity and Comfy Setup Audit

> **Main plan:** [MAIN_PLAN.md](./MAIN_PLAN.md)
> **Status:** In Progress — Step 1 (audit) ✅, Step 2 (WSL infra device) in progress, Step 3 (vault I/O) designed & agreed with user; state-changing commands pending confirmation.
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

### Step 2 — Add WSL as an infra device
- [ ] Create `infra/wsl/CLAUDE.md` with verified facts (§2) + transient-mounts gotcha.
- [ ] Update infra root CLAUDE.md: add WSL row to the device index; note the ComfyUI I/O cross-device workflow.

### Step 3 — Vault I/O for ComfyUI (design agreed with user: **both input and output** on the vault)
Target layout: `/mnt/user/mount/vault/Images/comfyui/{input,output}` (= `N:\Images\comfyui\{input,output}`).
- [ ] NAS: create the two folders.
- [ ] WSL: make mounts persistent via systemd units — CIFS `//NAS/array` → `/mnt/nas`, rclone crypt `nas-vault:` → `/home/hugo/mnt/vault`. Both are currently manual/transient (F1).
- [ ] WSL: edit `docker-compose.yml` volumes — replace `/mnt/e/Programas/ComfyUI_Data/input:/opt/ComfyUI/input` and `./output:/opt/ComfyUI/output` with the vault paths (back up compose file as `.bak-YYYYMMDD` first).
- [ ] NAS app: add bind mount `${COMFYUI_DIR_HOST}:/data/comfyui`; set in live `.env`: `COMFYUI_OUTPUTS_PATH=/data/comfyui/output`, `COMFYUI_INPUTS_PATH=/data/comfyui` (workspace root, D3), plus the three `OutputPaths__*` forward-slash overrides (D1). Back up `.env` first.
- [ ] Recreate comfyui container + restart app container; verify end-to-end: generate → file appears in vault output dir under correct `Text-2-Image/_samples/...` layout → app serves it via `/image/...`.

## 4. Open Questions

- Is the WSL IP (`10.0.0.2`) stable enough for `COMFYUI_BASE_URL`, or should we re-login Tailscale on the PC and use that address? — Accepted DHCP IP for now; Tailscale re-login remains a pending follow-up (PC currently logged out).
- ~~Do inputs need NAS visibility too, or only outputs (v1 scope)?~~ **Resolved:** both input and output go to vault folders (user decision 2026-11-11).
- Should ComfyUI itself be documented as a service in the infra repo? — Covered by `infra/wsl/CLAUDE.md` (it lives in WSL, not Unraid); no separate `unraid/services/` entry.

## 5. Test Evidence

- **NAS→WSL reachability:** from the NAS, `curl http://10.0.0.2:8188/system_stats` returns ComfyUI stats (version 0.45.0, frontend 1.58.5, templates 0.11.106).
- **Stray-folder forensics** (user's failed test): `ls /mnt/user/mount/vault/Images/StableDiffusion` shows both `Text-2-Image/` and a folder literally named `Text-2-Image\_samples`; `find ... -name '*Text-2-Image*'` returns exactly those two paths; `ls -laR` of the stray tree: all dirs size 0, created Nov 11 19:28, **no files** — `CreateDirectory` succeeded, image save failed (output dir not NAS-visible).
- **Vault mount verification:** `mount | grep vault` → fuse.rclone `vault-local:` at `/mnt/user/mount/vault`; rclone remotes include both `nas-vault:` and `vault-local:`.

## 6. Decisions & Findings

- **D1 — Stray-folder root cause (backslash bug).** `OutputPathsOptions` defaults use Windows backslashes; on Linux, `Path.Combine("/data/outputs", "Text-2-Image\\_samples")` creates a single directory literally named `Text-2-Image\_samples`. The app's ComfyUI save flow (`ImageService.cs:417` → `_io.CreateDirectory(GetCurrentSaveFolder(...))`) built the empty tree; the image itself never landed because ComfyUI's output dir was WSL ext4. **Fix now (no rebuild):** env overrides in the NAS `.env`: `OutputPaths__Txt2ImgSamples=Text-2-Image/_samples`, `OutputPaths__Img2ImgSamples=Image-2-Image/_samples`, `OutputPaths__Img2VidSamples=Image-2-Video/_samples` (forward slashes are valid on both platforms). **Follow-up (needs image rebuild):** change the defaults in `OutputPathsOptions.cs` to forward slashes.
- **D2 — I/O location.** User decision: both input and output live on the vault at `/mnt/user/mount/vault/Images/comfyui/{input,output}`.
- **D3 — `COMFYUI_INPUTS_PATH` semantics.** Must be the workspace root (`/data/comfyui`), not `.../input` — the app appends `/input/<filename>` itself (`ComfyUIService.cs:1096`).
- **D4 — Vault path correction.** Plaintext vault is `/mnt/user/mount/vault` (persistent `vault-local:` rclone mount on the NAS); `/mnt/user/vault` is the ciphertext cryptdir. `.env.example` was already correct; Phase 4 notes conflated them.
- **F1 — WSL mounts are transient.** The CIFS mount (`//NAS/array` → `/mnt/nas`) and the rclone vault mount in WSL are manual; both are lost on WSL reboot. systemd units (Step 3) must land before container volumes depend on them.
