# Wildcard Generation Guide

## Overview

This guide provides comprehensive instructions for creating high-quality wildcard collections for AI image generation prompts. Whether you're creating collections manually or using LLM assistance (Ollama, ChatGPT, etc.), these guidelines ensure consistent, effective results.

---

## Table of Contents

1. [What Are Wildcards?](#what-are-wildcards)
2. [Verbosity Levels](#verbosity-levels)
3. [Theme Categories](#theme-categories)
4. [Quality Guidelines](#quality-guidelines)
5. [LLM Generation](#llm-generation)
6. [Best Practices](#best-practices)
7. [Examples](#examples)

---

## What Are Wildcards?

Wildcards are randomizable text snippets that add variety to AI image generation prompts. They follow the pattern `__collection-name__` and are replaced with random entries from the specified collection.

### Syntax

```
__collection-name__
```

### Example Usage

**Prompt:**
```
portrait of a woman wearing __clothing-tops__, __facial-expression__, __lighting-style__
```

**Expanded (possible result):**
```
portrait of a woman wearing elegant silk blouse, gentle smile, soft golden hour lighting
```

---

## Verbosity Levels

Choose the appropriate verbosity level based on your use case:

### Minimal (1-3 words)
**Best for:** Quick variations, simple combinations, tag-like elements

**Examples:**
- `red dress`
- `casual outfit`
- `formal wear`
- `winter coat`

**Use Cases:**
- Base clothing items
- Simple colors
- Basic objects
- Quick modifiers

---

### Balanced (3-6 words) ? **RECOMMENDED**
**Best for:** Standard prompt building, versatile applications, general use

**Examples:**
- `elegant red evening dress`
- `casual denim jacket outfit`
- `professional business formal wear`
- `cozy winter wool coat`

**Use Cases:**
- General character descriptions
- Fashion and clothing
- Common expressions
- Standard poses and actions
- Most artistic styles

---

### Detailed (6-12 words)
**Best for:** Specific imagery, scene building, rich descriptions

**Examples:**
- `elegant flowing red evening dress with lace details`
- `casual distressed denim jacket with white t-shirt underneath`
- `professional dark navy business suit with crisp white shirt`
- `cozy oversized winter wool coat with fur-lined hood`

**Use Cases:**
- Detailed scene descriptions
- Complex character outfits
- Environmental atmospheres
- Lighting and mood setting
- Camera composition details

---

### Verbose (12+ words)
**Best for:** Highly specific scenes, story-driven generation, maximum detail

**Examples:**
- `elegant flowing red evening dress with intricate lace details, silk fabric, and a dramatic train trailing behind`
- `casual vintage distressed denim jacket worn over a simple white t-shirt, paired with comfortable jeans and sneakers`
- `professional tailored dark navy business suit with crisp white dress shirt, silk tie, and polished leather shoes`
- `cozy oversized winter wool coat with thick fur-lined hood, brass buttons, and deep side pockets`

**Use Cases:**
- Cinematic scenes
- Novel-style descriptions
- Maximum specificity needed
- Complex multi-element compositions

---

## Theme Categories

### 1. Fashion & Clothing
**Typical Verbosity:** Balanced  
**Subcategories:** tops, bottoms, shoes, accessories, outfits, styles

**Examples:**
- `vintage leather motorcycle jacket`
- `flowing summer maxi dress`
- `tailored pinstripe business suit`

---

### 2. Locations & Settings
**Typical Verbosity:** Detailed  
**Subcategories:** indoor, outdoor, fantasy, historical, modern, natural

**Examples:**
- `ancient stone castle on a misty mountain peak`
- `modern glass and steel office building in downtown`
- `cozy wooden cabin in snowy pine forest`

---

### 3. Artistic Styles
**Typical Verbosity:** Balanced  
**Subcategories:** medium, technique, movement, era, influence

**Examples:**
- `impressionist oil painting style`
- `detailed graphite pencil sketch`
- `vibrant pop art aesthetic`

---

### 4. Lighting & Atmosphere
**Typical Verbosity:** Detailed  
**Subcategories:** natural, artificial, mood, time-of-day, weather

**Examples:**
- `soft golden hour sunlight filtering through trees`
- `dramatic rim lighting with deep shadows`
- `ethereal blue moonlight illuminating the scene`

---

### 5. Character Features
**Typical Verbosity:** Balanced  
**Subcategories:** hair, eyes, body, expression, age, ethnicity

**Examples:**
- `long flowing auburn hair`
- `piercing blue eyes`
- `gentle warm smile`

---

### 6. Actions & Poses
**Typical Verbosity:** Balanced  
**Subcategories:** standing, sitting, dynamic, static, interactions

**Examples:**
- `leaning casually against a wall`
- `dynamic running pose mid-stride`
- `sitting cross-legged on the floor`

---

### 7. Objects & Props
**Typical Verbosity:** Minimal  
**Subcategories:** furniture, technology, tools, decorative, functional

**Examples:**
- `vintage typewriter`
- `ornate picture frame`
- `modern smartphone`

---

### 8. Colors & Palettes
**Typical Verbosity:** Minimal  
**Subcategories:** primary, secondary, combinations, moods, schemes

**Examples:**
- `vibrant red`
- `pastel pink`
- `deep navy blue`

---

### 9. Emotions & Expressions
**Typical Verbosity:** Balanced  
**Subcategories:** positive, negative, neutral, complex, subtle

**Examples:**
- `gentle warm smile`
- `contemplative thoughtful gaze`
- `excited joyful expression`

---

### 10. Composition & Framing
**Typical Verbosity:** Balanced  
**Subcategories:** shot-type, angle, perspective, focus, depth

**Examples:**
- `medium shot from eye level`
- `dramatic low angle looking up`
- `tight close-up on face`

---

## Quality Guidelines

### ? Do's

1. **Use Concrete, Visual Descriptions**
   - ? `glowing sunset sky with orange and purple hues`
   - ? `beautiful sky`

2. **Focus on Observable Characteristics**
   - ? `tight-fitting leather jacket with silver zippers`
   - ? `cool jacket`

3. **Maintain Consistent Verbosity Within Collection**
   - All entries should be similar in length and detail level

4. **Include Variety**
   - Cover different aspects, perspectives, and styles within the theme

5. **Use Specific, Precise Language**
   - ? `vintage leather motorcycle jacket`
   - ? `old jacket`

6. **Test Entries Work in Prompts**
   - Verify each entry produces expected results

7. **Balance Common and Unique Options**
   - Mix popular choices with creative variations

8. **Consider Weight Distribution**
   - Use weights (0.1-2.0) to control probability of selection

---

### ? Don'ts

1. **Avoid Vague or Abstract Concepts**
   - ? `feeling of nostalgia`
   - ? `sepia-toned vintage photograph aesthetic`

2. **Don't Use Contradictory Terms**
   - ? `bright dark lighting`

3. **Avoid Excessive Redundancy**
   - ? `red crimson scarlet dress`
   - ? `crimson red evening dress`

4. **Don't Include Prompt Syntax**
   - ? `high quality, detailed, 8k`
   - ? Focus on descriptive content only

5. **Avoid Overly Complex Nested Descriptions**
   - ? `jacket (leather, vintage, with zippers (silver, large))`
   - ? `vintage leather jacket with large silver zippers`

6. **Don't Mix Verbosity Levels Randomly**
   - Keep all entries in a collection at the same verbosity level

7. **Avoid Non-Visual Concepts**
   - ? `sounds of birds singing`
   - ? `birds in flight with open beaks`

8. **Don't Duplicate Existing Entries**
   - Check for near-duplicates before adding

---

## LLM Generation

### Using Ollama or Other LLMs

LLMs can generate high-quality wildcard collections quickly. See `LLM_PROMPTS.json` for tested prompt templates.

### Basic Generation Workflow

1. **Define Your Goal**
   - Theme, category, target count, verbosity level

2. **Load Template**
   - Use templates from `WILDCARD_TEMPLATE.json`

3. **Customize Prompt**
   - Fill in theme, keywords, constraints

4. **Generate**
   - Use LLM (Ollama, ChatGPT, Claude, etc.)

5. **Review & Refine**
   - Check quality, remove duplicates, adjust weights

6. **Import**
   - Save as JSON and import into the system

### Example Prompt

```
Generate 20 balanced verbosity wildcard entries for "facial-expressions" collection.
Theme: Human facial expressions and emotions
Category: Character Features
Verbosity: Balanced (3-6 words)
Focus: Variety of emotions, both subtle and obvious

Rules:
- Each entry should be 3-6 words
- Visual, observable descriptions only
- No abstract concepts
- Include positive, negative, and neutral emotions
- Maintain variety

Output format: JSON array with "value" and "weight" properties.
Example: {"value": "gentle warm smile", "weight": 1.0}
```

---

## Best Practices

### 1. Start with a Plan
- Define theme and category clearly
- Choose appropriate verbosity level
- Set target entry count (10-30 recommended)

### 2. Group Related Concepts
- Organize entries by subcategory
- Use consistent naming conventions
- Consider creating multiple focused collections vs. one large one

### 3. Test and Iterate
- Generate sample images using your wildcards
- Refine entries based on results
- Adjust weights for better probability distribution

### 4. Document Your Collections
- Add clear descriptions
- Use meaningful category names
- Include keywords/tags for searchability

### 5. Maintain Quality Over Quantity
- 15 high-quality entries > 50 mediocre ones
- Remove duplicates and near-duplicates
- Focus on useful, practical variations

### 6. Consider Context
- Think about how entries will combine with other prompts
- Ensure entries work standalone
- Avoid dependencies on other wildcards

### 7. Use Weights Strategically
- Common/safe options: 1.0 (default)
- Rare/experimental: 0.3-0.7
- Popular/desired: 1.2-1.5
- Very rare: 0.1-0.2

---

## Examples

### Example 1: Simple Color Collection

**Collection:** `basic-colors`  
**Category:** Colors  
**Verbosity:** Minimal  
**Description:** Basic color names for general use

**Entries:**
```json
[
  {"value": "red", "weight": 1.0, "sortOrder": 0},
  {"value": "blue", "weight": 1.0, "sortOrder": 1},
  {"value": "green", "weight": 1.0, "sortOrder": 2},
  {"value": "yellow", "weight": 1.0, "sortOrder": 3},
  {"value": "purple", "weight": 1.0, "sortOrder": 4},
  {"value": "orange", "weight": 1.0, "sortOrder": 5},
  {"value": "pink", "weight": 1.0, "sortOrder": 6},
  {"value": "black", "weight": 1.0, "sortOrder": 7},
  {"value": "white", "weight": 1.0, "sortOrder": 8},
  {"value": "gray", "weight": 1.0, "sortOrder": 9}
]
```

---

### Example 2: Detailed Lighting Collection

**Collection:** `cinematic-lighting`  
**Category:** Lighting  
**Verbosity:** Detailed  
**Description:** Cinematic lighting styles for dramatic scenes

**Entries:**
```json
[
  {"value": "dramatic rim lighting with deep shadows and highlights", "weight": 1.0, "sortOrder": 0},
  {"value": "soft golden hour sunlight filtering through windows", "weight": 1.2, "sortOrder": 1},
  {"value": "harsh overhead fluorescent lighting with stark shadows", "weight": 0.8, "sortOrder": 2},
  {"value": "ethereal blue moonlight illuminating the scene from above", "weight": 1.0, "sortOrder": 3},
  {"value": "warm candlelight flickering with orange and amber tones", "weight": 1.1, "sortOrder": 4},
  {"value": "neon city lights reflecting off wet surfaces", "weight": 1.0, "sortOrder": 5}
]
```

---

### Example 3: Balanced Fashion Collection

**Collection:** `casual-outfits`  
**Category:** Fashion & Clothing  
**Verbosity:** Balanced  
**Description:** Casual everyday outfit descriptions

**Entries:**
```json
[
  {"value": "comfortable jeans and t-shirt", "weight": 1.5, "sortOrder": 0},
  {"value": "casual denim jacket over hoodie", "weight": 1.2, "sortOrder": 1},
  {"value": "relaxed fit khaki pants", "weight": 1.0, "sortOrder": 2},
  {"value": "cozy oversized sweater outfit", "weight": 1.3, "sortOrder": 3},
  {"value": "athletic joggers and sneakers", "weight": 1.0, "sortOrder": 4},
  {"value": "vintage band t-shirt ensemble", "weight": 0.8, "sortOrder": 5},
  {"value": "flannel shirt layered look", "weight": 1.0, "sortOrder": 6}
]
```

---

## Tips for Success

### For Manual Creation
1. Write 3-5 entries as examples
2. Identify the pattern and verbosity
3. Brainstorm variations maintaining that pattern
4. Review for duplicates and consistency
5. Test with actual prompts

### For LLM Generation
1. Use specific, detailed prompts
2. Provide 2-3 example entries in your format
3. Specify exact verbosity level
4. Request JSON output format
5. Review and refine the results
6. Regenerate individual entries if needed

### For Both Approaches
- **Start small:** 10-15 entries per collection
- **Expand gradually:** Add entries based on actual usage needs
- **Categorize thoughtfully:** Use clear, searchable categories
- **Weight strategically:** Adjust probabilities after testing
- **Document thoroughly:** Clear names and descriptions help future use

---

## Troubleshooting

### Problem: Entries too similar
**Solution:** Increase variety by exploring different aspects of the theme. Consider subcategories.

### Problem: Inconsistent verbosity
**Solution:** Review all entries and standardize length. Use word count as a guide.

### Problem: LLM produces low-quality results
**Solution:** Refine prompt with more specific rules, provide better examples, try different temperature settings.

### Problem: Collection too large
**Solution:** Split into focused subcollections. Example: `clothing-tops` instead of `all-clothing`.

### Problem: Weights not working as expected
**Solution:** Ensure total weights are balanced. Very low weights (< 0.3) rarely appear. Adjust relative to collection size.

---

## Resources

- **Template Files:** `WILDCARD_TEMPLATE.json`
- **LLM Prompts:** `LLM_PROMPTS.json`
- **Theme Catalog:** `THEME_CATALOG.json`
- **Sample Collections:** See `Documentation/Examples/` folder

---

## Need Help?

- Check existing sample collections for inspiration
- Review theme catalog for category ideas
- Use LLM prompt templates as starting points
- Start with small, focused collections
- Test frequently with actual image generation

---

**Last Updated:** Phase 3, Step 13  
**Version:** 1.0
