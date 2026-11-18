# Phase 1 - State And Domain Model

## Status

**Current Step:** Complete and tested  
**Parent Plan:** `Documentation/Plans/qwen-character-reference-sheet/MAIN_PLAN.md`  
**Complexity:** 8 points

---

## Scope

This phase establishes the persisted app-state shape and domain defaults for the Character page. It does not add UI, workflow fragments, ComfyUI execution, output collection, or database entities.

---

## Checklist

- [x] Add app-state model for Character page state at `AppState.Character`.
- [x] Add slot definition/state models for default and custom slots.
- [x] Add loader mode model and asset-state model.
- [x] Add default slot catalog with renamed labels and addable presets.
- [x] Add basic unit tests for default slot construction, label normalization, and add-slot behavior.

---

## Implementation Notes

- App-state root is `AppState.Character`.
- Default AIO checkpoint is `Base/Qwen-Rapid-AIO-NSFW-v19.safetensors`.
- RTX upscale defaults enabled.
- Missing prerequisites should be auto-run in later phases unless output is already available.
- `easy cleanGpuUsed` remains an advanced/OOM mitigation toggle for later workflow execution.
- Default slots are created from `CharacterReferenceSlotCatalog.CreateDefaultSlots()` and exclude the three custom source slots.
- The add card presets are modeled by `CharacterReferenceSlotCatalog.GetAddablePresets()` and include Expression, Pose, Landscape, and Blank.
- Loader-mode active asset exposure is centralized through `AppStateCharacter.ActiveAssets`, which returns either checkpoint assets or split diffusion/CLIP/VAE assets.

---

## Validation Plan

- [x] Focused model tests for default slot construction.
- [x] Focused model tests for addable presets and label normalization.
- [x] Focused model tests for loader-mode active asset exclusivity.
- [x] File diagnostics/build validation after edits.

Validation command:

```powershell
dotnet test .\BlazorWebApp.Tests\BlazorWebApp.Tests.csproj --filter FullyQualifiedName~CharacterStateTests --no-restore
```

Result: passed, 7 tests succeeded. The run emitted existing repo-wide warnings, including package vulnerability warnings for `Magick.NET-Q16-AnyCPU` and nullable/analyzer warnings outside the new Character files.
