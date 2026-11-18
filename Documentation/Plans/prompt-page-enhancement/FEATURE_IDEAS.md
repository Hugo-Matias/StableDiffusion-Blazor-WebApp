# Prompt Page Enhancement - Additional Feature Ideas

## Status
**Document Type:** Brainstorming & Design Discussion  
**Related Plan:** `MAIN_PLAN.md`  
**Purpose:** Capture feature ideas for potential integration or future phases

---

## Feature Categories

### ? **Approved for Integration**
Features agreed to add to the main plan

### ?? **Needs Discussion**
Features requiring technical design decisions

### ?? **Future Consideration**
Features deferred to later phases or separate plans

### ? **Out of Scope**
Features rejected or not applicable

---

## LLM Tools Enhancements

### ? 1. Prompt Mixing/Blending
**Status:** Nice-to-have for Phase 5 (LLM Tools)  
**Complexity:** 5 points  

**Concept:**
Combine two or more prompts intelligently using LLM to create hybrid prompts.

**Implementation Strategy:**
- Add to LLM Tools tab as additional feature
- UI: Two input fields + blend ratio slider
- System prompt: "Combine these prompts into a cohesive description: [prompt1] and [prompt2]. Blend ratio: [ratio]%"
- Save result to history

**UI Mockup:**
```
???????????????????????????????????????????????
? Prompt Mixer                                ?
???????????????????????????????????????????????
? Prompt A: [cyberpunk city            ]     ?
? Prompt B: [underwater world          ]     ?
?                                             ?
? Blend Ratio:  [====|====] 50/50            ?
?                                             ?
? [Mix Prompts]                               ?
?                                             ?
? Result: A bioluminescent cyberpunk city... ?
???????????????????????????????????????????????
```

**Integration Point:** Phase 5, Step 4 (expansion presets) - add as additional preset option

---

### ?? 3. Prompt Evolution/Breeding
**Status:** Interesting concept - needs design discussion  
**Complexity:** 13 points (complex feature)

**Concept:**
Generate variations of a prompt and allow user to "breed" the best results iteratively.

**Discussion Points:**

1. **Where to implement?**
   - Option A: Dedicated sub-tab in LLM Tools
   - Option B: New feature in Batch Variation Generator (idea #11)
   - Option C: Integrate with Wildcards (random variations)

2. **How to generate variations?**
   - LLM-based: Use system prompt to create variations
   - Template-based: Swap wildcards randomly
   - Hybrid: LLM + wildcards + tag substitutions

3. **Evolution mechanism:**
   ```
   Generation 1: [Original Prompt]
                 ?
   Generate 5 variations
                 ?
   User rates/selects 2 best
                 ?
   Generation 2: Create variations from best 2
                 ?
   Repeat...
   ```

4. **Storage:**
   - Store generations in database with lineage/tree structure
   - Track which variations came from which parents
   - Visualize as tree or timeline

**Proposed Implementation:**
```csharp
// Phase 5 extension or Phase 12 (new phase)

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

**System Prompts:**
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

**Questions to Resolve:**
- Should variations be generated instantly or as background task?
- How many variations per generation? (3-5-10?)
- Should we track generation metrics (average rating, diversity score)?
- Integration with image generation (auto-generate images for each variation)?

---

### ?? 4. Contextual Prompt Suggestions
**Status:** Needs performance testing  
**Complexity:** 8 points

**Concept:**
Real-time suggestions based on current prompt content.

**Challenge:**
LLM models (8-9B params) may be too slow for real-time suggestions.

**Discussion Points:**

1. **Trigger Mechanism:**
   - Option A: On-demand button (user requests suggestions)
   - Option B: Debounced (trigger after 2-3 seconds of no typing)
   - Option C: Manual trigger with keyboard shortcut (Ctrl+Space)

2. **Performance Testing Required:**
   - Test with smaller models (1-3B params):
     - `TinyLlama-1.1B`
     - `Phi-3-mini-3.8B`
     - `Qwen2.5-1.5B-Instruct`
   - Measure response times for suggestion generation
   - Evaluate quality vs. speed trade-off

3. **Suggestion Types:**
   - Missing elements: "Add lighting", "Add background", "Add mood"
   - Composition suggestions: "Consider adding: subject placement"
   - Style suggestions: "This could work well with: art style X"
   - Quality improvements: "Add quality boosters"

4. **UI Design:**
   ```
   ???????????????????????????????????????????????
   ? Prompt: [woman in a park            ]      ?
   ?                                             ?
   ? ?? Suggestions:                             ?
   ?   ? Add lighting (golden hour, dramatic)   ?
   ?   ? Add mood (peaceful, contemplative)     ?
   ?   ? Add background details (trees, bench)  ?
   ?   ? Add quality tags (high quality, 4k)    ?
   ???????????????????????????????????????????????
   ```

**Proposed Implementation:**
- Phase 5 extension (Step 6 - Prompt Analyzer)
- Add "Get Suggestions" button to LLM Tools
- Use lightweight model for suggestions (< 4B params)
- Cache suggestions for same prompt (don't regenerate)

**System Prompt Template:**
```
"Analyze this prompt and suggest missing elements: '{prompt}'

Current elements detected:
- Subject: {detected_subject}
- Style: {detected_style}
- Setting: {detected_setting}

Provide 3-5 concrete suggestions for improvement in these categories:
1. Lighting (if missing)
2. Mood/Atmosphere (if missing)
3. Background/Environment details (if minimal)
4. Composition guidance (if unclear)
5. Quality/Technical aspects (if missing)

Format as brief, actionable suggestions."
```

---

### ? 5. Prompt Simplification + Exposed System Prompts
**Status:** Approved for Phase 5  
**Complexity:** 3 points

**Implementation:**
Add dual functionality to LLM Enhancer with exposed system prompts.

**UI Design:**
```
???????????????????????????????????????????????????????
? LLM Prompt Tools                                    ?
???????????????????????????????????????????????????????
? Input Prompt: [long complex prompt text...  ]      ?
?                                                     ?
? [Enhance] [Simplify]                                ?
?                                                     ?
? ? Advanced: System Prompts                         ?
?                                                     ?
? Enhancement Prompt:                                 ?
? [Expand this prompt with details: {prompt}  ]      ?
?                                                     ?
? Simplification Prompt:                              ?
? [Distill this prompt to essentials: {prompt}]      ?
?                                                     ?
? [Save Custom Templates]                             ?
???????????????????????????????????????????????????????
```

**System Prompts:**
```csharp
public class LLMSystemPrompts
{
    // Enhancement
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
    
    // Custom user templates
    public List<CustomSystemPrompt> UserTemplates { get; set; }
}

public class CustomSystemPrompt
{
    public string Name { get; set; }
    public string Template { get; set; }
    public string Description { get; set; }
}
```

**Features:**
- Toggle between Enhance/Simplify modes
- Expose system prompts in collapsible "Advanced" panel
- Allow users to edit and save custom templates
- Store templates in database (per-user settings)
- Preset templates dropdown (built-in + user-created)

**Integration:** Phase 5, Step 2-3 (LLMPromptEnhancerForm refactor)

---

## Analytics & Performance Tracking

### ?? 6-9. Prompt Performance Analytics Tab
**Status:** Interesting concept - technical design needed  
**Complexity:** 13 points (new tab + database schema)

**Concepts Combined:**
- #6: Prompt Performance Tracking (ratings, correlations)
- #7: Tag Co-occurrence Analysis
- #8: Prompt Complexity Analyzer
- #9: Historical Prompt Browser

**Discussion: Technical Implementation**

#### Challenge: Database Scale
- Parsing all images could be compute-intensive for large databases
- Need efficient querying and caching strategies

#### Proposed Architecture:

```
????????????????????????????????????????????????
? Analytics Tab (New - Phase 12)              ?
????????????????????????????????????????????????
? Sub-tabs:                                    ?
? • Performance Tracker                        ?
? • Tag Analysis                               ?
? • History Browser                            ?
????????????????????????????????????????????????
```

#### Database Schema Extensions:

```csharp
// Extend existing Image entity
public class Image
{
    // ...existing properties...
    
    // NEW: Analytics properties
    public float? UserRating { get; set; }  // 1-5 stars
    public bool IsFavorite { get; set; }
    public DateTime? LastViewedAt { get; set; }
    public int ViewCount { get; set; }
}

// NEW: Tag usage tracking
public class TagUsage
{
    public int Id { get; set; }
    public string TagName { get; set; }
    public int UsageCount { get; set; }
    public float AverageRating { get; set; }  // Avg of images using this tag
    public DateTime LastUsed { get; set; }
}

// NEW: Tag co-occurrence
public class TagCooccurrence
{
    public int Id { get; set; }
    public string Tag1 { get; set; }
    public string Tag2 { get; set; }
    public int CooccurrenceCount { get; set; }
    public float AverageRating { get; set; }  // Avg rating of images with both tags
}

// NEW: Prompt analytics cache
public class PromptAnalytics
{
    public int Id { get; set; }
    public int PromptId { get; set; }
    public int TimesUsed { get; set; }
    public float AverageRating { get; set; }
    public int ImageCount { get; set; }
    public DateTime LastUsed { get; set; }
    public DateTime CacheUpdatedAt { get; set; }
    public Prompt Prompt { get; set; }
}
```

#### Performance Strategy:

1. **Lazy Loading / Background Processing:**
   ```csharp
   // Don't analyze all images at once
   // Process in background as images are viewed/rated
   public class AnalyticsService
   {
       public async Task UpdateAnalyticsForImage(int imageId)
       {
           // Update tag usage
           // Update co-occurrence
           // Update prompt analytics
           // Run as background task
       }
       
       public async Task<AnalyticsSummary> GetAnalyticsSummary(bool forceRefresh = false)
       {
           // Check cache first
           // Only recompute if stale or forced
       }
   }
   ```

2. **Caching Strategy:**
   - Compute analytics incrementally (not all at once)
   - Cache results in `PromptAnalytics` table
   - Refresh cache on timer (hourly/daily) or on-demand
   - Use indexes on frequently queried columns

3. **Pagination & Filtering:**
   - Don't load all historical prompts at once
   - Implement virtual scrolling / pagination
   - Filter by date range, model, rating

#### UI Mockup:

```
???????????????????????????????????????????????????????????
? Analytics Tab                                           ?
???????????????????????????????????????????????????????????
? [Performance] [Tag Analysis] [History]                  ?
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
? • Your best checkpoint: ponyDiffusionV6XL.safetensors ?
???????????????????????????????????????????????????????????
```

#### Implementation Phases:

**Phase 12: Analytics Foundation** (13 points)
1. Extend Image entity with rating/favorite fields
2. Create analytics database tables
3. Implement AnalyticsService with caching
4. Add rating UI to image cards (5-star system)
5. Background task for analytics computation

**Phase 13: Analytics UI** (8 points)
1. Create Analytics tab
2. Implement Performance Tracker sub-tab
3. Implement Tag Analysis sub-tab
4. Implement History Browser sub-tab
5. Add visualizations (charts, heatmaps)

**Questions to Resolve:**
- Should analytics run automatically or on-demand?
- How often to refresh analytics cache?
- Should we track model performance separately?
- Integration with Civitai ratings (if available)?

---

## Organization & Workflow Features

### ? 14-15. Prompt Categories/Tags System
**Status:** Approved for integration  
**Complexity:** 5 points

**Implementation:**
Extend Prompt entity to support categorization, similar to Wildcards.

**Database Schema:**

```csharp
// Extend existing Prompt entity
public class Prompt
{
    // ...existing properties...
    
    // NEW: Categorization
    public string? Category { get; set; }  // e.g., "Characters", "Landscapes"
    public List<string>? Tags { get; set; }  // User-defined tags
    public DateTime? LastUsedAt { get; set; }
    public int UsageCount { get; set; }
}
```

**UI Integration:**

```
Styles Tab Layout:
??????????????????????????????????????????????????????
? Categories  ? Styles                               ?
?             ?                                       ?
? All (45)    ? ?????????????????????????????????????
? Characters  ? ? SDXL Base                        ??
? Landscapes  ? ? Category: Quality                ??
? Quality     ? ? Tags: sdxl, base, quality        ??
? Lighting    ? ? [Edit] [Delete] [Duplicate]      ??
? [+ New]     ? ?????????????????????????????????????
?             ?                                       ?
??????????????????????????????????????????????????????
```

**Features:**
- Category dropdown in style editor
- LLM suggestion button: "Suggest categories and tags for this prompt"
- Filter styles by category (left sidebar)
- Tag-based search
- Reuse same category/folder logic as Wildcards (consistency)

**LLM Integration:**
```
System Prompt:
"Analyze this prompt and suggest:
1. An appropriate category (Characters, Landscapes, Quality, Lighting, Mood, Style, etc.)
2. 3-5 descriptive tags

Prompt: '{prompt_text}'

Return as JSON:
{
  "category": "suggested_category",
  "tags": ["tag1", "tag2", "tag3"]
}"
```

**Integration Point:** Phase 1, Step 4 (PromptStyleEditor enhancement)

---

### ?? 16. Semantic Search with NLP
**Status:** Needs implementation discussion  
**Complexity:** 8 points

**Concept:**
Natural language search: "find prompts about fantasy forests"

**Discussion: How to Implement?**

#### Option A: LLM-Based Search
```csharp
public async Task<List<Prompt>> SemanticSearch(string query)
{
    // Use LLM to understand query
    var embedding = await OllamaService.GetEmbedding(query);
    
    // Compare against prompt embeddings (pre-computed)
    var results = await DB.FindSimilarPrompts(embedding, limit: 20);
    
    return results;
}
```

**Pros:**
- True semantic understanding
- Can find conceptually similar prompts

**Cons:**
- Requires embedding model (separate from chat model)
- Need to pre-compute and store embeddings for all prompts
- Performance overhead

---

#### Option B: Keyword Expansion + Fuzzy Search
```csharp
public async Task<List<Prompt>> SemanticSearch(string query)
{
    // Use LLM to expand query to keywords
    var systemPrompt = "Extract keywords from this search query: '{query}'";
    var keywords = await OllamaService.GenerateKeywords(query);
    
    // Traditional search across expanded keywords
    var results = await DB.SearchPrompts(keywords);
    
    return results;
}
```

**Pros:**
- Simpler implementation
- Leverages existing search infrastructure
- Faster than embeddings

**Cons:**
- Not true semantic search
- Dependent on LLM quality

---

#### Option C: Hybrid (Full-Text Search + LLM Ranking)
```csharp
public async Task<List<Prompt>> SemanticSearch(string query)
{
    // Step 1: Traditional search (fast, broad)
    var candidates = await DB.SearchPrompts(query, limit: 100);
    
    // Step 2: LLM re-ranking (slower, precise)
    var ranked = await OllamaService.RankByRelevance(query, candidates);
    
    return ranked.Take(20);
}
```

**Pros:**
- Best of both worlds
- Fast initial filtering
- Accurate final ranking

**Cons:**
- More complex implementation
- Still requires LLM call (but only for ranking)

---

**Recommendation:**
- **Phase 1:** Implement Option B (keyword expansion) - simplest
- **Future:** Upgrade to Option C if needed

**Integration Point:** Phase 1, Step 5 (search implementation)

---

### ? 17. Favorites & Quick Access
**Status:** Approved - needs implementation discussion  
**Complexity:** 3 points

**Implementation:**

#### Database:
```csharp
// Extend Prompt entity
public class Prompt
{
    // ...existing...
    public bool IsPinned { get; set; }  // Pin to top
    public bool IsFavorite { get; set; }
    public int SortOrder { get; set; }  // Manual ordering for pinned
}
```

#### UI Design:

```
Styles Tab:
??????????????????????????????????????????????
? [? Favorites] [?? Pinned] [All]          ?
??????????????????????????????????????????????
? Quick Access Bar:                          ?
? [SDXL Base] [Quality++] [Cinematic]       ?
? (Hotkeys: Ctrl+1, Ctrl+2, Ctrl+3)        ?
??????????????????????????????????????????????
? Pinned Styles:                             ?
? ?? SDXL Base                               ?
? ?? Quality Boost                           ?
?                                            ?
? All Styles:                                ?
? ? Cinematic Lighting                      ?
?   Portrait Style                           ?
??????????????????????????????????????????????
```

#### Features:
- Pin icon on each style card (toggle pinned)
- Star icon for favorites
- Drag to reorder pinned styles
- Hotkeys for top 10 pinned (Ctrl+1 through Ctrl+0)
- Filter tabs: Favorites / Pinned / All

**Integration Point:** Phase 1, Step 2-3 (table view + actions)

---

## Workflow & Automation

### ?? 11. Batch Variation Generator + A/B Testing
**Status:** Interesting but requires Generation page redesign  
**Complexity:** Separate plan needed

**Discussion:**
This is a great feature but extends beyond Prompts page scope.

**Considerations:**
1. **Image Results Panel Redesign:**
   - Current: Only shows latest batch
   - Needed: Session history with multiple batches
   - Similar to video session history

2. **A/B Comparison UI:**
   - Compare multiple prompt variations
   - Compare sampler settings
   - Compare seed variations
   - Side-by-side image viewer

3. **Integration with Wildcards:**
   - Generate variations by cycling through wildcard options
   - Batch generate: 1 image per wildcard entry

**Recommendation:**
- Create separate plan: "Generation Results Enhancement"
- Coordinate with Prompts plan for wildcard integration
- Phase 4 (Wildcard Integration) should prepare for this

**Potential Integration Point:**
When Phase 4 complete, could add:
- "Generate Variations" button in Wildcards tab
- Sends batch queue to generation with different wildcard selections

---

### ?? 12. Prompt Queue System
**Status:** Future consideration (already planned separately)  
**Complexity:** 8 points (separate plan)

**Notes:**
- Native ComfyUI queue support
- Requires WebSocket communication enhancement
- Should be part of Generation workflow plan
- Can leverage existing queue infrastructure

**Integration with Prompts Page:**
- "Add to Queue" button for styles
- Batch add multiple styles to queue
- Preview queue before execution

---

## Creative & Experimental Features

### ? 26-29. LLM Creative Tools Collection
**Status:** Approved for Phase 5 expansion  
**Complexity:** 8 points (combined)

**Features to Implement:**

#### 26. Random Prompt Generator
```
UI:
??????????????????????????????????????????
? Need Inspiration?                      ?
??????????????????????????????????????????
? Genre:     [Fantasy ?]                ?
? Mood:      [Any ?]                    ?
? Complexity: ????? (Medium)            ?
?                                        ?
? [?? Inspire Me!]                       ?
?                                        ?
? Generated: A mystical forest clearing ?
? at dawn with ancient stone ruins...   ?
??????????????????????????????????????????
```

**System Prompt:**
```
"Generate a creative image prompt with these constraints:
- Genre: {genre}
- Mood: {mood}
- Complexity: {complexity}

Create a cohesive, detailed prompt suitable for image generation."
```

---

#### 27. Prompt Templates Engine (Mad Libs Style)
```
UI:
??????????????????????????????????????????
? Template Builder                       ?
??????????????????????????????????????????
? Template:                              ?
? A [adjective] [character] in a        ?
? [location] during [time-of-day]       ?
?                                        ?
? Fill in:                               ?
? adjective:    [beautiful    ?]        ?
? character:    [woman        ?]        ?
? location:     [park         ?]        ?
? time-of-day:  [sunset       ?]        ?
?                                        ?
? [Generate Variations]                  ?
?                                        ?
? Result: A beautiful woman in a park   ?
? during sunset                          ?
??????????????????????????????????????????
```

**Database:**
```csharp
public class PromptTemplate
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Template { get; set; }  // "A [adjective] [character]..."
    public List<string> Variables { get; set; }  // ["adjective", "character"...]
    public Dictionary<string, List<string>> VariableOptions { get; set; }
}
```

**Features:**
- Pre-built templates (characters, landscapes, scenes)
- User-created templates
- Wildcards as template variables
- "Fill All Random" button
- Save filled template as new style

---

#### 28. Character/Scene Builder (Structured Form)
```
UI:
??????????????????????????????????????????????????????
? Scene Builder                                      ?
??????????????????????????????????????????????????????
? Subject:                                           ?
?   Type:       [Character ?]                       ?
?   Details:    [1girl, long hair, blue dress]      ?
?                                                    ?
? Environment:                                       ?
?   Location:   [Forest clearing    ]               ?
?   Time:       [Golden hour        ]               ?
?   Weather:    [Partly cloudy      ]               ?
?                                                    ?
? Lighting:                                          ?
?   Type:       [Natural, dramatic  ]               ?
?   Direction:  [Side lighting      ]               ?
?                                                    ?
? Mood:         [Peaceful, contemplative]           ?
?                                                    ?
? Style:        [Cinematic, high detail]            ?
?                                                    ?
? [Assemble Prompt]                                  ?
?                                                    ?
? Generated Prompt:                                  ?
? A young woman with long flowing hair wearing a... ?
??????????????????????????????????????????????????????
```

**Assembly Logic:**
```csharp
public string AssemblePrompt(SceneBuilder scene)
{
    var parts = new List<string>();
    
    // Subject (required)
    parts.Add(scene.Subject);
    
    // Environment
    if (!string.IsNullOrEmpty(scene.Location))
        parts.Add($"in a {scene.Location}");
    
    if (!string.IsNullOrEmpty(scene.Time))
        parts.Add($"during {scene.Time}");
    
    if (!string.IsNullOrEmpty(scene.Weather))
        parts.Add($"with {scene.Weather}");
    
    // Lighting
    if (!string.IsNullOrEmpty(scene.Lighting))
        parts.Add($"{scene.Lighting} lighting");
    
    // Style/Mood at end
    if (!string.IsNullOrEmpty(scene.Style))
        parts.Add(scene.Style);
    
    return string.Join(", ", parts);
}
```

---

#### 29. Prompt Remixing
```
UI:
??????????????????????????????????????????
? Prompt Remixer                         ?
??????????????????????????????????????????
? Select styles to remix:                ?
? ? SDXL Base                           ?
? ? Cinematic Lighting                  ?
? ? Fantasy Style                       ?
?                                        ?
? Remix Method:                          ?
? ? Shuffle elements                   ?
? ? Combine random parts               ?
? ? LLM intelligent mix                ?
?                                        ?
? [?? Remix!]                            ?
??????????????????????????????????????????
```

**Implementation:**
- Shuffle: Randomly reorder tags/phrases
- Combine: Take random segments from each
- LLM Mix: Use LLM to intelligently combine

---

#### 33. Prompt Roulette
```
UI:
??????????????????????????????????????????
?         PROMPT ROULETTE ??            ?
??????????????????????????????????????????
?     [Spin the Wheel!]                  ?
?                                        ?
?    ??                                  ?
?   ?  ?                                ?
?  ? ?? ?  ? Click to spin              ?
?   ?  ?                                ?
?    ?                                   ?
?                                        ?
? Your Lucky Prompt:                     ?
? [Waiting for spin...]                  ?
?                                        ?
? [Generate Image] [Spin Again]         ?
??????????????????????????????????????????
```

**Logic:**
```csharp
public async Task<string> SpinRoulette()
{
    var components = new
    {
        Subject = await GetRandom("subjects"),
        Style = await GetRandom("styles"),
        Lighting = await GetRandom("lighting"),
        Mood = await GetRandom("moods"),
        Location = await GetRandom("locations")
    };
    
    return $"{components.Subject}, {components.Style}, " +
           $"{components.Lighting}, {components.Mood}, " +
           $"{components.Location}";
}
```

---

**Integration:**
All these features can be sub-tabs or sections within Phase 5 (LLM Tools tab).

Suggested structure:
```
LLM Tools Tab:
?? Enhancer (main feature)
?? Mixer
?? Templates
?? Scene Builder
?? Remixer
?? Roulette (fun mode)
```

---

## Import & Integration

### ? 31. External Import Features
**Status:** Approved for Phase 10 (Import/Export)  
**Complexity:** 5 points (extend existing phase)

**Services to Consider:**

#### PromptHero
- API: Unknown if public API exists
- Fallback: Manual copy-paste import

#### Lexica
- API: Has search API
- Can fetch popular prompts
- Rate limited

#### Civitai
- API: Extensive API available
- Can import prompts from models
- Can import from image metadata

**Implementation Strategy:**

```csharp
public class ExternalImportService
{
    public async Task<List<Prompt>> ImportFromCivitai(int modelId)
    {
        // Fetch model data from Civitai API
        var model = await CivitaiService.GetModel(modelId);
        
        // Extract prompts from example images
        var prompts = model.ModelVersions
            .SelectMany(v => v.Images)
            .Where(i => !string.IsNullOrEmpty(i.Meta?.Prompt))
            .Select(i => new Prompt
            {
                Title = $"Civitai - {model.Name}",
                Positive = i.Meta.Prompt,
                Negative = i.Meta.NegativePrompt
            })
            .Distinct()
            .ToList();
        
        return prompts;
    }
    
    public async Task<List<Prompt>> ImportFromLexica(string searchQuery, int limit = 10)
    {
        // Lexica API search
        var results = await LexicaService.Search(searchQuery, limit);
        
        return results.Select(r => new Prompt
        {
            Title = $"Lexica - {searchQuery}",
            Positive = r.Prompt,
            // Lexica doesn't have negative prompts
        }).ToList();
    }
    
    public async Task<Prompt> ImportFromText(string text)
    {
        // Parse pasted text (various formats)
        // A1111 format, ComfyUI format, plain text
        return Parser.ParseExternalPrompt(text);
    }
}
```

**UI:**
```
Import Dialog:
??????????????????????????????????????????
? Import Prompts                         ?
??????????????????????????????????????????
? Source:                                ?
? ? Civitai (model ID or URL)          ?
? ? Lexica (search query)              ?
? ? Paste Text                         ?
? ? File Upload (JSON/CSV)             ?
?                                        ?
? Input: [                        ]     ?
?                                        ?
? [Import]                               ?
??????????????????????????????????????????
```

**Integration Point:** Phase 10, Step 2 (extend import functionality)

---

## Out of Scope

### ? 2. Style Transfer Between Prompts
**Reason:** Too complex; prompt styles already serve this purpose

### ? 10. Prompt Recipes/Workflows
**Reason:** Too complex for current application state

### ? 13. Auto-Enhancement Toggle
**Reason:** User should control workflow explicitly

### ? 18-20. Community Features (Marketplace, Collaboration, Challenges)
**Reason:** Single-user desktop application

### ? 21-25. Advanced Testing Features (A/B testing framework, versioning, regex)
**Reason:** Complexity vs. value trade-off

### ? 30. Mobile App
**Reason:** Desktop-focused application

### ? 32. Clipboard Monitoring
**Reason:** Can already send to generation with existing buttons

### ? 34-38. Learning/Educational Features
**Reason:** Out of scope for tool application

---

## Summary: Features to Integrate

### Immediate Integration (Existing Phases):

| Feature | Phase | Complexity |
|---------|-------|------------|
| Prompt Mixing | Phase 5 | +5 |
| Simplify + Exposed Prompts | Phase 5 | +3 |
| Categories/Tags | Phase 1 | +5 |
| Favorites/Quick Access | Phase 1 | +3 |
| Random Generator | Phase 5 | +2 |
| Templates Engine | Phase 5 | +5 |
| Scene Builder | Phase 5 | +5 |
| Remixer | Phase 5 | +3 |
| Roulette | Phase 5 | +2 |
| External Import | Phase 10 | +5 |

**Total Additional Complexity: 38 points**

### Future Phases (New):

| Feature | New Phase | Complexity |
|---------|-----------|------------|
| Prompt Evolution | Phase 12 | 13 |
| Analytics Tab | Phase 13 | 13 |
| Contextual Suggestions | Phase 5 Extension | 8 |
| Semantic Search | Phase 1 Extension | 8 |

**Total New Phase Complexity: 42 points**

### Deferred (Separate Plans):

| Feature | Separate Plan | Complexity |
|---------|---------------|------------|
| Batch Variations + A/B | Generation Enhancement Plan | TBD |
| Queue System | Generation Enhancement Plan | 8 |

---

## Next Steps

1. **Review these feature designs**
2. **Prioritize which to integrate into main plan**
3. **Decide on new phases vs. existing phase extensions**
4. **Update MAIN_PLAN.md with approved features**

---

**Status: Awaiting review and prioritization decisions**
