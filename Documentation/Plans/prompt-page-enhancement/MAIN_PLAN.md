# Prompt Page Enhancement - Implementation Plan

## Status
**Current Phase:** Phase 2 - Backend Wildcard System  
**Status:** ✅ **COMPLETED** (All 8 steps completed, 38 unit tests passing)  

### Recently Completed
- ✅ Database entities and migrations
- ✅ Service layer implementation
- ✅ Wildcard parsing and business logic
- ✅ Sample data seeding (14 collections, 70+ entries)
- ✅ Comprehensive unit tests (38 tests, 100% pass rate)

### Next Up
- Phase 3: UI Components (WildcardManager, collection browser, editor)

---

## Implementation Guidelines

**Follow these conventions throughout execution:**

### Execution Workflow (per step)
1. **Initial Code Writing** ? 2. **Test and Debug Features** ? 3. **Discuss Improvements** ? 4. **Update Phase Document**
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

---

## Problem Statement

The current Prompts page has several limitations that hinder workflow efficiency:

1. **Prompt Styles UI Issues**
   - ResourceCard component is designed for image-heavy resources, not text management
   - Poor information density - can't scan many styles quickly
   - No preview of actual prompt content without opening dialog
   - Editing workflow requires multiple clicks

2. **Broken Wildcards System**
   - Path references deprecated WebUI backend folder structure
   - No dynamic loading in prompt fields
   - File-based storage creates management challenges
   - No sample wildcards for new users

3. **Limited LLM Integration**
   - Ollama features only accessible via TagDrawer
   - No image-to-prompt capability
   - Missing batch processing for multiple styles
   - No negative prompt generation helpers

4. **Underutilized Tag System**
   - Danbooru tags work well in autocomplete but lack visual tools
   - No organized tag browser by category
   - Weight management is text-based only
   - No presets for common tag combinations

---

## Proposed Solution

Transform the Prompts page into a comprehensive prompt engineering workbench with four specialized tabs, each optimized for its specific workflow.

### Architecture Overview

```
???????????????????????????????????????????????????????????????
?  Prompts Page                                                ?
?  Tabs: [Styles] [Wildcards] [LLM Tools] [Tag Builder]      ?
???????????????????????????????????????????????????????????????
?                                                              ?
?  Active Tab Content (full-width, focused experience)        ?
?                                                              ?
?  Each tab provides:                                          ?
?  - Specialized UI for its domain                            ?
?  - Preview/testing capabilities                             ?
?  - Integration with generation workflow                     ?
?                                                              ?
???????????????????????????????????????????????????????????????
```

### Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Tabbed layout | Each feature needs substantial space; reduces clutter; allows focused workflows |
| Database storage for wildcards | Enables sharing, versioning, searching; easier backup than file system |
| Dedicated LLM Tools tab | Consolidates AI features; room for advanced UI and batch operations |
| Enhanced tag system | Leverage existing autocomplete; add visual tools without breaking current functionality |
| Preview/testing area | Users need to validate prompts before sending to generation |

### Development Conventions

- **Database-First**: Store all user content (styles, wildcards, presets) in database
- **Component Reusability**: Build components usable in both TagDrawer and Prompts page
- **Real-time Updates**: Changes reflect immediately across all UI elements
- **Non-Destructive Workflow**: Always preview before applying to main prompt fields
- **Keyboard Shortcuts**: Support keyboard navigation for power users
- **Responsive Design**: All panels adapt to different screen sizes

---

## Implementation Phases

### Phase 1: Styles Tab Redesign
**Objective:** Replace ResourceCard UI with information-dense table/list optimized for text management  
**Complexity:** 19 points (was 8, added categorization, favorites, search)  
**Status:** [ ] Not Started

#### Steps
1. Create `PromptStyleCard` component with compact text-focused design
2. Implement table view with columns: Title, Preview (Positive), Preview (Negative), Actions
3. Add inline editing capabilities (click-to-edit fields)
4. Create improved `PromptStyleEditor` dialog
5. **NEW:** Add category/folder system (similar to Wildcards structure)
6. **NEW:** Add tags system (List<string> property) with LLM suggestion button
7. Implement search and filter by prompt content + semantic search (keyword expansion)
8. Add sorting (by title, creation date, usage count, category)
9. **NEW:** Add favorites/pinning system with quick access bar
10. **NEW:** Implement keyboard shortcuts (Ctrl+1-9 for pinned items)
11. Add bulk operations (select multiple, export, delete)
12. Add quick preview tooltip showing full prompt text

#### Success Criteria
- View 15+ styles without scrolling (vs. current 6-8)
- Edit workflow reduced from 4 clicks to 2
- Search finds styles by content (semantic keyword expansion)
- Category/folder organization is intuitive
- Pinned styles accessible via hotkeys
- Tags support with LLM auto-suggestion
- No breaking changes to existing database schema
- Performance with 100+ styles remains smooth

#### Database Schema Extensions

```csharp
// Extend existing Prompt entity
public class Prompt
{
    // ...existing properties...
    
    // NEW: Organization
    public string? Category { get; set; }  // e.g., "Characters", "Landscapes"
    public List<string>? Tags { get; set; }  // User-defined tags for filtering
    
    // NEW: Favorites & Usage
    public bool IsPinned { get; set; }
    public bool IsFavorite { get; set; }
    public int SortOrder { get; set; }  // For manual pinned ordering
    public DateTime? LastUsedAt { get; set; }
    public int UsageCount { get; set; }
}
```

#### Files Affected
- `BlazorWebApp/Components/Prompts/PromptsPanel.razor` (major refactor)
- `BlazorWebApp/Components/Prompts/PromptDialog.razor` (enhance)
- New: `BlazorWebApp/Components/Prompts/Styles/PromptStyleCard.razor`
- New: `BlazorWebApp/Components/Prompts/Styles/PromptStyleTable.razor`
- New: `BlazorWebApp/Components/Prompts/Styles/CategoryBrowser.razor`
- Extend: `BlazorWebApp/Services/DatabaseService.cs` (add category/tags support)
- Extend: `BlazorWebApp/Services/OllamaService.cs` (category/tag suggestions)

---

### Phase 2: Wildcards Database Foundation
**Objective:** Create database schema and service layer for wildcards storage  
**Complexity:** 13 points  
**Status:** ✅ **COMPLETED** (All 8 steps completed, 38 unit tests passing)

#### Steps
1. Design database schema (WildcardCollection, WildcardEntry entities)
2. Create entity classes and DTOs
3. Add database migration for new tables
4. Implement CRUD methods in DatabaseService
5. Create WildcardService for business logic (random selection, parsing)
6. Add indexing for performance
7. Create seed data with sample wildcards (outfits, locations, styles, etc.)
8. Write unit tests for wildcard service

#### Success Criteria
- Schema supports hierarchical organization (categories, collections)
- Efficient storage for 1000+ entries
- Random selection performance < 10ms
- Sample wildcards cover 10+ common categories
- Migration runs successfully without data loss

#### Database Schema Design

```csharp
public class WildcardCollection
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int UsageCount { get; set; }
    public ICollection<WildcardEntry> Entries { get; set; }
}

public class WildcardEntry
{
    public int Id { get; set; }
    public int CollectionId { get; set; }
    public string Value { get; set; }
    public float Weight { get; set; } = 1.0f;
    public int SortOrder { get; set; }
    public WildcardCollection Collection { get; set; }
}
```

#### Sample Collections
- clothing/tops, clothing/bottoms, clothing/full-outfits
- locations/indoor, locations/outdoor, locations/fantasy
- styles/art-medium, styles/lighting, styles/mood
- characters/hair-color, characters/hair-style, characters/eye-color
- actions/poses, actions/expressions

#### Files Affected
- `BlazorWebApp/Data/Entities/WildcardCollection.cs` (new)
- `BlazorWebApp/Data/Entities/WildcardEntry.cs` (new)
- `BlazorWebApp/Data/Dtos/WildcardDto.cs` (new)
- `BlazorWebApp/Services/DatabaseService.cs` (extend)
- `BlazorWebApp/Services/WildcardService.cs` (new)
- `BlazorWebApp/Data/Migrations/` (new migration)

---

### Phase 3: Wildcards Tab UI
**Objective:** Build comprehensive wildcards management interface  
**Complexity:** 13 points  
**Status:** [ ] Not Started

#### Steps
1. Create `WildcardsTab` component with split-pane layout
2. Implement collection browser (left pane) with category grouping
3. Implement entry list/editor (right pane)
4. Add collection CRUD operations (create, rename, delete, duplicate)
5. Add entry CRUD operations (add, edit, delete, reorder)
6. Create wildcard syntax preview panel
7. Implement import from file system (.txt files)
8. Implement export to various formats (JSON, TXT)
9. Add search across all collections
10. Add drag-and-drop for reordering entries

#### Success Criteria
- Intuitive two-pane layout similar to file explorer
- Can create and edit collections without confusion
- Import preserves existing wildcards from file system
- Export compatible with A1111/ComfyUI wildcard formats
- Drag-and-drop works smoothly for reordering

#### UI Layout Design

```
??????????????????????????????????????????????????????
? Collections ? Entries & Editor                     ?
?             ?                                       ?
? ? Clothing  ? Collection: Tops                     ?
?   - Tops    ? ??????????????????????????????????? ?
?   - Bottoms ? ? [+ Add Entry]  [Import] [Export]? ?
?   - Shoes   ? ??????????????????????????????????? ?
?             ?                                       ?
? ? Locations ? Entries:                             ?
?   - Indoor  ? 1. ? white t-shirt                   ?
?   - Outdoor ? 2. ? black hoodie                    ?
?             ? 3. ? red dress shirt                 ?
? ? Styles    ?                                       ?
?   - Art     ? Preview: __clothing/tops__           ?
?   - Mood    ?                                       ?
??????????????????????????????????????????????????????
```

#### Files Affected
- `BlazorWebApp/Components/Prompts/WildcardsPanel.razor` (major refactor)
- New: `BlazorWebApp/Components/Prompts/Wildcards/WildcardsTab.razor`
- New: `BlazorWebApp/Components/Prompts/Wildcards/CollectionBrowser.razor`
- New: `BlazorWebApp/Components/Prompts/Wildcards/EntryEditor.razor`
- New: `BlazorWebApp/Components/Prompts/Wildcards/WildcardPreview.razor`

---

### Phase 4: Dynamic Wildcard Integration
**Objective:** Integrate wildcards into TextFieldAutocomplete for real-time suggestions  
**Complexity:** 8 points  
**Status:** [ ] Not Started

#### Steps
1. Modify TextFieldAutocomplete to detect `__` trigger for wildcard autocomplete
2. Implement wildcard collection suggestions (show matching collections)
3. Add visual differentiation (icon/color for wildcards vs. tags)
4. Create hover preview showing collection entries
5. Update Parser.cs to handle wildcard expansion
6. Implement random selection from collection during parsing
7. Add expansion preview in prompt field (show what was selected)
8. Test integration with PromptFields component
9. Add settings for wildcard behavior (random seed, preview mode)

#### Wildcard Syntax

```
Basic:      __collection_name__
Nested:     __category/collection__
Example:    a woman wearing __clothing/tops__ and __clothing/bottoms__

Parsing Flow:
Input:  "a woman wearing __clothing/tops__"
Parse:  Lookup "clothing/tops" collection
Select: Random entry with weight consideration
Output: "a woman wearing white t-shirt"
```

#### Success Criteria
- `__` triggers wildcard autocomplete (similar to tag autocomplete)
- Visual distinction clear between wildcards and tags
- Preview tooltip shows collection contents before selection
- Parser expands wildcards during generation
- Works in both positive and negative prompts
- No breaking changes to existing tag autocomplete

#### Files Affected
- `BlazorWebApp/Components/Shared/Generation/TextFieldAutocomplete.razor` (extend)
- `BlazorWebApp/Extensions/Parser.cs` (extend ParseStyles method)
- `BlazorWebApp/Services/WildcardService.cs` (add parsing logic)

---

### Phase 5: LLM Tools Tab - Core Features
**Objective:** Consolidate and expand LLM-powered prompt engineering tools  
**Complexity:** 8 points  
**Status:** [ ] Not Started

#### Steps
1. Create `LLMToolsTab` component
2. Refactor LLMPromptEnhancerForm into reusable base component
3. **NEW:** Add dual functionality: Enhance AND Simplify buttons
4. **NEW:** Add collapsible "Advanced" panel with exposed system prompts
5. **NEW:** Allow users to edit and save custom system prompt templates
6. Add side-by-side comparison panel (original vs. enhanced/simplified)
7. Implement prompt expansion presets dropdown
8. Create negative prompt generator (analyze positive prompt, suggest negatives)
9. Add prompt analyzer (detect style, complexity, token count estimate)
10. Implement batch enhancement (process multiple styles at once)
11. Add generation history with undo/redo functionality
12. Add "apply to style" feature (enhance and save as new style)

#### System Prompts Architecture

```csharp
public class LLMSystemPrompts
{
    // Enhancement (existing)
    public string EnhancePrompt { get; set; } = 
        "Expand this simple prompt into a detailed, descriptive image generation prompt: \"{prompt}\". " +
        "Add artistic details, lighting, mood, and composition elements. Keep it concise.";
    
    // Simplification (NEW)
    public string SimplifyPrompt { get; set; } = 
        "Take this complex prompt and distill it to its essential elements: \"{prompt}\". " +
        "Keep only the most important descriptors. Remove redundancy and unnecessary detail.";
    
    // Negative (existing)
    public string NegativePrompt { get; set; } = 
        "Expand this negative prompt with detailed descriptions of what to avoid: \"{prompt}\". " +
        "Add specific undesired elements, artifacts, and quality issues.";
    
    // Custom templates (NEW)
    public List<CustomSystemPrompt> UserTemplates { get; set; }
}

public class CustomSystemPrompt
{
    public string Name { get; set; }
    public string Template { get; set; }
    public string Description { get; set; }
}
```

#### Preset Templates

```csharp
public enum PromptExpansionPreset
{
    ShortToDetailed,      // "woman" -> "beautiful woman with long flowing hair..."
    TagsToNarrative,      // "1girl, red dress" -> "A young woman wearing an elegant red dress..."
    MinimalToAtmospheric, // "forest" -> "A mystical ancient forest with rays of light..."
    BoostQuality,         // Add quality/detail boosters
    AddLighting,          // Add lighting descriptions
    AddMood              // Add emotional/atmospheric elements
}
```

#### Success Criteria
- All core LLM features accessible in one location
- Enhance/Simplify dual functionality works seamlessly
- System prompts exposed and editable
- Custom templates can be saved and reused
- Comparison view makes it easy to evaluate results
- Presets accelerate common workflows
- Batch processing works for 10+ styles
- History allows experimentation without fear
- Generated results maintain coherence and quality

#### Files Affected
- New: `BlazorWebApp/Components/Prompts/LLM/LLMToolsTab.razor`
- `BlazorWebApp/Components/Shared/Generation/LLMPromptEnhancerForm.razor` (refactor to base)
- New: `BlazorWebApp/Components/Prompts/LLM/PromptComparison.razor`
- New: `BlazorWebApp/Components/Prompts/LLM/PromptAnalyzer.razor`
- New: `BlazorWebApp/Components/Prompts/LLM/BatchProcessor.razor`
- New: `BlazorWebApp/Data/Entities/CustomSystemPrompt.cs`
- `BlazorWebApp/Services/OllamaService.cs` (extend)
- `BlazorWebApp/Services/DatabaseService.cs` (store custom templates)

---

### Phase 5.5: LLM Creative Tools
**Objective:** Add advanced LLM-powered creative prompt generation features  
**Complexity:** 13 points (NEW PHASE)  
**Status:** [ ] Not Started

#### Overview
Expand LLM Tools tab with creative sub-tabs for exploration and experimentation. Each sub-tab provides a focused interface for a specific creative workflow.

#### Sub-Tab Structure

```
LLM Tools Tab:
?? Enhancer (Phase 5 - main feature)
?? Mixer (NEW)
?? Templates (NEW)
?? Scene Builder (NEW)
?? Remixer (NEW)
?? Daily Challenge (NEW)
?? Roulette (NEW - fun mode)
```

#### Steps

1. **Prompt Mixer Sub-tab** (Complexity: 2)
   - Two input fields + blend ratio slider
   - System prompt: "Combine these prompts: [A] and [B]. Blend ratio: [ratio]%"
   - Save mixed result to history

2. **Random Generator Sub-tab** (Complexity: 2)
   - Genre, Mood, Complexity filters
   - "Inspire Me" button
   - System prompt: "Generate creative prompt with: Genre={genre}, Mood={mood}, Complexity={level}"

3. **Templates Sub-tab (Mad Libs Style)** (Complexity: 3)
   - Create/edit templates with variables: "A [adjective] [character] in [location]"
   - Dropdown/wildcard options for each variable
   - "Fill All Random" button
   - Save filled template as new style

4. **Scene Builder Sub-tab** (Complexity: 3)
   - Structured form: Subject, Environment, Lighting, Mood, Style
   - Assembly logic combines sections into coherent prompt
   - Preview assembled prompt before saving

5. **Remixer Sub-tab** (Complexity: 2)
   - Select multiple styles to remix
   - Methods: Shuffle elements, Combine parts, LLM intelligent mix
   - Generate creative combinations

6. **Daily Challenge Sub-tab** (Complexity: 3)
   - Display daily creative challenge
   - Prompt building area to experiment
   - Save successful attempts
   - Leaderboard/history of past challenges
   - Challenge generation via LLM or predefined list

7. **Roulette Sub-tab (Fun Mode)** (Complexity: 1)
   - Spin-the-wheel UI
   - Randomly combine: Subject + Style + Lighting + Mood + Location
   - One-click generation from random combo

#### Database Extensions

```csharp
// Store templates
public class PromptTemplate
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Template { get; set; }  // "A [adjective] [character]..."
    public List<string> Variables { get; set; }  // ["adjective", "character"...]
    public Dictionary<string, List<string>> VariableOptions { get; set; }
}

// Daily challenges
public class DailyChallenge
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string ChallengeText { get; set; }
    public string? Hints { get; set; }
    public List<string> UserAttempts { get; set; }
}
```

#### Success Criteria
- Each sub-tab provides intuitive workflow
- LLM responses are creative and useful
- Templates system is flexible and reusable
- Daily Challenge provides fresh inspiration
- Scene Builder assembles coherent prompts
- Roulette mode is fun and generates valid prompts
- All features integrate with history and saving

#### Files Affected
- New: `BlazorWebApp/Components/Prompts/LLM/PromptMixer.razor`
- New: `BlazorWebApp/Components/Prompts/LLM/RandomGenerator.razor`
- New: `BlazorWebApp/Components/Prompts/LLM/TemplateBuilder.razor`
- New: `BlazorWebApp/Components/Prompts/LLM/SceneBuilder.razor`
- New: `BlazorWebApp/Components/Prompts/LLM/PromptRemixer.razor`
- New: `BlazorWebApp/Components/Prompts/LLM/DailyChallenge.razor`
- New: `BlazorWebApp/Components/Prompts/LLM/PromptRoulette.razor`
- New: `BlazorWebApp/Data/Entities/PromptTemplate.cs`
- New: `BlazorWebApp/Data/Entities/DailyChallenge.cs`
- Extend: `BlazorWebApp/Services/DatabaseService.cs`
- Extend: `BlazorWebApp/Services/OllamaService.cs`

---

### Phase 6: Vision-Language Model Integration
**Objective:** Add image-to-prompt generation using VL models via Ollama  
**Complexity:** 13 points  
**Status:** [ ] Not Started

#### Steps
1. Research Ollama VL model support (LLaVA, MiniCPM-V, others)
2. Extend OllamaService to handle image inputs (base64 encoding)
3. Create VLModelService for image analysis
4. Create image upload component with drag-and-drop
5. Implement image-to-prompt API integration
6. Add prompt refinement controls (detail level, focus areas, style)
7. Create image-prompt pair gallery for comparison
8. Add integration with Gallery page (send image to analysis)
9. Implement batch processing for multiple images
10. Add prompt templates for VL models (interrogation styles)

#### VL Model Interrogation Styles

```
Detailed:     Full description of everything visible
Focus:        Describe specific subject/object
Artistic:     Emphasize artistic style and techniques
Technical:    Camera settings, lighting, composition
Tags:         Generate Danbooru-style tags
Simple:       Brief, essential description only
```

#### Success Criteria
- Can upload image and receive useful prompt
- Multiple VL models available for comparison
- Refinement options improve prompt quality
- Batch processing works for gallery selection
- Integration with Gallery page works seamlessly
- Generated prompts suitable for txt2img generation

#### Files Affected
- `BlazorWebApp/Services/OllamaService.cs` (extend for image support)
- New: `BlazorWebApp/Services/VLModelService.cs`
- New: `BlazorWebApp/Components/Prompts/LLM/ImageToPromptForm.razor`
- New: `BlazorWebApp/Components/Prompts/LLM/ImagePromptGallery.razor`
- New: `BlazorWebApp/Data/Dtos/Ollama/VLPromptRequest.cs`

---

### Phase 7: Tag Builder Tab
**Objective:** Create visual interface for Danbooru tag-based prompt building  
**Complexity:** 8 points  
**Status:** [ ] Not Started

#### Steps
1. Create `TagBuilderTab` component with category navigation
2. Implement category browser (character, clothing, pose, background, etc.)
3. Create tag selection panel with visual cards/chips
4. Implement visual weight editor with sliders
5. Add tag combination preview (show final prompt)
6. Create tag combination presets (character archetypes, scenes)
7. Add tag conflict detection (warn about contradictory tags)
8. Implement tag recommendation engine (suggest related tags)
9. Add "export to prompt" functionality
10. Create save/load system for tag combinations

#### Category Organization

```
Categories:
??? Character
?   ??? Count (1girl, 2girls, multiple_girls)
?   ??? Age (child, teenager, adult, mature)
?   ??? Hair (color, length, style)
?   ??? Eyes (color, shape)
?   ??? Body (body_type, skin_tone)
??? Clothing
?   ??? Upper (shirt, dress, jacket)
?   ??? Lower (pants, skirt, shorts)
?   ??? Full (dress, bodysuit)
?   ??? Accessories (hat, glasses, jewelry)
??? Pose & Action
?   ??? Pose (standing, sitting, lying)
?   ??? Expression (smile, sad, angry)
?   ??? Action (walking, running, dancing)
??? Background
?   ??? Location (indoor, outdoor, fantasy)
?   ??? Time (day, night, sunset)
?   ??? Weather (sunny, rainy, cloudy)
??? Style & Quality
    ??? Art Style (anime, realistic, painting)
    ??? Quality (masterpiece, best quality)
    ??? Composition (closeup, portrait, landscape)
```

#### Success Criteria
- Can build complex prompt without typing
- Category organization is intuitive
- Weight management is visual and easy
- Presets provide starting points for common scenes
- Conflict detection prevents common mistakes
- Recommendations accelerate tag discovery
- Export integrates with generation workflow

#### Files Affected
- New: `BlazorWebApp/Components/Prompts/Tags/TagBuilderTab.razor`
- New: `BlazorWebApp/Components/Prompts/Tags/CategoryBrowser.razor`
- New: `BlazorWebApp/Components/Prompts/Tags/TagWeightEditor.razor`
- New: `BlazorWebApp/Components/Prompts/Tags/TagPresetManager.razor`
- New: `BlazorWebApp/Services/TagBuilderService.cs`
- Extend: `BlazorWebApp/Services/CsvService.cs` (add category methods)

---

### Phase 8: Tag System Enhancements
**Objective:** Enhance existing tag autocomplete with visual features  
**Complexity:** 5 points  
**Status:** [ ] Not Started

#### Steps
1. Add category badges in autocomplete dropdown
2. Add tag usage statistics (show how often used)
3. Create tag favorites/pinning system
4. Implement tag aliasing improvements
5. Add tag combination suggestions (frequently used together)
6. Improve fuzzy search algorithm
7. Add tag preview on hover (show related tags, examples)
8. Implement recent tags quick access

#### Success Criteria
- Autocomplete is more informative
- Favorites speed up frequent tag access
- Usage stats help find popular tags
- Search finds tags more reliably
- No breaking changes to existing functionality
- Performance remains fast

#### Files Affected
- `BlazorWebApp/Components/Shared/Generation/TextFieldAutocomplete.razor` (enhance)
- `BlazorWebApp/Services/CsvService.cs` (extend)
- New: `BlazorWebApp/Services/TagFavoritesService.cs`

---

### Phase 9: Prompt Testing & Preview
**Objective:** Add testing area to preview prompt results before generation  
**Complexity:** 5 points  
**Status:** [ ] Not Started

#### Steps
1. Create `PromptPreview` component
2. Add preview pane showing final parsed prompt
3. Implement style/wildcard/tag expansion visualization
4. Add token counter (estimate for SD models)
5. Create "Send to Generation" quick actions (Txt2Img, Img2Img)
6. Add comparison mode (compare multiple variations side-by-side)
7. Implement prompt validation (warn about common issues)
8. Add prompt template system

#### Preview Features

```
Input:  "a woman wearing __clothing/tops__, masterpiece"
Styles: [SDXL Base], [Quality Boost]

Preview Output:
???????????????????????????????????????????????????????
? Final Prompt (with expansions):                     ?
?                                                      ?
? a woman wearing white t-shirt, masterpiece,         ?
? best quality, highly detailed, 8k uhd              ?
?                                                      ?
? Wildcards Expanded:                                 ?
? • __clothing/tops__ ? "white t-shirt"              ?
?                                                      ?
? Styles Applied:                                     ?
? • SDXL Base ? (base style text)                    ?
? • Quality Boost ? "best quality, highly detailed..."?
?                                                      ?
? Token Count: ~28 tokens                            ?
? Validation: ? No issues detected                   ?
???????????????????????????????????????????????????????
```

#### Success Criteria
- Preview shows exactly what will be sent to model
- Token counter helps optimize prompts
- Validation catches common mistakes
- Easy to test multiple variations
- Integration with generation pages works
- Preview updates in real-time

#### Files Affected
- New: `BlazorWebApp/Components/Prompts/Shared/PromptPreview.razor`
- New: `BlazorWebApp/Services/PromptValidationService.cs`
- New: `BlazorWebApp/Services/TokenCounterService.cs`

---

### Phase 10: Import/Export & Backup
**Objective:** Enable data portability and backup of prompt resources  
**Complexity:** 8 points (was 3, added external imports)  
**Status:** [ ] Not Started

#### Steps
1. Implement export all styles to JSON
2. Implement import styles from JSON
3. Add wildcard collection export (JSON format)
4. Add wildcard collection import
5. Create full backup functionality (all prompt data)
6. Create restore from backup functionality
7. Add export to A1111/ComfyUI formats
8. Add sharing functionality (generate JSON for sharing)
9. **NEW:** Implement Civitai import (from model ID or image metadata)
10. **NEW:** Implement Lexica import (search and import popular prompts)
11. **NEW:** Add paste text import (parse various formats)

#### Export Formats

```json
// Styles Export
{
  "version": "1.0",
  "styles": [
    {
      "name": "SDXL Base",
      "positive": "masterpiece, best quality",
      "negative": "low quality, bad anatomy",
      "loras": [],
      "category": "Quality",
      "tags": ["sdxl", "base", "quality"]
    }
  ]
}

// Wildcards Export
{
  "version": "1.0",
  "collections": [
    {
      "name": "Tops",
      "category": "Clothing",
      "entries": ["white t-shirt", "black hoodie"]
    }
  ]
}
```

#### External Import Service

```csharp
public class ExternalImportService
{
    public async Task<List<Prompt>> ImportFromCivitai(int modelId)
    {
        // Fetch model data from Civitai API
        // Extract prompts from example images
        // Return parsed prompts
    }
    
    public async Task<List<Prompt>> ImportFromLexica(string searchQuery, int limit = 10)
    {
        // Lexica API search
        // Return popular prompts
    }
    
    public async Task<Prompt> ImportFromText(string text)
    {
        // Parse pasted text (A1111, ComfyUI, plain text)
        // Return parsed prompt
    }
}
```

#### Success Criteria
- Can backup all prompt data to single file
- Can restore from backup without data loss
- Export format is human-readable (JSON)
- Import validates data before applying
- Sharing generates portable JSON
- Compatible with other tools where possible
- Civitai import extracts prompts from models
- Lexica search finds relevant community prompts
- Paste import handles multiple formats

#### Files Affected
- New: `BlazorWebApp/Services/ImportExportService.cs`
- New: `BlazorWebApp/Services/ExternalImportService.cs`
- Extend: `BlazorWebApp/Services/DatabaseService.cs`
- Extend: `BlazorWebApp/Services/CivitaiService.cs` (if not already sufficient)
- New: `BlazorWebApp/Components/Prompts/Shared/ImportExportDialog.razor`
- New: `BlazorWebApp/Components/Prompts/Shared/ExternalImportDialog.razor`

---

### Phase 11: Performance & Polish
**Objective:** Optimize performance and add finishing touches  
**Complexity:** 5 points  
**Status:** [ ] Not Started

#### Steps
1. Implement virtualization for large lists (styles, wildcards)
2. Add loading states and skeleton screens
3. Optimize database queries (add indexes, caching)
4. Implement keyboard shortcuts
5. Create keyboard shortcuts documentation
6. Add user preferences (default tab, layout options)
7. Add comprehensive tooltips and help text
8. Polish animations and transitions
9. Add responsive breakpoints for mobile
10. Conduct performance testing with large datasets

#### Keyboard Shortcuts

```
Global:
Ctrl+1-4    : Switch between tabs
Ctrl+N      : New (style/wildcard/etc depending on tab)
Ctrl+S      : Save current item
Ctrl+F      : Focus search
Escape      : Close dialogs

Styles Tab:
Ctrl+E      : Edit selected style
Ctrl+D      : Duplicate selected style
Delete      : Delete selected style

Wildcards Tab:
Ctrl+I      : Import wildcards
Ctrl+E      : Export collection
```

#### Success Criteria
- Smooth scrolling with 1000+ items
- Loading states prevent confusion
- Database queries < 100ms for common operations
- Keyboard shortcuts are discoverable
- UI feels polished and professional
- Works reasonably well on tablets
- No critical bugs or UX issues

#### Files Affected
- All tab components (add virtualization)
- `BlazorWebApp/Services/DatabaseService.cs` (optimize)
- New: `BlazorWebApp/Components/Prompts/Shared/KeyboardShortcutsHelp.razor`
- Update: CSS files for animations

---

### Phase 12: Analytics & Prompt Evolution
**Objective:** Add analytics dashboard and prompt evolution/breeding system  
**Complexity:** 21 points (NEW PHASE)  
**Status:** [ ] Not Started

#### Overview
Create comprehensive analytics for prompt performance tracking and implement experimental prompt evolution system. This phase combines data-driven insights with creative exploration.

#### Part A: Analytics Foundation (8 points)

##### Steps
1. Extend Image entity with rating and analytics properties
2. Create analytics database schema (TagUsage, TagCooccurrence, PromptAnalytics)
3. Implement AnalyticsService with caching strategy
4. Add background processing for analytics computation
5. Implement rating UI on image cards (5-star system)
6. Create analytics computation logic (incremental, not batch)

##### Database Extensions

```csharp
// Extend Image entity
public class Image
{
    // ...existing properties...
    public float? UserRating { get; set; }  // 1-5 stars
    public bool IsFavorite { get; set; }
    public DateTime? LastViewedAt { get; set; }
    public int ViewCount { get; set; }
}

// Tag usage tracking
public class TagUsage
{
    public int Id { get; set; }
    public string TagName { get; set; }
    public int UsageCount { get; set; }
    public float AverageRating { get; set; }
    public DateTime LastUsed { get; set; }
}

// Tag co-occurrence
public class TagCooccurrence
{
    public int Id { get; set; }
    public string Tag1 { get; set; }
    public string Tag2 { get; set; }
    public int CooccurrenceCount { get; set; }
    public float AverageRating { get; set; }
}

// Prompt analytics cache
public class PromptAnalytics
{
    public int Id { get; set; }
    public int PromptId { get; set; }
    public int TimesUsed { get; set; }
    public float AverageRating { get; set; }
    public int ImageCount { get; set; }
    public DateTime LastUsed { get; set; }
    public DateTime CacheUpdatedAt { get; set; }
}
```

##### Performance Strategy
- Lazy loading with background processing
- Incremental analytics updates (per-image, not batch)
- Cached results with configurable refresh
- Pagination and virtual scrolling

#### Part B: Analytics UI (5 points)

##### Steps
1. Create Analytics tab with sub-tabs
2. Implement Performance Tracker (top prompts, usage stats)
3. Implement Tag Analysis (co-occurrence, effectiveness)
4. Implement Historical Browser (timeline, filters)
5. Add visualizations (charts, heatmaps)

##### UI Structure

```
???????????????????????????????????????????????????????????
? Analytics Tab                                           ?
???????????????????????????????????????????????????????????
? [Performance] [Tag Analysis] [History] [Evolution]      ?
???????????????????????????????????????????????????????????
? Performance Tracker:                                    ?
?                                                         ?
? Top Performing Prompts (by avg rating):                ?
? 1. ????? "SDXL Base" - 4.8/5 (24 uses)             ?
? 2. ????? "Quality Boost" - 4.2/5 (18 uses)          ?
?                                                         ?
? Most Used Prompts:                                      ?
? 1. "SDXL Base" - 24 times                              ?
? 2. "Cinematic Lighting" - 18 times                     ?
?                                                         ?
? Tag Performance Heatmap:                                ?
? [Visual heatmap of tag effectiveness]                  ?
?                                                         ?
? Recommendations:                                        ?
? • "dramatic lighting" often paired with "volumetric"   ?
? • High-rated images use avg 15-20 tags                 ?
???????????????????????????????????????????????????????????
```

#### Part C: Prompt Evolution System (8 points)

##### Steps
1. Create PromptEvolution entity with tree structure
2. Implement variation generation (LLM-based)
3. Create Evolution UI with generation tree visualization
4. Add rating and selection mechanism
5. Implement "breeding" logic (combine best variations)
6. Add evolution history and lineage tracking
7. **DISCUSSION POINT:** Integration strategy with A/B testing and batch generation

##### Database Schema

```csharp
public class PromptEvolution
{
    public int Id { get; set; }
    public int GenerationNumber { get; set; }
    public int? ParentId { get; set; }
    public string PromptText { get; set; }
    public float Rating { get; set; }
    public bool IsSelected { get; set; }
    public DateTime CreatedAt { get; set; }
    public PromptEvolution? Parent { get; set; }
    public ICollection<PromptEvolution> Children { get; set; }
}
```

##### Evolution Workflow

```
Generation 1: [Original Prompt]
             ?
Generate 5 variations (LLM)
             ?
User rates/selects 2 best
             ?
Generation 2: Create variations from best 2 (breeding)
             ?
Repeat...
```

##### System Prompts

```
Initial Generation:
"Generate 5 creative variations of this prompt: '{prompt}'. 
Each should maintain the core concept but explore different:
- Descriptive angles
- Mood/atmosphere
- Composition details
- Artistic elements"

Breeding Generation:
"These two prompts were rated highly: '{prompt1}' and '{prompt2}'.
Generate 5 new prompts that combine the best aspects of both.
Maintain coherence while introducing subtle variations."
```

##### Discussion Points for Phase Planning

> **NOTE:** These topics require design decisions before implementation:

1. **A/B Testing Integration:**
   - Should evolution trigger automatic image generation?
   - How to integrate with batch generation queue?
   - Should we compare images side-by-side automatically?
   - Requires coordination with Generation page enhancement plan

2. **Batch Generation Workflow:**
   - Generate images for all variations at once?
   - Sequential generation with preview?
   - Queue integration strategy?

3. **Storage and Performance:**
   - How many generations to keep in history?
   - Archive old evolution trees?
   - Performance with large evolution trees?

4. **UI Complexity:**
   - Tree visualization (graph/timeline/list)?
   - Inline image previews in evolution tree?
   - Mobile/tablet experience?

#### Success Criteria
- Analytics provide actionable insights
- Performance tracking guides prompt optimization
- Tag analysis reveals useful patterns
- Historical browser helps find past work
- Evolution system generates creative variations
- Breeding produces coherent prompts
- Tree visualization shows lineage clearly
- **Discussion items resolved before implementation**

#### Files Affected
- Extend: `BlazorWebApp/Data/Entities/Image.cs`
- New: `BlazorWebApp/Data/Entities/TagUsage.cs`
- New: `BlazorWebApp/Data/Entities/TagCooccurrence.cs`
- New: `BlazorWebApp/Data/Entities/PromptAnalytics.cs`
- New: `BlazorWebApp/Data/Entities/PromptEvolution.cs`
- New: `BlazorWebApp/Services/AnalyticsService.cs`
- New: `BlazorWebApp/Services/PromptEvolutionService.cs`
- New: `BlazorWebApp/Components/Prompts/Analytics/AnalyticsTab.razor`
- New: `BlazorWebApp/Components/Prompts/Analytics/PerformanceTracker.razor`
- New: `BlazorWebApp/Components/Prompts/Analytics/TagAnalysis.razor`
- New: `BlazorWebApp/Components/Prompts/Analytics/HistoricalBrowser.razor`
- New: `BlazorWebApp/Components/Prompts/Analytics/EvolutionTree.razor`
- Extend: `BlazorWebApp/Services/DatabaseService.cs`
- New: `BlazorWebApp/Data/Migrations/` (analytics tables)

---

## Stress Points & Risks

| Risk | Mitigation Strategy | Complexity |
|------|---------------------|------------|
| Database migration fails | Create rollback scripts; test on backup DB first | 5 |
| Ollama VL models not available | Check Ollama docs first; have text-only fallback | 3 |
| Performance with large datasets | Implement pagination and virtualization early | 5 |
| Breaking existing features | Maintain backward compatibility; use feature flags | 3 |
| Wildcard parsing conflicts | Use unique delimiters `__name__`; extensive testing | 5 |
| UI overwhelms users | Progressive disclosure; good defaults; tooltips | 3 |
| Import/export format issues | Support multiple formats; validation on import | 3 |
| Analytics computation overhead | Background processing; caching; lazy loading | 5 |
| LLM response quality varies | Provide editing; multiple attempts; user feedback | 3 |
| Evolution tree complexity | Clear visualization; pruning old branches; limits | 5 |

**Total Risk Mitigation Complexity: 40 points**

---

## Total Complexity Summary

| Phase | Description | Points | Cumulative |
|-------|-------------|--------|------------|
| Phase 1 | Styles Tab Redesign (extended) | 19 | 19 |
| Phase 2 | Wildcards Database | 13 | 32 |
| Phase 3 | Wildcards UI | 13 | 45 |
| Phase 4 | Wildcard Integration | 8 | 53 |
| Phase 5 | LLM Tools Core (extended) | 8 | 61 |
| **Phase 5.5** | **LLM Creative Tools (NEW)** | **13** | **74** |
| Phase 6 | VL Model Integration | 13 | 87 |
| Phase 7 | Tag Builder | 8 | 95 |
| Phase 8 | Tag Enhancements | 5 | 100 |
| Phase 9 | Preview & Testing | 5 | 105 |
| Phase 10 | Import/Export (extended) | 8 | 113 |
| Phase 11 | Performance & Polish | 5 | 118 |
| **Phase 12** | **Analytics & Evolution (NEW)** | **21** | **139** |

**Total Project Complexity: 139 Fibonacci points**

This represents a comprehensive feature implementation with significant value delivery. The plan is structured for incremental delivery with each phase providing independent benefits.

---

## MVP Definition

Given the extended scope, we recommend defining a clear MVP:

### MVP Scope (Phases 1-4 + 9): 50 points
- **Phase 1:** Styles redesign with categories, favorites, search
- **Phase 2-4:** Complete wildcards system
- **Phase 9:** Preview and testing
- **Result:** Fixes critical issues, provides solid foundation

### Extended MVP (Add Phase 5-5.5): 71 points
- Adds all LLM creative tools
- Provides comprehensive prompt engineering capabilities

### Full Implementation: 139 points
- All phases including analytics and evolution
- Complete vision realized

---

## Discussion Points

### ? Resolved

1. **Wildcards Storage:** Database-only confirmed
2. **Feature Additions:** Approved and integrated into phases
3. **LLM Creative Tools:** Separated into Phase 5.5
4. **Analytics:** Consolidated into Phase 12

### ?? Requires Discussion

1. **Phase 12 Evolution System:**
   - Integration with A/B testing (requires Generation page redesign)
   - Batch generation workflow coordination
   - Tree visualization approach
   - These topics should be discussed during Phase 12 planning

2. **Execution Priority:**
   - MVP-first approach (Phases 1-4)?
   - Or include LLM tools early (Phases 1-5.5)?

3. **Performance Testing:**
   - Should we test smaller LLM models before Phase 5?
   - Validate contextual suggestions feasibility?

---

## Next Steps

1. **Confirm Plan Approval:** Review extended complexity and phase structure
2. **Decide on MVP Scope:** Which phases to implement first?
3. **Performance Testing:** Test LLM models if needed before Phase 5
4. **Begin Phase 1:** Create `PHASE_1.md` and start implementation

---

## References

### Current Implementation
- Prompts Page: `BlazorWebApp/Pages/Prompts.razor`
- Styles Panel: `BlazorWebApp/Components/Prompts/PromptsPanel.razor`
- Wildcards Panel: `BlazorWebApp/Components/Prompts/WildcardsPanel.razor`
- LLM Enhancer: `BlazorWebApp/Components/Shared/Generation/LLMPromptEnhancerForm.razor`
- Tag Drawer: `BlazorWebApp/Components/Shared/Generation/TagDrawer.razor`

### Services
- Database: `BlazorWebApp/Services/DatabaseService.cs`
- Ollama: `BlazorWebApp/Services/OllamaService.cs`
- CSV/Tags: `BlazorWebApp/Services/CsvService.cs`
- Parser: `BlazorWebApp/Extensions/Parser.cs`

### Components
- Autocomplete: `BlazorWebApp/Components/Shared/Generation/TextFieldAutocomplete.razor`
- Resource Card: `BlazorWebApp/Components/Resources/ResourceCard.razor`

---

## Changelog

| Phase | Changes |
|-------|---------|
| Planning | Initial plan created with 11 phases, 89 complexity points |
| Planning Update | Extended to 12 phases, 139 complexity points |
| | - Phase 1: Added categories, tags, favorites, semantic search (+11 pts) |
| | - Phase 5: Added simplify functionality, exposed prompts (same 8 pts) |
| | - Phase 5.5: NEW - LLM Creative Tools sub-tabs (+13 pts) |
| | - Phase 10: Added external imports (+5 pts) |
| | - Phase 12: NEW - Analytics & Evolution (+21 pts) |

---

**Status: Updated and ready for final review and execution approval**
