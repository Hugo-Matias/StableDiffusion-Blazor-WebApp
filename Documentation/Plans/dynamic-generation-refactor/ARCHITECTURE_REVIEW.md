# Architecture Review & System Check

## Status Report
**Date:** 2025-01-27
**Scope:** Generation Services, Components, and Workflow Templates

### 1. Service Architecture

#### ImageService
- **Status:** Functional but contains legacy debt.
- **Issues:**
  - Contains `[Obsolete]` methods `GetImages(ModeType)` and `GetVideo()` which are no longer needed with the unified `GenerateImagesAsync` flow.
  - `PrepareGenerationParametersAsync` handles some parameter logic that might be better placed in `GenerationParameterService` to keep `ImageService` focused purely on execution.

#### WorkflowService
- **Status:** Robust, handles schema parsing and composition well.
- **Issues:**
  - Contains `[Obsolete]` method `ParseFragmentDefaults` which is deprecated in favor of schema-based defaults.
  - `RenderFragment` and `ComposeWorkflowFromGenerationParameters` are well separated.

#### GenerationParameterService
- **Status:** Central hub for parameter state.
- **Issues:**
  - `InitializeFromWorkflowAsync` is complex and handles multiple responsibilities (state loading, default resolution, source resolution).
  - Source resolution caching (`_sourceOptionsCache`) is good but could be extracted to a `DataSourceService` if more dynamic sources are added.

### 2. Component Architecture (Generate.razor)

#### State Management (High Priority)
- **Issue:** `Generate.razor` maintains a "Shadow State" of local variables (`_width`, `_height`, `_steps`, etc.) that duplicates data already present in `GenerationParameters`.
- **Impact:** 
  - Requires manual synchronization (`InitializeLocalStateFromFragments`).
  - Requires boilerplate event handlers (`HandleWidthChanged`, etc.) to sync back to service.
  - Increases risk of state desynchronization bugs.
- **Recommendation:** Bind components directly to `GenerationParameters` values or use a lightweight ViewModel wrapper that reads/writes directly to the service.

#### Fragment Discovery
- **Issue:** `DiscoverFragments` logic in `Generate.razor` iterates over fragments to find "Latent" and "Sampler" types using `FragmentType`.
- **Impact:** UI logic is coupled to specific fragment types.
- **Recommendation:** Move discovery logic to `GenerationParameterService` or a helper class. Expose `PrimaryLatentFragment` and `PrimarySamplerFragment` as properties.

#### Component Rendering
- **Issue:** Mixed rendering strategy. "Core" components (`LatentForm`, `SamplerForm`) are hardcoded in the Razor markup, while "Optional" components are rendered dynamically via `RenderOptionalFragmentForm`.
- **Impact:** Adding new core component types requires modifying `Generate.razor`.
- **Recommendation:** Move towards a fully data-driven layout where even core components are rendered based on their `FragmentType` or `Order` in the schema, possibly using a `FragmentRenderer` component.

### 3. Workflow Templates & Scriban

- **Templates:** Found templates for `flux`, `sd`, `qwen`, `wan`, `z-image`.
- **Scriban Files:** `BlazorWebApp\Workflows\*.sbn` appear to be modularized.
- **Validation:** Ensure all `.sbn` files use the new `{{ param | json }}` syntax and have correct `#meta` blocks. (Verified in Phase 7/8, but ongoing vigilance needed).

### 4. Code Cleanup Opportunities

| File | Action | Reason |
|------|--------|--------|
| `ImageService.cs` | Remove `GetImages`, `GetVideo` | Obsolete legacy methods |
| `WorkflowService.cs` | Remove `ParseFragmentDefaults` | Obsolete legacy method |
| `Generate.razor` | Refactor to remove local state | Reduce boilerplate and state duplication |
| `Generate.razor` | Extract code to `Generate.razor.cs` | Improve readability of markup |

## Recommendations for Next Steps

1.  **Refactor Generate.razor State:** Prioritize removing the local variables in `Generate.razor` and binding directly to the `GenerationParameterService`.
2.  **Service Cleanup:** Delete the obsolete methods in `ImageService` and `WorkflowService` to finalize the migration.
3.  **Unified Rendering:** Explore a `FragmentRenderer` component that can handle both "Designed" (Core) and "Dynamic" (Optional) fragments uniformly.

