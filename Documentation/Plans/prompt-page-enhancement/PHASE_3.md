# Phase 3: Wildcards Tab UI - Implementation Document

## Phase Info
**Status:** [ ] Not Started  
**Complexity:** 13 points  
**Started:** Current Session  
**Related Plan:** [MAIN_PLAN.md](MAIN_PLAN.md)

---

## Implementation Guidelines

**Follow these conventions throughout this phase:**

### Execution Workflow (per step)
1. **Initial Code Writing** ? 2. **Test and Debug Features** ? 3. **Discuss Improvements** ? 4. **Update This Document**
   - Do NOT proceed until testing is complete
   - User must approve before updating this document
   - Build runs only after user requests or after completing all file edits

### Progress Symbols
- `[ ]` Not started | `[~]` In progress | `[x]` Complete and tested | `[!]` Blocked

### Complexity Points (Fibonacci)
**1** Trivial | **2** Simple | **3** Moderate | **5** Medium | **8** Complex | **13** Very Complex | **21+** Epic

### Key Rules
- **Each step = commit checkpoint** - test thoroughly before proceeding
- **Minimal changes only** - focused on phase objectives
- **Document all issues and resolutions** in this file
- **This document must have enough context** to resume in a new session
- **User permission required** before next step

---

## Objective

Build a comprehensive wildcard management interface with intuitive UI for creating, editing, and organizing wildcard collections and entries.

---

## Context

### Dependencies
- Phase 2 (Backend Wildcard System) - **COMPLETED** ?
  - Database entities created
  - Service layer functional
  - Sample data seeded
  - 41 unit tests passing

### Current State
- Backend infrastructure ready
- 14 sample collections with 70+ entries
- Wildcard detection: `__([a-zA-Z0-9](?:[a-zA-Z0-9_\-./]*[a-zA-Z0-9])?)__`
- Services available: `IWildcardService`, `IDatabaseService`

### Key Architectural Decisions
- **Split-pane Layout:** Collections browser (left) + Entry editor (right)
- **Real-time Updates:** Changes reflect immediately across UI
- **Import/Export:** Support for .txt files and JSON formats
- **Drag-and-Drop:** For intuitive entry reordering
- **Preview Panel:** Show wildcard syntax and expansion examples

### Files/Services Involved
- Existing: `BlazorWebApp/Components/Prompts/WildcardsPanel.razor` (to refactor)
- New Components: UI components for collection/entry management
- Services: `IWildcardService`, `IDatabaseService` (already implemented)

---

## Execution Checklist

### Step 1: Design UI Layout & Structure
**Complexity:** 2 points  
**Status:** [x] Complete

#### Tasks
- [x] Review existing `WildcardsPanel.razor` structure
- [x] Design split-pane layout (30% left, 70% right)
- [x] Create component hierarchy diagram
- [x] Define state management approach
- [x] Plan navigation flow
- [x] Review with user

#### Proposed Component Hierarchy
```
WildcardsTab.razor (replaces WildcardsPanel.razor - main container)
??? CollectionBrowser.razor (left pane - based on Phase 1 CategoryBrowser)
?   ??? Search TextField
?   ??? "New Collection" Button
?   ??? MudList with expansion panels for categories
?   ??? Individual collection items (MudListItem)
??? EntryManager.razor (right pane)
?   ??? Collection header (name, description, edit button)
?   ??? Action toolbar (Add Entry, Import, Export)
?   ??? EntryList (MudList with drag-drop support)
?   ?   ??? EntryListItem.razor (individual entry with actions)
?   ??? WildcardPreview.razor (syntax preview panel)
??? Dialogs (MudDialog overlays)
    ??? CollectionEditorDialog.razor (Create/Edit collection)
    ??? EntryEditorDialog.razor (Add/Edit entry)
    ??? WildcardImportDialog.razor (Import from files)
```

#### Design Decisions

**1. Replace, Don't Preserve**
- `WildcardsPanel.razor` ? Will be completely replaced by `WildcardsTab.razor`
- File-based approach ? Database-driven approach
- No backward compatibility needed (deprecated feature)

**2. Visual Consistency with Phase 1**
- Use existing `CategoryBrowser.razor` as design reference
- Maintain same visual style and interaction patterns
- Consistent use of MudBlazor components (MudList, MudListItem, chips, icons)
- Similar color scheme and spacing

**3. CategoryBrowser Design Pattern (from Phase 1)**
```csharp
// Key patterns to reuse:
- MudPaper wrapper with elevation
- MudList with Clickable and Dense properties
- MudStack for layout (Row/Column with spacing)
- Icon + Text + Chip layout for list items
- Selected state highlighting with CSS class
- Refresh button in header
- Loading state with MudProgressLinear
- Special categories (All, Favorites, Pinned, Uncategorized)
- Dynamic category list from database
```

**4. Component Structure Simplification**
```
Removed complexity:
- CategoryGroup.razor ? Use MudExpansionPanels directly
- CollectionListItem.razor ? Use MudListItem with inline template
- CollectionActions.razor ? Inline buttons in browser
- EntryList.razor ? Part of EntryManager
- EntryEditor.razor ? Use dialog instead
- EntryActions.razor ? Inline toolbar
```

#### State Management
```csharp
// WildcardsTab.razor (component-level)
private List<WildcardCollection> _collections = new();
private Dictionary<string, List<WildcardCollection>> _collectionsByCategory = new();
private WildcardCollection? _selectedCollection;
private List<WildcardEntry> _entries = new();
private bool _isLoading = true;
private string? _selectedCategory = null;
private string _searchText = string.Empty;

// No IStateService needed - component state only
```

#### Navigation Flow
```
1. Load collections from database (grouped by category)
2. Display in CollectionBrowser (similar to CategoryBrowser)
3. User selects category ? Expand/show collections
4. User selects collection ? Load entries in EntryManager
5. User edits ? Open dialog ? Save ? Refresh ? Update UI
```

#### Changes Made
**Analysis Complete:**
- Reviewed existing WildcardsPanel (file-based, deprecated)
- Reviewed Phase 1 CategoryBrowser (visual design reference)
- Simplified component hierarchy (fewer nested components)
- Defined replacement strategy (no preservation needed)
- Planned state management (component-level, no global state)
- Mapped MudBlazor components based on Phase 1 patterns

**Key Takeaways:**
1. **Replace completely** - WildcardsPanel is deprecated
2. **Visual consistency** - Follow CategoryBrowser design patterns
3. **Simplified structure** - Fewer components, inline templates where possible
4. **Database-first** - No file system integration needed

**Ready for Step 2** - Create WildcardsTab.razor base structure

---

### Step 2: Refactor WildcardsPanel Base
**Complexity:** 2 points  
**Status:** [~] In Progress

#### Tasks
- [ ] ~~Back up current `WildcardsPanel.razor`~~ (Will be replaced)
- [ ] Create new `WildcardsTab.razor` component in `Components/Prompts/Wildcards/`
- [ ] Create folder structure: `BlazorWebApp/Components/Prompts/Wildcards/`
- [ ] Set up MudBlazor layout (MudGrid with split columns)
- [ ] Add responsive breakpoints (xs/md/lg)
- [ ] Implement loading states (MudProgressLinear)
- [ ] Add service injection (IWildcardService, IDatabaseService, ISnackbar)
- [ ] Test basic layout renders

#### Layout Code Structure
```razor
@* WildcardsTab.razor *@
@inject IWildcardService WildcardService
@inject IDatabaseService Database
@inject IDialogService DialogService
@inject ISnackbar Snackbar

<MudGrid Spacing="2">
    <MudItem xs="12" md="4" lg="3">
        <!-- Collections Browser (similar to CategoryBrowser from Phase 1) -->
        <MudPaper Elevation="1" Class="pa-4" Style="height: calc(100vh - 200px); overflow-y: auto;">
            @if (_isLoading)
            {
                <MudProgressLinear Indeterminate Color="Color.Primary" />
            }
            else
            {
                <CollectionBrowser Collections="_collections"
                                   SelectedCollection="_selectedCollection"
                                   OnCollectionSelected="HandleCollectionSelected" />
            }
        </MudPaper>
    </MudItem>
    <MudItem xs="12" md="8" lg="9">
        <!-- Entry Manager -->
        <MudPaper Elevation="1" Class="pa-4" Style="height: calc(100vh - 200px); overflow-y: auto;">
            @if (_selectedCollection != null)
            {
                <EntryManager Collection="_selectedCollection"
                              OnEntriesChanged="RefreshCollection" />
            }
            else
            {
                <MudStack AlignItems="AlignItems.Center" Justify="Justify.Center" Style="height: 100%;">
                    <MudIcon Icon="@Icons.Material.Filled.TouchApp" Size="Size.Large" Color="Color.Default" />
                    <MudText Typo="Typo.h6" Color="Color.Default">Select a collection to view entries</MudText>
                </MudStack>
            }
        </MudPaper>
    </MudItem>
</MudGrid>

@code {
    private bool _isLoading = true;
    private List<WildcardCollection> _collections = new();
    private WildcardCollection? _selectedCollection;
    
    protected override async Task OnInitializedAsync()
    {
        await LoadCollections();
    }
    
    private async Task LoadCollections()
    {
        _isLoading = true;
        StateHasChanged();
        
        try
        {
            _collections = await Database.GetAllWildcardCollections();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error loading collections: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }
    
    private async Task HandleCollectionSelected(WildcardCollection collection)
    {
        _selectedCollection = collection;
        StateHasChanged();
    }
    
    private async Task RefreshCollection()
    {
        await LoadCollections();
    }
}
```

#### Folder Structure to Create
```
BlazorWebApp/Components/Prompts/Wildcards/
??? WildcardsTab.razor           (Main component - replaces WildcardsPanel)
??? CollectionBrowser.razor      (Left pane)
??? EntryManager.razor           (Right pane)
??? WildcardPreview.razor        (Preview panel)
??? CollectionEditorDialog.razor (Create/Edit dialog)
??? EntryEditorDialog.razor      (Entry dialog)
??? WildcardImportDialog.razor   (Import dialog)
```

#### CSS Classes to Match Phase 1
```css
/* Reuse from CategoryBrowser */
.category-browser { }
.category-item { }
.category-item-selected { 
    background-color: var(--mud-palette-action-default-hover);
}
```

#### Changes Made
{Update after completion}

---

### Step 3: Create CollectionBrowser Component
**Complexity:** 3 points  
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `CollectionBrowser.razor` component
- [ ] Implement category grouping logic
- [ ] Add collection selection handling
- [ ] Implement category expansion/collapse
- [ ] Add collection count badges
- [ ] Add "Create New Collection" button
- [ ] Add search/filter functionality
- [ ] Test with existing collections

#### Key Features
- Grouped by category with collapsible panels
- Show entry count for each collection
- Highlight selected collection
- Search across collection names
- Quick actions (rename, delete, duplicate)

#### UI Mockup
```
???????????????????????
? Collections         ?
? [Search...    ]  [+]?
???????????????????????
? ? Clothing (3)      ?
?   • tops (8)        ?
?   • bottoms (6)     ?
?   • shoes (5)       ?
? ? Locations (3)     ?
?   • indoor (6)      ?
?   • outdoor (6)     ?
?   • fantasy (6)     ?
? ? Styles (3)        ?
? ? Characters (3)    ?
? ? Actions (2)       ?
???????????????????????
```

#### Changes Made
{Update after completion}

---

### Step 4: Create EntryManager Component
**Complexity:** 3 points  
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `EntryManager.razor` component
- [ ] Display collection header with name/description
- [ ] Implement entry list with virtual scrolling
- [ ] Add entry selection/highlighting
- [ ] Add "Add Entry" button
- [ ] Implement entry editing (inline or dialog)
- [ ] Add delete with confirmation
- [ ] Test CRUD operations

#### Key Features
- Show collection details at top
- List all entries with weights
- Inline editing for quick changes
- Add new entries easily
- Delete with confirmation
- Show entry count and statistics

#### UI Mockup
```
????????????????????????????????????????
? Collection: Tops                     ?
? Category: Clothing                   ?
? Description: Various upper body...   ?
? ??????????????????????????????????  ?
? ? [+ Add Entry]  [Import] [Export]?  ?
? ??????????????????????????????????  ?
?                                      ?
? Entries (8):                         ?
? ????????????????????????????????   ?
? 1. ? white t-shirt      [1.0] [?][?]?
? 2. ? black hoodie       [1.0] [?][?]?
? 3. ? red dress shirt    [1.0] [?][?]?
? 4. ? blue sweater       [1.0] [?][?]?
? 5. ? green tank top     [1.0] [?][?]?
? ...                                  ?
????????????????????????????????????????
```

#### Changes Made
{Update after completion}

---

### Step 5: Implement Drag-and-Drop Reordering
**Complexity:** 3 points  
**Status:** [ ] Not Started

#### Tasks
- [ ] Add MudBlazor DropZone to entry list
- [ ] Implement drag handle UI
- [ ] Add drop indicators (visual feedback)
- [ ] Update entry SortOrder on drop
- [ ] Persist changes to database
- [ ] Test drag-drop behavior
- [ ] Handle edge cases (empty lists, single item)

#### Implementation Notes
- Use MudBlazor's `MudDropContainer`
- Visual drag handle (?) icon
- Show drop target indicator
- Update all affected entries' SortOrder
- Smooth animations

#### Changes Made
{Update after completion}

---

### Step 6: Create Collection CRUD Dialogs
**Complexity:** 2 points  
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `CollectionEditorDialog.razor`
- [ ] Add form fields (Name, Description, Category)
- [ ] Implement validation (unique names)
- [ ] Add Create/Update/Delete operations
- [ ] Add duplicate collection feature
- [ ] Test all CRUD operations

#### Dialog Fields
```razor
<MudDialog>
    <DialogContent>
        <MudTextField @bind-Value="Name" Label="Collection Name" Required />
        <MudTextField @bind-Value="Category" Label="Category" />
        <MudTextField @bind-Value="Description" Label="Description" 
                      Lines="3" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancel</MudButton>
        <MudButton OnClick="Save" Color="Color.Primary">Save</MudButton>
    </DialogActions>
</MudDialog>
```

#### Changes Made
{Update after completion}

---

### Step 7: Create Entry Editor Dialog
**Complexity:** 2 points  
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `EntryEditorDialog.razor`
- [ ] Add form fields (Value, Weight)
- [ ] Add weight slider (0.1 to 2.0)
- [ ] Implement validation
- [ ] Add preview of selection probability
- [ ] Test add/edit/save operations

#### Dialog Layout
```razor
<MudDialog>
    <DialogContent>
        <MudTextField @bind-Value="Value" Label="Entry Value" 
                      Required HelperText="Text that will be inserted" />
        <MudSlider @bind-Value="Weight" Min="0.1" Max="2.0" Step="0.1"
                   ValueLabel Color="Color.Primary">
            Weight: @Weight
        </MudSlider>
        <MudAlert Severity="Severity.Info">
            Probability: ~@CalculateProbability()%
        </MudAlert>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancel</MudButton>
        <MudButton OnClick="Save" Color="Color.Primary">Save</MudButton>
    </DialogActions>
</MudDialog>
```

#### Changes Made
{Update after completion}

---

### Step 8: Implement Wildcard Preview Panel
**Complexity:** 2 points  
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `WildcardPreview.razor` component
- [ ] Show syntax example (`__collection/name__`)
- [ ] Display all possible values
- [ ] Add "Test Random" button (show random selection)
- [ ] Show weight distribution visualization
- [ ] Add copy syntax button

#### Preview Features
- Show wildcard syntax
- List all possible values
- Random selection demo
- Visual weight indicator
- Copy to clipboard

#### UI Mockup
```
????????????????????????????????????????
? Preview                              ?
????????????????????????????????????????
? Syntax: __clothing/tops__            ?
? [Copy to Clipboard]                  ?
?                                      ?
? Possible Values (8):                 ?
? • white t-shirt (10%)                ?
? • black hoodie (10%)                 ?
? • red dress shirt (10%)              ?
? ...                                  ?
?                                      ?
? [Test Random Selection]              ?
? Result: "blue sweater"               ?
????????????????????????????????????????
```

#### Changes Made
{Update after completion}

---

### Step 9: Implement Import from Files
**Complexity:** 3 points  
**Status:** [ ] Not Started

#### Tasks
- [ ] Create `WildcardImportDialog.razor`
- [ ] Add file upload component (MudFileUpload)
- [ ] Support .txt file format (one entry per line)
- [ ] Support JSON format (our export format)
- [ ] Parse and validate file contents
- [ ] Show preview before import
- [ ] Handle duplicate entries
- [ ] Test with various file formats

#### Import Format Support

**Text File (.txt):**
```
white t-shirt
black hoodie
red dress shirt
```

**JSON Format:**
```json
{
  "name": "tops",
  "category": "Clothing",
  "description": "Upper body clothing",
  "entries": [
    {"value": "white t-shirt", "weight": 1.0},
    {"value": "black hoodie", "weight": 1.0}
  ]
}
```

#### Changes Made
{Update after completion}

---

### Step 10: Implement Export Functionality
**Complexity:** 2 points  
**Status:** [ ] Not Started

#### Tasks
- [ ] Add export button to entry manager
- [ ] Implement JSON export format
- [ ] Implement .txt export format (one entry per line)
- [ ] Add download file functionality
- [ ] Add "Export All" option
- [ ] Test export formats

#### Export Formats

**JSON (Full Data):**
```json
{
  "version": "1.0",
  "collections": [
    {
      "name": "clothing/tops",
      "category": "Clothing",
      "description": "Various upper body clothing items",
      "entries": [
        {"value": "white t-shirt", "weight": 1.0, "sortOrder": 0},
        {"value": "black hoodie", "weight": 1.0, "sortOrder": 1}
      ]
    }
  ]
}
```

**Text (Simple):**
```
white t-shirt
black hoodie
red dress shirt
```

#### Changes Made
{Update after completion}

---

### Step 11: Add Search and Filter
**Complexity:** 2 points  
**Status:** [ ] Not Started

#### Tasks
- [ ] Add search box to collection browser
- [ ] Implement collection name search
- [ ] Add filter by category
- [ ] Implement entry value search (within selected collection)
- [ ] Add highlight on search results
- [ ] Test search performance with large datasets

#### Search Features
- Search collection names
- Filter by category
- Search entry values
- Clear search button
- Search across all collections option

#### Changes Made
{Update after completion}

---

### Step 12: Polish UI and Add Keyboard Shortcuts
**Complexity:** 1 point  
**Status:** [ ] Not Started

#### Tasks
- [ ] Add loading skeletons
- [ ] Implement smooth animations
- [ ] Add keyboard shortcuts (Ctrl+N for new, Delete for remove)
- [ ] Add tooltips to all buttons
- [ ] Add empty state messages
- [ ] Polish responsive behavior
- [ ] Test on different screen sizes

#### Keyboard Shortcuts
```
Ctrl+N       : New Collection
Ctrl+E       : Edit Selected
Ctrl+I       : Import
Ctrl+Shift+E : Export
Delete       : Delete Selected
Escape       : Close Dialogs
Ctrl+F       : Focus Search
```

#### Changes Made
{Update after completion}

---

### Step 13: Create Wildcard Generation Documentation & Templates
**Complexity:** 2 points  
**Status:** [ ] Not Started

#### Tasks
- [ ] Create wildcard generation guide document
- [ ] Design JSON template structure for LLM generation
- [ ] Define verbosity levels (minimal, balanced, detailed, verbose)
- [ ] Create theme categories with examples
- [ ] Add keyword/tag system for collection types
- [ ] Create prompt templates for LLM generation
- [ ] Add quality guidelines and best practices
- [ ] Create example generation prompts
- [ ] Test templates with actual LLM (Ollama)

#### Purpose
Provide structured templates and guidelines so users and LLMs can easily generate high-quality, themed wildcard collections that integrate seamlessly with the system.

#### JSON Template Structure

```json
{
  "metadata": {
    "template_version": "1.0",
    "generator": "human|llm|mixed",
    "theme": "string",
    "verbosity": "minimal|balanced|detailed|verbose",
    "keywords": ["tag1", "tag2", "tag3"],
    "target_use_case": "string",
    "estimated_entries": 10
  },
  "collection": {
    "name": "string",
    "category": "string",
    "description": "string",
    "entries": [
      {
        "value": "string",
        "weight": 1.0,
        "tags": ["optional", "metadata"],
        "notes": "optional generation context"
      }
    ]
  },
  "generation_prompt": {
    "system_prompt": "string",
    "user_prompt": "string",
    "constraints": ["constraint1", "constraint2"]
  }
}
```

#### Verbosity Levels

**Minimal (1-3 words):**
```json
{
  "description": "Short, essential keywords only",
  "example_entries": [
    "red dress",
    "casual outfit",
    "formal wear"
  ],
  "use_case": "Quick variations, simple combinations"
}
```

**Balanced (3-6 words):**
```json
{
  "description": "Descriptive phrases with key details",
  "example_entries": [
    "elegant red evening dress",
    "casual denim jacket outfit",
    "professional business formal wear"
  ],
  "use_case": "Standard prompt building, versatile"
}
```

**Detailed (6-12 words):**
```json
{
  "description": "Rich descriptions with multiple attributes",
  "example_entries": [
    "elegant flowing red evening dress with lace details",
    "casual distressed denim jacket with white t-shirt underneath",
    "professional dark navy business suit with crisp white shirt"
  ],
  "use_case": "Detailed scene building, specific imagery"
}
```

**Verbose (12+ words):**
```json
{
  "description": "Comprehensive descriptions with atmosphere and context",
  "example_entries": [
    "elegant flowing red evening dress with intricate lace details, silk fabric, and a dramatic train trailing behind",
    "casual vintage distressed denim jacket worn over a simple white t-shirt, paired with comfortable jeans and sneakers",
    "professional tailored dark navy business suit with crisp white dress shirt, silk tie, and polished leather shoes"
  ],
  "use_case": "Highly specific scenes, story-driven generation"
}
```

#### Theme Categories

```json
{
  "theme_categories": [
    {
      "name": "Fashion & Clothing",
      "subcategories": ["tops", "bottoms", "shoes", "accessories", "outfits", "styles"],
      "keywords": ["fashion", "clothing", "apparel", "outfit", "attire"],
      "typical_verbosity": "balanced"
    },
    {
      "name": "Locations & Settings",
      "subcategories": ["indoor", "outdoor", "fantasy", "historical", "modern", "natural"],
      "keywords": ["location", "setting", "environment", "place", "scene"],
      "typical_verbosity": "detailed"
    },
    {
      "name": "Artistic Styles",
      "subcategories": ["medium", "technique", "movement", "era", "influence"],
      "keywords": ["art", "style", "aesthetic", "artistic", "visual"],
      "typical_verbosity": "balanced"
    },
    {
      "name": "Lighting & Atmosphere",
      "subcategories": ["natural", "artificial", "mood", "time-of-day", "weather"],
      "keywords": ["lighting", "atmosphere", "ambiance", "mood", "illumination"],
      "typical_verbosity": "detailed"
    },
    {
      "name": "Character Features",
      "subcategories": ["hair", "eyes", "body", "expression", "age", "ethnicity"],
      "keywords": ["character", "person", "human", "features", "appearance"],
      "typical_verbosity": "balanced"
    },
    {
      "name": "Actions & Poses",
      "subcategories": ["standing", "sitting", "dynamic", "static", "interactions"],
      "keywords": ["action", "pose", "position", "gesture", "movement"],
      "typical_verbosity": "balanced"
    },
    {
      "name": "Objects & Props",
      "subcategories": ["furniture", "technology", "tools", "decorative", "functional"],
      "keywords": ["object", "item", "prop", "element", "thing"],
      "typical_verbosity": "minimal"
    },
    {
      "name": "Colors & Palettes",
      "subcategories": ["primary", "secondary", "combinations", "moods", "schemes"],
      "keywords": ["color", "palette", "hue", "tone", "shade"],
      "typical_verbosity": "minimal"
    },
    {
      "name": "Emotions & Expressions",
      "subcategories": ["positive", "negative", "neutral", "complex", "subtle"],
      "keywords": ["emotion", "feeling", "expression", "mood", "sentiment"],
      "typical_verbosity": "balanced"
    },
    {
      "name": "Composition & Framing",
      "subcategories": ["shot-type", "angle", "perspective", "focus", "depth"],
      "keywords": ["composition", "framing", "shot", "view", "camera"],
      "typical_verbosity": "balanced"
    }
  ]
}
```

#### LLM Generation Prompt Templates

**Template 1: Basic Collection Generation**
```
System Prompt:
You are a creative assistant helping generate wildcard collections for AI image generation prompts. 
Generate a collection of {count} entries following these guidelines:

Theme: {theme}
Category: {category}
Verbosity: {verbosity_level}
Keywords: {keywords}

Rules:
1. Each entry should be a complete, usable phrase
2. Maintain consistent verbosity level
3. Ensure variety and creativity
4. Avoid repetition and redundancy
5. Focus on visual, concrete descriptions
6. Each entry should work standalone in a prompt

Output Format: JSON array of entry objects with "value" and "weight" properties.

User Prompt:
Generate {count} wildcard entries for "{collection_name}" with theme "{theme}".
Verbosity level: {verbosity_level}.
Focus on: {keywords_list}.

Example entry format: {"value": "example text", "weight": 1.0}
```

**Template 2: Themed Expansion**
```
System Prompt:
You are expanding an existing wildcard collection with themed variations. 
Generate {count} new entries that complement the existing collection while exploring the theme more deeply.

Existing Collection: {collection_name}
Current Entries: {existing_entries_sample}
Theme Extension: {new_theme_direction}
Verbosity: {verbosity_level}

Rules:
1. Complement existing entries without duplicating
2. Explore unexplored aspects of the theme
3. Maintain consistent style and verbosity
4. Add creative variations
5. Consider edge cases and unique options

User Prompt:
Add {count} new entries to "{collection_name}" exploring: {theme_direction}.
Current collection focuses on: {current_focus}.
New entries should explore: {new_focus}.
```

**Template 3: Quality Enhancement**
```
System Prompt:
You are enhancing existing wildcard entries to improve their quality and effectiveness.

Task: {enhance_type}
Options: [increase_verbosity, add_details, improve_variety, fix_quality]

Current Entries: {entries_to_enhance}
Target Verbosity: {target_verbosity}
Enhancement Focus: {focus_areas}

Rules:
1. Preserve the core concept of each entry
2. Enhance without changing meaning
3. Maintain visual focus
4. Improve descriptive quality
5. Ensure entries work in prompts

User Prompt:
Enhance these wildcard entries:
{entries_list}

Focus on: {enhancement_goals}
Target verbosity: {verbosity_level}
```

#### Quality Guidelines

```markdown
# Wildcard Entry Quality Guidelines

## Do's ?
- Use concrete, visual descriptions
- Focus on observable characteristics
- Maintain consistent verbosity within collection
- Include variety in perspectives and styles
- Use specific, precise language
- Test entries work in actual prompts
- Consider common use cases
- Balance common and unique options

## Don'ts ?
- Avoid vague or abstract concepts
- Don't use contradictory terms
- Avoid excessive redundancy
- Don't include prompt syntax in entries
- Avoid overly complex nested descriptions
- Don't mix verbosity levels randomly
- Avoid non-visual concepts
- Don't duplicate existing entries

## Best Practices
1. **Specificity**: "vintage leather jacket" > "jacket"
2. **Visual Focus**: "glowing sunset sky" > "beautiful sky"
3. **Usability**: Phrases that work standalone in prompts
4. **Variety**: Cover different aspects of the theme
5. **Consistency**: Maintain collection style
6. **Testing**: Verify entries produce expected results
7. **Organization**: Group related concepts
8. **Weight Distribution**: Use weights for probability control
```

#### Example Generation Session

```json
{
  "generation_request": {
    "collection_name": "sci-fi-environments",
    "category": "Locations",
    "theme": "Science Fiction Environments",
    "verbosity": "detailed",
    "keywords": ["futuristic", "technology", "space", "cyberpunk"],
    "target_count": 15,
    "focus": "varied sci-fi settings with different tech levels"
  },
  "llm_prompt": "Generate 15 detailed wildcard entries for sci-fi environments...",
  "expected_output": [
    {
      "value": "sleek chrome-plated space station orbiting a distant planet",
      "weight": 1.0,
      "tags": ["space", "station", "futuristic"]
    },
    {
      "value": "neon-lit cyberpunk city street with holographic advertisements",
      "weight": 1.2,
      "tags": ["cyberpunk", "urban", "neon"]
    }
  ]
}
```

#### Documentation Files to Create

1. **`WILDCARD_GENERATION_GUIDE.md`**
   - Complete guide for generating wildcards
   - Verbosity level explanations
   - Theme category reference
   - Quality guidelines
   - Example prompts

2. **`WILDCARD_TEMPLATE.json`**
   - Blank template for new collections
   - Inline documentation
   - Example entries

3. **`LLM_PROMPTS.json`**
   - Collection of tested LLM prompts
   - Templates for different generation types
   - System prompts and user prompts
   - Expected output formats

4. **`THEME_CATALOG.json`**
   - Complete list of theme categories
   - Subcategories and keywords
   - Recommended verbosity levels
   - Example collections for each theme

#### Integration Points

- Add "Generate with LLM" button in UI (future phase)
- Provide template download in UI
- Link documentation from Help/About
- Include templates in repository
- Add examples to seed data

#### Changes Made
{Update after completion}

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1. Design UI Layout | [x] | 2 pts | Complete - Simplified structure, Phase 1 patterns |
| 2. Refactor Base Panel | [~] | 2 pts | Ready to implement - WildcardsTab.razor |
| 3. Collection Browser | [ ] | 3 pts | Based on CategoryBrowser design |
| 4. Entry Manager | [ ] | 3 pts | Right pane with entry list |
| 5. Drag-Drop Reorder | [ ] | 3 pts | Entry reordering |
| 6. Collection CRUD | [ ] | 2 pts | Create/Edit/Delete collections |
| 7. Entry Editor | [ ] | 2 pts | Add/Edit entry dialog |
| 8. Preview Panel | [ ] | 2 pts | Wildcard syntax preview |
| 9. Import from Files | [ ] | 3 pts | .txt and JSON import |
| 10. Export | [ ] | 2 pts | JSON and .txt export |
| 11. Search & Filter | [ ] | 2 pts | Search collections and entries |
| 12. Polish & Shortcuts | [ ] | 1 pt | Final touches |
| 13. Generation Docs | [ ] | 2 pts | Templates for LLM generation |

**Completed:** 2 points / 29 points (7%)

---

## Issues & Resolutions

{Document any issues encountered during implementation}

---

## Commit Checkpoints

- [ ] After Step 4 complete (Basic UI structure working)
- [ ] After Step 7 complete (Full CRUD operations functional)
- [ ] After Step 10 complete (Import/Export working)
- [ ] After Step 12 complete (UI polished)
- [ ] After Step 13 complete (Documentation and templates ready)

---

## Success Criteria

- [ ] Intuitive two-pane layout similar to file explorer
- [ ] Can create and edit collections without confusion
- [ ] Import preserves existing wildcards from file system
- [ ] Export compatible with standard formats
- [ ] Drag-and-drop works smoothly for reordering
- [ ] Search finds collections and entries quickly
- [ ] Keyboard shortcuts improve workflow
- [ ] Responsive design works on tablets
- [ ] No breaking changes to existing backend
- [ ] Performance remains smooth with 100+ collections
- [ ] **NEW:** Documentation enables easy wildcard generation
- [ ] **NEW:** Templates work with LLM generation tools
- [ ] **NEW:** Quality guidelines are clear and actionable

---

## Phase Summary

{Update after completion}

### Accomplishments
{List after completion}

### Metrics
- Components Created: {count}
- Lines of Code: {approx}
- Features Implemented: {list}

### Deferred Items
{List if any features are postponed}

---

**Phase Status:** Ready to Begin

---

## Related Documentation

- [MAIN_PLAN.md](MAIN_PLAN.md) - Overall project plan
- [PHASE_2.md](PHASE_2.md) - Backend implementation (COMPLETED)
- [IMPLEMENTATION_GUIDE.md](../IMPLEMENTATION_GUIDE.md) - General guidelines

---

## Notes for Implementation

### MudBlazor Components to Use
- `MudGrid` / `MudItem` - Layout
- `MudPaper` - Panels
- `MudList` / `MudListItem` - Collection lists
- `MudExpansionPanel` - Category groups
- `MudDialog` - Editors
- `MudTextField` - Input fields
- `MudSlider` - Weight selection
- `MudButton` / `MudIconButton` - Actions
- `MudFileUpload` - File import
- `MudDropContainer` - Drag-drop
- `MudSkeleton` - Loading states

### State Management
- Use component-level state for UI
- Call `WildcardService` for business logic
- Call `DatabaseService` for persistence
- Use `EventCallback` for parent-child communication
- Consider `IStateService` if global state needed

### Performance Considerations
- Virtual scrolling for large lists
- Debounce search input
- Lazy load categories
- Cache collection list
- Optimize database queries

### Testing Strategy
- Test with empty database
- Test with seeded sample data
- Test with large datasets (100+ collections)
- Test import/export round-trip
- Test drag-drop edge cases
- Test on mobile/tablet
