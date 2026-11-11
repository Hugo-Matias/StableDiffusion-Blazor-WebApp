# Phase 2: Container Assets in Infra Repo

> **Main plan:** [MAIN_PLAN.md](./MAIN_PLAN.md)
> **Status:** [x] Complete and tested (user-approved)
> **Complexity:** 3 points
> **Depends on:** Phase 1 (configurable ComfyUI endpoint; findings F1/F2/D2 shape this phase)
> **Unblocks:** Phase 3 (build + push image to the private registry)

---

## 1. Objective

Multi-stage Dockerfile, `docker-compose.yml`, and `.env.example` for hosting BlazorWebApp on the Unraid NAS, committed to the infra repo following its layout conventions — so Phase 3 can build/push the image and Phase 4 can deploy it via Compose Manager without further design work.

---

## 2. Context & Background

- **Infra repo conventions** (`e:\Documentos\GitHub Repos\infra`): per-device folders `<device>/services/<name>/`; LF-only for `*.yml`/`Dockerfile` (enforced by `.gitattributes`); no secrets in the repo (`.env.example` instead, real `.env` gitignored); pinned date-tagged images; `${VAR:?required}` pattern for mandatory env vars.
- **Working precedent — StashManagerBlazor** (`infra/unraid/services/stashmanager/docker-compose.yml`): identical pipeline — image built on PC, pushed to `10.0.0.11:5000`, pinned by date tag in `.env`; persistent data under `/mnt/user/appdata/<service>/`; vault access as a plain bind mount of the NAS path (`/mnt/user/mount/vault`), not the rclone name; Compose Manager project at `/boot/config/plugins/compose.manager/projects/<name>/`.
- **Phase 1 findings that constrain this phase:**
  - F1 — `OutputDir`, `ResourcesPath`, `ResourcePreviewsPath` must be **absolute** container paths or startup crashes (`ArgumentException`).
  - F2 — content root = WORKDIR; the Dockerfile must keep `WORKDIR /app` as the publish dir so `appsettings.json` is found next to the DLL.
  - D2 — N:\ and \\NAS\ are NAS-accessible, so bind mounts can target vault paths directly (exact subpaths still to confirm read-only in Phase 4).

---

## 3. Steps Completed

### Step 0 — `Program.cs` connection string made configurable [x] (user-approved deviation)

The plan assumed the `ConnectionStrings__DefaultConnection` env override would work, but `BlazorWebApp/Program.cs:34` hardcoded the relative path (`Data Source=BlazorWebApp.db`). One-line change so it reads from configuration with the old value as fallback:

```csharp
builder.Services.AddDbContextFactory<AppDbContext>(opt => { opt.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=BlazorWebApp.db"); opt.EnableSensitiveDataLogging(); });
```

Local dev is unchanged because `appsettings.json` has **no** `ConnectionStrings` section, so the fallback applies. The container sets the env var to `Data Source=/app/data/BlazorWebApp.db` (see Step 2). This replaces a fragile single-file bind mount for the DB.

### Step 1 — Multi-stage `Dockerfile` [x]

Written at `infra/unraid/services/blazorwebapp/Dockerfile`, based on the draft in `Documentation/Architecture/02-DEPLOYMENT-GUIDE.md`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "BlazorWebApp/BlazorWebApp.csproj" \
    && dotnet publish "BlazorWebApp/BlazorWebApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENTRYPOINT ["dotnet", "BlazorWebApp.dll"]
```

Key points (documented in the file header):

- **Build context is this repo's solution root, NOT the infra folder** — Phase 3 runs `docker build -f <path-to-this-file> -t 10.0.0.11:5000/blazorwebapp:<date-tag> .` from here.
- The ignore rules live in a **`Dockerfile.dockerignore` companion file** next to the Dockerfile (BuildKit picks it up automatically), so all Phase 2 assets stay together in the infra repo. It excludes `.git/`, `.vs/`, `Documentation/`, `Utils/`, `TestEndpoint/`, `**/bin/`, `**/obj/`, `*.db` — keeping the context small while retaining `appsettings.json`, `libs/`, and the solution file.
- `WORKDIR /app` is the publish dir (F2) — do not move without re-checking config loading.

### Step 2 — `docker-compose.yml` [x]

Written at `infra/unraid/services/blazorwebapp/docker-compose.yml`. Key choices:

| Item | Value | Rationale |
|------|-------|-----------|
| Image | `${BLAZORWEBAPP_IMAGE:?set BLAZORWEBAPP_IMAGE}` | Pinned date tag in `.env`, never `:latest` (infra convention) |
| Port | `"${HTTP_PORT:-8189}:5000"` | Host port 5000 is taken by DockerRegistry; final value confirmed after Phase 4 conflict check |
| `ASPNETCORE_URLS` | `http://+:5000` | Listen on all interfaces inside the container |
| `ComfyUI__BaseUrl` | `${COMFYUI_BASE_URL:?set COMFYUI_BASE_URL}` | Phase 1's single key; desktop Tailscale address in `.env` |
| `ConnectionStrings__DefaultConnection` | `Data Source=/app/data/BlazorWebApp.db` | Absolute path under the DB bind mount (Step 0) |
| `OutputDir` / `ResourcesPath` / `ResourcePreviewsPath` | `/data/outputs`, `/data/resources`, `/data/previews` | Container-absolute per F1; map to the three vault bind mounts below |
| `ComfyUI__OutputsPath` | `${COMFYUI_OUTPUTS_PATH:-}` (empty default) | Final results are read from disk by the app; empty degrades gracefully — previews still stream over WS. See open item O1 |
| `ComfyUI__InputsPath` | `${COMFYUI_INPUTS_PATH:-}` (empty default) | Deliberate: uploaded input images live in the desktop's ComfyUI inputs dir, not visible from this container. An empty path makes `CachedInputFileIsAvailable` short-circuit to **true** (`ComfyUIService.cs` — returns true when `_comfyInputsPath` is null/whitespace), so cached-input checks never false-negative on a Linux path that can't exist here |
| Volumes | `/mnt/user/appdata/blazorwebapp:/app/data` + three parameterized vault mounts (`${OUTPUT_DIR_HOST:?}`, `${RESOURCES_DIR_HOST:?}`, `${PREVIEWS_DIR_HOST:?}`) | DB persistence (StashManagerBlazor precedent); data paths from `.env` so they can be corrected after Phase 4 read-only inspection without touching the compose file |
| Restart | `unless-stopped` | Infra convention |

### Step 3 — `.env.example` [x]

Written at `infra/unraid/services/blazorwebapp/.env.example`: pinned image tag, `HTTP_PORT=8189`, `COMFYUI_BASE_URL=http://<desktop-tailscale-host>:8188` placeholder (Tailscale preferred over LAN IP), the three host-side vault paths as best guesses from the desktop `appsettings.json` mapping with a "verify read-only first" note, and the two optional ComfyUI path vars left empty.

No gitignore edit needed: `infra/.gitignore` already covers `.env`/`*.env` with a `!.env.example` exception (plan Step 3's second half was already satisfied).

---

## 4. Test Evidence

### Build — validates the Step 0 `Program.cs` change

```
dotnet build BlazorWebApp/BlazorWebApp.csproj -c Release
→ 780 Warning(s), 0 Error(s)   (warnings are the repo's known unrelated noise, untouched files)
```

### Compose validation — plan success criterion "`docker compose config` validates cleanly"

No `.env` exists yet (only `.env.example`), so the required vars were set inline for the check:

```powershell
$env:BLAZORWEBAPP_IMAGE='10.0.0.11:5000/blazorwebapp:2026-11-11'
$env:COMFYUI_BASE_URL='http://desktop-tailscale-host:8188'
$env:OUTPUT_DIR_HOST='/mnt/user/mount/vault/Images/StableDiffusion'
$env:RESOURCES_DIR_HOST='/mnt/user/Programas/Stable-Diffusion/Models'
$env:PREVIEWS_DIR_HOST='/mnt/user/mount/vault/Images/StableDiffusion/resource_previews'
docker compose -f <infra>/unraid/services/blazorwebapp/docker-compose.yml config
```

→ Validated cleanly. Resolved output confirms the intended shape: image pinned, port `8189→5000`, all env vars present (`ComfyUI__InputsPath`/`ComfyUI__OutputsPath` resolve to empty strings as designed), four bind mounts with correct targets, `restart: unless-stopped`. In Phase 4 the same command run from the Compose Manager project dir will read `.env` automatically.

### Conventions check

- All four files written with LF endings (per infra `.gitattributes`: `*.yml`, `Dockerfile` → `eol=lf`).
- No secrets in any committed file; the only values are paths, ports, and a placeholder hostname.

---

## 5. Decisions & Findings (for later phases)

### D1 — Folder location: `infra/unraid/services/blazorwebapp/` (user-approved deviation from plan text)

The plan literally said `infra/services/blazorwebapp/`. The repo's actual convention is per-device folders, and the StashManagerBlazor precedent lives at `unraid/services/stashmanager/`. User confirmed the assets go in **`unraid/services/blazorwebapp/`**. MAIN_PLAN Phase 2 text should be amended to match.

### D2 — Connection string code change (user-approved)

See Step 0. The plan's assumption that an env override alone would persist the DB was wrong; this one-line `Program.cs` change is what makes it work. Local dev behavior unchanged (fallback path).

### O1 — `ComfyUI__OutputsPath` / inputs: WSL output dir must land in a NAS folder (user decision)

The app reads **final** generation results from disk (`ImageService`) and we can't rely on WS previews alone, so the desktop's ComfyUI output dir needs to be configured to write into a NAS-visible folder (WSL → rclone `N:\` or `\\NAS\` mount), after which `COMFYUI_OUTPUTS_PATH` in `.env` points at that vault path. Same approach for input images if disk-based cached-input checks are ever needed (`COMFYUI_INPUTS_PATH`). **Follow-up (later, not blocking Phase 4):** inspect the WSL setup — the whole Comfy stack is hosted there — and expand the infra repo docs to cover it; worth its own plan when picked up. Until then the compose defaults stay empty (graceful degradation).

### O2 — Open: exact vault subpaths (Phase 4, read-only first)

`OUTPUT_DIR_HOST`, `RESOURCES_DIR_HOST`, `PREVIEWS_DIR_HOST` in `.env.example` are best guesses from the desktop `appsettings.json` mapping (`N:\Images\StableDiffusion` → `/mnt/user/mount/vault/Images/StableDiffusion`, etc.). User confirmed this mapping should be right — verify each exact path read-only (`ssh nas "ls <path>"`) in Phase 4 before first deploy; correct only the `.env` values, not the compose file.

### O3 — Working-tree Data files are not in the publish output (accepted)

The untracked `BlazorWebApp/Data/` content (Presets JSONs, Wildcards JSONs, Captions CSVs) is copied into the container by `COPY . .` but only matters if referenced from disk at runtime. Verified graceful degradation: `CaptionLibraryService` guards with `Directory.Exists`; `WildcardService` is DB-backed ("seeding is now manual"). Features that read these files from disk will simply find nothing in a fresh container — acceptable for v1; re-seed via the UI/DB if needed after deploy.

### O4/O5 — Python.NET and ONNX GPU services: deprecated (user decision)

`DynamicPromptsService` (Python.NET, `PYTHONNET_PYDLL` points at a Windows path) and the ONNX-based cleanup services (GPU → CPU fallback in-container) are **not used** and there are no plans to revisit them. No action needed for the container deploy: DynamicPrompts is a lazy singleton never injected anywhere (startup unaffected), and ONNX falls back to CPU if ever invoked. If they're removed from the codebase later, nothing in this phase depends on them.

---

## 6. Commit Checkpoint

Two repos are touched:

**Infra repo** (`e:\Documentos\GitHub Repos\infra`):

| File | Change |
|------|--------|
| `unraid/services/blazorwebapp/Dockerfile` | New — multi-stage build (Step 1) |
| `unraid/services/blazorwebapp/Dockerfile.dockerignore` | New — BuildKit companion ignore file (Step 1) |
| `unraid/services/blazorwebapp/docker-compose.yml` | New — compose project (Step 2) |
| `unraid/services/blazorwebapp/.env.example` | New — env template (Step 3) |

**This repo** (`Stable-Diffusion-WebUI/BlazorWebApp`):

| File | Change |
|------|--------|
| `BlazorWebApp/Program.cs` | Connection string from configuration with fallback (Step 0) |
| `Documentation/Plans/unraid-hosting/MAIN_PLAN.md` | Phase 2 status/steps updated; folder path amended per D1; changelog entry |
| `Documentation/Plans/unraid-hosting/PHASE_2.md` | This document |
