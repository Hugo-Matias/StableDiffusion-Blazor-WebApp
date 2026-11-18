# LM Studio Caption Extraction & Multi-Source Caption Library - Implementation Plan

## Status
**Current Phase:** Planning

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
- **1**: Trivial | **2**: Simple | **3**: Moderate | **5**: Medium | **8**: Complex | **13**: Very complex | **21+**: Epic (split it)

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

The caption library (committed as the caption-library-integration feature) loads a single hard-coded file: `BlazorWebApp/Data/captions.csv`. There is no way to:

1. Generate captions for local image folders using the user's LM Studio server (OpenAI-compatible API at `localhost:1234`).
2. Load more than one caption set into the app, or switch between them in the UI.

Goal: a simple util script that extracts structured captions from images via LM Studio and writes CSVs matching the existing schema, plus multi-source support in the app with a source selector in the Caption Library panel.

---

## Proposed Solution

Two independent pieces, each its own phase:

### 1. `Utils/Generate-Captions.py` (Python, stdlib-only)
- Discovers images (`png`, `jpg`, `jpeg`, `webp`) under a user-supplied path.
- Computes **aspect ratio locally** from image dimensions via header parsing (PNG IHDR / JPEG SOF / WebP chunks) — no Pillow dependency, exact values, no model involvement.
- Sends each image to LM Studio (`POST /v1/chat/completions`, base64 data URL in an `image_url` content part) with a strict-JSON prompt requesting the full caption schema.
- Parses the model's JSON into CSV rows matching the committed schema exactly:
  `Caption, Aspect Ratio, Nudity, Artistic Quality, Erotic Score, Pornographic Score, Males, Females, Clothing` (Clothing as a single-quote JSON-array string, same convention as the existing file).
- Writes to `BlazorWebApp/Data/Captions/{output-name}.csv`.
- **Resumable**: images whose filename already appears in the output CSV are skipped; failures are reported and can be retried on re-run. Supports `--dry-run`, `--model` (defaults to first model from `GET /v1/models`), `--base-url` (default `http://localhost:1234`).

### 2. Multi-source support in the app
- Move `Data/captions.csv` → `Data/Captions/captions.csv` (`git mv`, same commit as the service change).
- `CaptionLibraryService` enumerates `Data/Captions/*.csv` at load time, keeps one lazy cache per source (same double-checked-lock pattern), and exposes:
  - `Task<List<CaptionSource>> GetSourcesAsync()` — name + row count per file. Display name = filename without extension, first letter capitalized.
  - A `source` parameter on `SearchAsync`, `GetRandomCaptionAsync`, `GetByIdAsync`, and the distinct-value methods. Row `Id`s remain per-file; all lookups are scoped by source.
- `AppStatePromptsCaptionLibrary` gains a `SelectedSource` (string, persisted). The Caption Library filter bar gets a source selector (`MudSelect`, `Variant.Text`) that resets pagination on change and re-queries distinct values for the active source.

### Key Decisions
| Decision | Rationale |
|----------|-----------|
| Python script, stdlib-only | Matches `Fetch-CoTags.py` conventions (argparse, resumable, no dependencies); base64/JSON handling is natural in Python |
| Structured JSON from the model | Generated sets keep full filter-bar compatibility (nudity, scores, counts, clothing). Robust parse with raw-text fallback keeps it safe |
| Aspect ratio computed locally | Exact, free, and removes one failure mode from the model prompt |
| `Data/Captions/` folder in repo | Zero-config auto-discovery; generated sets live next to the app content root. Trade-off accepted: generated CSVs become part of the working tree (user can gitignore or commit per set) |
| Per-source cache, source-scoped Ids | Minimal change to the existing lazy-load pattern; avoids composite-key churn across DTO/UI |

### Conventions
- Script follows `Fetch-CoTags.py` style: module docstring with usage block, argparse, stdlib only (`urllib`, `json`, `csv`, `argparse`).
- CSV output must be RFC 4180 quoted (Python `csv` module default) — Sylvan Csv in the app already handles it.
- UI changes follow the design language: `Variant.Text` controls, no new layout variant, filter bar stays inside the existing TagDrawer panel; children of layout slots render flush.
- Service keeps its logging pattern (warn on missing file, warn per-row parse failures capped at 5).

---

## Implementation Phases

### Phase 1: LM Studio caption extraction script
**Objective:** Working `Utils/Generate-Captions.py` that produces schema-compatible CSVs from an image folder via the local LM Studio server.
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - CLI skeleton: argparse (`--input-path`, `--output-name`, `--model`, `--base-url`, `--dry-run`), image discovery, stdlib dimension/aspect-ratio parsing for PNG/JPEG/WebP; dry-run prints the discovered images with computed ratios.
- [ ] Step 2 - LM Studio client: model listing, chat-completion call with base64 data URL, strict-JSON prompt (see Code Examples), robust JSON extraction (strip code fences / leading prose, validate fields, clamp scores to 0–1, fallback = raw text as Caption with empty metadata).
- [ ] Step 3 - CSV writer + resumability: write/append rows in the exact committed schema to `BlazorWebApp/Data/Captions/{output-name}.csv`; skip images already present by filename; summary report (done/skipped/failed) at exit.

#### Success Criteria
- Dry run lists all supported image types with correct aspect ratios on a test folder.
- A real run against LM Studio produces a CSV that the app's existing loader parses without row failures.
- Re-running skips already-captioned images; killing mid-run and re-running resumes cleanly.
- Malformed model output never crashes the script — falls back to raw-text caption.

---

### Phase 2: Multi-source support in the app
**Objective:** App discovers every CSV under `Data/Captions/`, exposes sources, and lets the user switch active source from the Caption Library panel.
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - Service refactor: `git mv Data/captions.csv Data/Captions/captions.csv`; enumerate folder at load; per-source lazy caches; add `GetSourcesAsync()` and `source` parameters to all query methods (interface + implementation). Keep a sensible default source (first file alphabetically, or "captions" if present) so existing call sites keep working.
- [ ] Step 2 - UI: persist `SelectedSource` in `AppStatePromptsCaptionLibrary`; add source selector to the Caption Library filter bar (`MudSelect`, `Variant.Text`); on change reset page and refresh distinct values (aspect ratios, nudity levels, clothing items) for the active source.

#### Success Criteria
- With two CSVs in `Data/Captions/`, the panel lists both sources with correct row counts.
- Switching source re-filters the grid, random pick, and all filter dropdowns; persisted selection survives app restart.
- The original 82k-row set behaves exactly as before when selected.
- Removing a CSV while the app runs degrades gracefully (empty list + warning log), matching existing behavior for a missing file.

---

### Phase 3: Documentation & wrap-up
**Objective:** Knowledge base reflects the new capability; plan finalized.
**Complexity:** 2 points
**Status:** [ ] Not Started

#### Steps
- [ ] Step 1 - Update `Documentation/Plans/caption-library-integration/MAIN_PLAN.md` with a pointer to multi-source support and this plan; ensure script usage is fully documented in its module docstring.
- [ ] Step 2 - Mark all phases complete here, record lessons learned, note follow-ups (e.g. optional `--max-dim` downscale if large images become a problem).

#### Success Criteria
- A new session can resume purely from these docs: run the script, see sources appear in the app.

---

## Stress Points & Risks
| Risk | Mitigation | Complexity |
|------|------------|------------|
| Model returns malformed / non-JSON output | Strip code fences and leading prose; validate field types; clamp scores to 0–1; fallback stores raw text as Caption with empty metadata, row still written | 2 |
| Row `Id` ambiguity across sources (same Id in two files) | All service lookups take a source parameter; UI always passes the active source. No composite keys introduced | 3 |
| Moving `captions.csv` breaks the hard-coded path | Move and folder enumeration land in the same commit/step; default-source fallback keeps old call sites working | 2 |
| Large images → big base64 payloads / slow inference | Send as-is for v1 (LM Studio handles multi-MB data URLs); optional `--max-dim` downscale deferred to a follow-up if needed | 1 |
| CSV quoting of model text containing commas/quotes/newlines | Python `csv` module writes RFC 4180; app's Sylvan Csv reader already parses the existing file with quoted captions — verified by Phase 1 success criteria | 1 |
| Generated sets bloat the working tree | Accepted trade-off (user decision); user can commit or gitignore per set. Documented in script docstring | 1 |

---

## Changelog
| Phase | Changes |
|-------|---------|
| Planning | Initial plan created: Python extraction script + multi-source app support, decisions confirmed with user (Python / structured JSON / `Data/Captions/` in repo) |

---

## Code Examples

### Structured prompt sent to LM Studio (Phase 1, Step 2)
```text
Describe this image and respond ONLY with a JSON object, no markdown fences, using exactly these keys:
{
  "caption": "<detailed descriptive caption of the scene, subjects, clothing, pose, setting>",
  "nudity": "<none | partial | full>",
  "artistic_quality": <0.0-1.0>,
  "erotic_score": <0.0-1.0>,
  "pornographic_score": <0.0-1.0>,
  "males": <int count of male subjects>,
  "females": <int count of female subjects>,
  "clothing": ["<item>", ...]
}
```

### CSV row mapping (model JSON → committed schema)
| Model field | CSV column | Notes |
|-------------|-----------|-------|
| `caption` | `Caption` | fallback: raw model text if JSON parse fails |
| *(computed locally)* | `Aspect Ratio` | from image dimensions, e.g. `2:3`, reduced fraction or nearest common ratio |
| `nudity` | `Nudity` | validated against known values, else stored as-is |
| `artistic_quality` | `Artistic Quality` | clamped 0–1 |
| `erotic_score` | `Erotic Score` | clamped 0–1 |
| `pornographic_score` | `Pornographic Score` | clamped 0–1 |
| `males` / `females` | `Males` / `Females` | int, default 0 |
| `clothing` | `Clothing` | serialized as single-quote array string: `['dress', 'heels']` (matches existing file convention) |

### Source selector state bridge (Phase 2, Step 2 sketch)
```csharp
// AppStatePromptsCaptionLibrary
public string SelectedSource { get; set; } = "captions";

// Filter bar: MudSelect bound to state.Captions.SelectedSource,
// options from ICaptionLibraryService.GetSourcesAsync();
// OnChange → reset page, re-query SearchAsync(filter, source) and distinct values.
```

---

## References
- `Documentation/Plans/caption-library-integration/MAIN_PLAN.md` — the original caption library feature this extends
- `BlazorWebApp/Services/CaptionLibraryService.cs` / `ICaptionLibraryService.cs` — service being refactored in Phase 2
- `BlazorWebApp/Data/Dtos/CaptionRecord.cs`, `CaptionFilter.cs` — schema and filter DTOs (unchanged)
- `Utils/Fetch-CoTags.py` — script conventions to mirror (docstring, argparse, resumability, stdlib-only)
- LM Studio OpenAI-compatible API: `GET /v1/models`, `POST /v1/chat/completions` at `http://localhost:1234`
