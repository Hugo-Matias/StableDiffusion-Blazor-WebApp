# Phase 1: Styles Tab Redesign - Implementation Document

## Phase Info
**Status:** [ ] In Progress  
**Complexity:** 19 points  
**Started:** Current Session  
**Related Plan:** [MAIN_PLAN.md](MAIN_PLAN.md)

---

## Objective
Replace ResourceCard UI with information-dense table/list optimized for text management, add categorization, favorites, and semantic search capabilities.

---

## Implementation Steps

### Step 1: Database Schema Extension [ ]
**Complexity:** 3 points  
**Description:** Extend Prompt entity with new properties for organization and tracking

**Tasks:**
- [ ] Add new properties to Prompt entity (Category, Tags, IsPinned, IsFavorite, SortOrder, LastUsedAt, UsageCount)
- [ ] Create database migration
- [ ] Test migration on development database
- [ ] Verify all existing prompts still load correctly

**Files to Modify:**
- `BlazorWebApp/Data/Entities/Prompt.cs`
- `BlazorWebApp/Data/Migrations/` (new migration file)

**Schema Changes:**
```csharp
// NEW properties to add:
public string? Category { get; set; }
public List<string>? Tags { get; set; }
public bool IsPinned { get; set; }
public bool IsFavorite { get; set; }
public int SortOrder { get; set; }
public DateTime? LastUsedAt { get; set; }
public int UsageCount { get; set; }
```

**Success Criteria:**
- [ ] Migration runs without errors
- [ ] Existing prompts load with null/default values for new fields
- [ ] Can save prompts with new properties
- [ ] No data loss

---

### Step 2: Database Service Extensions [ ]
**Complexity:** 3 points  
**Description:** Add database methods for new prompt operations

**Tasks:**
- [ ] Add category filtering methods
- [ ] Add tag searching methods
- [ ] Add favorites/pinned filtering
- [ ] Add sorting methods (by category, usage count, last used)
- [ ] Add usage tracking update methods
- [ ] Test all new methods

**Files to Modify:**
- `BlazorWebApp/Services/DatabaseService.cs`
- `BlazorWebApp/Services/IDatabaseService.cs`

**New Methods to Add:**
```csharp
Task<List<Prompt>> GetPromptsByCategory(string category);
Task<List<Prompt>> GetPromptsByTags(List<string> tags);
Task<List<Prompt>> GetPinnedPrompts();
Task<List<Prompt>> GetFavoritePrompts();
Task<List<string>> GetAllCategories();
Task<List<string>> GetAllTags();
Task UpdatePromptUsage(int promptId);
Task<List<Prompt>> SearchPromptsWithKeywords(string query);
```

**Success Criteria:**
- [ ] All methods return expected results
- [ ] Filtering works correctly
- [ ] Performance acceptable with 100+ prompts
- [ ] No breaking changes to existing methods

---

### Step 3: PromptStyleCard Component [ ]
**Complexity:** 2 points  
**Description:** Create compact card component for displaying individual prompt styles

**Tasks:**
- [ ] Create new component with MudBlazor card/paper
- [ ] Add compact layout with title, category badge, tags chips
- [ ] Add preview text (truncated positive/negative)
- [ ] Add action buttons (edit, favorite, pin, delete)
- [ ] Add hover effects and tooltips
- [ ] Add click handlers for actions

**Files to Create:**
- `BlazorWebApp/Components/Prompts/Styles/PromptStyleCard.razor`
- `BlazorWebApp/Components/Prompts/Styles/PromptStyleCard.razor.css`

**Component Structure:**
```razor
<MudCard Class="prompt-style-card">
    <MudCardHeader>
        <MudStack Row Justify="SpaceBetween">
            <MudText Typo="Typo.h6">@Prompt.Title</MudText>
            <MudChip Size="Size.Small">@Prompt.Category</MudChip>
        </MudStack>
    </MudCardHeader>
    <MudCardContent>
        <!-- Preview text -->
        <!-- Tags -->
    </MudCardContent>
    <MudCardActions>
        <!-- Action buttons -->
    </MudCardActions>
</MudCard>
```

**Success Criteria:**
- [ ] Card displays all prompt information
- [ ] Actions work correctly
- [ ] Responsive layout
- [ ] Visual feedback on hover/click

---

### Step 4: CategoryBrowser Component [ ]
**Complexity:** 2 points  
**Description:** Create sidebar component for category navigation

**Tasks:**
- [ ] Create tree/list view component
- [ ] Load categories from database
- [ ] Add "All" option
- [ ] Add category selection handling
- [ ] Add "Add Category" button
- [ ] Style as sidebar panel

**Files to Create:**
- `BlazorWebApp/Components/Prompts/Styles/CategoryBrowser.razor`

**Component Structure:**
```razor
<MudPaper Class="category-browser pa-4">
    <MudText Typo="Typo.h6">Categories</MudText>
    <MudList Clickable>
        <MudListItem OnClick="() => OnCategorySelected.InvokeAsync(null)">
            All (@TotalCount)
        </MudListItem>
        @foreach (var category in Categories)
        {
            <MudListItem OnClick="() => OnCategorySelected.InvokeAsync(category)">
                @category.Name (@category.Count)
            </MudListItem>
        }
    </MudList>
</MudPaper>
```

**Success Criteria:**
- [ ] Categories load and display
- [ ] Selection works
- [ ] Count badges show correctly
- [ ] Responsive layout

---

### Step 5: PromptStyleTable Component [ ]
**Complexity:** 3 points  
**Description:** Create main table/list view for displaying all styles

**Tasks:**
- [ ] Create component with MudTable or custom list
- [ ] Implement sorting (title, category, usage, date)
- [ ] Implement search bar
- [ ] Add bulk selection checkboxes
- [ ] Add pagination or virtual scrolling
- [ ] Integrate with CategoryBrowser

**Files to Create:**
- `BlazorWebApp/Components/Prompts/Styles/PromptStyleTable.razor`

**Success Criteria:**
- [ ] Displays 15+ items without scrolling
- [ ] Sorting works for all columns
- [ ] Search filters results
- [ ] Selection works
- [ ] Performance good with 100+ items

---

### Step 6: Favorites & Pinning System [ ]
**Complexity:** 2 points  
**Description:** Implement favorites and pinning functionality

**Tasks:**
- [ ] Add quick access bar at top of table
- [ ] Show pinned items in quick access
- [ ] Add star icon for favorites
- [ ] Add pin icon for pinning
- [ ] Implement drag-to-reorder for pinned items
- [ ] Save pin order to database

**Files to Modify:**
- `BlazorWebApp/Components/Prompts/Styles/PromptStyleTable.razor`
- `BlazorWebApp/Components/Prompts/Styles/PromptStyleCard.razor`

**Success Criteria:**
- [ ] Can pin/unpin prompts
- [ ] Can favorite prompts
- [ ] Pinned items show in quick access
- [ ] Order persists

---

### Step 7: Enhanced PromptDialog [ ]
**Complexity:** 2 points  
**Description:** Update dialog to support new properties

**Tasks:**
- [ ] Add category dropdown/input
- [ ] Add tags input (chips)
- [ ] Add LLM suggestion button for tags
- [ ] Keep existing functionality intact
- [ ] Test save/update operations

**Files to Modify:**
- `BlazorWebApp/Components/Prompts/PromptDialog.razor`

**Success Criteria:**
- [ ] Can set category
- [ ] Can add/remove tags
- [ ] LLM suggestions work (if Phase 5 API ready)
- [ ] No breaking changes

---

### Step 8: Semantic Search Implementation [ ]
**Complexity:** 2 points  
**Description:** Add keyword expansion search

**Tasks:**
- [ ] Create search helper method
- [ ] Implement keyword extraction (split, synonyms)
- [ ] Add to DatabaseService search methods
- [ ] Test search accuracy
- [ ] Add search in UI

**Files to Modify:**
- `BlazorWebApp/Services/DatabaseService.cs`
- `BlazorWebApp/Components/Prompts/Styles/PromptStyleTable.razor`

**Implementation:**
```csharp
public async Task<List<Prompt>> SemanticSearch(string query)
{
    // Expand query to keywords
    var keywords = ExpandQueryKeywords(query);
    
    // Search across all text fields
    var results = await _context.Prompts
        .Where(p => keywords.Any(k => 
            p.Title.Contains(k) || 
            p.Positive.Contains(k) || 
            p.Negative.Contains(k) ||
            p.Tags.Any(t => t.Contains(k))))
        .ToListAsync();
    
    return results;
}
```

**Success Criteria:**
- [ ] Search finds relevant prompts
- [ ] Fast enough for real-time search
- [ ] Better than basic text search

---

### Step 9: Keyboard Shortcuts [ ]
**Complexity:** 1 point  
**Description:** Add keyboard support for pinned items

**Tasks:**
- [ ] Implement Ctrl+1-9 handlers
- [ ] Apply pinned prompt on shortcut
- [ ] Add visual indicator of shortcuts
- [ ] Test keyboard navigation

**Files to Modify:**
- `BlazorWebApp/Components/Prompts/PromptsPanel.razor`

**Success Criteria:**
- [ ] Shortcuts work
- [ ] Visual feedback
- [ ] No conflicts with browser shortcuts

---

### Step 10: Integration & Refactoring [ ]
**Complexity:** 3 points  
**Description:** Integrate all components and refactor PromptsPanel

**Tasks:**
- [ ] Replace ResourceCard with new components
- [ ] Wire up all event handlers
- [ ] Ensure backward compatibility
- [ ] Test complete workflow
- [ ] Update styles/CSS

**Files to Modify:**
- `BlazorWebApp/Components/Prompts/PromptsPanel.razor`

**Success Criteria:**
- [ ] All features work together
- [ ] No regressions in existing functionality
- [ ] UI is responsive
- [ ] Performance acceptable

---

## Testing Checklist

### Functionality Tests
- [ ] Can create new prompt with category and tags
- [ ] Can edit existing prompt
- [ ] Can delete prompt
- [ ] Can pin/unpin prompts
- [ ] Can favorite prompts
- [ ] Can search by text
- [ ] Can filter by category
- [ ] Can filter by tags
- [ ] Can sort by different columns
- [ ] Keyboard shortcuts work
- [ ] Quick access bar works
- [ ] Bulk operations work

### Performance Tests
- [ ] Load time with 10 prompts
- [ ] Load time with 100 prompts
- [ ] Load time with 500 prompts
- [ ] Search response time
- [ ] Sorting response time

### Compatibility Tests
- [ ] Existing prompts load correctly
- [ ] Can still apply prompts to generation
- [ ] PromptDialog still works elsewhere (if used)
- [ ] No breaking changes to API

---

## Issues & Resolutions

| Issue | Resolution | Date |
|-------|------------|------|
| - | - | - |

---

## Deviations from Plan

| Original Plan | Actual Implementation | Reason |
|---------------|----------------------|--------|
| - | - | - |

---

## Progress Tracking

| Step | Status | Notes |
|------|--------|-------|
| 1. Database Schema | [ ] | |
| 2. Database Service | [ ] | |
| 3. PromptStyleCard | [ ] | |
| 4. CategoryBrowser | [ ] | |
| 5. PromptStyleTable | [ ] | |
| 6. Favorites & Pinning | [ ] | |
| 7. Enhanced Dialog | [ ] | |
| 8. Semantic Search | [ ] | |
| 9. Keyboard Shortcuts | [ ] | |
| 10. Integration | [ ] | |

---

## Code Snippets & References

### MudBlazor Components Used
- MudCard, MudCardHeader, MudCardContent, MudCardActions
- MudTable or MudList
- MudPaper
- MudChip, MudChipSet
- MudTextField (search)
- MudIconButton
- MudMenu

### Database Query Examples
```csharp
// Get prompts by category
var prompts = await _context.Prompts
    .Where(p => p.Category == category)
    .OrderBy(p => p.Title)
    .ToListAsync();

// Get pinned prompts ordered by SortOrder
var pinned = await _context.Prompts
    .Where(p => p.IsPinned)
    .OrderBy(p => p.SortOrder)
    .ToListAsync();
```

---

## Next Steps After Completion

1. Run full test suite
2. Request user approval
3. Update MAIN_PLAN.md status
4. Commit changes with message: "feat: Phase 1 - Styles Tab Redesign complete"
5. Proceed to Phase 2 (Wildcards Database Foundation)

---

**Current Step:** Ready to begin Step 1 (Database Schema Extension)  
**Blockers:** None  
**Questions for User:** None yet
