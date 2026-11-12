# Phase 3: Build and Push Image to Private Registry

> **Main plan:** [MAIN_PLAN.md](./MAIN_PLAN.md)
> **Status:** [x] Complete and tested (user-approved)
> **Complexity:** 2 points
> **Depends on:** Phase 2 (container assets in infra repo, committed as `67f4773`)
> **Unblocks:** Phase 4 (deploy to Unraid + end-to-end verification)

---

## 1. Objective

A reproducible app image available at `10.0.0.11:5000/blazorwebapp:<date-tag>` so the NAS can pull and run it via Compose Manager without further design work.

---

## 2. Steps Completed

### Step 1 — Docker Desktop insecure-registry config [x]

Verified read-only, no change needed: `docker info --format '{{json .RegistryConfig}}'` already contains `"IndexConfigs":{"10.0.0.11:5000":{...,"Secure":false,...}}`. (The Docker Desktop daemon was down at session start; it was started and the engine waited for before checking.)

### Step 2 — `DockerRegistry` running on NAS [x]

Verified read-only via `ssh nas "docker ps -a --filter name=DockerRegistry"`: container **Up** (~25h). No state change required, so no confirmation prompt was needed. Catalog already responding with the pre-existing repos.

### Step 3 — Build, tag, push [x]

Built from this repo's solution root using the infra Dockerfile (BuildKit picks up the `Dockerfile.dockerignore` companion automatically):

```
docker build -f "e:/Documentos/GitHub Repos/infra/unraid/services/blazorwebapp/Dockerfile" \
  -t 10.0.0.11:5000/blazorwebapp:2026-11-11 .
```

→ exit 0; build stage (restore + publish) ~66s, only the repo's known warning noise (CS0414/BL0005/etc.), 0 errors. Local image `10.0.0.11:5000/blazorwebapp:2026-11-11` (2.27GB).

Pushed with `docker push 10.0.0.11:5000/blazorwebapp:2026-11-11` → digest `sha256:90aa5ffb…`.

---

## 3. Test Evidence (success criteria)

| Criterion | Result |
|-----------|--------|
| Image visible in registry catalog | ✓ `ssh nas "curl -s http://localhost:5000/v2/_catalog"` → `{"repositories":["blazorwebapp","hugomatiascom","pokeblazor","stashmanager"]}` |
| NAS can pull it (daemon trusts the registry) | ✓ `ssh nas "docker pull 10.0.0.11:5000/blazorwebapp:2026-11-11"` → Pull complete, digest matches the push (`sha256:90aa5ffb…`). The image is now cached on the NAS — Phase 4's `compose up -d` will not re-download it |

---

## 4. Decisions & Findings (for later phases)

### F1 — `-f <infra Dockerfile>` + companion ignore file works as designed

The build from this repo's solution root picked up `Dockerfile.dockerignore` automatically; the context stayed small and the whole build finished in a few minutes. No changes to any Phase 2 asset were needed.

### F2 — BuildKit produced an attested manifest list

Push and NAS pull both resolved cleanly without any `--platform` pinning, so no extra handling is required for this registry/daemon pair.

---

## 5. Commit Checkpoint

Phase 3 consumed the assets committed in Phase 2 (infra `67f4773`) and produced only a registry image plus plan docs:

| File | Change |
|------|--------|
| `Documentation/Plans/unraid-hosting/PHASE_3.md` | This document |
| `Documentation/Plans/unraid-hosting/MAIN_PLAN.md` | Phase 3 status/steps marked complete; stale "commit pending user request" line under Phase 2 fixed (commits: this repo `df4d1c9`, infra `67f4773`); changelog entry |
