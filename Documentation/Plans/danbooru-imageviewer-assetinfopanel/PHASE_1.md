# Phase 1: Locality Helper & AssetInfoPanel Parameter Extension

> **Main plan:** [MAIN_PLAN.md](./MAIN_PLAN.md#phase-1-locality-helper--assetinfopanel-parameter-extension)
> **Status:** [ ] Not Started
> **Complexity:** 5 points
> **Depends on:** None
> **Unblocks:** Phase 2 - `ImageViewer` can consume `AssetInfoPanel` without changing Gallery behavior, and external URLs can be filtered before metadata/file IO paths run.

---

## 1. Objective

Add a single, reusable locality rule to `IImageSendToService`, then make `AssetInfoPanel` configurable enough for `ImageViewer` to reuse it without regressing existing callers. After this phase, the codebase has one authoritative `IsLocal(string? path)` helper, `AssetInfoPanel` exposes additive parameters for AI-metadata visibility and caller-owned header content, and external Danbooru URLs are prevented from reaching `IO.ReadMetadata(Asset.Path)`.

---

## 2. Context & Background

This phase establishes the safe surface the later migration depends on. Today, `AssetInfoPanel` is already the current implementation of the workflow-aware "Send To" UI, and it is consumed by both `AssetViewer` and `AssetInfoDrawer`. `ImageViewer` still carries a duplicated inline panel. The main plan explicitly chose Approach A: integrate `AssetInfoPanel` into `ImageViewer` and keep one implementation.

Relevant inherited constraints from the main plan:

- `AssetInfoPanel parameters are additive - default behaviour unchanged for existing callers (AssetViewer, AssetInfoDrawer).`
- `Locality detection lives in ImageSendToService (new helper)`.
- `All new event hooks (e.g., bookmark request from external viewer) route through IEventService pub/sub (the Danbooru page is already a subscriber of DanbooruMediaSavedEventArgs).`
- `Minimal, focused changes - avoid over-engineering`.

Current code facts that matter before implementation starts:

- `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor` already owns workflow button enumeration through `IImageSendToService` and metadata loading through `LoadWorkflowData()`.
- `BlazorWebApp/Components/Shared/AssetViewer.razor` renders `<AssetInfoPanel ... ShowKeyboardShortcuts="true" ... />` inside its existing slide-out info panel.
- `BlazorWebApp/Components/Shared/Image/AssetInfoDrawer.razor` renders `<AssetInfoPanel ... ShowKeyboardShortcuts="false" ... />` and expects no visual changes.
- `BlazorWebApp/Services/ImageSendToService.cs` currently calls `_io.GetBase64FromFile(asset.Path)` in all image-send methods, so remote URLs must be blocked before those paths are reachable.

This phase does not change `ImageViewer` yet. It only prepares the shared component and the service contract for the migration that follows.

---

## 3. Prerequisites

- **Artifacts from prior phases:** None.
- **Files the executor must read before writing code:**
  - `BlazorWebApp/Services/IImageSendToService.cs` - service contract to extend without changing existing call sites.
  - `BlazorWebApp/Services/ImageSendToService.cs` - current locality-sensitive send paths and workflow filtering behavior.
  - `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor` - target component that must gain additive parameters only.
  - `BlazorWebApp/Components/Shared/AssetViewer.razor` - regression reference for the Gallery fullscreen consumer.
  - `BlazorWebApp/Components/Shared/Image/AssetInfoDrawer.razor` - regression reference for the drawer consumer.
  - `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor.css` - existing selectors that should remain compatible when markup is extended.
  - `BlazorWebApp/Data/Entities/Image.cs` - confirms Danbooru `Image` instances store the remote CDN URL in `Path`.
  - `BlazorWebApp/Program.cs` - confirms `IImageSendToService` is already registered as `builder.Services.AddScoped<IImageSendToService, ImageSendToService>();`.
- **External references:** _Not applicable for this phase._

---

## 4. Files Inventory

### To Create

| Path                                                     | Purpose                                                                                                                             |
| -------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| `BlazorWebApp.Tests/Services/ImageSendToServiceTests.cs` | Unit coverage for `IsLocal(string? path)` covering local gallery paths, local Danbooru library paths, remote URLs, and empty values |

### To Modify

| Path                                                        | Change                                                                                  |
| ----------------------------------------------------------- | --------------------------------------------------------------------------------------- |
| `BlazorWebApp/Services/IImageSendToService.cs`              | Add `bool IsLocal(string? path)` to the public contract                                 |
| `BlazorWebApp/Services/ImageSendToService.cs`               | Implement `IsLocal` and keep the rule centralized next to the file-based send methods   |
| `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor` | Add additive parameters and short-circuit workflow metadata loading for non-local paths |

### To Leave Untouched (but referenced)

| Path                                                         | Why it matters                                                     |
| ------------------------------------------------------------ | ------------------------------------------------------------------ |
| `BlazorWebApp/Components/Shared/AssetViewer.razor`           | Confirms the baseline caller contract that must remain unchanged   |
| `BlazorWebApp/Components/Shared/Image/AssetInfoDrawer.razor` | Confirms the second baseline caller contract                       |
| `BlazorWebApp/Components/Shared/Image/ImageViewer.razor`     | Not modified in this phase; it consumes the new surface in Phase 2 |
| `BlazorWebApp/Program.cs`                                    | DI registration already exists and should not be duplicated        |

---

## 5. Step-by-Step Execution

Each step below is a commitable checkpoint. Follow the execution order from the main plan: code, focused validation, improvement discussion, then document the phase.

### Step 1.1: Add `IsLocal` to the send service contract and implementation

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Add `bool IsLocal(string? path)` to `IImageSendToService`.
- [ ] Implement the method in `ImageSendToService` with the exact rule from the main plan: `null`/empty -> `false`; `http://` and `https://` -> `false`; anything else -> `true`.
- [ ] Keep the helper pure and synchronous. It must not touch disk, HTTP, or `NavigationManager`.
- [ ] Add unit coverage for `/image/...`, `/files/danbooru/...`, `https://...`, and `null`/empty.

#### Implementation Notes

The helper belongs in the existing send service because that is where remote paths become unsafe today. The later phases should call `SendTo.IsLocal(Asset.Path)` rather than duplicating URL checks in Razor components. Keep the initial implementation intentionally small; the main plan does not require URI parsing, Windows path normalization, or protocol expansion beyond HTTP/HTTPS.

#### Code Sketch

```csharp
// BlazorWebApp/Services/IImageSendToService.cs
public interface IImageSendToService
{
    bool IsLocal(string? path);
    List<Workflow> GetImageWorkflows();
    List<Workflow> GetParameterWorkflows();
    // existing members unchanged
}

// BlazorWebApp/Services/ImageSendToService.cs
public bool IsLocal(string? path)
{
    if (string.IsNullOrWhiteSpace(path))
        return false;

    return !path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}
```

```csharp
// BlazorWebApp.Tests/Services/ImageSendToServiceTests.cs
namespace BlazorWebApp.Tests.Services;

public class ImageSendToServiceTests
{
    [Theory]
    [InlineData("/image/foo.png", true)]
    [InlineData("/files/danbooru/general/score_0/123.jpg", true)]
    [InlineData("https://cdn.donmai.us/sample.jpg", false)]
    [InlineData("", false)]
    public void IsLocal_ReturnsExpectedValue(string? path, bool expected)
    {
        var service = CreateService();

        service.IsLocal(path).Should().Be(expected);
    }
}
```

#### Conventions to Respect

- `Locality detection lives in ImageSendToService (new helper)`.
- `Minimal, focused changes - avoid over-engineering`.

#### Validation

- Run the targeted service tests for `ImageSendToServiceTests`.
- Confirm the helper does not introduce build errors at existing `IImageSendToService` injection sites.

#### Changes Made

_To be filled in after the step is implemented._

---

### Step 1.2: Extend `AssetInfoPanel` with additive parameters only

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Add `ShowAiMetadata` with default `true`.
- [ ] Keep `ShowKeyboardShortcuts` exposed with default `false` on `AssetInfoPanel`.
- [ ] Add `RenderFragment? HeaderTrailing` to allow caller-owned header content in later phases.
- [ ] Preserve the current render path for `AssetViewer` and `AssetInfoDrawer` when the new parameters are not supplied.

#### Implementation Notes

`AssetInfoPanel` already has `ShowKeyboardShortcuts`; the work here is to make the default contract explicit and to introduce only additive parameters. Do not reshape the component API around Danbooru-specific naming. The phase goal is to make the panel reusable by `ImageViewer` while keeping current Gallery consumers byte-for-byte equivalent unless they opt in.

#### Code Sketch

```razor
@code {
    [Parameter] public ImageEntity? Asset { get; set; }
    [Parameter] public bool IsVideo { get; set; }
    [Parameter] public bool ShowAiMetadata { get; set; } = true;
    [Parameter] public bool ShowKeyboardShortcuts { get; set; } = false;
    [Parameter] public RenderFragment? HeaderTrailing { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public HashSet<string>? SelectedParams { get; set; }
    [Parameter] public EventCallback<HashSet<string>> SelectedParamsChanged { get; set; }
}
```

```razor
<div class="info-panel-header">
    <MudIcon Icon="@(IsVideo ? "fa-solid fa-video" : "fa-solid fa-image")" Size="Size.Small" />
    <span>@(IsVideo ? "Video" : "Image") Details</span>
    <div class="header-spacer"></div>
    @HeaderTrailing
    <button class="viewer-btn small" @onclick="OnClose" title="Close">
        <MudIcon Icon="fa-solid fa-xmark" Size="Size.Small" />
    </button>
</div>
```

#### Conventions to Respect

- `AssetInfoPanel parameters are additive - default behaviour unchanged for existing callers (AssetViewer, AssetInfoDrawer).`
- Do not make Gallery callers pass new required parameters.

#### Validation

- Build the project after the parameter additions.
- Smoke-check the `AssetViewer` and `AssetInfoDrawer` callers for compile-time compatibility only in this step; behavior testing lands in later phases.

#### Changes Made

_To be filled in after the step is implemented._

---

### Step 1.3: Short-circuit workflow metadata loading for external URLs

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] Update `LoadWorkflowData()` in `AssetInfoPanel` to return early when the asset path is not local.
- [ ] Keep all existing parsing behavior for local assets unchanged.
- [ ] Verify that external Danbooru images do not attempt `IO.ReadMetadata(Asset.Path)`.

#### Implementation Notes

This guard belongs in `AssetInfoPanel`, not in the later Danbooru caller, because `AssetInfoPanel` is the code that currently performs the metadata read. The short-circuit should happen before the `try` block so the remote-path case is a deliberate no-op, not an exception-swallowing path.

#### Code Sketch

```csharp
private async Task LoadWorkflowData()
{
    if (Asset == null || string.IsNullOrWhiteSpace(Asset.Path))
        return;

    if (!SendTo.IsLocal(Asset.Path))
    {
        _workflowData = null;
        return;
    }

    try
    {
        var metadataInfo = await IO.ReadMetadata(Asset.Path);
        // existing parsing unchanged
    }
    catch
    {
        // existing ignore behavior unchanged
    }
}
```

#### Conventions to Respect

- Keep the implementation local to the component that owns the metadata read.
- Preserve the current parsing behavior for local assets.

#### Validation

- Confirm local Gallery assets still surface workflow data.
- Confirm remote Danbooru assets skip metadata reads without warnings or broken UI.

#### Changes Made

_To be filled in after the step is implemented._

---

## 6. Integration Points

- **DI registrations:** none in this phase; `Program.cs` already contains `builder.Services.AddScoped<IImageSendToService, ImageSendToService>();`.
- **Events to publish / subscribe:** none added in this phase.
- **Configuration bindings:** none.
- **Startup side-effects:** none.

---

## 7. Testing Strategy

- **Automated tests to add/update:**
  - `BlazorWebApp.Tests/Services/ImageSendToServiceTests.cs` - add `IsLocal` coverage for gallery paths, Danbooru library paths, remote URLs, and empty/null.
- **Manual verification checklist:**
  1. Open a Gallery asset in `AssetViewer` and confirm the info panel still renders as before.
  2. Open `AssetInfoDrawer` and confirm no caller changes were required.
  3. Inspect a remote Danbooru `Image` through `AssetInfoPanel` and confirm workflow metadata is simply absent rather than erroring.
- **Regression watch-list:**
  - Gallery info panel rendering.
  - Existing workflow data expansion for local images.
  - Component compile compatibility at all `AssetInfoPanel` call sites.

---

## 8. Stress Points Specific to This Phase

- **Locality check false positives or negatives**
  - Failure mode: local routes such as `/image/...` or `/files/danbooru/...` get treated as remote and lose workflow-send capability later.
  - Mitigation: encode the four known path shapes in unit tests before Phase 2 consumes the helper.
- **Visual regression in `AssetViewer` or `AssetInfoDrawer`**
  - Failure mode: additive parameters accidentally alter header or shortcut rendering for existing consumers.
  - Mitigation: keep parameter defaults aligned to current usage and avoid changing caller markup in this phase.
- **External metadata reads still occurring**
  - Failure mode: `IO.ReadMetadata(Asset.Path)` still runs against `https://...` and silently 404s.
  - Mitigation: return early from `LoadWorkflowData()` before the IO call when `!SendTo.IsLocal(Asset.Path)`.
- **Backend unavailable means empty workflow lists**
  - Failure mode: executor mistakes an empty workflow list for a regression while validating the component.
  - Mitigation: validate render stability separately from backend-dependent workflow population.

---

## 9. Resolved Assumptions

- **Scope of `IsLocal`:** The helper is a path classifier only. The main plan defines a simple HTTP/HTTPS exclusion rule, and the current service is the code that would fail on remote paths.
- **Test placement:** `BlazorWebApp.Tests/Services` is the correct home for the new tests because the repository already groups service-level xUnit/Moq tests there.
- **Header extensibility shape:** `RenderFragment? HeaderTrailing` is the narrowest additive hook because the close button and header shell already live inside `AssetInfoPanel`.

---

## 10. Open Clarifications

_None - phase is fully specified._

---

## 11. Progress Tracking

| Step | Status | Complexity | Notes |
| ---- | ------ | ---------- | ----- |
| 1.1  | [ ]    | 2          |       |
| 1.2  | [ ]    | 2          |       |
| 1.3  | [ ]    | 1          |       |

---

## 12. Issues & Resolutions

_Populated during execution._

### Issue: _Not applicable for this phase yet._

- **Impact:** Pending
- **Resolution:** Pending

---

## 13. Commit Checkpoints

- [ ] Step 1.1 complete
- [ ] Step 1.2 complete
- [ ] Step 1.3 complete
- [ ] Phase build green

---

## 14. Phase Summary

_To be filled in after the phase is complete._

- **Accomplishments:**
- **Deferred to later phase:**
- **Lessons learned:**

---

## 15. Cross-References

- Main plan section: [Phase 1: Locality Helper & AssetInfoPanel Parameter Extension](./MAIN_PLAN.md#phase-1-locality-helper--assetinfopanel-parameter-extension)
- Prior phase: N/A
- Next phase: [PHASE_2.md](./PHASE_2.md)
- Related plans / docs:
  - `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor`
  - `BlazorWebApp/Services/ImageSendToService.cs`
  - `BlazorWebApp/Components/Shared/AssetViewer.razor`
  - `BlazorWebApp/Components/Shared/Image/AssetInfoDrawer.razor`
