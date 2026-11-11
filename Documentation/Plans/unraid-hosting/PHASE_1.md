# Phase 1: Make ComfyUI Endpoint Configurable

> **Main plan:** [MAIN_PLAN.md](./MAIN_PLAN.md)
> **Status:** [x] Complete and tested
> **Complexity:** 2 points
> **Depends on:** None (first phase)
> **Unblocks:** Phases 2–4 — the container's `.env` can now point `ComfyUI__BaseUrl` at the desktop.

---

## 1. Objective

Both HTTP and WebSocket connections to ComfyUI resolve from a single config key (`ComfyUI:BaseUrl`), defaulting to `http://localhost:8188` so local development is unchanged. This removes the two hardcoded endpoints that block hosting the app on the NAS while inference stays on the desktop.

---

## 2. Context & Background

Two services had ComfyUI hardcoded:

- `BlazorWebApp/Services/ComfyUIService.cs` — `_httpClient.BaseAddress = new Uri("http://localhost:8188/")`.
- `BlazorWebApp/Services/ComfyUIWebsocketService.cs` — `new Uri($"ws://localhost:8188/ws?clientId=...")`.

Existing pattern to follow: `OllamaService.cs` reads `configuration["Ollama:BaseUrl"] ?? "http://10.0.0.11:11434"`. Note Ollama already defaults to the NAS, so it works from a container unchanged — no Phase 2 work needed for it.

---

## 3. Steps Completed

### Step 1 — `ComfyUIService.cs` reads `ComfyUI:BaseUrl` [x]

Constructor now takes `IConfiguration`, stores it, and resolves the base URL with the same fallback as before:

```csharp
_comfyOutputsPath = ResolveComfyPath("ComfyUI:OutputsPath");
_comfyInputsPath = ResolveComfyPath("ComfyUI:InputsPath");
var comfyBaseUrl = _configuration["ComfyUI:BaseUrl"] ?? "http://localhost:8188";
_logger.LogInformation($"ComfyUI base URL: {comfyBaseUrl}");
_httpClient.BaseAddress = new Uri(comfyBaseUrl);
```

The `LogInformation` line is the verification hook used in testing below.

### Step 2 — WS URI derived from the same key [x]

`BlazorWebApp/Services/ComfyUIWebsocketService.cs` constructor now takes `IConfiguration` and derives the WebSocket endpoint so both services resolve identically:

```csharp
var baseUri = new Uri(configuration["ComfyUI:BaseUrl"] ?? "http://localhost:8188");
var wsBuilder = new UriBuilder(baseUri);
wsBuilder.Scheme = baseUri.Scheme == Uri.UriSchemeHttps ? Uri.UriSchemeWss : Uri.UriSchemeWs;
wsBuilder.Path = "/ws";
_comfyWsBaseUri = wsBuilder.Uri;
```

`ConnectLoop` connects via `new Uri(_comfyWsBaseUri, $"?clientId={_backend.ComfyWSClientId}")`, preserving the `/ws?clientId=...` query. (Note: the plan text said "swap http→ws / https→wsc" — typo in the plan; correct scheme is `wss`. Implemented correctly.)

### Step 3 — Documentation [x]

- `BlazorWebApp/appsettings.json`: added `"ComfyUI": { "BaseUrl": "http://localhost:8188", ... }` section (alongside existing `OutputsPath` / `InputsPath`).
- `Documentation/Architecture/02-DEPLOYMENT-GUIDE.md`: new "ComfyUI Base URL" subsection under Configuration Options — documents the single-key behavior, localhost default, double-underscore env override (`set ComfyUI__BaseUrl=http://192.168.1.50:8188`), the `--listen` prerequisite on the remote ComfyUI, and `curl http://<host>:8188/system_stats` as the pre-flight check.

---

## 4. Test Evidence

Tested by publishing (`dotnet publish -c Release`) to a temp dir and running the published DLL from that directory (content root = CWD, so `appsettings.json` is found — see Finding F2). Per user constraint: app was launched but **no generation triggered** (LM Studio on the same machine has no capacity for both); server health verified via the WS connection instead.

### RUN 1 — default config (no env override)

```
info: BlazorWebApp.Services.ComfyUIService[0]
      ComfyUI base URL: http://localhost:8188
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5199
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: BlazorWebApp.Services.ComfyUIWebsocketService[0]
      WS connected | ClientID: 5ac434f5-10a9-4a5d-9b87-a6254822bb22
```

→ Default behavior unchanged; HTTP base URL and WS both resolve to `localhost:8188`; **WS connected** proves live server health against the real desktop ComfyUI.

### RUN 2 — env override `ComfyUI__BaseUrl=http://localhost:9999` (nothing listening there)

```
info: BlazorWebApp.Services.ComfyUIService[0]
      ComfyUI base URL: http://localhost:9999
warn: ... WS connect failed, retrying in 5s...   (×3 over the run window)
```

→ The override redirects **both** the HTTP client and the derived WS endpoint (`ws://localhost:9999/ws?clientId=...`), proving single-key control.

### Build

`dotnet build` passed with 0 errors (~779 known unrelated warnings — repo's known noise, untouched files).

---

## 5. Decisions & Findings (for later phases)

### D1 — Config key naming: `ComfyUI:BaseUrl` (user-approved deviation from plan text)

The plan literally said `ComfyUi:BaseUrl`. User decision: **keep `ComfyUI`** casing, consistent with the existing `ComfyUI:` section in appsettings.json (`OutputsPath`, `InputsPath`). Plan text amended accordingly. Env override is therefore `ComfyUI__BaseUrl` (double underscore).

### D2 — Container paths can target N:\ and \\NAS\ directly (user decision)

User: "N:\ and \\NAS\ are both folders accessible from the NAS so the mounted volume can target them. N:\ is an encrypted rclone mount for the nas vault." This resolves the plan's stress point about final-result retrieval depending on a local disk path: no ComfyUI `/view` API workaround needed — Phase 2 bind mounts can point `OutputDir`, `ResourcesPath`, and `ResourcePreviewsPath` at NAS-side paths.

**Working precedent — StashManagerBlazor** (`E:\Documentos\ASP.NET\StashManagerBlazor\`), deployed on the same NAS via Compose Manager:

- Vault access is a plain bind mount of the NAS path, not the rclone name: `- /mnt/user/mount/vault:/data` (comment in its compose file confirms `/mnt/user/mount/vault/Videos/VR`, 2026-11-06). So `N:\Images\StableDiffusion` maps to a path under `/mnt/user/mount/vault/Images/StableDiffusion` on the NAS — exact subpath to confirm during Phase 4 (read-only inspection first, per infra repo rules).
- Persistent app data: `- /mnt/user/appdata/stashmanager/data:/app/Data` for its SQLite DB. Same pattern applies here (`/mnt/user/appdata/blazorwebapp/`).
- Image built on PC, pushed to `10.0.0.11:5000`, pinned by date tag in `.env` (`STASHMANAGER_IMAGE=10.0.0.11:5000/stashmanager:2026-11-01`) — the exact deploy pipeline Phase 3 will reuse.
- `restart: unless-stopped`; port remapped via `${HTTP_PORT:-...}` in `.env`.

### F1 — Absolute-path startup requirement (affects Dockerfile/compose)

`Program.cs` constructs three `PhysicalFileProvider`s at startup from config values (`OutputDir` → `/image`, `ResourcesPath` → `/files/resources`, `ResourcePreviewsPath` → `/files/resource_previews`). All three must be **absolute** in any deployment or the app crashes with `ArgumentException: The path must be absolute`. Local dev works because appsettings.json holds Windows absolute paths. Phase 2 compose env vars must therefore set all three to container-absolute paths (e.g. under bind-mounted volumes), not relative ones.

### F2 — Content root / WORKDIR requirement (affects Dockerfile)

`WebApplicationBuilder` sets the content root to `Directory.GetCurrentDirectory()`; `appsettings.json` is loaded from there. Running a published DLL with CWD ≠ publish dir means config isn't found → null paths → the F1 crash above. Confirmed empirically: successful runs logged `Content root path: E:\Temp\blazor_p1_pub`. Not an app bug — local dev via `dotnet run` is unaffected. The Dockerfile must keep `WORKDIR /app` as the publish dir (the draft in 02-DEPLOYMENT-GUIDE.md already does).

### F3 — Ollama needs no change

`Ollama:BaseUrl` defaults to `http://10.0.0.11:11434/api` (the NAS) — reachable from a container on the same host unchanged.

---

## 6. Commit Checkpoint

Phase-1-relevant files for the commit:

| File | Change |
|------|--------|
| `BlazorWebApp/Services/ComfyUIService.cs` | Config-driven base URL (Step 1) |
| `BlazorWebApp/Services/ComfyUIWebsocketService.cs` | Derived WS URI from same key (Step 2) |
| `Documentation/Architecture/02-DEPLOYMENT-GUIDE.md` | "ComfyUI Base URL" subsection (Step 3) |
| `Documentation/Plans/unraid-hosting/MAIN_PLAN.md` | Phase 1 marked complete; naming amended to `ComfyUI:BaseUrl`; changelog entry |
| `Documentation/Plans/unraid-hosting/PHASE_1.md` | This document |

Note: `BlazorWebApp/appsettings.json` is documented as "not in repo" (02-DEPLOYMENT-GUIDE.md, Directory Structure section) — verify tracked status with `git ls-files BlazorWebApp/appsettings.json` before staging; if untracked, leave it out of the commit.
