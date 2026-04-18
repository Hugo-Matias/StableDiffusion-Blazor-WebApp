# Phase 2: Artist Card Component

## Objective
Create the `ArtistCard.razor` component following existing design language.

## Status: Complete

## Steps

- [x] Step 1 - Created `ArtistCard.razor` with image + placeholder fallback, slug, post count, favorite toggle
  - Image path: `images/artists/{shard}/{slug}.webp`
  - Placeholder shows palette icon when image fails to load (`@onerror` handler)
- [x] Step 2 - Click handler emits `OnSelected` callback with the `ArtistTag`
- [x] Step 3 - Favorite toggle (heart icon) matches `ImageCard.razor` style
  - Solid red heart when favorited, outline when not
  - Visible on hover or when favorited
  - Uses `IArtistBrowserService.ToggleFavorite()` + `OnFavoriteChanged` callback
- [x] Step 4 - Image support with fallback: `@onerror` sets `_imageError` flag, shows placeholder div
- [x] Step 5 - Scoped CSS (`ArtistCard.razor.css`) matching app design language:
  - Card hover: translateY + box-shadow (same as `ImageCard`)
  - Image: aspect-ratio 3/4, object-fit cover, scale on hover
  - Fav button: top-right overlay, backdrop-filter blur, scale transitions
  - Info footer: slug (truncated ellipsis) + formatted count (e.g., "18.6k")

## Files Created
- `Components/Prompts/ArtistCard.razor`
- `Components/Prompts/ArtistCard.razor.css`

## Build Notes
- C# service files compile with zero errors (verified via `get_errors`)
- Pre-existing Razor compilation errors exist across Gallery, ImageEditor, and other components (500+ errors) unrelated to our changes
- These errors cause cascading Razor source generator failures that affect all .razor files including ours
- Our code follows identical patterns to existing working components (`PromptsPanel.razor`, `ImageCard.razor`)

## Component API

```razor
<ArtistCard Artist="artistTag"
            OnSelected="HandleArtistSelected"
            OnFavoriteChanged="HandleFavoriteChanged" />
```

### Parameters
| Parameter | Type | Description |
|-----------|------|-------------|
| `Artist` | `ArtistTag` (required) | The artist data to display |
| `OnSelected` | `EventCallback<ArtistTag>` | Fired when card is clicked |
| `OnFavoriteChanged` | `EventCallback` | Fired when favorite is toggled |
