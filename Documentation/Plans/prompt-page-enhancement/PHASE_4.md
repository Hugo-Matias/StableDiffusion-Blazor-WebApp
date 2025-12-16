# Phase 4: Autocomplete Multi-Trigger System - Implementation Document

## Phase Info
**Status:** [~] In Progress  
**Complexity:** 13 points (increased from 8, expanded scope)  
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

Transform TextFieldAutocomplete into a comprehensive multi-trigger system supporting:
1. **Wildcards** (`_` trigger) - Hierarchical category/collection navigation with prompt insertion
2. **Tags** (`#` trigger) - Force tag search (including tags starting with special chars)
3. **LoRAs** (`<` trigger) - Add to Parameters.Loras list without prompt insertion
4. **Styles** (`@` trigger) - Add to Parameters.Styles list without prompt insertion
5. **Default** (no trigger) - Existing tag/dictionary search behavior

Extend Parser to handle wildcard expansion during generation.

---

## Context

### Dependencies
- Phase 2 (Backend Wildcard System) - **COMPLETED** ?
  - WildcardService with SelectRandomEntry method
  - Database with collections and entries
  - 14 sample collections with categories ready for use
- Phase 3 (Wildcards Tab UI) - **COMPLETED** ?
  - Users can manage collections via UI
  - Import/Export functionality available

### Current State
- TextFieldAutocomplete supports tag/dictionary autocomplete
- TriggerChar parameter exists but unused (`'\0'`)
- Parser.cs handles style expansion but not wildcards
- No dynamic wildcard suggestions in prompt fields
- Wildcard syntax: `__category/collection__` format

### Key Architectural Decisions

#### Multi-Trigger Architecture

| Trigger | Purpose | Behavior | Applied To | Phase 4 |
|---------|---------|----------|------------|---------|
| `_` | Wildcards | Hierarchical navigation, insert `__category/collection__` | Prompt text | ? Full Implementation |
| `#` | Tags (forced) | Force tag search (bypass other triggers) | Prompt text | ? Full Implementation |
| `<` | LoRAs | Add to LoRA list, remove trigger from prompt | Parameters.Loras | ? Full Implementation |
| `@` | Styles | Add to Styles list, remove trigger from prompt | Parameters.Styles | ? Full Implementation |
| `\` | Escape | Reserved for backend (not a trigger) | N/A | ?? Document only |
| (none) | Default | Existing tag/dictionary search | Prompt text | ? Keep existing |

#### Wildcard Hierarchical Navigation

**User Flow:**
```
1. User types "_" ? Show all categories (unfiltered)
2. User types "_c" ? Filter categories: clothing, colors, characters
3. User selects "clothing" ? Autocomplete to "__clothing/"
4. Automatically show collections: tops, bottoms, shoes, accessories
5. User selects "tops" ? Complete to "__clothing/tops__ " (with space)
6. User deletes "tops" ? Cursor at "__clothing/|__ "
7. Show collections again for editing
```

**Key Rules:**
- Single `_` triggers category search
- Double `__category/` triggers collection search
- Selection applies complete wildcard syntax
- Editing mid-wildcard re-triggers collection search
- Only apply on dropdown selection, NOT on raw text entry

#### Non-Prompt Triggers (LoRA/Style)

**Behavior:**
- Trigger shows autocomplete dropdown
- Selection adds to respective list (Parameters.Loras or Parameters.Styles)
- Trigger character is **removed from prompt** after selection
- No prompt text insertion
- Can be triggered anywhere in text (not position-dependent)

### Files/Services Involved
- `BlazorWebApp/Components/Shared/Generation/TextFieldAutocomplete.razor` (major refactor)
- `BlazorWebApp/Extensions/Parser.cs` (extend for wildcards)
- `BlazorWebApp/Services/WildcardService.cs` (already has expansion logic)
- `BlazorWebApp/Components/Shared/Generation/PromptFields.razor` (event handling)

---

## Execution Checklist

### Step 1: Design Multi-Trigger Architecture
**Complexity:** 2 points  
**Status:** [x] Complete and tested

#### Tasks
- [x] Review TextFieldAutocomplete.razor structure
- [x] Understand current tag autocomplete mechanism
- [x] Design AutocompleteMode enum and state machine
- [x] Design WildcardContext for hierarchical navigation
- [x] Plan trigger detection logic
- [x] Document mode routing strategy

#### Architecture Design

```csharp
// Autocomplete mode enum
public enum AutocompleteMode
{
    None,                   // No trigger detected
    Tags,                   // Default or # trigger
    WildcardCategory,       // _ trigger (searching categories)
    WildcardCollection,     // __category/ (searching collections)
    Lora,                   // < trigger
    Style                   // @ trigger
}

// Wildcard navigation context
private class WildcardContext
{
    public WildcardState State { get; set; }
    public string? SelectedCategory { get; set; }
    public int TriggerStartPosition { get; set; }
    public string SearchQuery { get; set; } = "";
}

public enum WildcardState
{
    SearchingCategories,     // After "_" or "_c"
    SearchingCollections,    // After "__category/" or "__category/c"
    Complete                 // After "__category/collection__ "
}
```

#### Success Criteria
- [x] Clear separation of concerns per mode
- [x] State machine handles wildcard navigation
- [x] Extensible for future triggers
- [x] No breaking changes to existing functionality

#### Changes Made
? **COMPLETED** - Foundation architecture successfully implemented

**Files Modified:**
- `BlazorWebApp/Components/Shared/Generation/TextFieldAutocomplete.razor`

**Implementation Details:**
1. Added `AutocompleteMode` enum (6 modes: None, Tags, WildcardCategory, WildcardCollection, Lora, Style)
2. Added `WildcardState` enum (3 states: SearchingCategories, SearchingCollections, Complete)
3. Added `WildcardContext` class with properties: State, SelectedCategory, TriggerStartPosition, SearchQuery
4. Added event callback parameters: `OnLoraSelected`, `OnStyleSelected`
5. Added state variables for each mode: `_currentMode`, `_wildcardContext`, `_wildcardCategorySuggestions`, `_wildcardCollectionSuggestions`, `_loraSuggestions`, `_styleSuggestions`

**Validation:**
- ? Build successful (no compilation errors)
- ? No breaking changes (all existing code preserved)
- ? All types properly documented with XML comments
- ? Foundation ready for trigger detection in Step 2

---

### Step 2: Implement Trigger Detection System
**Complexity:** 3 points  
**Status:** [x] Complete and tested

#### Tasks
- [x] Implement DetectAutocompleteMode() method
- [x] Add trigger detection for each mode: `_`, `#`, `<`, `@`
- [x] Implement wildcard state detection (category vs collection)
- [x] Handle cursor position for mid-wildcard editing
- [x] Test trigger detection with various patterns
- [x] Ensure no trigger interference

#### Trigger Detection Logic

```csharp
private async Task<AutocompleteMode> DetectAutocompleteMode(string textBeforeCursor)
{
    // Priority order: Wildcards > LoRA > Style > Tag Force > Default
    
    // 1. Wildcard detection (highest priority for complexity)
    var lastUnderscore = textBeforeCursor.LastIndexOf('_');
    if (lastUnderscore >= 0)
    {
        var afterUnderscore = textBeforeCursor.Substring(lastUnderscore);
        
        // Pattern: Single _ (category search)
        if (Regex.IsMatch(afterUnderscore, @"^_[a-zA-Z0-9\-]*$"))
        {
            _wildcardContext = new WildcardContext
            {
                State = WildcardState.SearchingCategories,
                TriggerStartPosition = lastUnderscore,
                SearchQuery = afterUnderscore.Substring(1)
            };
            return AutocompleteMode.WildcardCategory;
        }
        
        // Pattern: __category/ (collection search)
        var categoryMatch = Regex.Match(afterUnderscore, @"^__([a-zA-Z0-9\-]+)/([a-zA-Z0-9\-]*)$");
        if (categoryMatch.Success)
        {
            _wildcardContext = new WildcardContext
            {
                State = WildcardState.SearchingCollections,
                SelectedCategory = categoryMatch.Groups[1].Value,
                TriggerStartPosition = lastUnderscore,
                SearchQuery = categoryMatch.Groups[2].Value
            };
            return AutocompleteMode.WildcardCollection;
        }
        
        // Pattern: Editing existing wildcard "__category/|__ "
        // (Check if cursor is inside wildcard and after slash)
        var editResult = DetectWildcardEditMode();
        if (editResult != AutocompleteMode.None)
            return editResult;
    }
    
    // 2. LoRA trigger '<'
    var lastLt = textBeforeCursor.LastIndexOf('<');
    if (lastLt >= 0)
    {
        var afterLt = textBeforeCursor.Substring(lastLt + 1);
        if (!afterLt.Contains(',') && !afterLt.Contains('\n'))
        {
            return AutocompleteMode.Lora;
        }
    }
    
    // 3. Style trigger '@'
    var lastAt = textBeforeCursor.LastIndexOf('@');
    if (lastAt >= 0)
    {
        var afterAt = textBeforeCursor.Substring(lastAt + 1);
        if (!afterAt.Contains(',') && !afterAt.Contains('\n'))
        {
            return AutocompleteMode.Style;
        }
    }
    
    // 4. Tag force trigger '#'
    var lastHash = textBeforeCursor.LastIndexOf('#');
    if (lastHash >= 0)
    {
        var afterHash = textBeforeCursor.Substring(lastHash + 1);
        if (!afterHash.Contains(',') && !afterHash.Contains('\n'))
        {
            return AutocompleteMode.Tags; // Force tag search
        }
    }
    
    // 5. Default: existing tag/dictionary behavior
    return AutocompleteMode.Tags;
}
```

#### Success Criteria
- [x] Each trigger detected correctly
- [x] Wildcard category/collection states distinguished
- [x] Mid-wildcard editing supported
- [x] No false positives
- [x] Performance acceptable (< 10ms)

#### Changes Made
? **COMPLETED** - Trigger detection system successfully implemented

**Files Modified:**
- `BlazorWebApp/Components/Shared/Generation/TextFieldAutocomplete.razor`

**Implementation Details:**
1. Added `@using System.Text.RegularExpressions` directive for pattern matching
2. Implemented `DetectAutocompleteMode(string textBeforeCursor)` method with priority order:
   - Wildcards (`_` for categories, `__category/` for collections)
   - LoRA (`<` trigger)
   - Style (`@` trigger)
   - Tag force (`#` trigger)
   - Default tags
3. Implemented `GetSearchQueryForMode()` to extract search text after trigger
4. Implemented `ClearAllSuggestions()` helper to reset all suggestion lists
5. Implemented `HasAnySuggestions()` to check for results in current mode
6. Refactored `SearchSuggestions()` to use mode routing with switch statement

**Trigger Pattern Details:**
| Trigger | Pattern | Detection | Mode |
|---------|---------|-----------|------|
| `_` | Word boundary + `_chars` | Regex `^_[a-zA-Z0-9\-]*$` | WildcardCategory |
| `__cat/` | `__category/chars` | Regex `^__([a-zA-Z0-9\-_]+)/([a-zA-Z0-9\-_]*)$` | WildcardCollection |
| `<` | No `>`, `,`, `\n` after | String checks | Lora |
| `@` | No `,`, `\n` after | String checks | Style |
| `#` | No `,`, `\n` after | String checks | Tags (forced) |

**Validation:**
- ? Build successful
- ? No breaking changes to existing autocomplete
- ? Mode routing properly delegates to search methods

---

### Step 3: Implement Search Methods for Each Mode
**Complexity:** 3 points  
**Status:** [x] Complete and tested

#### Tasks
- [x] Refactor existing SearchSuggestions() to use mode routing
- [x] Implement SearchWildcardCategories()
- [x] Implement SearchWildcardCollections()
- [x] Implement SearchLoras()
- [x] Implement SearchStyles()
- [x] Keep existing SearchTagsAndDictionary()
- [x] Test each search path independently

#### Search Routing

```csharp
private async Task SearchSuggestions()
{
    await InvokeAsync(async () =>
    {
        // Clear all suggestion lists
        ClearAllSuggestions();
        
        if (!EnableAutocomplete)
        {
            _showSuggestions = false;
            StateHasChanged();
            return;
        }
        
        // Detect mode based on cursor position and text
        var textBeforeCursor = Value.Substring(0, Math.Min(_cursorPosition, Value.Length));
        _currentMode = await DetectAutocompleteMode(textBeforeCursor);
        
        // Route to appropriate search method
        switch (_currentMode)
        {
            case AutocompleteMode.WildcardCategory:
                await SearchWildcardCategories();
                break;
                
            case AutocompleteMode.WildcardCollection:
                await SearchWildcardCollections();
                break;
                
            case AutocompleteMode.Lora:
                await SearchLoras();
                break;
                
            case AutocompleteMode.Style:
                await SearchStyles();
                break;
                
            case AutocompleteMode.Tags:
            default:
                await SearchTagsAndDictionary();
                break;
        }
        
        StateHasChanged();
    });
}
```

#### Wildcard Search Methods

```csharp
private async Task SearchWildcardCategories()
{
    if (_wildcardContext == null) return;
    
    var allCategories = await WildcardService.GetCategories();
    
    if (!string.IsNullOrWhiteSpace(_wildcardContext.SearchQuery))
    {
        allCategories = allCategories
            .Where(c => c.StartsWith(_wildcardContext.SearchQuery, 
                StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
    
    _wildcardCategorySuggestions = allCategories.Take(MaxSuggestions).ToList();
    _showSuggestions = _wildcardCategorySuggestions.Any();
}

private async Task SearchWildcardCollections()
{
    if (_wildcardContext == null || string.IsNullOrEmpty(_wildcardContext.SelectedCategory))
        return;
    
    var collections = await WildcardService.GetCollectionsByCategory(
        _wildcardContext.SelectedCategory);
    
    if (!string.IsNullOrWhiteSpace(_wildcardContext.SearchQuery))
    {
        collections = collections
            .Where(c => c.Name.StartsWith(_wildcardContext.SearchQuery, 
                StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
    
    _wildcardCollectionSuggestions = collections.Take(MaxSuggestions).ToList();
    _showSuggestions = _wildcardCollectionSuggestions.Any();
}
```

#### Success Criteria
- All search paths work independently
- Results filtered correctly
- Performance acceptable (< 50ms per search)
- No cross-contamination between modes

#### Changes Made
? **COMPLETED** - All search methods implemented

**Files Modified:**
- `BlazorWebApp/Services/IWildcardService.cs` - Added 3 new interface methods
- `BlazorWebApp/Services/WildcardService.cs` - Implemented interface methods
- `BlazorWebApp/Components/Shared/Generation/TextFieldAutocomplete.razor` - All search methods

**New Interface Methods (IWildcardService):**
```csharp
Task<List<string>> GetCategories();
Task<List<WildcardCollection>> GetCollectionsByCategory(string category);
Task<List<WildcardCollection>> SearchCollections(string searchQuery, int maxResults = 10);
```

**Service Injections Added:**
- `@inject IWildcardService WildcardService`
- `@inject IDatabaseService DatabaseService`

**Search Methods Implemented:**

| Method | Source | Filtering | Status |
|--------|--------|-----------|--------|
| `SearchWildcardCategories()` | WildcardService.GetCategories() | StartsWith query | ? |
| `SearchWildcardCollections()` | WildcardService.GetCollectionsByCategory() | StartsWith query | ? |
| `SearchLoras()` | DatabaseService.GetResources() | Title/Filename/Tags Contains | ? |
| `SearchStyles()` | DatabaseService.GetPrompts() | Name Contains | ? |
| `SearchTagsAndDictionary()` | CsvService/CacheService | Existing behavior | ? (preserved) |

**LoRA Search Implementation:**
- Queries all resources from database
- Filters for Type.Name = "LORA" or "LoCon"
- Only shows enabled LoRAs (`r.IsEnabled`)
- Searches Title, Filename, and Tags
- Returns as `List<LocalResource>`

**Validation:**
- ? Build successful
- ? All service injections working
- ? Each search path independent and tested
- ? No breaking changes to existing functionality

---

### Step 4: Implement Visual Differentiation UI
**Complexity:** 2 points  
**Status:** [x] Complete and tested

#### Tasks
- [x] Update dropdown UI to show current mode
- [x] Add mode-specific icons and colors
- [x] Add section headers for each mode
- [x] Add entry count badges for wildcards
- [x] Test visual clarity
- [x] Ensure accessibility (screen readers)

#### UI Structure

```razor
@if (_showSuggestions)
{
    <MudPaper Class="autocomplete-dropdown" Elevation="8">
        <MudList Clickable Dense>
            
            @if (_currentMode == AutocompleteMode.WildcardCategory)
            {
                <MudListSubheader>
                    <MudIcon Icon="@Icons.Material.Filled.Category" Size="Size.Small" Color="Color.Tertiary" />
                    <MudText Typo="Typo.caption">Wildcard Categories</MudText>
                </MudListSubheader>
                @foreach (var category in _wildcardCategorySuggestions)
                {
                    <MudListItem OnClick="() => SelectWildcardCategory(category)">
                        <MudText Color="Color.Tertiary">
                            <MudIcon Icon="@Icons.Material.Filled.Folder" Size="Size.Small" />
                            _<strong>@category</strong>
                        </MudText>
                    </MudListItem>
                }
            }
            
            @if (_currentMode == AutocompleteMode.WildcardCollection)
            {
                <MudListSubheader>
                    <MudIcon Icon="@Icons.Material.Filled.Casino" Size="Size.Small" Color="Color.Tertiary" />
                    <MudText Typo="Typo.caption">Collections in "@_wildcardContext?.SelectedCategory"</MudText>
                </MudListSubheader>
                @foreach (var collection in _wildcardCollectionSuggestions)
                {
                    <MudListItem OnClick="() => SelectWildcardCollection(collection)">
                        <MudText Color="Color.Tertiary">
                            <MudIcon Icon="@Icons.Material.Filled.CollectionsBookmark" Size="Size.Small" />
                            <strong>@collection.Name</strong>
                        </MudText>
                        <MudChip Size="Size.Small" Color="Color.Info">
                            @collection.Entries.Count entries
                        </MudChip>
                    </MudListItem>
                }
            }
            
            @if (_currentMode == AutocompleteMode.Lora)
            {
                <MudListSubheader>
                    <MudIcon Icon="@Icons.Material.Filled.Layers" Size="Size.Small" Color="Color.Success" />
                    <MudText Typo="Typo.caption">LoRA Resources</MudText>
                </MudListSubheader>
                @* LoRA list items *@
            }
            
            @if (_currentMode == AutocompleteMode.Style)
            {
                <MudListSubheader>
                    <MudIcon Icon="@Icons.Material.Filled.Style" Size="Size.Small" Color="Color.Primary" />
                    <MudText Typo="Typo.caption">Prompt Styles</MudText>
                </MudListSubheader>
                @* Style list items *@
            }
            
            @* Existing tag/dictionary UI *@
            
        </MudList>
    </MudPaper>
}
```

#### Success Criteria
- Clear visual distinction between modes
- Icons help identify mode instantly
- Headers provide context
- Badges show useful metadata
- Consistent with existing app theme

#### Changes Made
? **COMPLETED** - Visual differentiation UI implemented for all modes

**Files Modified:**
- `BlazorWebApp/Components/Shared/Generation/TextFieldAutocomplete.razor`

**Implementation Details:**
1. Updated dropdown condition from `(_tagSuggestions.Any() || _wordSuggestions.Any())` to `HasAnySuggestions()` - **Critical fix!**
2. Added mode-specific UI sections with conditional rendering:
   - **WildcardCategory**: Purple/Tertiary, Folder icon, shows `_category`
   - **WildcardCollection**: Purple/Tertiary, Casino icon, collection name + entry count badge
   - **Lora**: Green/Success, Layers icon, shows base model chip
   - **Style**: Blue/Primary, Style icon
   - **Tags**: Preserved existing behavior
3. Added selection highlighting with `isSelected` for keyboard navigation
4. Entry count badges display collection size

**Visual Design:**
| Mode | Color | Icon | Badge |
|------|-------|------|-------|
| WildcardCategory | Tertiary | Folder | - |
| WildcardCollection | Tertiary | CollectionsBookmark | Entry count |
| Lora | Success | Layers | Base model |
| Style | Primary | Style | - |
| Tags | Various | Label/History | Uses/Source |

**Validation:**
- ? Build successful
- ? All triggers show appropriate dropdown UI
- ? Visual distinction clear between modes
- ? Icons and colors consistent with app theme

---

### Step 5: Implement Selection Logic for All Modes
**Complexity:** 3 points  
**Status:** [x] Complete and tested

#### Tasks
- [x] Implement SelectWildcardCategory() - inserts `__category/`
- [x] Implement SelectWildcardCollection() - completes to `__category/collection__ `
- [x] Implement SelectLora() - adds to Parameters.Loras, removes `<` from prompt
- [x] Implement SelectStyle() - adds to Parameters.Styles, removes `@` from prompt
- [x] Update SelectTag() to handle `#` trigger removal
- [x] Test each selection path
- [x] Verify cursor positioning

#### Wildcard Selection Logic

```csharp
private async Task SelectWildcardCategory(string category)
{
    if (_wildcardContext == null) return;
    
    var beforeTrigger = Value.Substring(0, _wildcardContext.TriggerStartPosition);
    var afterCursor = Value.Substring(_cursorPosition);
    
    // Insert "__category/" and keep autocomplete open for collections
    var newValue = $"{beforeTrigger}__{category}/{afterCursor}";
    
    Value = newValue;
    await _textFieldRef.SetText(newValue);
    await ValueChanged.InvokeAsync(newValue);
    
    // Position cursor after "/"
    var newCursorPos = beforeTrigger.Length + category.Length + 3; // "__category/|"
    await SetCursorPosition(newCursorPos);
    
    // Update context for collection search
    _wildcardContext.State = WildcardState.SearchingCollections;
    _wildcardContext.SelectedCategory = category;
    _wildcardContext.SearchQuery = "";
    
    // Auto-trigger collection search
    await SearchWildcardCollections();
}

private async Task SelectWildcardCollection(WildcardCollection collection)
{
    if (_wildcardContext == null) return;
    
    // Find pattern in text
    var pattern = $"__{_wildcardContext.SelectedCategory}/";
    var patternIndex = Value.LastIndexOf(pattern, _cursorPosition);
    
    if (patternIndex == -1) return;
    
    var beforePattern = Value.Substring(0, patternIndex);
    
    // Handle existing "__" suffix if present
    var afterPattern = Value.Substring(_cursorPosition);
    var existingEndIndex = afterPattern.IndexOf("__");
    if (existingEndIndex >= 0)
    {
        afterPattern = afterPattern.Substring(existingEndIndex + 2);
    }
    
    // Complete wildcard syntax
    var wildcardSyntax = $"__{_wildcardContext.SelectedCategory}/{collection.Name}__ ";
    var newValue = $"{beforePattern}{wildcardSyntax}{afterPattern}";
    
    Value = newValue;
    await _textFieldRef.SetText(newValue);
    await ValueChanged.InvokeAsync(newValue);
    
    _showSuggestions = false;
    _wildcardContext = null;
    
    // Position cursor after space
    var newCursorPos = beforePattern.Length + wildcardSyntax.Length;
    await SetCursorPosition(newCursorPos);
    
    StateHasChanged();
}
```

#### Non-Prompt Selection Logic

```csharp
private async Task SelectLora(LoraResource lora)
{
    // Add to Parameters.Loras list (via event callback)
    await OnLoraSelected.InvokeAsync(new Lora
    {
        Name = lora.Name,
        Strength = 1.0f,
        IsEnabled = true
    });
    
    // Remove '<' trigger from prompt
    var textBeforeCursor = Value.Substring(0, _cursorPosition);
    var lastLt = textBeforeCursor.LastIndexOf('<');
    
    var beforeTrigger = Value.Substring(0, lastLt);
    var afterCursor = Value.Substring(_cursorPosition);
    
    Value = $"{beforeTrigger}{afterCursor}";
    await _textFieldRef.SetText(Value);
    await ValueChanged.InvokeAsync(Value);
    
    _showSuggestions = false;
    await SetCursorPosition(lastLt);
    StateHasChanged();
}

private async Task SelectStyle(PromptStyle style)
{
    // Add to Parameters.Styles list (via event callback)
    await OnStyleSelected.InvokeAsync(style);
    
    // Remove '@' trigger from prompt
    var textBeforeCursor = Value.Substring(0, _cursorPosition);
    var lastAt = textBeforeCursor.LastIndexOf('@');
    
    var beforeTrigger = Value.Substring(0, lastAt);
    var afterCursor = Value.Substring(_cursorPosition);
    
    Value = $"{beforeTrigger}{afterCursor}";
    await _textFieldRef.SetText(Value);
    await ValueChanged.InvokeAsync(Value);
    
    _showSuggestions = false;
    await SetCursorPosition(lastAt);
    StateHasChanged();
}
```

#### Success Criteria
- [x] Wildcard category ? collection flow seamless
- [x] Wildcard completion includes proper spacing
- [x] LoRA/Style triggers removed from prompt
- [x] LoRA/Style added to respective lists
- [x] Cursor positioning correct for all modes
- [x] No text duplication or loss

#### Changes Made
? **COMPLETED** - All selection methods implemented and tested

**Files Modified:**
- `BlazorWebApp/Components/Shared/Generation/TextFieldAutocomplete.razor`

**New Methods Added (Phase 4: Selection Methods region):**

| Method | Behavior | Result |
|--------|----------|--------|
| `SelectWildcardCategory(string)` | Inserts `__category/`, updates context, auto-triggers collection search | Cursor after `/` |
| `SelectWildcardCollection(WildcardCollection)` | Completes to `__category/collection__ ` | Cursor after space |
| `SelectLoraResource(LocalResource)` | Invokes `OnLoraSelected`, removes `<trigger` from prompt | Cursor at trigger position |
| `SelectStyleItem(PromptStyle)` | Invokes `OnStyleSelected`, removes `@trigger` from prompt | Cursor at trigger position |

**Implementation Details:**
1. **SelectWildcardCategory**: 
   - Inserts `__category/` at trigger position
   - Updates `_wildcardContext` for collection state
   - Auto-triggers `SearchWildcardCollections()`
   - Keeps dropdown open for seamless flow

2. **SelectWildcardCollection**:
   - Strips category prefix from collection name if present
   - Completes syntax with trailing `__ ` (double underscore + space)
   - Closes dropdown and clears context

3. **SelectLoraResource**:
   - Creates `Lora` object with default strength 1.0
   - Invokes callback for parent to handle
   - Removes trigger + typed text from prompt

4. **SelectStyleItem**:
   - Invokes callback with `PromptStyle`
   - Removes trigger + typed text from prompt

**Validation:**
- ? Build successful
- ? Wildcard category selection transitions to collections
- ? Wildcard collection selection completes syntax correctly
- ? LoRA/Style selection removes trigger from prompt
- ? Cursor positioning correct for all modes

---

### Step 6: Add Wildcard Collection Preview Tooltip
**Complexity:** 2 points  
**Status:** [>] Deferred to Phase 9

#### Deferral Note
This step has been deferred to Phase 9 as a nice-to-have enhancement.
Full implementation details preserved in [PHASE_9_DEFERRED_WORK.md](PHASE_9_DEFERRED_WORK.md).

#### Original Tasks (for reference)
- [ ] Add hover state detection for wildcard collections
- [ ] Load collection entries on hover (lazy load)
- [ ] Display tooltip with first 5 entries + weights
- [ ] Show total entry count
- [ ] Style tooltip consistently
- [ ] Test preview performance

---

### Step 7: Extend Parser for Wildcard Expansion
**Complexity:** 2 points  
**Status:** [x] Complete and tested

#### Tasks
- [x] Add ExpandWildcardsAsync() method to Parser.cs
- [x] Add ParseParametersAsync() method with wildcard expansion
- [x] Add DetectWildcards() utility method
- [x] Integrate with WildcardService.ParseWildcards() for weighted selection
- [x] Handle null/empty inputs gracefully
- [x] Preserve existing sync ParseParameters for backward compatibility

#### Implementation Details

**Files Modified:**
- `BlazorWebApp/Extensions/Parser.cs`

**New Methods Added:**

1. **ParseParametersAsync()** - Async version with wildcard expansion:
```csharp
public static async Task<SharedParameters> ParseParametersAsync(
    this SharedParameters param, 
    IEnumerable<PromptStyle> styles,
    IWildcardService? wildcardService = null)
```
- Order of operations:
  1. Expand wildcards (first, uses database)
  2. Apply styles (second, uses template strings)
  3. Parse LoRAs (third, extracts tags)
  4. Generate seed if -1

2. **ExpandWildcardsAsync()** - Delegates to WildcardService:
```csharp
public static async Task<string> ExpandWildcardsAsync(
    string? input, 
    IWildcardService wildcardService)
```
- Uses WildcardService.ParseWildcards() for weighted random selection
- Handles null/empty input gracefully

3. **DetectWildcards()** - Utility for UI to preview wildcards:
```csharp
public static List<string> DetectWildcards(string? input)
```
- Returns list of wildcard names found in input
- Useful for showing what will be expanded

**Design Decisions:**
- Created async version rather than modifying existing sync method for backward compatibility
- Delegated actual expansion to WildcardService which already handles weighted selection
- WildcardService can be null (optional parameter) to support existing callers

#### Success Criteria
- [x] Regex matches wildcard syntax correctly (`__category/collection__`)
- [x] Expands to random weighted entries via WildcardService
- [x] Handles missing collections gracefully (keeps original text)
- [x] Supports both `__category/collection__` and `__collection__` formats
- [x] Backward compatible - existing sync ParseParameters still works
- [x] Build successful

---

### Step 8: Integrate Parser with Generation Flow
**Complexity:** 1 point  
**Status:** [x] Complete and tested

#### Tasks
- [x] Locate prompt processing in ImageService BuildParameters methods
- [x] Add IWildcardService injection to ImageService
- [x] Convert BuildTxt2ImgParameters to async BuildTxt2ImgParametersAsync
- [x] Convert BuildImg2ImgParameters to async BuildImg2ImgParametersAsync
- [x] Call ParseParametersAsync with wildcardService for wildcard expansion
- [x] Ensure both positive and negative prompts expand
- [x] Update tests for new dependency
- [x] Verify no breaking changes

#### Implementation Details

**Files Modified:**
- `BlazorWebApp/Services/ImageService.cs`
- `BlazorWebApp.Tests/Services/ImageServiceTests.cs`

**Changes Made:**
1. **Added IWildcardService injection:**
```csharp
private readonly IWildcardService _wildcardService;

public ImageService(
    // ...existing parameters...
    IWildcardService wildcardService)
{
    // ...existing assignments...
    _wildcardService = wildcardService;
}
```
2. **Converted BuildTxt2ImgParameters to async:**
```csharp
private async Task BuildTxt2ImgParametersAsync(string scriptName)
{
    _parsingParams = await Parser.ParseParametersAsync(
        new SharedParameters(_state.ParametersTxt2Img), 
        _state.State.Generation.Styles,
        _wildcardService);
    // ...rest of method unchanged...
}
```
3. **Converted BuildImg2ImgParameters to async:**
```csharp
private async Task<Img2ImgParameters> BuildImg2ImgParametersAsync(string scriptName)
{
    _parsingParams = await Parser.ParseParametersAsync(
        new SharedParameters(_state.ParametersImg2Img), 
        _state.State.Generation.Styles,
        _wildcardService);
    // ...rest of method unchanged...
}
```
4. **Updated GetImages to use async methods:**
```csharp
case ModeType.Img2Img:
    var img2imgParams = await BuildImg2ImgParametersAsync(scriptName);
    Images = await _router.PostImg2Img(img2imgParams);
    break;
default:
    await BuildTxt2ImgParametersAsync(scriptName);
    Images = await _router.PostTxt2Img(_txt2imgParams);
    break;
```
5. **Updated tests:**
- Added `Mock<IWildcardService> _mockWildcardService` 
- Updated `CreateService()` to pass mock to constructor

#### Success Criteria
- [x] Wildcards expand before styles (order preserved)
- [x] Works for both Txt2Img and Img2Img prompts
- [x] Both positive and negative prompts expanded
- [x] Error handling prevents generation failures (WildcardService handles errors gracefully)
- [x] Backward compatible - existing sync paths unchanged
- [x] Build successful
- [x] Tests pass

---

### Step 9: Update PromptFields for Event Callbacks
**Complexity:** 1 point  
**Status:** [~] In Progress

#### Tasks
- [ ] Add event handlers for OnLoraSelected
- [ ] Add event handlers for OnStyleSelected
- [ ] Wire up callbacks to TextFieldAutocomplete
- [ ] Update Parameters.Loras list on LoRA selection
- [ ] Update Parameters.Styles list on Style selection
- [ ] Test event flow
- [ ] Verify UI updates correctly

#### Event Handler Implementation

```csharp
// In PromptFields.razor

private async Task HandleLoraSelected(Lora lora)
{
    // Add to existing Loras list
    if (Parameters.Loras == null)
        Parameters.Loras = new List<Lora>();
    
    Parameters.Loras.Add(lora);
    await ParametersChanged.InvokeAsync(Parameters);
    StateHasChanged();
}

private async Task HandleStyleSelected(PromptStyle style)
{
    // Add to generation state styles
    var currentStyles = State.State.Generation.Styles?.ToList() ?? new List<PromptStyle>();
    if (!currentStyles.Any(s => s.Name == style.Name))
    {
        currentStyles.Add(style);
        State.State.Generation.Styles = currentStyles;
        await OnStylesChanged.InvokeAsync();
        StateHasChanged();
    }
}
```

#### Success Criteria
- LoRA added to list visible in UI
- Style added to style chips
- No duplicate additions
- UI updates immediately
- Parameters object updated correctly

#### Changes Made
{Update after completion}

---

### Step 10: Comprehensive Testing
**Complexity:** 2 points  
**Status:** [ ] Not Started

#### Tasks
- [ ] Test all trigger modes independently
- [ ] Test wildcard hierarchical navigation
- [ ] Test LoRA and Style triggers
- [ ] Test Tag force trigger (#)
- [ ] Test escape scenarios (\)
- [ ] Test mixed usage (multiple triggers in one prompt)
- [ ] Test edge cases and error conditions
- [ ] Verify no regressions in existing functionality
- [ ] Performance testing with large datasets

#### Test Scenarios

**Wildcard Tests:**
1. `_` ? Shows categories
2. `_clot` ? Filters to clothing, colors
3. Select "clothing" ? `__clothing/` ? Shows collections
4. Select "tops" ? `__clothing/tops__ ` (with space)
5. Delete "tops" ? `__clothing/|__ ` ? Shows collections again
6. `__clothing/bottoms__` ? Expands to random entry

**LoRA Tests:**
1. `<` ? Shows LoRAs
2. `<detail` ? Filters LoRAs
3. Select "detail-tweaker" ? Added to list, `<detail` removed from prompt

**Style Tests:**
1. `@` ? Shows styles
2. `@qual` ? Filters styles
3. Select "quality-boost" ? Added to styles, `@qual` removed from prompt

**Tag Force Tests:**
1. `#_meta` ? Searches tags starting with "_meta"
2. `#<test` ? Searches tags containing "<test"

**Mixed Tests:**
1. `1girl, __clothing/tops__, <detail-tweaker, @quality-boost, masterpiece`
2. Wildcard expands, LoRA added to list, Style added to list, tags remain

**Edge Cases:**
1. Missing wildcard collection ? Keeps `__name__` in prompt
2. Empty collection ? Keeps `__name__` in prompt
3. Multiple wildcards ? All expand correctly
4. Rapid typing ? Debounce works correctly
5. Quick trigger switching ? Mode changes smoothly

#### Success Criteria
- All test scenarios pass
- No errors or crashes
- Performance acceptable (< 100ms per operation)
- Existing tag autocomplete works
- Style expansion works
- User experience feels natural and responsive

#### Changes Made
{Update after completion}

---

## Progress Tracking

| Step | Status | Complexity | Notes |
|------|--------|------------|-------|
| 1. Architecture Design | [x] | 2 pts | ? COMPLETE - Enums, state machine, context classes added |
| 2. Trigger Detection | [x] | 3 pts | ? COMPLETE - DetectAutocompleteMode(), mode routing |
| 3. Search Methods | [x] | 3 pts | ? COMPLETE - All modes, WildcardService, LoRA search |
| 4. Visual UI | [x] | 2 pts | ? COMPLETE - Mode-specific dropdown, icons, colors |
| 5. Selection Logic | [x] | 3 pts | ? COMPLETE - All selection methods, cursor positioning |
| 6. Preview Tooltip | [>] | 2 pts | ?? DEFERRED to Phase 9 - See PHASE_9_DEFERRED_WORK.md |
| 7. Parser Extension | [x] | 2 pts | ? COMPLETE - ParseParametersAsync, ExpandWildcardsAsync |
| 8. Parser Integration | [x] | 1 pt | ? COMPLETE - ImageService uses async methods with wildcards |
| 9. Event Callbacks | [~] | 1 pt | ?? IN PROGRESS - PromptFields updates |
| 10. Comprehensive Testing | [ ] | 2 pts | All scenarios, edge cases |

**Completed:** 16 points / 19 points (84%) - Adjusted for deferred Step 6  
**In Progress:** Step 9 - Event Callbacks

---

## Issues & Resolutions

### Issue 1: {Title}
**Description:** {What happened}  
**Impact:** {Effect on functionality}  
**Resolution:** {How it was fixed}

---

## Commit Checkpoints

- [x] After Step 2 complete (Trigger detection working)
- [x] After Step 5 complete (All selection logic working)
- [x] After Step 8 complete (Parser integration working)
- [ ] After Step 10 complete (All tests passing)

**Latest Checkpoint:**
- ? **Step 5 Complete:** All selection logic implemented and tested
  - Visual UI: Mode-specific dropdowns with icons and colors
  - Selection Methods: 
    - `SelectWildcardCategory()` - inserts `__category/`, auto-shows collections
    - `SelectWildcardCollection()` - completes to `__category/collection__ `
    - `SelectLoraResource()` - adds to LoRA list, removes trigger
    - `SelectStyleItem()` - adds to styles, removes trigger
  - All test scenarios passing:
    - `_` ? categories dropdown ?
    - Category selection ? `__category/` ? collections dropdown ?
    - Collection selection ? `__category/collection__ ` ?
    - `<` ? LoRAs dropdown ?
    - `@` ? Styles dropdown ?
  - Build: ? Successful
  - Ready for Step 6: Preview Tooltip (optional) or Step 7: Parser Extension

---

## Success Criteria

### Wildcard System
- [ ] `_` triggers category search
- [ ] `__category/` triggers collection search
- [ ] Hierarchical navigation works smoothly
- [ ] Mid-wildcard editing supported
- [ ] Wildcards insert with correct syntax
- [ ] Parser expands wildcards during generation
- [ ] Preview tooltip shows collection contents
- [ ] Works in both positive and negative prompts

### Multi-Trigger System
- [ ] `#` forces tag search (bypasses other triggers)
- [ ] `<` triggers LoRA autocomplete
- [ ] `@` triggers Style autocomplete
- [ ] LoRAs added to Parameters.Loras list
- [ ] Styles added to Parameters.Styles list
- [ ] Trigger characters removed from prompt after selection
- [ ] Visual distinction clear between all modes

### General
- [ ] No breaking changes to existing tag autocomplete
- [ ] No breaking changes to style expansion
- [ ] Performance acceptable (< 100ms total)
- [ ] Error handling prevents crashes
- [ ] All edge cases handled gracefully
- [ ] Comprehensive tests pass

---

## Phase Summary

**Status:** In Progress [~] - 52% Complete (11/21 points)

### Accomplishments
? **Step 1 Complete:** Multi-Trigger Architecture Design
- AutocompleteMode enum (6 modes)
- WildcardState enum (3 states)
- WildcardContext class for state tracking
- Event callback parameters (OnLoraSelected, OnStyleSelected)
- 6 new state variables

? **Step 2 Complete:** Trigger Detection System
- DetectAutocompleteMode() with priority order
- Regex patterns for wildcard detection
- GetSearchQueryForMode() for trigger text extraction
- ClearAllSuggestions() and HasAnySuggestions() helpers

? **Step 3 Complete:** Search Methods for Each Mode
- IWildcardService extended with 3 new methods
- SearchWildcardCategories(), SearchWildcardCollections()
- SearchLoras() - database LORA/LoCon resources
- SearchStyles() - prompts converted to PromptStyle

? **Step 4 Complete:** Visual Differentiation UI
- Mode-specific dropdown sections
- Icons: Folder, CollectionsBookmark, Layers, Style
- Colors: Tertiary (wildcards), Success (LoRA), Primary (Style)
- Entry count badges for collections

? **Step 5 Complete:** Selection Logic for All Modes
- SelectWildcardCategory() - hierarchical navigation
- SelectWildcardCollection() - completes wildcard syntax
- SelectLoraResource() - adds to list via callback
- SelectStyleItem() - adds to list via callback

? **Step 7 Complete:** Parser Extension for Wildcards
- ParseParametersAsync() - async version with wildcard expansion
- ExpandWildcardsAsync() - delegates to WildcardService
- DetectWildcards() - utility for previewing wildcards

? **Step 8 Complete:** Parser Integration with Generation Flow
- Txt2Img and Img2Img parameter building updated for wildcards
- Async conversion for parameter methods
- Dependency injection for IWildcardService

### Metrics
- **Files Modified:** 3 (TextFieldAutocomplete.razor, IWildcardService.cs, WildcardService.cs)
- **Lines of Code Added:** ~400 lines
- **Methods Added:** 10+ new methods
- **Build Status:** ? Successful, no breaking changes

### Remaining Work
- Steps 6: Preview Tooltip (optional, can defer)
- Steps 9-10: PromptFields updates and comprehensive testing
- Estimated remaining: 3 points

### Deferred Items
- Expansion preview UI (may move to Phase 9)
- Advanced LoRA weight slider (Phase 5+)
- Style preview before applying (Phase 9)

---

**Phase Status:** In Progress [~] - 52% Complete

---

## Related Documentation

- [MAIN_PLAN.md](MAIN_PLAN.md) - Overall project plan
- [PHASE_2.md](PHASE_2.md) - Backend implementation (COMPLETED)
- [PHASE_3.md](PHASE_3.md) - Wildcards UI (COMPLETED)
- [IMPLEMENTATION_GUIDE.md](../IMPLEMENTATION_GUIDE.md) - General guidelines

---

## Notes for Implementation

### Multi-Trigger Architecture

**Trigger Priority (highest to lowest):**
1. Wildcard (`_`) - Most complex, needs state machine
2. LoRA (`<`) - Non-prompt, adds to list
3. Style (`@`) - Non-prompt, adds to list
4. Tag Force (`#`) - Prompt insertion
5. Default (none) - Existing tag/dictionary behavior

**Special Characters:**
- `\` - Reserved for backend escaping (not a trigger)
- Space, comma, newline - Word boundaries

### Wildcard State Machine

**States:**
- `SearchingCategories` - After `_` or `_c`
- `SearchingCollections` - After `__category/` or `__category/to`
- `Complete` - After `__category/collection__ `

**Transitions:**
- Category selection ? `SearchingCollections` (auto-trigger)
- Collection selection ? `Complete` (close dropdown)
- Delete in complete ? `SearchingCollections` (reopen dropdown)

### Parser Integration Strategy

**Order of Operations:**
1. Wildcard expansion (first, uses database)
2. Style expansion (second, uses template strings)
3. LoRA parsing (third, extracts tags)
4. Seed generation (fourth, if -1)

**Why this order:**
- Wildcards can contain style references
- Styles can contain LoRA tags
- LoRAs are final prompt addition

### Performance Considerations

**Critical Paths:**
- Trigger detection: < 10ms
- Wildcard category search: < 50ms
- Wildcard collection search: < 50ms
- LoRA search: < 30ms (file system)
- Style search: < 30ms (database)
- Parser expansion: < 50ms per 10 wildcards

**Optimization Strategies:**
- Cache category list (rarely changes)
- Lazy load collection entries for preview
- Debounce search (100ms, existing)
- Limit suggestions (MaxSuggestions parameter)

### Error Handling

**Scenarios:**
- Missing wildcard collection ? Keep original `__name__`
- Empty collection ? Keep original `__name__`
- Database error ? Log, gracefully degrade to tags-only
- LoRA file not found ? Show in dropdown but warn
- Style not found ? Skip, don't crash

### Testing Strategy

**Unit Tests:**
- [ ] Parser.ExpandWildcards with valid wildcards
- [ ] Parser.ExpandWildcards with missing wildcards
- [ ] Trigger detection for each mode
- [ ] Selection logic for each mode

**Integration Tests:**
- [ ] Complete wildcard workflow
- [ ] LoRA/Style addition to lists
- [ ] Mixed trigger usage
- [ ] Error recovery

**Manual Testing:**
- [ ] UX validation (feels natural)
- [ ] Performance with large datasets
- [ ] Visual feedback clarity
- [ ] Accessibility (keyboard navigation)

---

## Scope Change Summary

**Original Scope (8 points):**
- Wildcard trigger detection
- Wildcard autocomplete UI
- Parser expansion
- Basic testing

**Expanded Scope (21 points):**
- ? Multi-trigger architecture (4 triggers + default)
- ? Hierarchical wildcard navigation (categories ? collections)
- ? LoRA trigger (add to list, don't insert)
- ? Style trigger (add to list, don't insert)
- ? Tag force trigger (escape mechanism)
- ? Preview tooltip for wildcards
- ? Comprehensive testing suite

**Justification:**
- Foundational architecture for future phases
- All triggers share similar code patterns
- Building together is more efficient than piecemeal
- User experience is cohesive and complete

**Impact on Timeline:**
- Complexity increased from 8 to 21 points (2.6x)
- Still achievable in single phase
- Reduces future phases (no LoRA/Style trigger work in Phase 5)
- Better ROI for implementation effort
