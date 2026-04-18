# Phase 5: Artist Preview Generator

## Objective
Add a dedicated generation feature to batch-produce artist preview images using current txt2img settings.

## Status: Complete

## Steps

- [x] Step 1 - Added `GeneratePreviewAsync` logic within `GenerateBatchPreviewsAsync` in `ArtistBrowserService`:
  - Clones current `GenerationParameters` from `IStateService`
  - Prepends artist tag to existing positive prompt
  - Randomizes seed per generation
  - Calls `IRouterService.PostGenerationAsync()` directly (bypasses ImageService/DB)
  - Converts result to webp via ImageMagick and saves to `wwwroot/images/artists/{shard}/{slug}.webp`
  - Skips artists with existing preview files
- [x] Step 2 - `GenerateBatchPreviewsAsync` iterates filtered artist list with:
  - `CancellationToken` for stop/cancel
  - `ArtistPreviewProgress` object for UI binding (current/total/slug/skipped/failed)
  - Configurable batch size limit
- [x] Step 3 - Added UI to `ArtistBrowserPanel.razor`:
  - Camera icon button in toolbar that toggles expandable `MudPaper` panel
  - Panel contains: batch size numeric field, Start/Stop button, progress bar with artist name
  - Generates from current filtered list (respects search/sort/favorites)
- [x] Step 4 - DI wiring: `ArtistBrowserService` now injects `IRouterService`, `IWorkflowService`, `IWebHostEnvironment`
- [x] Step 5 - Added `ArtistPreviewProgress` class, `GetPreviewImagePath()`, `HasPreviewImage()` to interface

## Files Modified
- `Services/IArtistBrowserService.cs` - Added `ArtistPreviewProgress`, generation methods, preview path helpers
- `Services/ArtistBrowserService.cs` - Full implementation with DI for router, workflow, environment
- `Components/Prompts/ArtistBrowserPanel.razor` - Added generator UI panel with start/stop/progress

## Build Status
Build successful.

## Key Technical Decisions

### Standalone Generation Path
- Does NOT use `ImageService.GenerateImagesAsync()` - avoids DB entries, gallery pollution, progress tracking conflicts
- Calls `IRouterService.PostGenerationAsync()` directly with cloned+modified params
- Each generation gets a random seed to ensure variety

### Prompt Composition
- Format: `"{artistTag}, {currentPositivePrompt}"`
- Reads current prompt from `IStateService.GenerationParameters` at generation time
- Empty prompt just uses the artist tag alone

### Progress Model
- `ArtistPreviewProgress` is a simple mutable object on the service singleton
- UI reads it during render; no events needed since the panel re-renders on StateHasChanged after completion
- Shows: current/total count, current artist slug, skipped count

### File Management
- Output: `{wwwroot}/images/artists/{shard}/{slug}.webp`
- Directories created on demand
- Skip via `File.Exists()` - no state tracking
- WebP conversion via ImageMagick (`MagickImage.WriteAsync` with `MagickFormat.WebP`)
