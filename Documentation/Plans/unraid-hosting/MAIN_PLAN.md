# Unraid Hosting (NAS Frontend / Desktop Inference) - Implementation Plan

## Status
**Current Phase:** Phase 2 complete — awaiting user permission for Phase 3

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow (per step)
1. **Initial Code Writing** → 2. **Test and Debug Features** → 3. **Discuss Improvements** → 4. **Update Phase Document**
   - Do NOT proceed to next step until testing is complete
   - User must explicitly approve before updating phase document
   - Build runs only after user requests or after completing all file edits

### Progress Tracking Symbols
- `[ ]` Not started
- `[~]` In progress
- `[x]` Complete and tested
- `[!]` Blocked/needs discussion

### Complexity Estimation (Fibonacci Points)
- **1**: Trivial (simple property change, config update)
- **2**: Simple (straightforward refactor, single file change)
- **3**: Moderate (multi-file change, simple logic)
- **5**: Medium (service extraction, interface creation)
- **8**: Complex (component migration, breaking changes)
- **13**: Very complex (architecture change, wide impact)
- **21+**: Epic (should be split into smaller phases)

### Key Rules
- **Each step = commitable checkpoint** for safe implementation
- **No time/date references** - use complexity points only
- **Detours are acceptable** after discussion - append to main plan
- **Phase documents must contain enough context** to resume in new sessions
- **Minimal, focused changes** - avoid over-engineering
- **User permission required** before moving to next phase

### Documentation Requirements
- Create `PHASE_{#}.md` when entering a new phase
- Update phase document after each step completion
- Document all issues, blockers, and resolutions
- Track commit checkpoints throughout execution

---

## Problem Statement

The Blazor app currently runs only on the desktop (the RTX 4090 machine) with ComfyUI hardcoded to `http://localhost:8188`. The user wants the **frontend hosted persistently on the Unraid NAS** so any device on the LAN/Tailscale can reach a stable endpoint, while **all inference stays on the desktop** because the NAS hardware (i5-4670K) is not suitable for GPU workloads.

This is feasible because the Blazor app is a thin server-side proxy: browsers only talk to the Blazor process, which forwards HTTP API calls and relays ComfyUI WebSocket progress events (previews arrive as base64 over the WS — see `ComfyUIWebsocketService.cs`). No shared filesystem with the desktop is required for image flow.

### Current state (verified)
- Two hardcoded endpoints block remote hosting:
  - `BlazorWebApp/Services/ComfyUIService.cs` line ~52: `_httpClient.BaseAddress = new Uri("http://localhost:8188/")`
  - `BlazorWebApp/Services/ComfyUIWebsocketService.cs` line ~50: `new Uri($"ws://localhost:8188/ws?clientId=...")`
- Existing config pattern to follow: `OllamaService.cs` uses `configuration["Ollama:BaseUrl"] ?? "http://10.0.0.11:11434"` (note: Ollama already defaults to the NAS, so it works from a container unchanged).
- Infra repo (`e:\Documentos\GitHub Repos\infra`) documents a private registry `DockerRegistry` at `10.0.0.11:5000` (plain HTTP) explicitly intended for this deploy; a stale `pokeblazor:latest` image exists there from a prior attempt.
- Infra repo conventions: compose definitions live under `services/<name>/`, persistent data under `/mnt/user/appdata/<service>/`, LF-only scripts, no secrets in repo (`.env.example` instead), pinned images, `unless-stopped` restart policy.

---

## Proposed Solution

Run the Blazor app as a **Compose-managed Docker container on Unraid**, pointing it at ComfyUI on the desktop over Tailscale/LAN:

```
Browser (any device, LAN/Tailscale)
   │  http://10.0.0.11:<port>
   ▼
BlazorWebApp container on Unraid
   │  HTTP + ws → ComfyUI__BaseUrl (desktop address)
   ▼
Desktop: ComfyUI --listen :8188   ← all inference here
```

### Key Decisions
| Decision | Rationale |
|----------|-----------|
| Host the app container on Unraid; inference stays on desktop | App is a thin proxy — NAS CPU/RAM is sufficient for Blazor Server; stable endpoint independent of desktop IP |
| New config key `ComfyUI:BaseUrl`, defaulting to `http://localhost:8188` (naming amended from plan-literal `ComfyUi:` per user decision — see PHASE_1.md D1) | Mirrors existing `OllamaService` pattern exactly; zero change to local dev behavior |
| Desktop addressed via its Tailscale hostname/IP in `.env` | Survives LAN IP changes; both machines are on Tailscale |
| Image built on PC (Docker Desktop), pushed to private registry `10.0.0.11:5000`, pulled by NAS | Existing infra with documented intent and precedent (`pokeblazor`) |
| Compose-managed deployment, not dockerMan template | Reproducible env config; matches infra repo convention for non-trivial services |
| SQLite DB persisted via bind mount `/mnt/user/appdata/blazorwebapp/` + `ConnectionStrings__DefaultConnection` env override to an absolute path under the volume | Connection string is currently relative (`Data Source=BlazorWebApp.db`); container working dir would lose state on image updates |

### Conventions
- Follow infra repo safety rules: read-only inspection first; confirm before any state-changing command on the NAS.
- `.env.example` in `services/blazorwebapp/`; real `.env` gitignored.
- Pin base images (`mcr.microsoft.com/dotnet/sdk:8.0`, `aspnet:8.0`) and tag app images explicitly (no `latest`).
- Check port conflicts on the NAS (`ss -tlnp`) before choosing the published port; Unraid UI holds 80/443.

---

## Implementation Phases

### Phase 1: Make ComfyUI endpoint configurable
**Objective:** Both HTTP and WebSocket connections to ComfyUI resolve from a single config key, defaulting to `localhost` so local development is unchanged.
**Complexity:** 2 points
**Status:** [x] Complete and tested — see [PHASE_1.md](./PHASE_1.md)

#### Steps
- [x] Step 1 - Add `ComfyUI:BaseUrl` read in `ComfyUIService.cs` (`configuration["ComfyUI:BaseUrl"] ?? "http://localhost:8188"`), replacing the hardcoded BaseAddress. (Naming amended from plan-literal `ComfyUi:` per user decision — PHASE_1.md D1.)
- [x] Step 2 - Derive the WebSocket URI from the same base URL in `ComfyUIWebsocketService.cs` (swap `http`→`ws` / `https`→`wss` scheme; keep `/ws?clientId=...` path). Both services must resolve the identical value.
- [x] Step 3 - Document the key in `appsettings.json` reference and `Documentation/Architecture/02-DEPLOYMENT-GUIDE.md`.

#### Success Criteria (all met — evidence in PHASE_1.md §4)
- Local dev with no config change behaves exactly as before (ComfyUI on localhost:8188). ✓ RUN 1: default base URL logged, `WS connected` against real desktop ComfyUI.
- Setting env var `ComfyUI__BaseUrl=http://<desktop>:8188` redirects both HTTP calls and the WS connection. ✓ RUN 2 with `http://localhost:9999`: both redirected (WS retry loop to port 9999).
- Build passes; targeted validation of touched files only (repo has known unrelated build noise). ✓ 0 errors, ~779 known warnings in untouched files.

---

### Phase 2: Container assets in infra repo
**Objective:** Multi-stage Dockerfile + compose project for the app, committed to `infra/unraid/services/blazorwebapp/` (path amended from plan-literal `infra/services/` per user decision — PHASE_2.md D1).
**Complexity:** 3 points
**Status:** [x] Complete and tested — see [PHASE_2.md](./PHASE_2.md)

#### Steps
- [x] Step 1 - Write multi-stage `Dockerfile` (sdk:8.0 build → aspnet:8.0 runtime) in `infra/unraid/services/blazorwebapp/`, based on the draft in `Documentation/Architecture/02-DEPLOYMENT-GUIDE.md`. Done — plus a BuildKit `Dockerfile.dockerignore` companion file so ignore rules travel with the Dockerfile (build context stays this repo's solution root).
- [x] Step 2 - Write `docker-compose.yml`: image from `10.0.0.11:5000/blazorwebapp:<tag>`, env vars (`ComfyUI__BaseUrl` from `.env`, `ConnectionStrings__DefaultConnection=Data Source=/app/data/BlazorWebApp.db`, `ASPNETCORE_URLS=http://+:5000`, plus absolute container paths for `OutputDir` / `ResourcesPath` / `ResourcePreviewsPath` — required at startup, see PHASE_1.md F1), bind mount `/mnt/user/appdata/blazorwebapp:/app/data`, published port (default 8189; final value after Phase 4 conflict check), `restart: unless-stopped`. Done — plus a one-line `Program.cs` change so the connection string reads from configuration with fallback (PHASE_2.md D2).
- [x] Step 3 - Write `.env.example` with desktop Tailscale address placeholder; add `.env` to gitignore. Done — infra repo already gitignores `.env`/`*.env` with a `!.env.example` exception, so no edit was needed.

#### Success Criteria (all met — evidence in PHASE_2.md §4)
- `docker compose config` validates cleanly. ✓ Validated with required vars set inline; resolved output confirms image pin, port mapping, env vars, and bind mounts.
- Files follow infra repo layout conventions (LF endings, no secrets). ✓ All four assets LF per `.gitattributes`; committed files hold only paths, ports, and a placeholder hostname. Commit pending user request (two repos: infra + this one for `Program.cs`/plan docs).

---

### Phase 3: Build and push image to private registry
**Objective:** A reproducible app image available at `10.0.0.11:5000`.
**Complexity:** 2 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - Verify Docker Desktop has `insecureRegistries` for `10.0.0.11:5000` (known gotcha in infra repo; push fails with "server gave HTTP response to HTTPS client" otherwise).
- [ ] Step 2 - Ensure the `DockerRegistry` container is running on the NAS (`restart=no` — it does not autostart after reboot; starting it is a state change, confirm first).
- [ ] Step 3 - Build from this repo's publish output (or Dockerfile build context), tag `10.0.0.11:5000/blazorwebapp:<tag>`, push.

#### Success Criteria
- Image visible in registry catalog (`curl http://10.0.0.11:5000/v2/_catalog`).
- NAS can pull it (daemon already trusts the registry).

---

### Phase 4: Deploy to Unraid and end-to-end verification
**Objective:** App live at `http://10.0.0.11:<port>` with a full generation round-trip through desktop ComfyUI.
**Complexity:** 3 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - Check published-port conflicts on NAS (`ss -tlnp`, `docker ps --format '{{.Names}}\t{{.Ports}}'`); create compose project directory and copy assets over (scp/rsync).
- [ ] Step 2 - Start ComfyUI on the desktop with `--listen` so port 8188 is reachable from the NAS; verify reachability (`curl http://<desktop>:8188/system_stats`).
- [ ] Step 3 - `docker compose up -d`; confirm container Up, logs clean.
- [ ] Step 4 - Verify from PC and a second device (LAN or Tailscale): app loads, ComfyUI connection status healthy, run one txt2img generation and watch previews stream through the NAS-hosted app.

#### Success Criteria
- Container `Up` with clean logs; survives NAS container restart policy.
- Full generate → preview → result flow works via `http://10.0.0.11:<port>` from a device other than the desktop.
- DB state persists across `docker compose pull && up -d`.

---

### Phase 5: Documentation and knowledge base update
**Objective:** Record the deployment so it can be resumed/rebuilt in a later session.
**Complexity:** 1 point
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - Update `Documentation/Architecture/02-DEPLOYMENT-GUIDE.md` with the remote-backend section (config key, `--listen`, Tailscale addressing).
- [ ] Step 2 - Record service facts in `infra/unraid/CLAUDE.md` (container name, port, compose location, registry tag scheme) per that repo's "keep this file updated as facts are confirmed" rule.
- [ ] Step 3 - Finalize this MAIN_PLAN: mark phases complete, capture lessons learned.

#### Success Criteria
- A fresh session can redeploy or troubleshoot from the docs alone.

---

## Stress Points & Risks
| Risk | Mitigation | Complexity |
|------|------------|------------|
| Desktop LAN IP changes break `ComfyUI__BaseUrl` | Use Tailscale hostname/IP in `.env`; document re-pointing steps | 1 |
| ComfyUI not started with `--listen` → NAS can't reach 8188 | Documented prerequisite; Phase 4 step verifies with curl before deploy | 1 |
| Desktop off / ComfyUI down while app is hosted on NAS | Acceptable: app shows "backend unavailable" (same as today); no auto-recovery needed for v1 | — |
| Preview streaming latency over the extra hop (desktop → NAS → browser) | Fine on gigabit LAN; note Tailscale-from-outside caveat in docs; verify in Phase 4 | 2 |
| Final result retrieval may depend on a local disk path that doesn't exist in the container (previews are WS base64, but final image persistence via ImageService is unverified) | **Resolved by PHASE_1.md D2 + user decision (PHASE_2.md O1)** — N:\ and \\NAS\ are NAS-accessible so bind mounts target vault paths directly; the desktop ComfyUI output dir must be pointed at a NAS-visible folder (WSL → rclone/UNC mount) and `COMFYUI_OUTPUTS_PATH` set to it, since WS previews alone aren't sufficient. Same approach for inputs if needed. WSL setup inspection + infra docs expansion is a recorded follow-up | 3 |
| SQLite DB lost on image update (relative connection string) | **Resolved by PHASE_2.md D2** — one-line Program.cs change reads the connection string from configuration with fallback; compose sets an absolute path under the bind-mounted volume. Verify persistence across image update in Phase 4 | 1 |
| Registry container not running after NAS reboot (`restart=no`) | Check before push/pull; optionally enable autostart with user confirmation | 1 |
| Published port conflict on NAS | Conflict check is an explicit Phase 4 step before choosing the port | 1 |

---

## Changelog
| Phase | Changes |
|-------|---------|
| Planning | Initial plan created: config key, container assets in infra repo, registry push, deploy + verification, docs |
| Phase 1 | Complete and tested (see PHASE_1.md): `ComfyUI:BaseUrl` config key drives both HTTP and WS endpoints; naming amended from plan-literal `ComfyUi:` per user decision (D1); container paths can target N:\ / \\NAS\ directly (D2, resolves final-result-retrieval stress point); findings F1–F3 recorded for Phase 2 Dockerfile/compose work |
| Phase 2 | Complete and tested (see PHASE_2.md): Dockerfile (+ BuildKit companion ignore file), docker-compose.yml, .env.example written to `infra/unraid/services/blazorwebapp/` — path amended from plan-literal `infra/services/` per user decision (D1); one-line Program.cs change makes the SQLite connection string configurable with fallback (D2); compose validated cleanly; open items recorded: WSL→NAS ComfyUI output dir is a Phase 4 prerequisite/follow-up (O1), vault subpaths to verify read-only in Phase 4 (O2, user-confirmed approach), Python.NET and ONNX services deprecated per user decision (O4/O5) |

---

## Code Examples

Config pattern to follow (existing `OllamaService.cs`):

```csharp
_baseUrl = configuration["Ollama:BaseUrl"] ?? "http://10.0.0.11:11434";
_client = new HttpClient { BaseAddress = new Uri(_baseUrl) };
```

Planned equivalent for ComfyUI (single source of truth, shared by HTTP + WS services):

```csharp
// As implemented (Phase 1): read in both constructors from the same key.
var baseUrl = configuration["ComfyUI:BaseUrl"] ?? "http://localhost:8188";
_httpClient.BaseAddress = new Uri(baseUrl);

// WebSocket derivation (as implemented in ComfyUIWebsocketService.cs):
var wsBuilder = new UriBuilder(baseUri);
wsBuilder.Scheme = baseUri.Scheme == Uri.UriSchemeHttps ? Uri.UriSchemeWss : Uri.UriSchemeWs;
wsBuilder.Path = "/ws";
_comfyWsBaseUri = wsBuilder.Uri; // ConnectLoop appends "?clientId=..." via relative URI resolution
```

---

## References
- `Documentation/Plans/IMPLEMENTATION_GUIDE.md` — planning conventions and templates
- `Documentation/Architecture/02-DEPLOYMENT-GUIDE.md` — existing deploy guide (Dockerfile draft, config reference)
- `e:\Documentos\GitHub Repos\infra\unraid\CLAUDE.md` — NAS access, Docker conventions, private registry facts, safety rules
- `BlazorWebApp/Services/OllamaService.cs` — config pattern precedent
- `BlazorWebApp/Services/ComfyUIService.cs`, `BlazorWebApp/Services/ComfyUIWebsocketService.cs` — hardcoded endpoints to replace
