# Phase 3 - Character Run Service And Output Collector

## Status

Implemented and validated.

## Scope

- Add a backend service for running Character reference slots without registering the Character composer as a Generate-page workflow.
- Resolve requested slots into dependency-safe execution order, including front-view and neutral prerequisite slots when needed.
- Submit the composed ComfyUI workflow directly and collect images by the expected `SaveImage` node ids returned by the composer.
- Persist collected ComfyUI output files as gallery `Image` records for the active project.
- Update `AppState.Character.Slots` with run status, last image id, and last output path.

## Implemented Files

- `BlazorWebApp/Services/ICharacterReferenceRunService.cs`
- `BlazorWebApp/Services/CharacterReferenceRunService.cs`
- `BlazorWebApp/Services/CharacterReferenceSlotPlanner.cs`
- `BlazorWebApp/Services/ComfyUIHistoryImageOutputCollector.cs`
- `BlazorWebApp/Models/ComfyImageOutputResponse.cs`
- `BlazorWebApp/Services/IComfyUIService.cs`
- `BlazorWebApp/Services/ComfyUIService.cs`
- `BlazorWebApp/Program.cs`

## Decisions

- Character run submission uses `PostWorkflowForImageOutputsAsync` instead of the existing Generate flow so it can map multiple slot outputs deterministically by node id.
- The legacy `GetFilenameFromHistory` behavior remains unchanged and still reads the first output node for existing image generations.
- Character image records point at the ComfyUI output file path returned by history instead of copying the image into a second app-managed output folder.
- Batch slot outputs are collected by node id, but Phase 3 records the first output image as the slot's `LastOutputImageId` and `LastOutputPath` because the app state currently has single-output fields.

## Validation Targets

- `CharacterReferenceSlotPlannerTests`: 3 passed.
- `ComfyUIHistoryImageOutputCollectorTests`: 1 passed.
- `CharacterReferenceRunServiceTests`: 3 passed, including dependency, partial-output, and custom-slot output persistence coverage.
- Existing `QwenCharacterReferenceWorkflowComposerTests`: 7 passed.
- Existing `CharacterStateTests`: 7 passed.

Validation commands:

```powershell
dotnet test .\BlazorWebApp.Tests\BlazorWebApp.Tests.csproj --filter "FullyQualifiedName~CharacterReferenceSlotPlannerTests|FullyQualifiedName~ComfyUIHistoryImageOutputCollectorTests|FullyQualifiedName~CharacterReferenceRunServiceTests" --no-restore
dotnet test .\BlazorWebApp.Tests\BlazorWebApp.Tests.csproj --filter "FullyQualifiedName~QwenCharacterReferenceWorkflowComposerTests|FullyQualifiedName~CharacterStateTests" --no-restore
```

Known unrelated noise remains: package vulnerability warnings for `Magick.NET-Q16-AnyCPU`, `Microsoft.Bcl.AsyncInterfaces` fallback resolution, and existing nullability/analyzer warnings outside the touched Character slice.

## Open Follow-Up

- Phase 4 UI should call `ICharacterReferenceRunService.RunAsync(...)` for all slots or selected slots.
- If Character slot batches become first-class in the UI, add a per-slot output history collection instead of only tracking the first persisted image in app state.
