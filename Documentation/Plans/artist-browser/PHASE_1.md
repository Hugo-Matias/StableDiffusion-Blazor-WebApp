# Phase 1: Data Model & Service

## Objective
Create the `ArtistTag` model and `ArtistBrowserService` that loads JSON data at startup and provides search/sort/filter capabilities.

## Status: Complete

## Steps

- [x] Step 1 - Created `ArtistTag` model at `Models/ArtistTag.cs`
  - Properties: `Tag`, `Slug`, `PostCount`, `Shard` with JSON serialization attributes
- [x] Step 2 - Created `IArtistBrowserService` interface at `Services/IArtistBrowserService.cs`
  - `ArtistSortMode` enum: PostCount, Name, Random
  - Methods: `GetArtists()`, `IsFavorite()`, `ToggleFavorite()`, `GetFavorites()`
  - `TotalCount` property
- [x] Step 3 - Implemented `ArtistBrowserService` at `Services/ArtistBrowserService.cs`
  - Lazy singleton load from `Data/search.json` with thread-safe lock
  - Search: case-insensitive contains on both `Slug` and `Tag`
  - Sort: PostCount (asc/desc), Name (asc/desc), Random (seeded shuffle)
  - Favorites: backed by `AppStatePrompts.FavoriteArtists` via `IStateService`
- [x] Step 4 - Added `List<string> FavoriteArtists` to `AppStatePrompts` in `Models/AppState.cs`
- [x] Step 5 - Registered `IArtistBrowserService` as singleton in `Program.cs`

## Files Modified
- `Models/ArtistTag.cs` (new)
- `Services/IArtistBrowserService.cs` (new)
- `Services/ArtistBrowserService.cs` (new)
- `Models/AppState.cs` (added `FavoriteArtists` property)
- `Program.cs` (DI registration)

## Build Status
Build successful.

## Notes
- Service loads lazily on first access rather than at startup to avoid blocking app initialization
- `GetArtists()` returns `IReadOnlyList<ArtistTag>` - the full filtered/sorted list is materialized for `MudVirtualize` consumption in Phase 3
- Favorites are stored as `List<string>` of tag values, persisted via existing AppState DB mechanism
- Random sort uses `OrderBy(_ => rng.Next())` with seeded `Random` - same conceptual pattern as `AppStateGallery.RandomSeed`
