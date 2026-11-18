# Phase 1 - Resources In-Page Detail Navigation

## Status

**Phase:** 1  
**Build Status:** Passed via alternate Release output | **Tests:** Focused diagnostics clean; runtime QA pending

---

## Objective

Replace the Resource modal load/version flow with CivitAI-style in-page navigation and create the detail shell needed to remove the old Resource dialog call paths.

---

## Context

- Main plan: `Documentation/Plans/resources-page-navigation-and-lora-fixes/MAIN_PLAN.md`
- The Resources page currently opens `ResourceVersionsDialog` and `LoadResourceDialog` from card selection.
- The new detail view must preserve Resources filter/results state and return with a Back action.
- The implementation should not keep obsolete Resource load/version/image/info dialog call paths.
- Resource media is local and should project into `AssetViewer` so `AssetInfoPanel` can provide parameter send-to, media/source send-to, and image-to-prompt support.

---

## Execution Checklist

### Step 1: In-Page Selection State

**Complexity:** 3
**Status:** [x] Complete

#### Tasks

- [x] Add selected resource/file state to the Resources page.
- [x] Route card selection into the detail panel instead of opening dialogs.
- [x] Add a Back action that restores the filtered results view.

### Step 2: Detail Shell and File Selection

**Complexity:** 3
**Status:** [x] Complete

#### Tasks

- [x] Create Resource detail component using `TwoColumnLayout` slot rules.
- [x] Add compact multi-file selector inside the detail sidebar.
- [x] Handle missing files through the new in-page flow.

### Step 3: Resource Actions and Media Viewer

**Complexity:** 5
**Status:** [x] Complete

#### Tasks

- [x] Move toggle, send-to, prompt, LoRA, and trigger-word actions into the detail page.
- [x] Add local Resource image projection into `AssetViewer`.
- [x] Add inline metadata edit/delete actions.

### Step 4: Remove Obsolete Dialog Paths

**Complexity:** 3
**Status:** [x] Complete

#### Tasks

- [x] Remove `LoadResourceDialog` and `ResourceVersionsDialog` references from `Resources.razor`.
- [x] Remove replaced Resource image/info dialog references.
- [x] Delete obsolete Resource dialog components once no references remain.

---

## Issues & Resolutions

- `ResourceAuditPanel.razor` still referenced the deleted Resource info dialog path. It now creates the DB record inline, publishes `ResourcesChangedEventArgs`, and refreshes the audit list.
- Release build initially failed because `ResourceAuditPanel.razor` was missing the Events namespace import. Added `@using BlazorWebApp.Events`.
- The normal Release output was locked by a running .NET host/debug adapter. Validation was rerun with `BaseOutputPath=.\Temp\validation-build\` and passed.
- Resource detail initially opened only saved `ResourceImage` rows in `AssetViewer`. Cover previews are now projected into viewer assets too, with local web paths normalized for media send-to resolution.
- CivitAI automatic resource cover previews still used the PNG conversion path. The cover download now detects video DTOs, saves them with a video extension under `ResourcePreviewsPath`, and leaves image previews on the PNG conversion path.
- Resource cover previews duplicated the first saved media item in the detail media list. Cover previews are now fallback-only when no saved media rows exist, and Resource detail uses CivitAI-style thumbnail navigation below the two hero previews.
- MudBlazor dialogs/popovers opened from `AssetViewer` info-panel actions were rendered below the custom fullscreen overlay. AssetViewer-specific z-index overrides now raise dialogs, popovers, menus, and tooltips while the viewer is open.

## Implementation Notes

- Added `ResourceDetailPanel.razor` and `ResourceDetailPanel.razor.css` for the in-page two-column detail experience.
- Rewired `Resources.razor` to selected-resource state and removed obsolete Resource dialog navigation.
- Deleted the replaced Resource load/version/info/image dialog components and the old image-card dialog wrapper.
- Added compact two-hero media previews for Resources and CivitAI detail views.
- Preserved LoRA generation values while displaying Resource titles in the selector and applied-card surfaces.
- Resource LoRA send-to now resolves against the backend LoRA list before queueing, matching the loader panel's value source.
- CivitAI saved media now streams video files directly instead of forcing every asset through PNG conversion.
- CivitAI resource cover preview downloads now stream video previews as video files, and CivitAI hero/strip rendering uses DTO video metadata in addition to URL extensions.
- Resource preview lookup recognizes common image and video preview extensions.
- Resource and CivitAI hero/thumbnail media now use contain-fit rendering so images and videos are visible inside their containers without cropping.

## Validation

- Focused diagnostics returned no errors for the touched Resource, CivitAI media, LoRA display, cache, and service files.
- Focused Release build task compiled source but could not overwrite `bin\Release\net8.0\BlazorWebApp.dll` while the app/debugger process held the file lock.
- `dotnet build .\BlazorWebApp\BlazorWebApp.csproj --configuration Release /property:GenerateFullPaths=true /property:BaseOutputPath=.\Temp\validation-build\ /consoleloggerparameters:ErrorsOnly` passed with existing warning noise.
- After the media polish pass, focused diagnostics remained clean and a quiet Release build returned `BUILD_EXIT_0`.
