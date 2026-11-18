---
name: wildcard-create
description: Create a new wildcard collection or expand an existing one. Guides you through discovery, structure definition, content generation, quality assurance, and export using the wildcard documentation system. This is Step 1 of 2. Run wildcard-create next to write the JSON file.
disable-model-invocation: true
---
# Wildcard Creation Wizard -- Define

You are guiding a user through creating a high-quality wildcard collection for AI image generation prompts. Your primary output is a validated, import-ready JSON file following the `WILDCARD_TEMPLATE.json` structure. You do **not** invent conventions — you follow the documented system in `Documentation/Wildcards/`.

---

## Reference Materials

Read before starting the wizard:

- `Documentation/Wildcards/WILDCARD_CREATION_WIZARD.md` -- 5-phase wizard flow with question templates, validation checklists, and LLM response patterns
- `Documentation/Wildcards/WILDCARD_GENERATION_GUIDE.md` -- Complete guide covering verbosity levels, theme categories, quality guidelines, best practices, and troubleshooting
- `Documentation/Wildcards/LLM_PROMPTS.json` -- Tested prompt templates for AI-assisted generation (basic_generation, themed_expansion, quality_enhancement, category_focused, diversity_focused)
- `Documentation/Wildcards/THEME_CATALOG.json` -- 10 theme categories with subcategories, keywords, verbosity examples, sample collections, and best practices per category
- `Documentation/Wildcards/WILDCARD_TEMPLATE.json` -- JSON structure template with required fields, weight instructions, and validation checklist

---

## Phase 1: Discovery & Planning

Ask this group first. Wait for answers before proceeding.

> I'll help you create a high-quality wildcard collection. Let's start by understanding your needs.
>
> 1. **Collection purpose** -- What type of images are you generating, and what aspect do you want to vary?
>    Examples: "portraits with different facial expressions", "landscapes with varied lighting", "randomized clothing styles"
> 2. **Scope** -- How specific or broad? (A) Very focused, (B) Moderately focused, (C) Broad coverage
>    Recommendation: Start with A or B for better quality and usability.
> 3. **Target count** -- How many entries? (10-40 recommended; quality over quantity)

Derive and propose:

- `Category` -- Match to one of the 10 categories from `THEME_CATALOG.json`:
  1. Fashion & Clothing, 2. Locations & Settings, 3. Artistic Styles, 4. Lighting & Atmosphere, 5. Character Features, 6. Actions & Poses, 7. Objects & Props, 8. Colors & Palettes, 9. Emotions & Expressions, 10. Composition & Framing
- `Subcategory` -- Optional focus within the category (from `THEME_CATALOG.json`)

Present the matched category with its subcategories and typical verbosity from the catalog:

> Based on your description, this falls under **[CATEGORY_NAME]**.
> Subcategories available: [LIST]
> Typical verbosity: [TYPICAL_VERBOSITY]
> Does this sound correct?

Ask: **"Does this scope look right before we define the structure?"**

---

## Phase 2: Structure Definition

### 2a -- Verbosity Level

Present the 4 verbosity levels with examples from `WILDCARD_GENERATION_GUIDE.md`:

> Choose the verbosity level for your entries:
>
> 1. **MINIMAL** (1-3 words) -- "red dress" | Quick variations, simple tags
> 2. **BALANCED** (3-6 words) -- "elegant red evening dress" | RECOMMENDED, versatile use
> 3. **DETAILED** (6-12 words) -- "elegant flowing red evening dress with lace details" | Scene building
> 4. **VERBOSE** (12+ words) -- Full cinematic descriptions | Maximum detail

Show the category's typical verbosity recommendation from `THEME_CATALOG.json`.

### 2b -- Collection Name

Naming conventions:

- Use lowercase with hyphens (kebab-case): `facial-expressions`, `dramatic-lighting`
- Be descriptive and specific, use plural form when appropriate
- Keep it short (2-4 words max)
- The name is used in prompts as `__collection-name__`

### 2c -- Description

Under 200 characters. Explain what the collection contains and when to use it.

Confirm all structure details before moving on.

---

## Phase 3: Content Generation

Ask how entries should be created:

> How would you like to create the entries?
>
> A) You provide 3-5 examples, I generate the rest (RECOMMENDED)
> B) I generate all entries based on requirements
> C) You provide all entries manually

### Path A -- Example-Guided Generation

Request 3-5 example entries matching the chosen verbosity. Validate them against quality guidelines before generating the remainder using `LLM_PROMPTS.json` basic_generation template patterns.

### Path B -- Full Generation

Gather specifics:

1. What aspects must be covered?
2. Any items that MUST be included?
3. Any items to AVOID?

Generate entries following the `basic_generation` or `category_focused` template from `LLM_PROMPTS.json`.

### Path C -- Manual Entry

Collect all entries and validate against quality guidelines.

### Quality Validation (All Paths)

After generating entries, run these checks from `WILDCARD_CREATION_WIZARD.md`:

- **Verbosity consistency** -- All entries match target word count range
- **Concrete visual descriptions** -- No abstract concepts
- **No duplicates or near-duplicates** -- Similarity threshold 80%
- **No prompt syntax** -- No "high quality, 8k, masterpiece" tags
- **Variety across subcategories** -- Good coverage distribution

Present the analysis:

> Generated [COUNT] entries.
> Verbosity consistency: [PASS/FAIL]
> Variety score: [SCORE]/10
> Quality issues found: [LIST or "None"]
>
> Would you like to: (A) Approve, (B) Regenerate specific entries, (C) Add more, (D) Remove some

---

## Phase 4: Weight Distribution

Present weight strategy options from `WILDCARD_GENERATION_GUIDE.md`:

> Assign weights to control entry probability:
>
> - Common/popular: 1.2-1.5
> - Standard: 1.0 (default)
> - Rare/experimental: 0.5-0.8
> - Very rare: 0.1-0.3
>
> Options:
> A) Keep all at 1.0 (equal probability)
> B) Specify custom weights for specific entries
> C) Auto-assign based on commonality

---

## Phase 5: Export & Finalization

### 5a -- Final Review

Present the complete collection summary:

```
COLLECTION: [name]
CATEGORY: [category]
DESCRIPTION: [description]
VERBOSITY: [level]
ENTRIES: [count]

QUALITY METRICS:
- Verbosity consistency: [PASS/FAIL]
- No duplicates: [YES/NO]
- Visual descriptions: [YES/NO]
- Variety score: [SCORE]/10
```

### 5b -- Generate JSON

Produce the final JSON following `WILDCARD_TEMPLATE.json` structure. The output must be a valid JSON object with only the `collection` block (removing all `_comment`, `_instructions`, and template fields):

```json
{
  "collection": {
    "name": "kebab-case-name",
    "category": "Category Name",
    "description": "Brief description under 200 characters",
    "entries": [
      {"value": "...", "weight": 1.0, "sortOrder": 0},
      ...
    ]
  }
}
```

### 5c -- Save File

Save the JSON to `BlazorWebApp/Data/Wildcards/{collection-name}.json` or ask the user for a preferred save location. Validate the JSON parses correctly before confirming.

---

## Common Pitfalls To Guard Against

Reference from `WILDCARD_CREATION_WIZARD.md`:

| Pitfall                  | Detection                     | Resolution                                 |
| ------------------------ | ----------------------------- | ------------------------------------------ |
| Too broad scope          | "all clothing items"          | Guide to focused subcollections            |
| Inconsistent verbosity   | Word counts vary wildly       | Show analysis, offer normalization         |
| Abstract concepts        | "feeling of happiness"        | Convert to visual: "bright smile"          |
| Prompt syntax in entries | Contains quality tags         | Remove, explain they belong in base prompt |
| Near-duplicates          | "red dress" + "crimson dress" | Keep more specific one                     |
| Poor weight distribution | Rare items weighted higher    | Explain inverse relationship               |

---

## Success Criteria

A completed session produces:

- A focused, well-scoped collection matching a `THEME_CATALOG.json` category
- Consistent verbosity across all entries
- High-quality, visual, concrete descriptions with no abstract concepts
- Good variety within the theme
- Appropriate weight distribution
- Valid JSON file ready for import through the Wildcards tab
