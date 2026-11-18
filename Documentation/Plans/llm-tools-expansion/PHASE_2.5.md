# Phase 2.5 - Tag Builder Quality Detour

## Status

**Phase:** 2.5 (detour from Phase 2)
**Build Status:** Clean (0 errors)
**Phase Status:** [x] Implementation complete - awaiting user validation

---

## Objective

Address weak / unrelated suggestion quality observed in the Phase 2 Tag Builder by fixing the resolution layer, prompt engineering on both passes, and adding a hybrid one-pass mode for capable models. Also add traceability so future quality issues are diagnosable from the UI.

---

## Diagnosis Summary

Reviewing Phase 2 against the symptoms ("suggestions are weak and not that relatable"):

1. **Resolution searches whole phrases against tag names only.** `CsvService.SearchTags` does `searchText.Replace(" ", "_")` and fuzzy-scores against tag names + aliases. A concept like `cyberpunk woman at sunset` collapses to one bad query and never matches `1girl`, `cyberpunk`, `sunset`, `cityscape` separately.
2. **`ExtractedConcept.Category` is captured then thrown away.** `ParseCategoryFromName` only maps `subject/character/artist/copyright`. Every other prefix becomes `null`, so the rich extraction taxonomy never biases the resolver.
3. **Top-K=5 plus no synonym expansion** => Pass 2 receives a small slate, often unrelated to the concept.
4. **Pass 1 prompt has no few-shot examples** and instructs `1-4 words` plain English, which small local models interpret as natural phrases that round-trip badly through the CSV.
5. **Pass 2 forbids the LLM from contributing its own knowledge.** "Select ONE tag per concept from the candidates" caps quality at the resolver.
6. **`RawPass1` / `RawPass2` are hard-coded `null`.** The traceability slot in the data model is never populated, blocking exactly this kind of diagnosis from the UI.
7. **CSV is re-parsed on every concept search.** Discourages the natural fix in #1 (multiple sub-queries per concept).
8. **Minor:** preset quality strings and final-output formatting lack polish.

---

## Execution Checklist

### Step 1: In-memory CSV tag index

**Complexity:** 3
**Status:** [x] Complete

#### Tasks

- [x] Add a lazy in-memory cache to `CsvService` that loads `danbooru.csv` once and reuses the parsed `Tag` list across calls.
- [x] Add `IReadOnlyList<Tag> GetAllTagsCached()` and `bool CheckTagExistsCached(string name)` helpers.
- [x] Refactor `SearchTags` and `CheckTagExists` / `GetTag` to use the cached list internally; preserve external signatures.
- [x] Invalidate cache when the file path changes (config reload not in scope; just re-init when `_path` was empty before).

#### Success Criteria

- A second call to `SearchTags` does not re-parse the CSV file.
- Existing callers (suggestion box, etc.) still behave the same.

---

### Step 2: Phrase decomposition, category bias, K bump in `ResolveAsync`

**Complexity:** 5
**Status:** [x] Complete

#### Tasks

- [x] Expand `ParseCategoryFromName` to recognize every prefix advertised by the Pass 1 system prompt (`subject`, `setting`, `lighting`, `clothing`, `mood`, `style`, `composition`, `color`, `time_of_day`, `weather`, `action`, `accessory`, `pose`, `expression`, `background`). Map them either to a `TagCategory` (mostly `General`) or to a new `string? Hint` field on `ExtractedConcept` for biasing.
- [x] In `ResolveAsync`, for each concept run multiple sub-queries: full-phrase + each significant token (drop stop-words and tokens length<3). Merge candidates, dedupe by tag name, keep best `FuzzyScore` per tag.
- [x] Bias scoring with the concept hint: when `concept.Category` or `Hint` matches a `Tag.Color`, add a fixed boost; never hide non-matching candidates.
- [x] Bump `maxCandidatesPerConcept` to 10.

#### Success Criteria

- "cyberpunk woman" produces both `1girl/solo/...` and `cyberpunk/science_fiction/...` candidates.
- "neon lights" yields `neon_lights`, `glowing`, `city_lights` etc. in candidates.
- Category-toggle filter still works; hint biasing only re-orders.

---

### Step 3: Pass 1 prompt rewrite with few-shot

**Complexity:** 2
**Status:** [x] Complete

#### Tasks

- [x] Rewrite the Pass 1 system prompt to include 2 worked examples and an explicit instruction to emit atomic visual nouns / phrases that map cleanly to Danbooru tag names (snake_case is fine, plain English is fine, but each line must be ONE concept).
- [x] Lower default temperature for Pass 1 to ~0.3 (only when caller doesn't override).

#### Success Criteria

- For "a cyberpunk woman at the beach at sunset", Pass 1 emits at least: subject, setting, time_of_day, style/atmosphere as separate lines.
- Output remains parseable by the existing line/JSON parser.

---

### Step 4: Pass 2 controlled augmentation + verification

**Complexity:** 5
**Status:** [x] Complete

#### Tasks

- [x] Update Pass 2 system prompt to:
  - Pick 1-2 tags per concept from candidates.
  - **Allow** emitting up to N model-augmented Danbooru-style snake_case tags (default N=6) when candidates are weak; mark them with a leading `~`.
  - Forbid prose / commentary; final line is comma-separated.
- [x] Post-process: split tags, strip `~` markers but remember which were augmented, run each augmented tag through `CsvService.CheckTagExistsCached`. Drop unverified augmentations by default; expose a flag to keep them in `TagBuilderResult`.
- [x] Lower default temperature for Pass 2 to ~0.4 (only when caller doesn't override).

#### Success Criteria

- For a concept with no good CSV candidates, the LLM is allowed to suggest a tag and the post-processor verifies it.
- Non-existing augmentations are dropped silently (logged at Debug).

---

### Step 5: Hybrid one-pass mode (Option G)

**Complexity:** 5
**Status:** [x] Complete

#### Tasks

- [x] Add `TagBuilderMode { TwoPass, Hybrid }` enum and a `Mode` field on `TagBuilderRequest` (default `TwoPass`).
- [x] Add `BuildHybridAsync` to `TagPromptService`: single LLM call with a system prompt that asks for a Danbooru tag prompt directly; verify each emitted tag against the CSV cache; drop unverified or mark them.
- [x] In `BuildAsync`, dispatch to either two-pass or hybrid based on `request.Mode`.
- [x] Add a `Mode` toggle to `TagBuilderView` and persist it on `AppStatePromptsLLMTagBuilder`.

#### Success Criteria

- Hybrid produces a valid prompt in a single round-trip with capable models.
- Falls back gracefully (mostly empty result) on weak models without crashing.

---

### Step 6: Traceability + debug panel

**Complexity:** 2
**Status:** [x] Complete

#### Tasks

- [x] Populate `RawPass1` and `RawPass2` in `BuildAsync`; add `RawHybrid` (reuses `RawPass2`) for hybrid.
- [x] Add a "Debug / Raw" `MudExpansionPanel` to `TagBuilderView` showing both raw responses when present.

#### Success Criteria

- Users can inspect the exact LLM responses to debug weak suggestions.

---

### Step 7: Preset polish

**Complexity:** 1
**Status:** [x] Complete

#### Tasks

- [x] Normalize quality-tag strings to use `", "` (comma + space) consistently with final output.
- [x] Add `source_anime, rating_safe` to the Pony preset quality tags (configurable; user can override via system prompt later).
- [x] Document preset strings as "verify dialect at implementation time".

---

## Progress Tracking

| Step | Status | Complexity | Notes                              |
| ---- | ------ | ---------- | ---------------------------------- |
| 1    | [x]    | 3          | CSV in-memory index                |
| 2    | [x]    | 5          | Decomposition + category bias      |
| 3    | [x]    | 2          | Pass 1 few-shot rewrite            |
| 4    | [x]    | 5          | Pass 2 augmentation + verification |
| 5    | [x]    | 5          | Hybrid one-pass mode               |
| 6    | [x]    | 2          | Traceability + debug panel         |
| 7    | [x]    | 1          | Preset polish                      |

**Total:** 23 points.

---

## Open Risks

1. **Hybrid mode model capability.** Smaller local models may not reliably produce Danbooru-vocabulary output in one pass. Mitigation: gracefully fall back to a near-empty prompt and surface raw response in the debug panel; users can switch back to two-pass.
2. **CSV cache memory.** `danbooru.csv` is large; loading it fully may add noticeable RAM. Mitigation: load lazily on first search; do not load on app start. Profile if needed.
3. **Pass 2 augmentation hallucinations.** Even verified tags might be tonally off. Mitigation: drop unverified by default; future phase can add embedding-based similarity.

---

## Phase Summary

Implementation complete. All 7 steps applied, build clean, no errors in modified files.

### Files Created

- `Documentation/Plans/llm-tools-expansion/PHASE_2.5.md` - this document.

### Files Modified

- `BlazorWebApp/Services/CsvService.cs` - Added lazy in-memory cache (`_cachedRawTags`, `_cachedRawNames`), exposed `GetAllTagsCached()` and `CheckTagExistsCached()`, refactored `SearchTags` / `GetTag` / `CheckTagExists` to use the cache. CSV is now parsed once per process lifetime.
- `BlazorWebApp/Models/TagPromptModels.cs` - Added `TagBuilderMode` enum (`TwoPass`, `Hybrid`); added `Hint` field to `ExtractedConcept`; added `Mode` and `KeepUnverifiedAugmentations` to `TagBuilderRequest`; added `Mode` and `DroppedAugmentations` to `TagBuilderResult`.
- `BlazorWebApp/Services/TagPromptService.cs` - Rewrote Pass 1 system prompt with few-shot examples and stricter atomicity rules; replaced `ParseCategoryFromName` with `MapPrefix` returning `(TagCategory?, string? Hint)`; added phrase tokenization (`TokenizeForSearch`) with stop-word list; rewrote `ResolveAsync` to merge full-phrase + per-token sub-queries, apply category/hint biasing, and return top 10 candidates; rewrote Pass 2 system prompt to allow up to 6 `~`-marked augmentation tags; added `PostProcessTags` that verifies augmentations against `CsvService.CheckTagExistsCached`; added `BuildHybridAsync` (one-pass mode); `BuildAsync` now dispatches by `request.Mode` and populates `RawPass1` / `RawPass2`; lowered default temperatures (0.3 Pass 1, 0.4 Pass 2/Hybrid) when caller doesn't override; updated preset quality strings (Pony now includes `source_anime, rating_safe`; consistent `, ` separators).
- `BlazorWebApp/Models/AppState.cs` - Added `Mode` and `KeepUnverifiedAugmentations` to `AppStatePromptsLLMTagBuilder`.
- `BlazorWebApp/Components/Prompts/LLM/Views/TagBuilderView.razor` - Added pipeline-mode `MudSelect` (Two-Pass / Hybrid), keep-unverified `MudCheckBox`, dropped-tags `MudAlert`, mode chip on result panel, and a "Debug / Raw LLM responses" `MudExpansionPanel` showing both raw responses; persistence wired through `_mode` / `_keepUnverified`.

### What changed for the user

- **Resolution quality**: phrases like `cyberpunk woman` now contribute candidates from `cyberpunk`, `1girl`, `solo`, `science_fiction`, etc., not just whatever fuzzy-matches the whole phrase.
- **Slot diversity**: per-concept candidate count went 5 to 10; Pass 2 may pick 1-2 per concept and add up to 6 verified augmentations, so prompts get richer without losing CSV grounding.
- **Hybrid mode**: optional one-pass pipeline for capable models. Each emitted tag is CSV-verified; unverified tags drop unless "Keep unverified tags" is checked.
- **Diagnostics**: raw LLM responses are now visible in a Debug expansion panel; dropped augmentations are listed in an info alert; previous run produced opaque output.

### Deferred Items

- NSFW gate (still tracked under Phase 2 open risk #3 / Phase 10).
- Per-concept embedding similarity for augmentation quality (potential Phase 10).
- Profile + tune CSV cache memory cost on large `danbooru.csv` files.
