# Resource Import - Implementation Plan

## Status

**Current Phase:** Phase 2 - Import Tab UI

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow (per step)

1. **Initial Code Writing** - 2. **Test and Debug Features** - 3. **Discuss Improvements** - 4. **Update Phase Document**
   - Do NOT proceed to next step until testing is complete
   - User must explicitly approve before updating phase document
   - Build runs only after user requests or after completing all file edits

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

- **Each step = commitable checkpoint** for safe implementation
- **No time/date references** - use complexity points only
- **Detours are acceptable** after discussion - append to main plan
- **Phase documents must contain enough context** to resume in new sessions
- **Minimal, focused changes** - avoid over-engineering
- **User permission required** before moving to next phase
- **All events must use pub/sub pattern via EventService.cs**

---

## Problem Statement

Resources downloaded externally (not through CivitAI integration) are not tracked by the app. Users need a way to import these "untracked" resource files into the database so they can be managed, searched, and loaded like CivitAI-sourced resources.

Additionally, CivitAI downloads currently default to disabled (`_storage`), but should default to active (enabled) placement.

---

## Proposed Solution

Add an "Import" tab to the Resources page that:

1. Scans known resource directories (`{ResourcesPath}/{Type}` and `{ResourcesPath}/_storage/{Type}`) for `.safetensors`, `.ckpt`, `.pt` files not tracked in the database
2. Allows single or batch import with a form for shared default metadata
3. Preserves file location as-is: files in main type path are Active, files in `_storage` are Inactive
4. Supports setting a cover image via a dedicated image picker dialog (single-select from app images)
5. Defers optional fields (tags, description, author, CivitAI IDs) to the existing `ResourceInfoDialog`

### Key Decisions

| Decision                                                    | Rationale                                                                                                                                                       |
| ----------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Scan only known dirs (enabled + `_storage`)                 | Keeps scope focused; drag-and-drop from arbitrary paths deferred as nice-to-have                                                                                |
| No file relocation on import                                | Files stay where they are; Active/Inactive state derived from current path                                                                                      |
| Batch import with shared defaults                           | Simpler UX; per-file overrides handled via `ResourceInfoDialog` after import                                                                                    |
| Reuse `ResourceInfoDialog` for optional fields              | Avoids duplicating UI; the dialog already supports all fields                                                                                                   |
| Use CivitAI `BaseModelsData.Models` for base model dropdown | Already loaded by `CivitaiService`; provides comprehensive list                                                                                                 |
| Dedicated image picker dialog (single-select)               | Avoids modifying the main gallery browser; simple modal with image cards, border highlight on selected, Ok/Cancel actions; subsequent clicks override selection |
| Copy cover image to `ResourcePreviewsPath` always           | Even if the source is an app-managed image, the preview folder is the canonical location                                                                        |

### Conventions

- All events use pub/sub via `EventService.cs`
- Resource type directories follow existing `_resourceTypeDirectories` pattern
- File extensions for resources: `.safetensors`, `.ckpt`, `.pt`
- Preview images stored as `{ResourcePreviewsPath}/{TypeName}/{FilenameWithoutExt}.png`

### Nice-to-Have (Deferred)

- Drag-and-drop import from arbitrary external paths (would require file relocation logic)

---

## Implementation Phases

### Phase 1: Import Service & Untracked File Detection

**Objective:** Create the service layer that scans known resource directories, identifies untracked files, and provides import functionality.
**Complexity:** 8 points
**Status:** [x] Complete

#### Steps

- [x] Step 1 - Create `ImportResourceModel` and detection logic in `ResourcesService` [3 pts]
  - Add model class with: `FileInfo`, detected `ResourceType`, detected `IsEnabled` (based on path), `SizeKb`
  - Add method to scan enabled + `_storage` directories for resource files not in the database (match by filename via `CheckResourceExistsByFilename`)
  - Return list of untracked files with detected type, path, size, and active/inactive state
- [x] Step 2 - Add import method to `ResourcesService` (single + batch) [5 pts]
  - Accept shared defaults: Title, Type, SubType, BaseModel, TriggerWords, cover image path
  - Create `Resource` entity per file; `IsEnabled` derived from file's current path
  - Title defaults to filename without extension if not provided
  - Copy cover image to `ResourcePreviewsPath/{TypeName}/{FilenameWithoutExt}.png`
  - Call `DatabaseService.CreateResource` for each file
  - Publish `ResourcesChangedEventArgs` after import completes

#### Success Criteria

- ✓ Untracked files are correctly identified across enabled + `_storage` directories for all resource types
- ✓ Import creates valid `Resource` entities in the database
- ✓ `IsEnabled` flag matches the file's actual location (main path = true, `_storage` = false)
- ✓ Cover images are copied to the preview folder

---

### Phase 2: Import Tab UI

**Objective:** Build the Blazor "Import" tab component on the Resources page with file selection, metadata form, cover image picker dialog, and batch import support.
**Complexity:** 13 points
**Status:** [ ] Not Started

#### Steps

- [ ] Step 1 - Create `ImportResourcePanel.razor` with untracked file list [5 pts]
  - Display untracked files grouped by detected type
  - Show Active/Inactive badge based on detected path
  - Support selecting single or multiple files for import (checkboxes)
  - Shared metadata form: Title (default from filename), Type (dropdown from `ResourceTypes`), SubType (autocomplete), Base Model (select from `CivitaiService.BaseModelsData.Models`), Trigger Words (chip input)
  - "Import" button processes selected files with shared defaults
- [ ] Step 2 - Create `ImagePickerDialog.razor` for cover image selection [5 pts]
  - Modal dialog with simple image cards (thumbnail grid)
  - Loads images from a source (app-managed images, e.g., project/folder images)
  - Single-select behavior: clicking an image selects it (highlighted border); clicking another overrides the selection
  - Ok button returns selected image ID; Cancel discards
  - Integrate into `ImportResourcePanel` as "Set Cover" button that opens the dialog
- [ ] Step 3 - Add the Import tab to `Resources.razor` [3 pts]
  - Add new `MudTabPanel` labeled "Import" to the existing tab structure
  - Wire up `ImportResourcePanel` component
  - Trigger scan on tab activation
  - Refresh resource tabs after successful import

#### Success Criteria

- Import tab appears on the Resources page
- Untracked files are listed and selectable
- Metadata form captures required fields with shared defaults
- Cover image can be set via the image picker dialog
- Batch import processes multiple files and refreshes the resource list

---

### Phase 3: CivitAI Download Default Behavior Change

**Objective:** Change CivitAI downloads to place files in the active (enabled) directory by default instead of `_storage`.
**Complexity:** 3 points
**Status:** [ ] Not Started

#### Steps

- [ ] Step 1 - Update `CivitaiService.DownloadResource` to use enabled path [2 pts]
  - Change download path from `_storage/{type}` to `{type}` (enabled path)
  - Set `IsEnabled = true` on the created `Resource` entity
  - Ensure trigger words `.txt` file follows the same path
- [ ] Step 2 - Verify preview image path is unaffected [1 pt]
  - Preview images already download to `ResourcePreviewsPath` (not `_storage`), confirm no changes needed

#### Success Criteria

- Newly downloaded CivitAI resources land in the enabled directory
- Resource entity is created with `IsEnabled = true`
- Preview image download path remains unchanged
- Existing resources are unaffected

---

## Stress Points & Risks

| Risk                                                     | Mitigation                                              | Complexity |
| -------------------------------------------------------- | ------------------------------------------------------- | ---------- |
| Large number of untracked files causing slow scan        | Async scanning with progress indicator                  | 2          |
| Duplicate filenames across subtypes                      | Use full path comparison, not just filename             | 1          |
| CivitAI download path change breaking existing workflows | Only affects new downloads; no migration needed         | 1          |
| Image picker dialog needs a clear image source           | Start with images from output folders; can expand later | 2          |

---

## Changelog

| Phase    | Changes                                                          |
| -------- | ---------------------------------------------------------------- |
| Planning | Initial plan created                                             |
| Phase 1  | Service layer complete - scanning and import methods implemented |

---

## References

- [Resources.razor](BlazorWebApp/Pages/Resources.razor) - Main resources page with tab structure
- [ResourcesService.cs](BlazorWebApp/Services/ResourcesService.cs) - Resource file operations
- [ResourceInfoDialog.razor](BlazorWebApp/Components/Resources/ResourceInfoDialog.razor) - Existing edit dialog (reusable for deferred fields)
- [CivitaiService.cs](BlazorWebApp/Services/CivitaiService.cs) - Download logic and base model data
- [DatabaseService.cs](BlazorWebApp/Services/DatabaseService.cs) - Resource CRUD operations
- [IOService.cs](BlazorWebApp/Services/IOService.cs) - File operations and preview path resolution
