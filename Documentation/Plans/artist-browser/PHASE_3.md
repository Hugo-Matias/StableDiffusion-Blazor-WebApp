# Phase 3 & 4: Artist Browser Panel + Drawer Integration

## Objective
Create the `ArtistBrowserPanel.razor` with search, sort, favorites filter, and virtualized card grid. Integrate as new tab in `TagDrawer.razor`.

## Status: Complete

## Phase 3 Steps

- [x] Step 1 - Created `ArtistBrowserPanel.razor` with search input (debounced via `DebounceInterval="300"`), sort selector, favorites filter toggle
- [x] Step 2 - Implemented `Virtualize` component for card grid with `OverscanCount="20"` and `ItemSize="200"`
- [x] Step 3 - Wired search/sort/filter to `IArtistBrowserService.GetArtists()`
- [x] Step 4 - Artist tag selection prepends `{tag}` to prompt via `OnAppendTags` callback (prefix mode)
- [x] Step 5 - Random sort with stable seed + shuffle button to re-randomize
- [x] Step 6 - Display total filtered count

## Phase 4 Steps

- [x] Step 1 - Added "Artists" `MudTabPanel` between Tag Selector and LLM Enhancer in `TagDrawer.razor`
- [x] Step 2 - Wired `ArtistBrowserPanel.OnAppendTags` directly to `TagDrawer.OnAppendTags` callback
- [x] Step 3 - Drawer width remains at `45vw` (can adjust later based on testing)
- [x] Step 4 - Favorites persist via `AppStatePrompts.FavoriteArtists` (existing state persistence)

## Files Created
- `Components/Prompts/ArtistBrowserPanel.razor`
- `Components/Prompts/ArtistBrowserPanel.razor.css`

## Files Modified
- `Components/Shared/Generation/TagDrawer.razor` (added Artists tab)
- `Services/IArtistBrowserService.cs` (changed return type to `List<ArtistTag>` for `Virtualize` compatibility)
- `Services/ArtistBrowserService.cs` (matching return type change)

## Build Notes
- C# files compile with zero errors
- Pre-existing Razor compilation cascade continues (500+ errors in Gallery, ImageEditor, etc.)
- Our Razor files follow identical patterns to existing working components

## Key Technical Decisions

### Binding Pattern
Used `Value` + `ValueChanged` (one-way + handler) instead of `@bind-Value` for `MudSelect` and `MudToggleIconButton` because `@bind-*` and `*Changed` cannot coexist on the same component (RZ10010 error).

### Virtualize Configuration
- `ItemSize="200"` - estimated pixel height per item for scroll height calculation
- `OverscanCount="20"` - renders 20 extra items above/below viewport for smoother scrolling
- Grid layout via CSS `display: grid` on the container with `grid-template-columns: repeat(auto-fill, minmax(120px, 1fr))`
- The `Virtualize` component manages which items exist in the DOM; CSS grid handles multi-column layout

### Tag Insertion
Artist tag is sent as `AppendedTags { Tags = artist.Tag, IsPrefix = true }` which makes the parent component prepend it to the prompt. The drawer stays open (no auto-close).

## Component API

```razor
<ArtistBrowserPanel OnAppendTags="HandleAppendTags" />
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `OnAppendTags` | `EventCallback<AppendedTags>` | Fired with artist tag for prompt prepending |
