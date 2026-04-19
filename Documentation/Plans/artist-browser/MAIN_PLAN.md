# Artist Browser - Implementation Plan

## Status
**Current Phase:** Execution (Phase 6 - User Tagging)

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow (per step)
1. **Initial Code Writing** -> 2. **Test and Debug Features** -> 3. **Discuss Improvements** -> 4. **Update Phase Document**
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
- **All events must use the pub/sub pattern** implemented by EventService.cs

### Documentation Requirements
- Create `PHASE_{#}.md` when entering a new phase
- Update phase document after each step completion
- Document all issues, blockers, and resolutions
- Track commit checkpoints throughout execution

---

## Problem Statement

The Anima base model is built on danbooru data, making artist tags highly effective for style guidance. Starting a prompt with an artist tag (e.g., `@dairi`) guides the output toward that artist's style. A dataset of 42,866 artist tags has been collected from the Anima Style Gallery API (`BlazorWebApp/Data/search.json`). Users need a visual browser within the Prompt Editor drawer to discover, search, sort, favorite, and insert artist tags into prompts.

---

## Proposed Solution

Add an **Artists** tab to the existing `TagDrawer` component (between "Tag Selector" and "LLM Enhancer"). This tab provides:
- A searchable, sortable, virtualized grid of artist cards
- Click-to-prepend artist tags to the positive prompt (with comma separator)
- Favorite toggle persisted via `AppStatePrompts`
- Placeholder-ready image cards (images stored in `wwwroot/images/artists/{shard}/{slug}.webp`)
- Sort by Post Count (default desc), Name, and Random

### Key Decisions

| Decision | Rationale |
|----------|-----------|
| Virtual scrolling (`MudVirtualize`) instead of pagination | Better fit for constrained drawer space; renders only visible DOM elements for 42K items |
| Singleton service for artist data | Load JSON once at startup, serve from memory |
| Favorites in `AppStatePrompts` as `List<string>` | Consistent with existing state persistence pattern |
| Images sharded by first letter (`{shard}/{slug}`) | Prevents file system slowdown with 42K potential files |
| Artist tag prepended (not appended) to prompt | Artist tags work best at the start of prompts for style guidance |
| Drawer stays open after tag insertion | Allow multiple artist selections without reopening |
| Tab placement between Tags and LLM Tools | User preference; logical grouping of prompt-building tools |

### Conventions
- Follow existing `ImageCard.razor` design language (simplified)
- Use `IEventService` pub/sub for cross-component communication
- Use existing `AppState` serialization pattern for favorites persistence
- Debounce search input (300ms) to avoid excessive re-renders
- Random sort uses a stable seed (same pattern as `AppStateGallery.RandomSeed`)

---

## Data Schema

Source: `BlazorWebApp/Data/search.json`

```json
{
  "slug": "dairi",          // Display name / description
  "tag": "@dairi",           // Tag to insert into prompt
  "imageId": "dairi-...",    // Unused for now
  "postCount": 18629,        // Sort metric / display
  "shard": "d",              // First letter for image sharding
  "hasImage": true           // Unused
}
```

Internal model keeps: `Tag`, `Slug`, `PostCount`, `Shard`

---

## Architecture Overview

```
BlazorWebApp/
  Data/
    search.json                          # Source data (42,866 entries)
  Models/
    ArtistTag.cs                         # Data model
    AppState.cs                          # + FavoriteArtists on AppStatePrompts
  Services/
    IArtistBrowserService.cs             # Interface
    ArtistBrowserService.cs              # Singleton: load, search, sort, favorites
  Components/
    Prompts/
      ArtistBrowserPanel.razor           # Main panel (search, sort, grid)
      ArtistCard.razor                   # Individual card component
    Shared/Generation/
      TagDrawer.razor                    # + New "Artists" tab
  wwwroot/
    images/artists/{shard}/{slug}.webp   # Future image storage
```

---

## Existing Code Context

### TagDrawer.razor (the drawer hosting tabs)
- Located at `Components/Shared/Generation/TagDrawer.razor`
- Uses `MudDrawer` with `MudTabs` containing "Tag Selector" and "LLM Enhancer"
- Width: `45vw`, Anchor: End, Temporary variant
- Communicates via `OnAppendTags` (`AppendedTags`) and `OnPromptReplaced` callbacks
- Instantiated in `PromptsForm.razor` (lines 116-117) - one for positive, one for negative prompt

### AppState Persistence
- `AppStatePrompts` at `Models/AppState.cs:283` - currently has `ActiveTabIndex` and `Wildcards`
- State persisted to DB via `DatabaseService`
- Gallery already uses `RandomSeed` pattern for stable random ordering

### Image Cards
- `ImageCard.razor` provides design reference: hover overlay, favorite heart toggle, info display
- Artist cards will be simplified: image, slug, post count, favorite toggle

---

## Implementation Phases

### Phase 1: Data Model & Service
**Objective:** Create the `ArtistTag` model and `ArtistBrowserService` that loads JSON data at startup and provides search/sort/filter capabilities.
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps
- [x] Step 1 - Create `ArtistTag` model class with `Tag`, `Slug`, `PostCount`, `Shard` properties
- [x] Step 2 - Create `IArtistBrowserService` interface with methods: `GetArtists()`, `SearchArtists()`, `GetSortedArtists()`, `ToggleFavorite()`, `IsFavorite()`, `GetFavorites()`
- [x] Step 3 - Implement `ArtistBrowserService` as singleton: deserialize `search.json` on first access, implement search (debounce-ready), sort (PostCount desc/asc, Name, Random with seed), favorites filter
- [x] Step 4 - Add `List<string> FavoriteArtists` property to `AppStatePrompts`
- [x] Step 5 - Register service in DI container

#### Success Criteria
- Service loads all 42,866 entries from JSON
- Search filters by slug/tag substring (case-insensitive)
- Sort works for all modes including stable random
- Favorites list persists through AppState

---

### Phase 2: Artist Card Component
**Objective:** Create the `ArtistCard.razor` component following existing design language.
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps
- [x] Step 1 - Create `ArtistCard.razor` with image placeholder, slug, post count, favorite toggle
- [x] Step 2 - Implement click handler that emits the artist tag for prompt prepending
- [x] Step 3 - Implement favorite toggle (heart icon matching `ImageCard.razor` style)
- [x] Step 4 - Add image support with fallback placeholder (`@onerror` handler)
- [x] Step 5 - Style the card (CSS) to match app design language, responsive sizing

#### Success Criteria
- Card displays artist info correctly
- Click fires tag selection event
- Favorite toggle works visually
- Placeholder shown when no image exists
- Card styling consistent with existing app

---

### Phase 3: Artist Browser Panel
**Objective:** Create the `ArtistBrowserPanel.razor` with search, sort controls, and virtualized card grid.
**Complexity:** 8 points
**Status:** [x] Complete

#### Steps
- [x] Step 1 - Create `ArtistBrowserPanel.razor` with search input (debounced), sort selector, favorites filter toggle
- [x] Step 2 - Implement `Virtualize` grid layout for artist cards
- [x] Step 3 - Wire up search/sort/filter to `IArtistBrowserService`
- [x] Step 4 - Handle artist tag selection: prepend `{tag}, ` to positive prompt via callback
- [x] Step 5 - Implement random sort with stable seed (refresh button to re-randomize)
- [x] Step 6 - Display total count and active filter indicators

#### Success Criteria
- Virtual scrolling renders smoothly with 42K items
- Search filters in real-time with debounce
- Sort modes all work correctly
- Tag insertion prepends to prompt without closing drawer
- Random sort is stable across scroll (seed-based)

---

### Phase 4: Drawer Integration
**Objective:** Add the Artists tab to `TagDrawer.razor` and wire everything together.
**Complexity:** 3 points
**Status:** [x] Complete

#### Steps
- [x] Step 1 - Add "Artists" `MudTabPanel` to `TagDrawer.razor` between Tag Selector and LLM Enhancer
- [x] Step 2 - Wire `ArtistBrowserPanel` tag selection to `OnAppendTags` callback (prefix mode)
- [x] Step 3 - Consider drawer width adjustment when Artists tab is active
- [x] Step 4 - Ensure state persistence works end-to-end (favorites survive app restart)

#### Success Criteria
- New tab appears in correct position
- Artist tag selection correctly prepends to prompt
- Drawer remains open after selection
- Favorites persist across sessions
- Drawer width accommodates the card grid

---

### Phase 5: Artist Preview Generator
**Objective:** Add a dedicated generation feature to batch-produce artist preview images using current txt2img settings.
**Complexity:** 8 points

#### Steps
- [x] Step 1 - Add `GeneratePreviewAsync` logic within `GenerateBatchPreviewsAsync`
- [x] Step 2 - Add `GenerateBatchPreviewsAsync` with CancellationToken, progress, batch size
- [x] Step 3 - Add expandable generator UI panel to `ArtistBrowserPanel.razor`
- [x] Step 4 - Wire up DI: `IRouterService`, `IWorkflowService`, `IWebHostEnvironment`
- [x] Step 5 - Add `ArtistPreviewProgress`, `GetPreviewImagePath()`, `HasPreviewImage()` to interface

#### Design Details
- **Prompt composition**: `"{artistTag}, {currentPositivePrompt}"` - artist tag prepended to whatever the user has in the prompt field
- **Workflow resolution**: uses `State.State.Generation.CurrentWorkflowId` + `IWorkflowService.GetWorkflowById()`
- **Image save**: raw base64 -> ImageMagick -> webp file at `wwwroot/images/artists/{shard}/{slug}.webp`
- **Skip logic**: `File.Exists()` check before generation, no state tracking needed
- **No DB interaction**: preview images are static assets, not gallery entries
- **Progress UI**: minimal - a linear progress bar + "{current}/{total} - {slug}" text inside the expandable panel

#### Success Criteria
- Generates preview images using current txt2img settings
- Saves as webp with correct shard/slug filename
- Skips existing images
- Can be started/stopped mid-batch
- Progress visible in UI
- Does not interfere with normal generation flow
- Artist cards update to show generated images

---

### Phase 6: User Tagging System
**Objective:** Allow users to catalog artists with custom tags (e.g., "landscape", "anime", "detailed") persisted in a sidecar JSON file, with filtering support.
**Complexity:** 8 points

#### Architecture
- **Storage**: `Data/artist-user-tags.json` sidecar file — `Dictionary<string, List<string>>` (slug -> tags)
- **Separate from favorites**: Favorites remain in `AppStatePrompts.FavoriteArtists` as-is
- **Only tagged artists stored**: File stays small (most artists won't be tagged)

#### Steps
- [ ] Step 1 - Create sidecar file service: `LoadUserTags()`, `SaveUserTags()`, `GetTagsForArtist(slug)`, `SetTagsForArtist(slug, tags)`, `RemoveTag(slug, tag)`, `GetAllUniqueTags()`
- [ ] Step 2 - Add methods to `IArtistBrowserService`: `GetUserTags(slug)`, `SetUserTags(slug, tags)`, `RemoveUserTag(slug, tag)`, `GetAllUserTags()`, filter support in `GetArtists()`
- [ ] Step 3 - Update `ArtistCard.razor`:
  - Tag icon button on hover (bottom-left corner)
  - Click opens `MudPopover` anchored to card
  - Popover shows current tags as closable `MudChip`s
  - `MudAutocomplete` suggests existing tags from global vocabulary + allows free-text for new tags
- [ ] Step 4 - Update `ArtistBrowserPanel.razor` toolbar:
  - `MudSelect` with multi-select for tag filtering
  - Options populated from `GetAllUserTags()`
  - AND logic: artist must have ALL selected tags
  - Empty selection = no filter
- [ ] Step 5 - Wire `GetArtists()` to accept optional `userTags` filter parameter

#### UI Details
- **Card tagging popover**: Compact, appears on tag icon click, dismisses on click-outside
- **Tag chips**: Small, colored, closable (X to remove tag)
- **Autocomplete**: Free-text input with dropdown suggestions from existing tags across all artists
- **Toolbar filter**: Multi-select dropdown showing all unique tags, sits alongside favorites toggle and sort controls

#### Success Criteria
- Users can add/remove custom tags per artist
- Tags persist across sessions via sidecar JSON
- Grid filterable by user tags
- Tag vocabulary auto-populated from existing tags
- Favorites system unchanged

---

### Phase 7: Polish & Testing
**Objective:** Final refinements, edge cases, and user experience improvements.
**Complexity:** 3 points

#### Steps
- [ ] Step 1 - Test with full 42K dataset: scroll performance, search responsiveness
- [ ] Step 2 - Handle edge cases: empty search results, special characters in tags
- [ ] Step 3 - Verify favorites count display and filter behavior
- [ ] Step 4 - Review and clean up CSS/styling for consistency
- [ ] Step 5 - Ensure no regressions in existing Tag Selector and LLM Enhancer tabs

#### Success Criteria
- Smooth performance with full dataset
- No visual or functional regressions
- Clean, consistent UI

---

## Stress Points & Risks

| Risk | Mitigation | Complexity |
|------|------------|------------|
| 42K items may cause memory pressure | Lightweight model (~4 fields per entry), singleton load | 2 |
| `MudVirtualize` with grid layout may need custom item sizing | Test early, fall back to fixed-height rows if needed | 5 |
| Drawer width too narrow for card grid | Dynamically adjust width when Artists tab is active | 2 |
| Random sort instability during virtual scroll | Use seeded `Random` consistent with gallery pattern | 2 |
| Large JSON file in wwwroot increases deploy size | ~3-4MB uncompressed, acceptable; could gzip if needed | 1 |
| 42K images in future could be large | Sharded folders + lazy loading + webp format | 3 |
| Preview generation blocks normal gen | Sequential - user must stop preview gen to use normal gen | 2 |
| ComfyUI connection errors mid-batch | Catch per-artist, log error, skip to next | 2 |
| Large webp files from high-res settings | User controls dimensions via normal settings; previews are thumbnails anyway | 1 |
| Sidecar JSON write contention | Single writer (singleton service), async file writes | 1 |
| Tag popover positioning in grid | MudPopover with anchor, may need flip behavior | 2 |

---

## Virtual Scrolling Notes

`MudVirtualize` in MudBlazor:
- Renders only visible items in the viewport (typically 20-40 DOM elements)
- Full scrollbar reflects total list height via calculated spacers
- Requires items to have a predictable height (fixed or estimated)
- For a grid layout: wrap virtualized items in a CSS grid where each "row" is a virtualized unit, OR use `MudVirtualize` with `OverscanCount` to render a few extra items above/below viewport
- Key consideration: `MudVirtualize` works with a flat list, so the grid layout is achieved via CSS (`display: grid` or `flex-wrap`) on the container, with the virtualizer managing which items exist in the DOM

This is effectively "infinite scroll" from the user's perspective -- they scroll down and content appears seamlessly -- but technically superior because items scrolled past are removed from the DOM, keeping memory usage constant regardless of list size.

---

## Changelog

| Phase | Changes |
|-------|---------|
| Planning | Initial plan created |
| Phase 1 | Data model, service, DI registration - Complete |
| Phase 2 | Artist card component with CSS - Complete |
| Phase 3 | Browser panel with Virtualize + search/sort/filter - Complete |
| Phase 4 | TagDrawer integration - Complete |
| Phase 5 | Artist Preview Generator - Complete |
| Phase 6 | User Tagging System added to plan |
