---
name: resource-library-integration
description: 'Implement resource-browser features for CivitAI, Danbooru, and local resources. Use when adding model discovery, download or import flows, resource cache or filter logic, AssetViewer integration, gallery-like browsing, or resource send-to actions.'
argument-hint: 'Resource surface or import flow to add or change'
---

# Resource Library Integration

Use this skill for asset-browser and import experiences.

## When To Use

- Work on CivitAI discovery or download flows
- Work on Danbooru browsing or library persistence
- Update resource cache or filter behavior
- Add local import or gallery-like media browsing
- Rework a media-viewing surface to use the shared modal

## Procedure

1. Decide whether the feature is:
   - remote metadata and download logic
   - local resource indexing and filtering
   - a hybrid browser that combines both
2. Reuse the relevant services instead of burying API calls in components:
   - `CivitaiService`
   - `ResourcesService`
   - `ResourceCacheService`
   - `ResourceFilterService`
   - `DanbooruService`
   - `DanbooruLibraryService`
3. Reuse `AssetViewer` for fullscreen image or video inspection instead of building a new viewer.
4. Use the documented layout patterns for resource-heavy pages: topbar or two-column, not ad hoc shells.
5. Use shared app-grid, token, and send-to patterns when adding cards and actions.
6. When filters affect other surfaces, route the state through the existing filter services and events.
7. Add or update narrow service tests when filter logic, cache logic, or import orchestration changes.

## Guardrails

- Do not instantiate parallel fullscreen viewers for the same media-browsing job.
- Do not duplicate filter state in components when a state service already owns it.
- Do not hardcode page-shell spacing or card widths when the tokenized patterns already exist.

## Key Anchors

- `../../../BlazorWebApp/Services/CivitaiService.cs`
- `../../../BlazorWebApp/Services/ResourcesService.cs`
- `../../../BlazorWebApp/Services/ResourceCacheService.cs`
- `../../../BlazorWebApp/Services/ResourceFilterService.cs`
- `../../../BlazorWebApp/Services/ResourceFilterStateService.cs`
- `../../../BlazorWebApp/Services/DanbooruService.cs`
- `../../../BlazorWebApp/Services/DanbooruLibraryService.cs`
- `../../../BlazorWebApp/Components/Shared/AssetViewer.razor`