# Phase 2: ImageViewer -> AssetInfoPanel Migration

> **Main plan:** [MAIN_PLAN.md](./MAIN_PLAN.md#phase-2-imageviewer---assetinfopanel-migration)
> **Status:** [ ] Not Started
> **Complexity:** 8 points
> **Depends on:** Phase 1 artifacts - `IImageSendToService.IsLocal`, additive `AssetInfoPanel` parameters, and the external metadata-read guard.
> **Unblocks:** Phase 3 - external-image-specific gating and bookmark CTA can be implemented on the shared panel instead of a duplicated `ImageViewer` fork.

---

## 1. Objective

Replace the duplicated inline info panel inside `BlazorWebApp/Components/Shared/Image/ImageViewer.razor` with the shared `AssetInfoPanel` component, while preserving `ImageViewer`'s viewer-specific behavior such as pan/zoom, slideshow, favorite/rating gating, and overlay lifecycle. After this phase, `ImageViewer` no longer owns the legacy `Txt2Img` / `Img2Img` parameter-send UI or its duplicated metadata rendering logic.

---

## 2. Context & Background

`ImageViewer.razor` currently contains an older fork of the info-panel UI. `AssetViewer.razor` already proves the intended composition pattern: it opens the same slide-out shell and places `AssetInfoPanel` inside it. The main plan explicitly chose Approach A to remove duplication and keep a single authoritative implementation.

Inherited constraints from the main plan:

- `Legacy SendSelectedTo(bool) and the Txt2Img/Img2Img routing in ImageViewer are removed`.
- `ImageViewer renders the info panel indistinguishable from the Gallery's AssetViewer on local images.`
- `ImageViewer.IsExternal currently gates favorite/rating UI too - make sure locality introduction does not regress that.`
- `Minimal, focused changes - avoid over-engineering`.

Current code facts the executor should anchor on:

- `ImageViewer.razor` already holds `_selectedParams`, `_showInfo`, `ToggleInfo()`, `Close()`, `SetBodyScrollLock(bool)`, pan/zoom state, and overlay lifecycle logic.
- `AssetInfoPanel.razor` already owns the workflow-aware button sections, prompt blocks, metadata selection, and selected-parameter clearing after workflow send.
- `AssetViewer.razor` already passes `SelectedParams` / `SelectedParamsChanged` and uses the exact composition target this phase should mirror.

This phase intentionally does not add the bookmark CTA yet. It only migrates the info panel to the shared component and removes the dead local code path.

---

## 3. Prerequisites

- **Artifacts from prior phases:**
  - `BlazorWebApp/Services/IImageSendToService.cs` - `IsLocal(string? path)` exists.
  - `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor` - additive parameters and external metadata guard are in place.
- **Files the executor must read before writing code:**
  - `BlazorWebApp/Components/Shared/Image/ImageViewer.razor` - source of truth for overlay behavior and legacy duplicated info panel.
  - `BlazorWebApp/Components/Shared/AssetViewer.razor` - current reference implementation for the shared panel composition.
  - `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor` - target component contract.
  - `BlazorWebApp/Components/Shared/Image/ImageViewer.razor.css` - contains `.info-panel`, `.info-panel-header`, and related viewer-side shell styles.
  - `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor.css` - contains inner panel content styles.
  - `BlazorWebApp/Services/ImageSendToService.cs` - confirms navigation occurs immediately on send.
  - `BlazorWebApp/Data/Entities/Image.cs` - confirms Danbooru `Image` instances only carry prompt-relevant fields.
- **External references:** _Not applicable for this phase._

---

## 4. Files Inventory

### To Create

| Path                             | Purpose |
| -------------------------------- | ------- |
| _Not applicable for this phase._ |         |

### To Modify

| Path                                                            | Change                                                                                          |
| --------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `BlazorWebApp/Components/Shared/Image/ImageViewer.razor`        | Replace the duplicated inline panel with `<AssetInfoPanel>` and remove legacy code-behind paths |
| `BlazorWebApp/Components/Shared/Image/ImageViewer.razor.css`    | Keep only the viewer shell styles that still belong to the overlay panel                        |
| `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor.css` | Extend only if the shared content component needs selector adjustments after migration          |

### To Leave Untouched (but referenced)

| Path                                                         | Why it matters                                                      |
| ------------------------------------------------------------ | ------------------------------------------------------------------- |
| `BlazorWebApp/Components/Shared/AssetViewer.razor`           | Regression reference for the final panel composition                |
| `BlazorWebApp/Components/Shared/Image/AssetInfoDrawer.razor` | Regression reference for the second shared-panel consumer           |
| `BlazorWebApp/Services/ImageSendToService.cs`                | Navigation behavior is validated against it, not changed by default |

---

## 5. Step-by-Step Execution

### Step 2.1: Host `AssetInfoPanel` inside `ImageViewer`

**Complexity:** 3
**Status:** [ ] Not Started

#### Tasks

- [ ] Replace the duplicated `<div class="info-panel-body">...</div>` content in `ImageViewer.razor` with `<AssetInfoPanel>`.
- [ ] Pass `Asset="CurrentImage"`, `IsVideo="IsCurrentVideo"`, `ShowAiMetadata="ShowAiMetadata"`, `ShowKeyboardShortcuts="true"`, `SelectedParams="_selectedParams"`, `SelectedParamsChanged="HandleSelectedParamsChanged"`, and `OnClose="ToggleInfo"`.
- [ ] Preserve the outer `.info-panel` slide-out container owned by `ImageViewer`.

#### Implementation Notes

Match the `AssetViewer` integration pattern rather than trying to nest the old markup inside the shared component. `ImageViewer` should keep the overlay shell and visibility toggle, while `AssetInfoPanel` owns the content. If `ImageViewer` does not yet expose a `HandleSelectedParamsChanged(HashSet<string>)` callback, add one with the same shape used by `AssetViewer` so two-way parameter selection remains explicit.

#### Code Sketch

```razor
<!-- BlazorWebApp/Components/Shared/Image/ImageViewer.razor -->
<div class="info-panel @(_showInfo ? "open" : "")">
    <AssetInfoPanel Asset="CurrentImage"
                    IsVideo="IsCurrentVideo"
                    ShowAiMetadata="ShowAiMetadata"
                    ShowKeyboardShortcuts="true"
                    SelectedParams="_selectedParams"
                    SelectedParamsChanged="HandleSelectedParamsChanged"
                    OnClose="ToggleInfo" />
</div>
```

```csharp
private Task HandleSelectedParamsChanged(HashSet<string> selectedParams)
{
    _selectedParams = selectedParams;
    return Task.CompletedTask;
}
```

#### Conventions to Respect

- Keep `ImageViewer` focused on viewer behavior, not metadata rendering.
- Reuse the existing shared component pattern already proven in `AssetViewer`.

#### Validation

- Open `ImageViewer` on a local image and confirm the info panel opens, closes, and reflects metadata selections.
- Compare local rendering against `AssetViewer` to ensure the shared content matches.

#### Changes Made

_To be filled in after the step is implemented._

---

### Step 2.2: Remove dead duplicated logic from `ImageViewer`

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Delete the old send-to button markup, meta grid, prompt blocks, and related duplicated info-panel markup.
- [ ] Remove now-unused methods and fields such as `SendSelectedTo`, `ToggleParam`, `_currentSampler`, `_currentModelName`, and the metadata-loading helpers that only existed for the old panel.
- [ ] Keep pan/zoom, navigation, slideshow, favorites, rating, and body-scroll behavior untouched.

#### Implementation Notes

This cleanup is part of the migration, not a later optional refactor. Once `AssetInfoPanel` owns metadata rendering, `ImageViewer` should not keep parallel state for sampler/model display. Retain only state that belongs to the overlay itself: indexing, pan/zoom, visibility, autoplay, and the selected-parameter hash set used by the shared component.

#### Code Sketch

```csharp
// Keep viewer-owned state only
private ElementReference _overlayRef;
private int _currentIndex;
private bool _showInfo;
private bool _autoPlay;
private HashSet<string> _selectedParams = new();

// Remove legacy panel-only members:
// private string? _currentSampler;
// private string? _currentModelName;
// private async Task LoadImageMetadata() { ... }
// private async Task SendSelectedTo(bool isImg2Img) { ... }
// private void ToggleParam(string param) { ... }
```

#### Conventions to Respect

- `Legacy SendSelectedTo(bool) and the Txt2Img/Img2Img routing in ImageViewer are removed`.
- Keep changes focused; do not refactor unrelated viewer behavior while deleting dead code.

#### Validation

- Run a targeted build after removing the dead members.
- Verify keyboard shortcuts and favorite/rating behavior still work in the overlay.

#### Changes Made

_To be filled in after the step is implemented._

---

### Step 2.3: Validate scroll unlock on workflow navigation, then add fallback only if needed

**Complexity:** 2
**Status:** [ ] Not Started

#### Tasks

- [ ] Validate the approved default path first: opening `ImageViewer`, sending to a workflow from `AssetInfoPanel`, and confirming body scroll is released on navigation with the existing overlay teardown.
- [ ] If validation fails, add a local `NavigationManager.LocationChanged` fallback inside `ImageViewer` while the overlay is visible.
- [ ] Do not add a new callback surface to `AssetInfoPanel` unless the local fallback also proves insufficient.

#### Implementation Notes

The planning discussion resolved this phase to the leaner path. `ImageSendToService` already navigates immediately through `NavigationManager.NavigateTo($"/generate/{workflow.Id}")`, and `ImageViewer` already releases scroll in both `Close()` and `Dispose()`. Treat explicit navigation hooks as a fallback, not the default design. If a fallback is required, keep it local to `ImageViewer` so the shared content component does not gain viewer-specific lifecycle concerns.

#### Code Sketch

```csharp
// Fallback only if validation proves Dispose is insufficient
[Inject] private NavigationManager Navigation { get; set; } = default!;

protected override void OnInitialized()
{
    Navigation.LocationChanged += HandleLocationChanged;
}

private async void HandleLocationChanged(object? sender, LocationChangedEventArgs e)
{
    if (IsVisible)
        await SetBodyScrollLock(false);
}

public void Dispose()
{
    Navigation.LocationChanged -= HandleLocationChanged;
    StopAutoPlay();
    _ = SetBodyScrollLock(false);
}
```

#### Conventions to Respect

- Prefer the smallest working change and validate before widening the component API.
- Keep the solution local to the viewer if a fix is required.

#### Validation

- Manual check: open viewer -> send image or parameters to a workflow -> land on `/generate/{workflowId}` -> verify page scroll is normal.
- If the fallback was added, repeat the same navigation check and confirm no duplicate event subscriptions remain after closing/reopening the viewer.

#### Changes Made

_To be filled in after the step is implemented._

---

### Step 2.4: Reconcile info-panel shell CSS

**Complexity:** 1
**Status:** [ ] Not Started

#### Tasks

- [ ] Remove obsolete selector overlap from `ImageViewer.razor.css` if the shared panel now owns the corresponding inner content styles.
- [ ] Prefer keeping inner-content selectors in `AssetInfoPanel.razor.css` and outer-shell selectors in `ImageViewer.razor.css`.
- [ ] Validate that the slide-out width, transition, and content scrolling still behave correctly.

#### Implementation Notes

`ImageViewer.razor.css` currently contains both the slide-out shell (`.info-panel`, `.info-panel.open`) and duplicated content selectors such as `.info-panel-header`, `.meta-grid`, and `.prompt-block`. After the migration, the shared content selectors belong in `AssetInfoPanel.razor.css`; the viewer stylesheet should mostly retain the overlay shell and positioning concerns.

#### Code Sketch

```css
/* BlazorWebApp/Components/Shared/Image/ImageViewer.razor.css */
.info-panel {
  position: fixed;
  top: 0;
  right: 0;
  bottom: 0;
  width: 340px;
  transform: translateX(100%);
}

.info-panel.open {
  transform: translateX(0);
}
```

#### Conventions to Respect

- Prefer extending `AssetInfoPanel.razor.css` for shared content styling.
- Do not duplicate content selectors across both components unless there is a demonstrated scoping requirement.

#### Validation

- Check desktop and narrow-width rendering of the panel.
- Confirm the panel body still scrolls independently from the underlying page.

#### Changes Made

_To be filled in after the step is implemented._

---

## 6. Integration Points

- **DI registrations:** none.
- **Events to publish / subscribe:** none required by default in this phase. If a `LocationChanged` fallback is needed, it is a local `NavigationManager` event subscription inside `ImageViewer`, not a new app-level pub/sub event.
- **Configuration bindings:** none.
- **Startup side-effects:** none.

---

## 7. Testing Strategy

- **Automated tests to add/update:** _Not applicable for this phase unless compile-only cleanup exposes a natural unit seam. The main verification is behavioral._
- **Manual verification checklist:**
  1. Open Danbooru viewer on a search result and confirm the shared info panel opens and closes.
  2. Open Danbooru viewer on a library item and confirm the local-image UI matches Gallery behavior.
  3. In Gallery `AssetViewer`, open the info panel and confirm no visual or functional regression.
  4. Send parameters from the viewer and confirm navigation lands on `/generate/{workflowId}`.
  5. Confirm body scroll is restored after navigation from the viewer.
- **Regression watch-list:**
  - `IsExternal` favorite/rating gating.
  - Keyboard shortcuts inside `ImageViewer`.
  - `AssetViewer` and `AssetInfoDrawer` shared-panel behavior.

---

## 8. Stress Points Specific to This Phase

- **Visual regression in Gallery consumers**
  - Failure mode: additive changes in the shared component make the Gallery panel look or behave differently.
  - Mitigation: compare against `AssetViewer.razor` during validation and keep defaults identical.
- **Selected-parameter mismatch for Danbooru assets**
  - Failure mode: hidden AI metadata still leaves stale parameter selections or exposes invalid values.
  - Mitigation: validate with Danbooru `Image` instances, which currently only populate `Path`, `Prompt`, `Width`, and `Height` from `new Image(post)`.
- **Body scroll remains locked after workflow navigation**
  - Failure mode: navigating from `ImageViewer` leaves `document.body.style.overflow = 'hidden'`.
  - Mitigation: validate the existing `Close()`/`Dispose()` path first; add a viewer-local `LocationChanged` fallback only if the check fails.
- **CSS ownership becomes split-brain**
  - Failure mode: duplicated content selectors in both `.razor.css` files diverge.
  - Mitigation: keep shell styles in `ImageViewer.razor.css` and shared content styles in `AssetInfoPanel.razor.css`.

---

## 9. Resolved Assumptions

- **Scroll-unlock default:** The approved default is the existing dispose-based teardown path. `ImageSendToService` already navigates immediately, and `ImageViewer` already releases body scroll in `Close()` and `Dispose()`.
- **Selected-parameter callback shape:** Mirror `AssetViewer` and keep a narrow `SelectedParamsChanged` handoff in `ImageViewer` instead of letting the shared panel mutate outer state implicitly.
- **CSS ownership split:** The viewer keeps the slide-out shell; the shared component owns the inner content styles.

---

## 10. Open Clarifications

_None - phase is fully specified._

---

## 11. Progress Tracking

| Step | Status | Complexity | Notes                                                |
| ---- | ------ | ---------- | ---------------------------------------------------- |
| 2.1  | [ ]    | 3          |                                                      |
| 2.2  | [ ]    | 2          |                                                      |
| 2.3  | [ ]    | 2          | Default to validation-first; fallback only if needed |
| 2.4  | [ ]    | 1          |                                                      |

---

## 12. Issues & Resolutions

_Populated during execution._

### Issue: _Not applicable for this phase yet._

- **Impact:** Pending
- **Resolution:** Pending

---

## 13. Commit Checkpoints

- [ ] Step 2.1 complete
- [ ] Step 2.2 complete
- [ ] Step 2.3 complete
- [ ] Step 2.4 complete
- [ ] Phase build green

---

## 14. Phase Summary

_To be filled in after the phase is complete._

- **Accomplishments:**
- **Deferred to later phase:**
- **Lessons learned:**

---

## 15. Cross-References

- Main plan section: [Phase 2: ImageViewer -> AssetInfoPanel Migration](./MAIN_PLAN.md#phase-2-imageviewer---assetinfopanel-migration)
- Prior phase: [PHASE_1.md](./PHASE_1.md)
- Next phase: [PHASE_3.md](./PHASE_3.md)
- Related plans / docs:
  - `BlazorWebApp/Components/Shared/Image/ImageViewer.razor`
  - `BlazorWebApp/Components/Shared/AssetViewer.razor`
  - `BlazorWebApp/Components/Shared/Image/AssetInfoPanel.razor`
  - `BlazorWebApp/Components/Shared/Image/ImageViewer.razor.css`
