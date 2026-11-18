# Phase 4 - Character Page UI

## Status

Implemented and build-validated.

## Scope

- Add a top-level `/characters` page for Qwen Character reference generation.
- Add a Characters nav entry without duplicating the global project selector.
- Render the page through `TabbedPageShell` and `TwoColumnLayout`.
- Provide a settings sidebar for source image, loader mode, active asset selection, LoRAs, run controls, and advanced sampler/upscale toggles.
- Provide responsive shot grids grouped by slot type, with stable card dimensions, per-slot enablement, run/view/remove actions, prompt expansion, and toolbar add-slot controls.
- Wire UI actions to `ICharacterReferenceRunService.RunAsync(...)`, persisted `AppState.Character`, existing image records, and `AssetViewer`.
- Refine the slot cards with wider responsive tracks, Gallery-style hover actions, compact params expansion, inline icon-only slot kind markers, slot-resolution preview aspect ratios, and a quieter outlined run button.
- Add the Camera add-slot preset as a `BodyAngle` slot with a neutral hidden prompt template that preserves identity, expression, and pose without choosing a camera direction up front.
- Add Body and Outfit add-slot presets as first-class slot types.
- Expose the existing shared LLM toolbar settings on the `/characters` route and use them for per-slot Ollama Suggest actions.
- Register Characters as an image-source Send To target through the existing media send-to dialog.

## Implemented Files

- `BlazorWebApp/Pages/Characters.razor`
- `BlazorWebApp/Components/Character/CharacterAssetSelect.razor`
- `BlazorWebApp/Components/Character/CharacterAssetSelect.razor.css`
- `BlazorWebApp/Components/Character/CharacterReferenceSettings.razor`
- `BlazorWebApp/Components/Character/CharacterReferenceSettings.razor.css`
- `BlazorWebApp/Components/Character/CharacterShotGrid.razor`
- `BlazorWebApp/Components/Character/CharacterShotGrid.razor.css`
- `BlazorWebApp/Components/Character/CharacterShotCard.razor`
- `BlazorWebApp/Components/Character/CharacterShotCard.razor.css`
- `BlazorWebApp/Components/Character/CharacterAddShotCard.razor`
- `BlazorWebApp/Components/Character/CharacterAddShotCard.razor.css`
- `BlazorWebApp/Services/CharacterPromptSuggestionService.cs`
- `BlazorWebApp/Services/MediaSendToService.cs`
- `BlazorWebApp/Components/Shared/NavBar.razor`
- `BlazorWebApp/Components/Shared/TopToolbar.razor`
- `BlazorWebApp/_Imports.razor`

## Decisions

- The page uses `Reference Sheet` as the initial `TabbedPageShell` tab so future Character Creator or saved-character tabs can be added without changing the route.
- `CharacterReferenceSettings` hides inactive loader assets by switching between AIO checkpoint selection and split diffusion/CLIP/VAE selectors.
- `CharacterAssetSelect` uses `IAssetResolverService.GetCachedAssetOptions(...)` so the UI follows the same ComfyUI-backed asset source as the rest of the app.
- `LoraForm` is reused directly for Qwen LoRA selection rather than introducing a Character-specific LoRA editor.
- Per-slot prompt and parameter controls remain collapsed by default; expansion state is persisted through `AppState.Character.Slots`.
- Slot prompts now use hidden `PromptTemplate` text plus visible `PromptExtension` text. The workflow appends the extension after the template with a single separating space.
- Generated output preview cards use `IIOService.GetImageStaticFile(...)` for thumbnails, and full inspection uses the existing `AssetViewer` loaded from the persisted image id.
- Add Slot moved from a trailing grid card into a toolbar button before Enable All/Disable All. The button opens a compact preset popover populated by `CharacterReferenceSlotCatalog.GetAddablePresets()`.
- Shot cards are grouped into separate grids per slot kind with a simple separator between groups.
- Slot preview actions now follow the Gallery card hover-overlay pattern: run, fullscreen, and params are only visible on hover/focus over the image area.
- Front Three-Quarter and Back Three-Quarter now sit directly after Back View so default slots are grouped by type before expressions begin.
- The Camera preset does not implement or depend on the ComfyUI multiangle node; any multiangle phrasing is left to the user or a later Suggest action so the default does not steer the slot toward one camera direction.
- Type text on shot cards was replaced by an icon-only cue shown inline before the slot label. The textual kind remains available through the icon tooltip.
- Shot preview frames now use each slot's `Width` and `Height` as their CSS aspect ratio, so portrait, square, and landscape slots read correctly in the grid.
- Character now reuses `ToolbarLLMSettings`; this component stores selections in `State.State.Generation.LLM`, which is already the shared LLM settings object used by Generate and LLM Tools.
- The Suggest action is hidden for Blank slots, uses Ollama JSON mode, and applies both a suggested label and prompt extension for editable custom slots.
- The shared media Send To dialog now includes `Characters / Source Image` for local images and routes the selected image into `AppState.Character.SourceImage` before navigating to `/characters`.
- Character source images now treat ComfyUI `LoadImage` filenames as per-run values. When source `ImageDataUri` is available, the run service uploads a fresh Comfy input and clears stale persisted `ImagePath` state so a failed or cleaned-up prompt cannot poison the next run.
- Live `RTXVideoSuperResolution` probing confirmed the app emits valid inputs for `images`, `resize_type`, `resize_type.scale`, and `quality`. Because `NvVFX_Load` failures happen inside the NVIDIA custom node runtime, Character generation now retries once without RTX upscale, disables the RTX toggle, and persists the successful non-RTX output instead of failing every slot.

## Validation Targets

- Razor diagnostics passed for the new Character page/components, navbar, and imports.
- VS Code `build` task passed for `BlazorWebApp/BlazorWebApp.csproj`.
- Running app route smoke test returned HTTP 200 for `/characters`.
- `CharacterStateTests`: 11 passed after adding default ordering, Camera, Body/Outfit, and prompt-extension coverage.
- Focused Character/workflow/send-to validation passed: 30 tests, 0 failures.
- Focused Character run-service validation passed after source upload and RTX fallback hardening: 5 tests, 0 failures.
- Follow-up VS Code `build` task passed with no Character-specific warnings in the captured output.
- Follow-up running app route smoke test returned HTTP 200 for `/characters`.

Validation command:

```powershell
dotnet build BlazorWebApp/BlazorWebApp.csproj /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary
dotnet test .\BlazorWebApp.Tests\BlazorWebApp.Tests.csproj --filter "FullyQualifiedName~CharacterStateTests" --no-restore
dotnet test .\BlazorWebApp.Tests\BlazorWebApp.Tests.csproj --filter "FullyQualifiedName~CharacterStateTests" --no-restore /p:OutputPath=..\Temp\character-test-output\
dotnet test .\BlazorWebApp.Tests\BlazorWebApp.Tests.csproj --filter "FullyQualifiedName~CharacterStateTests|FullyQualifiedName~QwenCharacterReferenceWorkflowComposerTests|FullyQualifiedName~MediaSendToServiceTests" --no-restore /p:OutputPath=..\Temp\character-test-output\
dotnet test .\BlazorWebApp.Tests\BlazorWebApp.Tests.csproj --filter "FullyQualifiedName~CharacterReferenceRunServiceTests" --no-restore /p:OutputPath=..\Temp\character-test-output\
Invoke-WebRequest -Uri "http://localhost:5051/characters" -UseBasicParsing | Select-Object -ExpandProperty StatusCode
```

Known unrelated noise remains: the project currently emits many existing nullability/analyzer warnings outside the touched Character UI slice. No build errors were introduced by Phase 4.

## Open Follow-Up

- Run the page against a live ComfyUI instance to verify asset option availability, per-slot generation, output thumbnails, and `AssetViewer` inspection end to end.
- If users want multiple outputs per slot, expand app state beyond `LastOutputImageId` and `LastOutputPath` to store a per-slot output history.
- Run a live Ollama Suggest smoke test with the user's selected model to tune the JSON prompt if a model family returns empty or overly long suggestions.
- If RTX upscale is still desired, fix the ComfyUI/container NVIDIA Video Effects runtime separately; the app fallback only prevents the failed node from aborting Character generation.
