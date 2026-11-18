# Gallery Density Optimization - Implementation Plan

## Status

**Current Phase:** Execution complete (Phase 6 complete)

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow (per step)

1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update Phase Document**
   - Do NOT proceed to the next step until testing is complete.
   - User must explicitly approve before updating the phase document.
   - Build runs only after user requests or after completing all file edits for a step.

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

- Each step is a commitable checkpoint for safe implementation.
- No time/date references in plan or phase documents.
- Detours are acceptable after discussion and must be appended to this plan or split into a new phase.
- Phase documents must contain enough context to resume in a new session.
- Changes must remain minimal and focused on Gallery density, shell width, and documented UI conventions.
- User permission is required before moving from one implementation phase to the next.

### Documentation Requirements

- Create `PHASE_{#}.md` when entering a new phase.
- Update phase documents after each approved step completion.
- Document all issues, blockers, and resolutions.
- Update `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` if the implementation changes global shell width semantics, introduces a reusable gallery density pattern, or adds a new token.

---

## Problem Statement

The Gallery page currently gives too much visual priority to surrounding controls and not enough priority to the images. The project strip consumes substantial vertical space, the folder tabs and action cluster feel heavy, and image cards carry rich metadata/action overlays that are useful elsewhere but too visually noisy for a pure browsing workflow.

The page is also affected by width constraints. A global `--app-shell-max-width` token exists and is used by `TabbedPageShell`, but the app body is still wrapped in `MudContainer MaxWidth.ExtraLarge` in `MainLayout`. Gallery-specific containers also add local viewport clamps such as `85vw` / `90vw`. This makes image-first layouts harder to tune consistently.

Paginated Gallery mode is the primary workflow and must be optimized first. Infinite masonry currently has behavioral issues around scroll detection and disk I/O timing, so it should not drive the initial implementation. It should receive a later cleanup phase after the paginated experience is improved.

---

## Proposed Solution

Introduce separate persisted Gallery display modes while leaving the existing navigation mode intact:

- Keep `UseInfiniteScroll` as the existing paginated/infinite navigation toggle.
- Add a visual Gallery presentation mode for image cards: `Rich` / `Pure`.
- Add a project panel density mode: `Expanded` / `Compact`.

Prioritize the paginated Gallery path by adding a dedicated `GalleryImageTile` component for pure browsing rather than changing shared `ImageCard`. Add compact project panel rendering so folder/project controls consume less vertical space. Normalize shell width behavior around `--app-shell-max-width`, with a Gallery-specific width override only if the global app clamp is still not sufficient.

### Key Decisions

| Decision                                                              | Rationale                                                                                                                                             |
| --------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| Keep `UseInfiniteScroll` separate from display modes                  | Navigation behavior and visual density are independent user choices. This avoids mixing pagination state with card presentation.                      |
| Add `GalleryPresentationMode` and `ProjectPanelMode` to Gallery state | Persisted state keeps the page stable across app sessions and avoids mode reset friction.                                                             |
| Create `GalleryImageTile` instead of modifying `ImageCard`            | `ImageCard` is shared by generation and Img2Vid surfaces. A dedicated Gallery tile avoids broad regressions and keeps the image-first layout focused. |
| Optimize paginated mode first                                         | User primarily uses paginated Gallery. Infinite masonry has known loading/detection issues and should not hold up the main improvement.               |
| Convert folder tabs to a compact selector in compact project mode     | Folder tabs are useful in expanded/project management mode, but too visually expensive during browsing.                                               |
| Move secondary project/folder actions into compact menus              | Actions remain available while reducing toolbar height and visual weight.                                                                             |
| Use global shell width token as the primary clamp                     | Width tuning should be global and easy to adjust rather than scattered across MudContainer and local `vw` values.                                     |
| Treat desktop as the primary target                                   | Responsiveness should not be ignored, but the design can assume a desktop-first local application workflow.                                           |

### Conventions

- Follow `Documentation/Plans/IMPLEMENTATION_GUIDE.md` for phase gates, status tracking, and phase documents.
- Follow `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` for layout, token, action, and form conventions.
- Follow `.github/instructions/design-language.instructions.md` and `.github/instructions/tokens.instructions.md` for Razor/CSS edits.
- Prefer `Variant.Text` and `Dense="true"` for compact controls.
- Use `MudIconButton Variant="Variant.Text" Size="Size.Small"` for compact toolbar actions.
- Do not add root `MudPaper` wrappers to layout-slot children unless a focused surface is deliberately needed.
- Keep Gallery-specific CSS variables local and named with a `--gallery-*` prefix.
- Avoid touching `ImageCard` except if a tiny compatibility fix is unavoidable and explicitly discussed.
- Use `AssetViewer` for new Gallery tile fullscreen behavior where feasible.

---

## File Context

### Primary Files

| File                                                              | Purpose                                                                                                 |
| ----------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| `BlazorWebApp/Pages/Index.razor`                                  | Gallery page composition, mode wiring, paginated/infinite branch selection.                             |
| `BlazorWebApp/Components/Gallery/GallerySettings.razor`           | Folder/project strip, filters, project actions, view mode controls.                                     |
| `BlazorWebApp/Components/Gallery/GallerySettings.razor.css`       | Gallery settings layout, project strip density, compact toolbar styling.                                |
| `BlazorWebApp/Components/Shared/Image/ImagesContainer.razor`      | Paginated image grid and toolbar. Primary Gallery browsing path.                                        |
| `BlazorWebApp/Components/Shared/Image/ImagesContainer.razor.css`  | Paginated grid width, gaps, card sizing.                                                                |
| `BlazorWebApp/Components/Gallery/InfiniteScrollMasonry.razor`     | Infinite masonry image path. Later cleanup target.                                                      |
| `BlazorWebApp/Components/Gallery/InfiniteScrollMasonry.razor.css` | Masonry width and gap styling. Later cleanup target.                                                    |
| `BlazorWebApp/Components/Shared/Project/ProjectCard.razor`        | Current Gallery-only project card. Candidate for density parameter or compact replacement.              |
| `BlazorWebApp/Components/Shared/Project/ProjectCard.razor.css`    | Expanded/compact project card dimensions and hover behavior.                                            |
| `BlazorWebApp/Models/AppState.cs`                                 | Persisted Gallery mode state.                                                                           |
| `BlazorWebApp/Components/Shared/MainLayout.razor`                 | Current body wrapper uses `MudContainer MaxWidth.ExtraLarge`; should consume shell width token instead. |
| `BlazorWebApp/wwwroot/site.css`                                   | Global shell width and layout tokens.                                                                   |
| `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`             | Must be updated if shell width semantics or new gallery density conventions become app-level rules.     |

### Files To Avoid Broad Changes In

| File                                                       | Reason                                                                             |
| ---------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| `BlazorWebApp/Components/Shared/Image/ImageCard.razor`     | Shared by Gallery, generation, and Img2Vid. Pure Gallery should use a new tile.    |
| `BlazorWebApp/Components/Shared/Image/ImageCard.razor.css` | Shared visual behavior. Avoid density changes that leak into other pages.          |
| `BlazorWebApp/wwwroot/js/InfiniteMasonry.js`               | Deferred to final infinite-scroll cleanup phase unless required for compatibility. |

---

## Implementation Phases

### Phase 1: State And Width Foundations

**Objective:** Add persisted visual mode state and make global shell width tuning reliable before changing Gallery UI density.
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps

- [ ] Add `GalleryPresentationMode` enum with `Rich` and `Pure` values.
- [ ] Add `ProjectPanelMode` enum with `Expanded` and `Compact` values.
- [ ] Add persisted Gallery state properties with safe defaults that preserve current behavior.
- [ ] Audit `MainLayout` body width handling and replace or neutralize `MudContainer MaxWidth.ExtraLarge` so `--app-shell-max-width` is the actual global page clamp.
- [ ] Verify `TabbedPageShell` and route pages still respect the global width token.
- [ ] Decide whether Gallery also needs a page-local width variable such as `--gallery-shell-max-width`, defaulting to the global token.
- [ ] Update design-language docs if global shell width semantics are clarified or changed.

#### Success Criteria

- Existing Gallery behavior remains unchanged by default.
- `UseInfiniteScroll` still controls only navigation mode.
- Global shell width can be tuned from one CSS variable.
- Gallery can opt into a wider image-first surface without scattering `vw` clamps.
- No unrelated page layout visibly regresses in desktop usage.

---

### Phase 2: Compact Project Panel

**Objective:** Reduce vertical space used by folders/projects while preserving project selection and management actions.
**Complexity:** 8 points
**Status:** [x] Complete

#### Steps

- [ ] Add a project panel density toggle in `GallerySettings` for `Expanded` / `Compact`.
- [ ] Keep current folder tabs and large cards in `Expanded` mode.
- [ ] In `Compact` mode, replace folder tabs with a dense folder selector/dropdown that includes an `All` option.
- [ ] Move lower-frequency actions into compact menus or icon-only toolbar controls: create project, delete folder, remove cover, reorder folders, filter toggle.
- [ ] Create compact project rendering with smaller covers, one-line names, selected state, and hover/overflow actions.
- [ ] Ensure folder selection continues to call `Gallery.SetCurrentFolder` and project selection continues to call `Gallery.SetCurrentProject`.
- [ ] Keep project edit/delete behavior available but visually secondary.
- [ ] Remove nested project `MudPaper` where it creates double-surface weight, or justify it as a focus surface if retained.

#### Success Criteria

- Compact project panel consumes substantially less vertical space than the current strip.
- Expanded mode remains available for project browsing/management.
- All existing project and folder actions remain reachable.
- Selecting folders/projects still refreshes images correctly.
- The current selected project is obvious in compact mode.

---

### Phase 3: Pure Paginated Gallery Tile

**Objective:** Make paginated Gallery image browsing image-first by introducing a dedicated pure tile and denser grid.
**Complexity:** 8 points
**Status:** [x] Complete

#### Steps

- [x] Create `GalleryImageTile.razor` and `GalleryImageTile.razor.css` under `Components/Gallery` or an agreed Gallery-specific folder.
- [x] Render image tiles with minimal radius, no metadata footer, no center action pill by default, and small state indicators only when needed.
- [x] Support click-to-view, favorite toggle, selection, info/menu access, delete, set project, project cover, and send-to actions as needed without recreating the full `ImageCard` overlay weight.
- [x] Prefer a single parent-hosted `AssetViewer` for fullscreen viewing in the paginated path.
- [x] Add a `Pure` branch to `ImagesContainer` for Gallery usage without changing non-Gallery consumers by default.
- [x] Reduce paginated grid gutters and fixed card sizing for pure mode using local `--gallery-*` CSS variables.
- [x] Keep rich mode using the existing `ImageCard` behavior.
- [x] Keep desktop-first layout decisions; only prevent obvious mobile breakage.

#### Success Criteria

- Paginated pure mode shows more image area per viewport than the current rich mode.
- `ImageCard` remains untouched or nearly untouched.
- Existing generated image and Img2Vid surfaces keep their current cards.
- Favorite, selection, fullscreen, delete, set project, and cover workflows still work from Gallery.
- Empty/loading/pagination states remain clear and compact.

---

### Phase 4: Gallery Toolbar And Filter Density

**Objective:** Make Gallery controls feel like tools around the images rather than sections competing with the images.
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps

- [x] Add compact controls for Gallery presentation mode and project panel mode.
- [x] Move mode toggles near existing navigation/filter controls without adding a new heavy toolbar row.
- [x] Replace large filter action buttons with compact icon or small text actions where appropriate.
- [x] Convert `Variant.Outlined` Gallery filter fields to `Variant.Text` unless a specific emphasis reason remains.
- [x] Reduce filter grid spacing to match design-language density rules.
- [x] Keep filters collapsible and default-collapsed.
- [x] Remove inline layout styles that should move to component CSS.

#### Success Criteria

- Gallery controls are discoverable but secondary to the image grid.
- Current filtering behavior is preserved.
- Mode toggles are easy to find and persisted.
- CSS follows token and design-language guidance.

---

### Phase 5: Documentation And Validation

**Objective:** Verify the improved paginated Gallery workflow and document the resulting conventions.
**Complexity:** 3 points
**Status:** [x] Complete

#### Steps

- [x] Run focused compile/error validation for touched Razor/CSS/C# files.
- [x] Run a targeted build if requested or after file edits for the phase are complete.
- [x] Perform desktop visual checks for expanded/rich, compact/rich, compact/pure, and paginated navigation.
- [x] Check that non-Gallery pages still obey global width expectations.
- [x] Update `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md` if global shell width or Gallery density patterns should become reusable guidance.
- [x] Update phase documents with outcomes, blockers, and any accepted detours.

#### Success Criteria

- Paginated Gallery pure mode is the recommended image-first browsing path.
- No broad shared card regressions are introduced.
- Documentation records the new display modes and shell-width expectations.
- Remaining issues are explicitly deferred or tracked.

---

### Phase 6: Infinite Masonry Revisit And AssetViewer Alignment

**Objective:** Revisit infinite masonry after the primary paginated experience is fixed, addressing known loading/detection issues and aligning viewer behavior.
**Complexity:** 8 points
**Status:** [x] Complete

#### Steps

- [x] Investigate scroll detection and delayed disk I/O behavior in `InfiniteMasonry.js` and `InfiniteScrollMasonry.razor`.
- [x] Decide whether the masonry implementation should use CSS columns, IntersectionObserver, ResizeObserver, or another simpler loading strategy.
- [x] Make masonry gap/card sizing compatible with pure Gallery density variables.
- [x] Move infinite masonry viewing toward `AssetViewer` instead of separate `ImageViewer` / `VideoViewer` where feasible.
- [x] Ensure loading next pages feels natural when files appear slowly on disk.
- [x] Validate with enough images/videos to reproduce current limbo-state issues.

#### Success Criteria

- Infinite scroll behavior is reliable enough to be a first-class navigation option again.
- Pure Gallery display mode works consistently in masonry.
- Viewer behavior is closer to the app-wide `AssetViewer` standard.
- Known masonry limitations are documented if any remain.

---

## Stress Points & Risks

| Risk                                                           | Mitigation                                                                                                    | Complexity |
| -------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------- | ---------- |
| Shared `ImageCard` regressions                                 | Use a dedicated `GalleryImageTile`; keep existing `ImageCard` as rich/default behavior.                       | 8          |
| Global shell width affects many pages                          | Treat width foundation as its own phase, inspect route wrappers, and validate several pages before moving on. | 5          |
| Compact project panel hides important actions                  | Use icon tooltips and overflow menus; keep expanded mode for management-heavy workflows.                      | 5          |
| State changes fail against old saved state                     | Add new enum properties with safe defaults and avoid replacing existing `UseInfiniteScroll`.                  | 3          |
| Pure tile duplicates too much `ImageCard` behavior             | Implement only Gallery-critical actions first; leave rich mode for full metadata/action density.              | 5          |
| Paginated grid sizing becomes too rigid                        | Drive density through local CSS variables and keep rich mode unchanged.                                       | 3          |
| Infinite masonry distracts from primary goal                   | Defer to Phase 6 and do not block paginated improvements on masonry fixes.                                    | 3          |
| Design-language conflict around Gallery being non-tabbed route | Document shell-width behavior at app level and keep Gallery-specific exceptions explicit.                     | 3          |

---

## Validation Strategy

- Prefer focused validation for touched files because this repository can have unrelated build/test noise.
- Use `get_errors` on edited files after each implementation phase.
- Run the existing `build` task or targeted `dotnet build BlazorWebApp/BlazorWebApp.csproj --no-restore` after completing file edits for a phase, unless the user requests a different validation path.
- For visual validation, run the app only when implementation begins and inspect the Gallery page in desktop dimensions.
- Check at least these states manually or through browser automation when feasible:
  - Paginated + Expanded + Rich
  - Paginated + Compact + Rich
  - Paginated + Compact + Pure
  - Filter collapsed and expanded
  - Project selection, folder selection, selected-image actions

---

## Open Clarifications

These are not blockers for the plan, but should be resolved before or during implementation:

1. Should `Pure` mode still expose the full send-to workflow menu from image tiles, or should send-to remain in rich mode / info drawer only?
2. Should compact project cards use square thumbnails, landscape strips, or a mixed row with a tiny cover plus text?
3. Should Gallery receive a page-specific width override, or should increasing `--app-shell-max-width` be enough after `MainLayout` is fixed?
4. Should `Pure` mode become the default after implementation, or should existing users stay on `Rich` by default?

Resolved assumptions for now:

- Existing users should remain on `Rich` and `Expanded` defaults until they choose otherwise.
- Paginated mode is the primary implementation and validation target.
- Infinite masonry is deferred even if pure tile markup can technically be reused there.
- Desktop-first design is acceptable, with only basic guardrails for narrow windows.

---

## Changelog

| Phase          | Changes                                                                                                                                                                                                                                                                        |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Planning       | Initial plan created for Gallery visual density, compact project panel, shell width tuning, pure paginated tile, and deferred infinite masonry cleanup.                                                                                                                        |
| Phase 1        | Added persisted Gallery visual modes, replaced the root `MudContainer` clamp with a token-driven app body shell, updated shell-width documentation, and validated with focused diagnostics plus a successful build.                                                            |
| Phase 2        | Added persisted compact project panel rendering, dense folder selection, compact toolbar/menu actions, compact project cards, MudBlazor-aware compact selector/rail sizing, and validated with focused diagnostics plus successful alternate-output builds.                    |
| Phase 4 Detour | Added reusable icon-first Gallery action buttons with hover/focus label reveal, applied them to paginated and masonry navigation/selection actions, removed inline toolbar layout styles, and validated with focused diagnostics plus an alternate-output build.               |
| Phase 4        | Completed Gallery settings/filter density cleanup with dense text-variant fields, compact mode chips, smaller filter/reset actions, component-scoped filter layout CSS, focused diagnostics, alternate-output build validation, and browser snapshot verification.             |
| Phase 5        | Validated the completed paginated Gallery density workflow with focused diagnostics, alternate-output build, desktop browser mode checks, pagination verification, shell-width checks, and documentation close-out.                                                            |
| Phase 6        | Removed duplicate infinite-scroll JS ownership, moved masonry viewing to `AssetViewer`, made appended/removed items and density changes trigger masonry relayout, reused `GalleryImageTile` for pure infinite masonry, and validated scroll append/viewer behavior in browser. |

---

## References

- `Documentation/Plans/IMPLEMENTATION_GUIDE.md`
- `Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md`
- `BlazorWebApp/Pages/Index.razor`
- `BlazorWebApp/Components/Gallery/GallerySettings.razor`
- `BlazorWebApp/Components/Gallery/InfiniteScrollMasonry.razor`
- `BlazorWebApp/Components/Shared/Image/ImagesContainer.razor`
- `BlazorWebApp/Components/Shared/Image/ImageCard.razor`
- `BlazorWebApp/Components/Shared/Project/ProjectCard.razor`
- `BlazorWebApp/Models/AppState.cs`
- `BlazorWebApp/Components/Shared/MainLayout.razor`
- `BlazorWebApp/wwwroot/site.css`
