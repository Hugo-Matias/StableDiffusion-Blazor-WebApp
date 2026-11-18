# Wildcard Creation Wizard

## Overview

This document provides a step-by-step questionnaire that LLMs (Large Language Models) can use to guide users through creating high-quality wildcard collections. The wizard ensures all best practices from the wildcard documentation are followed.

**For LLMs:** Use this as a structured conversation flow to help users create wildcard collections. Ask questions in order, validate responses, and provide helpful feedback based on the guidelines.

**Reference Documents:**
- `WILDCARD_GENERATION_GUIDE.md` - Complete guide with examples and best practices
- `WILDCARD_TEMPLATE.json` - JSON structure template
- `LLM_PROMPTS.json` - Tested prompt templates for generation
- `THEME_CATALOG.json` - 10 theme categories with specifications

---

## Wizard Flow

### Phase 1: Discovery & Planning
Goal: Understand user's needs and set collection parameters

### Phase 2: Structure Definition
Goal: Define collection metadata and entry requirements

### Phase 3: Content Generation
Goal: Create wildcard entries following guidelines

### Phase 4: Quality Assurance
Goal: Review, refine, and validate the collection

### Phase 5: Export & Finalization
Goal: Format and prepare for import

---

## Phase 1: Discovery & Planning

### Question 1.1: Collection Purpose
**LLM Prompt:**
```
Hello! I'll help you create a high-quality wildcard collection. Let's start by understanding your needs.

What type of images are you generating, and what aspect do you want to vary?

Examples:
- "I'm generating portraits and want to vary facial expressions"
- "I'm creating landscapes and need different lighting conditions"
- "I want to randomize clothing styles for character art"

Your answer:
```

**Validation:**
- Ensure answer describes a clear, focused theme
- Identify if theme matches one of the 10 categories from `THEME_CATALOG.json`

**LLM Follow-up:**
```
Based on your description, I believe this falls under the [CATEGORY_NAME] category.

From THEME_CATALOG.json, this category typically includes:
- Subcategories: [LIST_SUBCATEGORIES]
- Typical verbosity: [VERBOSITY_LEVEL]
- Keywords: [KEYWORDS]

Does this sound correct? If not, which category do you think fits better?

Available categories:
1. Fashion & Clothing
2. Locations & Settings
3. Artistic Styles
4. Lighting & Atmosphere
5. Character Features
6. Actions & Poses
7. Objects & Props
8. Colors & Palettes
9. Emotions & Expressions
10. Composition & Framing
```

---

### Question 1.2: Scope Definition
**LLM Prompt:**
```
Great! Now let's define the scope of your collection.

How specific or broad do you want this collection to be?

Options:
A) Very focused (e.g., "only evening dresses" not "all clothing")
B) Moderately focused (e.g., "casual tops" not "all tops")
C) Broad coverage (e.g., "all outdoor locations")

Recommendation: Start with focused collections (A or B) for better quality and usability.
See WILDCARD_GENERATION_GUIDE.md > Best Practices > "Start with a Plan"

Your choice (A/B/C):
```

**Validation:**
- If user chooses C, suggest splitting into multiple focused collections
- Reference: `WILDCARD_GENERATION_GUIDE.md` section on "Consider creating multiple focused collections vs. one large one"

---

### Question 1.3: Target Count
**LLM Prompt:**
```
How many entries do you want in this collection?

Recommendations:
- Starter collection: 10-15 entries
- Standard collection: 15-25 entries
- Comprehensive collection: 25-40 entries

Note: Quality > Quantity. It's better to have 15 excellent entries than 50 mediocre ones.
See WILDCARD_GENERATION_GUIDE.md > Best Practices > "Maintain Quality Over Quantity"

Your target count (10-40 recommended):
```

**Validation:**
- Warn if < 5 (too few for meaningful variety)
- Warn if > 50 (suggest splitting into subcollections)
- Reference weight distribution guidelines from guide

---

## Phase 2: Structure Definition

### Question 2.1: Verbosity Level
**LLM Prompt:**
```
Let's determine the verbosity level for your entries. This affects how detailed each entry will be.

Choose the verbosity level that best fits your use case:

1. MINIMAL (1-3 words)
   Example: "red dress"
   Best for: Quick variations, simple tags
   
2. BALANCED (3-6 words) ? RECOMMENDED
   Example: "elegant red evening dress"
   Best for: Standard prompts, versatile use
   
3. DETAILED (6-12 words)
   Example: "elegant flowing red evening dress with lace details"
   Best for: Specific imagery, scene building
   
4. VERBOSE (12+ words)
   Example: "elegant flowing red evening dress with intricate lace details, silk fabric, and dramatic train"
   Best for: Cinematic scenes, maximum detail

Based on your category ([CATEGORY_NAME]), the typical verbosity is: [TYPICAL_VERBOSITY]

See WILDCARD_GENERATION_GUIDE.md > Verbosity Levels for detailed examples.
See THEME_CATALOG.json > [category].typical_verbosity for category recommendations.

Your choice (1-4):
```

**Validation:**
- Store chosen verbosity level
- Show examples from `THEME_CATALOG.json` for the user's category
- Reference: `WILDCARD_GENERATION_GUIDE.md` verbosity sections

---

### Question 2.2: Collection Name
**LLM Prompt:**
```
Now let's name your collection. This name will be used in prompts like: __your-name-here__

Naming conventions:
? Use lowercase with hyphens (kebab-case)
? Be descriptive and specific
? Use plural form when appropriate
? Keep it short (2-4 words max)

Examples:
? facial-expressions
? casual-tops
? dramatic-lighting
? fantasy-locations

? FacialExpressions (no camelCase)
? all_stuff (too vague)
? items (too generic)

See WILDCARD_TEMPLATE.json > collection.name for more guidance.

Your collection name:
```

**Validation:**
- Check format: lowercase, hyphens only, no spaces
- Check length: 2-30 characters recommended
- Suggest improvements if needed

---

### Question 2.3: Description
**LLM Prompt:**
```
Please provide a brief description of your collection (under 200 characters).

This helps users understand:
- What the collection contains
- When to use it
- What makes it unique

Examples:
? "Cinematic lighting styles for dramatic scenes"
? "Casual everyday outfit descriptions"
? "Human facial expressions ranging from subtle to obvious emotions"

? "Stuff for images" (too vague)
? "A collection of various different lighting conditions and situations that can be used in many scenarios..." (too long)

See WILDCARD_TEMPLATE.json > collection.description for examples.

Your description:
```

**Validation:**
- Check length: < 200 characters
- Ensure it's descriptive and helpful
- Suggest improvements if too vague

---

### Question 2.4: Subcategory Focus (Optional)
**LLM Prompt:**
```
Would you like to focus on a specific subcategory within [CATEGORY_NAME]?

Available subcategories for [CATEGORY_NAME]:
[LIST_FROM_THEME_CATALOG]

This helps narrow the scope and improve consistency.

Options:
A) Yes, focus on: [USER_SPECIFIES]
B) No, cover multiple subcategories

See THEME_CATALOG.json > [category].subcategories for complete list.

Your choice:
```

**Validation:**
- Verify subcategory exists in `THEME_CATALOG.json`
- Adjust entry generation parameters based on subcategory

---

## Phase 3: Content Generation

### Question 3.1: Generation Method
**LLM Prompt:**
```
How would you like to create the entries?

Options:
A) I'll provide example entries, you generate the rest (RECOMMENDED)
B) You generate all entries based on requirements
C) I'll provide all entries manually

Option A is recommended for best results - your examples help me understand your style.

See WILDCARD_GENERATION_GUIDE.md > LLM Generation > Basic Generation Workflow

Your choice (A/B/C):
```

**Path A: User Provides Examples**
```
Perfect! Please provide 3-5 example entries that represent the style you want.

Requirements for your examples:
- Follow [VERBOSITY_LEVEL] verbosity (word count: [WORD_RANGE])
- Use concrete, visual descriptions
- Avoid abstract concepts
- Show variety

Example format:
1. "gentle warm smile"
2. "excited joyful expression"
3. "contemplative thoughtful gaze"

See WILDCARD_GENERATION_GUIDE.md > Quality Guidelines for detailed rules.

Your examples:
```

**Path B: Full Generation**
```
I'll generate all entries for you. To ensure quality, I need a few more details:

1. What specific aspects should I cover? (e.g., "include both positive and negative emotions")
2. Any specific items that MUST be included?
3. Any items to AVOID?

See LLM_PROMPTS.json > basic_generation template for the generation approach.

Your specifications:
```

**Path C: Manual Entry**
```
Please provide all [TARGET_COUNT] entries.

Format requirements:
- Each entry on a new line
- Follow [VERBOSITY_LEVEL] verbosity
- Maintain consistent style

Quality checklist (from WILDCARD_GENERATION_GUIDE.md):
? Concrete, visual descriptions
? Observable characteristics
? Specific, precise language
? No abstract concepts
? No prompt syntax (no "high quality, 8k")

Your entries:
```

---

### Question 3.2: Entry Generation & Review
**LLM Action:**
```
Based on your inputs, I'll now generate the wildcard entries.

Using:
- Template: [TEMPLATE_FROM_LLM_PROMPTS.json]
- Category: [CATEGORY]
- Verbosity: [VERBOSITY_LEVEL]
- Count: [TARGET_COUNT]
- Focus: [USER_REQUIREMENTS]

Generating entries...
```

**LLM Output:**
```
Here are the generated entries:

[DISPLAY_ENTRIES_WITH_NUMBERS]

Analysis:
- Total entries: [COUNT]
- Verbosity consistency: [CHECK_WORD_COUNTS]
- Variety score: [ASSESS_UNIQUENESS]
- Quality issues: [IDENTIFY_PROBLEMS]

See WILDCARD_GENERATION_GUIDE.md > Quality Guidelines for evaluation criteria.

Would you like to:
A) Approve all entries
B) Regenerate specific entries (specify which numbers)
C) Add more entries
D) Remove some entries
E) Adjust overall direction

Your choice:
```

**Validation Loop:**
- Check each entry against quality guidelines from `WILDCARD_GENERATION_GUIDE.md`
- Verify consistent verbosity
- Identify near-duplicates
- Flag abstract concepts
- Flag contradictory terms

---

### Question 3.3: Diversity Check
**LLM Prompt:**
```
Let's ensure your collection has good variety. I'll analyze the entries:

Coverage Analysis:
- [SUBCATEGORY_1]: [COUNT] entries ([PERCENTAGE]%)
- [SUBCATEGORY_2]: [COUNT] entries ([PERCENTAGE]%)
- [SUBCATEGORY_3]: [COUNT] entries ([PERCENTAGE]%)

Balance Assessment: [BALANCED/IMBALANCED]

Recommendations:
[IF_IMBALANCED]: Consider adding [X] more entries for [UNDERREPRESENTED_SUBCATEGORY]
[IF_REPETITIVE]: Some entries are very similar, consider replacing: [LIST]

See WILDCARD_GENERATION_GUIDE.md > Best Practices > "Include Variety"

Would you like to:
A) Add entries for underrepresented areas
B) Replace similar entries
C) Continue as is

Your choice:
```

---

## Phase 4: Quality Assurance

### Question 4.1: Verbosity Consistency Check
**LLM Analysis:**
```
Checking verbosity consistency...

Target: [VERBOSITY_LEVEL] ([WORD_COUNT_RANGE] words)

Entry Analysis:
? [COUNT] entries in target range
?? [COUNT] entries too short: [LIST_ENTRIES]
?? [COUNT] entries too long: [LIST_ENTRIES]

See WILDCARD_GENERATION_GUIDE.md > Quality Guidelines > "Maintain Consistent Verbosity"

Would you like me to adjust the flagged entries to match the target verbosity?
(Y/N):
```

---

### Question 4.2: Quality Guidelines Check
**LLM Analysis:**
```
Running quality checks based on WILDCARD_GENERATION_GUIDE.md...

? Concrete, visual descriptions: [PASS/FAIL]
? Observable characteristics: [PASS/FAIL]
? Specific language: [PASS/FAIL]
? Issues found:

[IF_ABSTRACT_CONCEPTS]:
- Entry #[X]: "[ENTRY]" contains abstract concept "[CONCEPT]"
  Suggestion: "[IMPROVED_VERSION]"

[IF_VAGUE_TERMS]:
- Entry #[Y]: "[ENTRY]" uses vague term "[TERM]"
  Suggestion: "[IMPROVED_VERSION]"

[IF_CONTRADICTIONS]:
- Entry #[Z]: "[ENTRY]" has contradictory terms
  Suggestion: "[IMPROVED_VERSION]"

[IF_PROMPT_SYNTAX]:
- Entry #[W]: "[ENTRY]" contains prompt syntax
  Suggestion: Remove technical terms, focus on description

See WILDCARD_GENERATION_GUIDE.md > Quality Guidelines > Do's and Don'ts

Would you like me to fix these issues automatically?
(Y/N):
```

---

### Question 4.3: Duplicate Detection
**LLM Analysis:**
```
Checking for duplicates and near-duplicates...

Potential duplicates found:
[IF_DUPLICATES]:
- Entry #[X]: "[ENTRY_1]"
- Entry #[Y]: "[ENTRY_2]"
  Similarity: [PERCENTAGE]% (threshold: 80%)
  Recommendation: Keep one, replace or remove the other

[IF_NO_DUPLICATES]:
? No duplicates detected

See WILDCARD_GENERATION_GUIDE.md > Don'ts > "Don't Duplicate Existing Entries"

Would you like to:
A) Auto-remove exact duplicates
B) Review similar entries manually
C) Keep all entries

Your choice:
```

---

### Question 4.4: Weight Distribution Strategy
**LLM Prompt:**
```
Now let's assign weights to control how often each entry appears.

Weight Guidelines (from WILDCARD_GENERATION_GUIDE.md):
- Common/safe options: 1.0 (default)
- Popular/desired: 1.2-1.5
- Standard options: 1.0
- Rare/experimental: 0.5-0.8
- Very rare: 0.1-0.3

Current entries with default weight (1.0):
[LIST_ALL_ENTRIES]

Distribution Strategy Options:
A) Keep all weights at 1.0 (equal probability)
B) I'll specify custom weights for specific entries
C) Auto-assign based on commonality (common=1.3, standard=1.0, rare=0.6)
D) Let me see probability calculations first

See WILDCARD_GENERATION_GUIDE.md > Best Practices > "Use Weights Strategically"

Your choice:
```

**If Option B:**
```
Please specify which entries should have custom weights:

Format: Entry number, weight, reason
Example: 5, 1.5, very popular style
Example: 12, 0.4, experimental/rare

Your custom weights:
```

**If Option D:**
```
Probability Calculations (assuming all weights = 1.0):

Total weight: [SUM_OF_WEIGHTS]
Each entry probability: [100/COUNT]%

Examples with adjusted weights:
[SHOW_SCENARIOS_WITH_DIFFERENT_WEIGHTS]

See calculation examples in EntryManager preview panel.

Now choose: A, B, or C
```

---

## Phase 5: Export & Finalization

### Question 5.1: Final Review
**LLM Summary:**
```
Let's review your complete wildcard collection:

COLLECTION METADATA:
- Name: [COLLECTION_NAME]
- Category: [CATEGORY]
- Description: [DESCRIPTION]
- Verbosity: [VERBOSITY_LEVEL]
- Entry Count: [COUNT]

QUALITY METRICS:
? Verbosity consistency: [PERCENTAGE]%
? No duplicates: [YES/NO]
? Visual descriptions: [YES/NO]
? Variety score: [SCORE]/10
? All quality checks passed: [YES/NO]

SAMPLE ENTRIES (first 5):
1. [ENTRY] (weight: [WEIGHT])
2. [ENTRY] (weight: [WEIGHT])
3. [ENTRY] (weight: [WEIGHT])
4. [ENTRY] (weight: [WEIGHT])
5. [ENTRY] (weight: [WEIGHT])

EXAMPLE USAGE:
Prompt: "portrait of a woman, __[COLLECTION_NAME]__"
Possible expansion: "portrait of a woman, [RANDOM_ENTRY]"

See complete structure in WILDCARD_TEMPLATE.json

Are you satisfied with this collection?
A) Yes, proceed to export
B) No, I want to make changes (specify what)

Your choice:
```

---

### Question 5.2: Export Format
**LLM Prompt:**
```
Your collection is ready! Choose export format:

A) JSON (recommended) - Full metadata, weights, ready for import
   Format: WILDCARD_TEMPLATE.json structure
   
B) Text (.txt) - Simple list, one entry per line
   Note: Loses weight information, requires manual metadata entry on import

C) Both formats

See import examples in Documentation/Examples/

Your choice:
```

---

### Question 5.3: JSON Export (if chosen)
**LLM Output:**
```json
{
  "collection": {
    "name": "[COLLECTION_NAME]",
    "category": "[CATEGORY]",
    "description": "[DESCRIPTION]",
    "entries": [
      {
        "value": "[ENTRY]",
        "weight": [WEIGHT],
        "sortOrder": 0
      },
      ...
    ]
  }
}
```

**LLM Instructions:**
```
Save this as: [COLLECTION_NAME].json

To import:
1. Copy the JSON above
2. Save to a .json file
3. Go to Wildcards tab in the application
4. Click "Import" button
5. Select your JSON file
6. Preview and confirm import

See WILDCARD_GENERATION_GUIDE.md > LLM Generation > "Import"
```

---

### Question 5.4: Text Export (if chosen)
**LLM Output:**
```
[ENTRY_1]
[ENTRY_2]
[ENTRY_3]
...
```

**LLM Instructions:**
```
Save this as: [COLLECTION_NAME].txt

To import:
1. Copy the text above
2. Save to a .txt file
3. Go to Wildcards tab in the application
4. Click "Import" button
5. Select your .txt file
6. Manually enter metadata:
   - Name: [COLLECTION_NAME]
   - Category: [CATEGORY]
   - Description: [DESCRIPTION]
7. Confirm import

Note: Weights default to 1.0, you can adjust them after import.
```

---

### Question 5.5: Documentation & Tips
**LLM Output:**
```
?? Congratulations! Your wildcard collection is complete!

NEXT STEPS:
1. Import the collection using the steps above
2. Test it in your image generation prompts
3. Adjust weights based on results
4. Consider creating complementary collections

USAGE TIPS:
- Use in prompts: __[COLLECTION_NAME]__
- Combine with other wildcards for variety
- Test with different base prompts
- Monitor which entries appear most often

MAINTENANCE:
- Review collection after 10-20 generations
- Add new entries based on gaps you notice
- Adjust weights for better probability distribution
- Remove entries that don't work well

EXPAND YOUR LIBRARY:
Consider creating related collections:
[SUGGEST_RELATED_COLLECTIONS_BASED_ON_CATEGORY]

For more help:
- WILDCARD_GENERATION_GUIDE.md - Complete guide
- THEME_CATALOG.json - Category reference
- LLM_PROMPTS.json - Generation templates

Happy generating! ??
```

---

## Quick Reference for LLMs

### Validation Checklist
Use this at each phase to ensure quality:

**Phase 1 Validations:**
- [ ] Theme is clear and focused
- [ ] Category matches THEME_CATALOG.json
- [ ] Scope is appropriate (not too broad)
- [ ] Target count is reasonable (10-40)

**Phase 2 Validations:**
- [ ] Verbosity level chosen
- [ ] Collection name follows kebab-case format
- [ ] Description is clear and under 200 characters
- [ ] Subcategory is valid (if specified)

**Phase 3 Validations:**
- [ ] All entries match verbosity level
- [ ] No duplicates or near-duplicates
- [ ] Entries use concrete, visual language
- [ ] No abstract concepts
- [ ] No prompt syntax (quality tags)
- [ ] Good variety across subcategories

**Phase 4 Validations:**
- [ ] Consistent word counts
- [ ] No quality guideline violations
- [ ] Weights assigned appropriately
- [ ] Probability distribution is reasonable

**Phase 5 Validations:**
- [ ] JSON structure matches WILDCARD_TEMPLATE.json
- [ ] All required fields present
- [ ] sortOrder increments correctly (0, 1, 2, ...)
- [ ] Ready for import

---

## Common Pitfalls & Resolutions

### Pitfall 1: Too Broad Scope
**Detection:** User wants "all clothing items" or "everything related to X"
**Resolution:** Guide to focused subcollections
**Reference:** WILDCARD_GENERATION_GUIDE.md > Best Practices > "Group Related Concepts"

### Pitfall 2: Inconsistent Verbosity
**Detection:** Word counts vary wildly (some 2 words, some 15 words)
**Resolution:** Show word count analysis, offer to normalize
**Reference:** WILDCARD_GENERATION_GUIDE.md > Quality Guidelines > "Maintain Consistent Verbosity"

### Pitfall 3: Abstract Concepts
**Detection:** Entries like "feeling of happiness" or "beautiful atmosphere"
**Resolution:** Convert to visual descriptions: "bright smile" or "soft warm lighting"
**Reference:** WILDCARD_GENERATION_GUIDE.md > Don'ts > "Avoid Vague or Abstract Concepts"

### Pitfall 4: Prompt Syntax in Entries
**Detection:** Entries contain "high quality, detailed, 8k, masterpiece"
**Resolution:** Explain these belong in base prompt, not wildcards
**Reference:** WILDCARD_GENERATION_GUIDE.md > Don'ts > "Don't Include Prompt Syntax"

### Pitfall 5: Duplicates or Near-Duplicates
**Detection:** "red dress" and "crimson dress" both present
**Resolution:** Keep more specific one, or combine: "crimson red dress"
**Reference:** WILDCARD_GENERATION_GUIDE.md > Don'ts > "Don't Duplicate Existing Entries"

### Pitfall 6: Poor Weight Distribution
**Detection:** All rare items with weight 1.5, common items with 0.3
**Resolution:** Explain inverse relationship, suggest flipping
**Reference:** WILDCARD_GENERATION_GUIDE.md > Best Practices > "Use Weights Strategically"

---

## Example Wizard Session (Abbreviated)

```
LLM: Hello! I'll help you create a high-quality wildcard collection. What type of images are you generating?

User: I want to create portraits with different hairstyles

LLM: Great! This falls under "Character Features" category. The typical verbosity for this category is "balanced" (3-6 words).
Available subcategories include: hair-styles, hair-colors, facial-features, etc.
Would you like to focus on hair-styles specifically or include hair colors too?

User: Just hairstyles

LLM: Perfect! How many entries? I recommend 15-25 for a good variety.

User: 20 entries

LLM: Now choose verbosity:
1. MINIMAL: "short hair"
2. BALANCED: "short pixie cut" (recommended)
3. DETAILED: "short stylish pixie cut with side-swept bangs"
4. VERBOSE: "short stylish pixie cut with asymmetric side-swept bangs and layered texture"

User: 2

LLM: Excellent choice. Let's name it. Format: lowercase-with-hyphens
Suggestion: "portrait-hairstyles" or "hair-styles"

User: hair-styles

LLM: Great! Brief description (under 200 chars):

User: Variety of hairstyle descriptions for portrait generation

LLM: Perfect! Would you like to provide 3-5 example entries in balanced verbosity (3-6 words) to set the style?

User: Yes
1. short pixie cut
2. long flowing waves
3. neat professional bun

LLM: Excellent examples! I'll generate 17 more entries matching your style...

[Generated entries...]

Analysis:
? All entries 3-6 words
? Good variety (short, long, curly, straight, formal, casual)
? Concrete, visual descriptions
? No duplicates

Would you like to adjust anything?

User: Looks good!

LLM: Great! Assigning default weight 1.0 to all entries. Want to customize weights?

User: No, keep default

LLM: Final review:
- Name: hair-styles
- Category: Character Features
- 20 entries, balanced verbosity
- All quality checks passed ?

Export as JSON?

User: Yes

LLM: [JSON output]...
Done! Import this in the Wildcards tab. Happy generating! ??
```

---

## Templates for LLM Responses

### Encouraging Feedback
```
Great choice! That aligns perfectly with [REFERENCE_FROM_DOCS].
```

### Corrective Feedback (Gentle)
```
I notice [ISSUE]. According to WILDCARD_GENERATION_GUIDE.md, it's better to [SOLUTION].
Would you like me to help adjust this?
```

### Validation Success
```
? Perfect! This entry follows all quality guidelines.
```

### Validation Failure
```
?? Issue detected: [PROBLEM]
Recommendation: [SOLUTION]
Reference: [DOC_SECTION]
```

### Progress Indicators
```
Phase [X]/5: [PHASE_NAME] [PROGRESS_BAR]
Next: [NEXT_STEP]
```

---

## Metadata for LLM Context

When using this wizard, LLMs should maintain context about:

```json
{
  "wizard_state": {
    "phase": 1-5,
    "category": "from THEME_CATALOG.json",
    "verbosity": "minimal|balanced|detailed|verbose",
    "target_count": 10-40,
    "collection_name": "kebab-case-name",
    "description": "brief description",
    "entries": [],
    "weights": {},
    "quality_checks": {
      "verbosity_consistent": true/false,
      "no_duplicates": true/false,
      "visual_descriptions": true/false,
      "variety_score": 0-10
    }
  }
}
```

---

## Success Criteria

A successful wizard session produces:

? A focused, well-scoped collection  
? Consistent verbosity across all entries  
? High-quality, visual, concrete descriptions  
? Good variety within the theme  
? Appropriate weight distribution  
? Valid JSON structure ready for import  
? User understands how to use the collection  

---

**Version:** 1.0  
**Last Updated:** Phase 3, Step 13  
**Compatible With:** All wildcard documentation files
