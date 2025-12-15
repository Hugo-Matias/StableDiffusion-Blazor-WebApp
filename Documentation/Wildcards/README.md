# Wildcard Documentation Index

## Overview

This directory contains comprehensive documentation for creating, managing, and using wildcard collections in the AI image generation system.

---

## ?? Documentation Files

### 1. WILDCARD_GENERATION_GUIDE.md
**Purpose:** Complete user guide for wildcard creation  
**Audience:** Manual creators and LLM-assisted users  
**Size:** 500+ lines

**Contents:**
- What are wildcards and how they work
- 4 verbosity levels with examples and use cases
- 10 theme categories with subcategories
- Quality guidelines (Do's and Don'ts)
- LLM generation workflow
- Best practices for manual and AI creation
- Troubleshooting guide
- Real-world examples

**When to use:**
- Learning about wildcard system for the first time
- Reference for quality standards
- Finding examples for specific categories
- Troubleshooting common issues

---

### 2. WILDCARD_TEMPLATE.json
**Purpose:** JSON structure template with inline documentation  
**Audience:** Anyone creating JSON imports  
**Size:** Annotated template

**Contents:**
- Complete JSON structure
- Required vs optional fields
- Metadata section for tracking
- Collection structure matching import format
- Generation prompt documentation
- Validation checklist
- Verbosity examples for all levels
- Import instructions

**When to use:**
- Creating a new collection JSON file
- Understanding the import format
- Reference for field requirements
- Checking JSON structure before import

---

### 3. LLM_PROMPTS.json
**Purpose:** Tested prompt templates for AI generation  
**Audience:** LLM users (Ollama, ChatGPT, Claude)  
**Size:** 5 comprehensive templates

**Contents:**
- **5 Prompt Templates:**
  1. Basic Generation (create from scratch)
  2. Themed Expansion (add to existing)
  3. Quality Enhancement (improve entries)
  4. Category-Focused (strict adherence)
  5. Diversity-Focused (maximum variety)
- Ollama model recommendations
- Temperature and parameter guidance
- Example requests and expected outputs
- Troubleshooting for common LLM issues
- Usage tips and iteration workflows

**When to use:**
- Generating collections with LLMs
- Expanding existing collections
- Improving entry quality
- Reference for optimal LLM settings

---

### 4. THEME_CATALOG.json
**Purpose:** Complete category reference and specifications  
**Audience:** All users, especially LLMs  
**Size:** 10 categories with full metadata

**Contents:**
- **10 Main Categories:**
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
- Subcategories (50+)
- Keywords for search/discovery
- Icons for UI integration
- Typical verbosity recommendations
- Verbosity examples for each category
- Sample collections
- Best practices per category
- Category selection guide
- Cross-category usage examples

**When to use:**
- Choosing the right category
- Understanding category conventions
- Finding subcategory options
- Reference for verbosity recommendations
- Exploring example collections

---

### 5. WILDCARD_CREATION_WIZARD.md ? **NEW**
**Purpose:** Step-by-step questionnaire for LLM-guided creation  
**Audience:** LLMs guiding users through collection creation  
**Size:** Complete 5-phase wizard

**Contents:**
- **5 Phase Workflow:**
  1. Discovery & Planning (understand needs, set parameters)
  2. Structure Definition (metadata, verbosity, naming)
  3. Content Generation (create entries with quality checks)
  4. Quality Assurance (validate, refine, adjust weights)
  5. Export & Finalization (format and prepare for import)
- Question templates for each phase
- Validation checklists
- Common pitfalls and resolutions
- Example wizard session
- LLM response templates
- Success criteria

**When to use:**
- LLMs guiding users through creation
- Structured approach to collection building
- Ensuring all best practices are followed
- Step-by-step validation at each phase

**How to use (for LLMs):**
```
1. Start at Phase 1, Question 1.1
2. Ask questions in order
3. Validate responses using checklist
4. Reference other docs as needed (guide, catalog, templates)
5. Provide feedback based on guidelines
6. Progress through all 5 phases
7. Export complete, validated collection
```

---

## ?? Quick Navigation

### "I want to understand wildcards"
? Start with `WILDCARD_GENERATION_GUIDE.md`

### "I want to create a collection manually"
? Read `WILDCARD_GENERATION_GUIDE.md` > Best Practices  
? Use `WILDCARD_TEMPLATE.json` for structure  
? Check `THEME_CATALOG.json` for category conventions

### "I want to use an LLM to generate"
? Read `WILDCARD_GENERATION_GUIDE.md` > LLM Generation  
? Use `LLM_PROMPTS.json` templates  
? Reference `THEME_CATALOG.json` for categories  
? Or use `WILDCARD_CREATION_WIZARD.md` for guided approach

### "I'm an LLM helping a user"
? Use `WILDCARD_CREATION_WIZARD.md` as main workflow  
? Reference `WILDCARD_GENERATION_GUIDE.md` for guidelines  
? Check `THEME_CATALOG.json` for category details  
? Use `LLM_PROMPTS.json` for generation templates  
? Validate against `WILDCARD_TEMPLATE.json` structure

### "I need examples"
? `WILDCARD_GENERATION_GUIDE.md` > Examples section  
? `THEME_CATALOG.json` > sample_collections  
? `Documentation/Examples/` folder (sample files)

### "I have a specific problem"
? `WILDCARD_GENERATION_GUIDE.md` > Troubleshooting  
? `WILDCARD_CREATION_WIZARD.md` > Common Pitfalls  
? `LLM_PROMPTS.json` > troubleshooting section

---

## ?? Document Relationships

```
WILDCARD_CREATION_WIZARD.md (LLM workflow)
    ?? References ? WILDCARD_GENERATION_GUIDE.md (quality guidelines)
    ?? References ? THEME_CATALOG.json (categories)
    ?? References ? LLM_PROMPTS.json (generation templates)
    ?? Outputs ? WILDCARD_TEMPLATE.json (JSON structure)

WILDCARD_GENERATION_GUIDE.md (main guide)
    ?? Points to ? WILDCARD_TEMPLATE.json (format reference)
    ?? Points to ? LLM_PROMPTS.json (LLM generation)
    ?? Points to ? THEME_CATALOG.json (category details)

LLM_PROMPTS.json (generation templates)
    ?? Uses ? THEME_CATALOG.json (category conventions)
    ?? Outputs ? WILDCARD_TEMPLATE.json (format)

THEME_CATALOG.json (category reference)
    ?? Used by all other documents
```

---

## ?? Getting Started Workflows

### Workflow 1: Manual Creation (Beginner)
1. Read `WILDCARD_GENERATION_GUIDE.md` (focus on "What Are Wildcards?" and "Verbosity Levels")
2. Choose category from `THEME_CATALOG.json`
3. Write 3-5 example entries
4. Use quality guidelines to validate
5. Expand to target count
6. Format using `WILDCARD_TEMPLATE.json`
7. Import via Wildcards tab

### Workflow 2: LLM Generation (Intermediate)
1. Read `WILDCARD_GENERATION_GUIDE.md` > LLM Generation section
2. Choose template from `LLM_PROMPTS.json`
3. Fill in parameters (category from `THEME_CATALOG.json`)
4. Generate with LLM (Ollama, ChatGPT, etc.)
5. Review against quality guidelines
6. Format as JSON using `WILDCARD_TEMPLATE.json`
7. Import via Wildcards tab

### Workflow 3: Wizard-Guided Creation (Recommended)
1. LLM loads `WILDCARD_CREATION_WIZARD.md`
2. LLM guides user through 5 phases
3. Wizard automatically references other docs
4. Validation at each phase
5. Export ready-to-import JSON
6. Import via Wildcards tab

---

## ?? Tips for Success

### For Manual Creators
- Start with `WILDCARD_GENERATION_GUIDE.md`
- Use examples from `THEME_CATALOG.json`
- Validate against quality guidelines
- Keep collections focused (10-25 entries)

### For LLM Users
- Use specific, detailed prompts from `LLM_PROMPTS.json`
- Always review and refine generated content
- Check against quality guidelines manually
- Iterate if results aren't satisfactory

### For LLMs Guiding Users
- Follow `WILDCARD_CREATION_WIZARD.md` workflow
- Validate every user input
- Reference appropriate docs for context
- Be encouraging but corrective when needed
- Ensure all quality checks pass before export

---

## ?? Quality Standards Summary

All collections should meet these criteria (from `WILDCARD_GENERATION_GUIDE.md`):

? **Consistent verbosity** - All entries similar length  
? **Visual descriptions** - Observable, concrete language  
? **No duplicates** - Each entry unique  
? **Good variety** - Cover different aspects of theme  
? **Specific language** - Precise, not vague  
? **No abstractions** - Avoid non-visual concepts  
? **No prompt syntax** - No quality tags like "8k, detailed"  
? **Appropriate weights** - 0.1-2.0 range, strategic distribution  

---

## ?? Related Files

### Application Files
- `BlazorWebApp/Components/Prompts/Wildcards/WildcardsTab.razor` - Main UI
- `BlazorWebApp/Components/Prompts/Wildcards/WildcardImportDialog.razor` - Import feature
- `BlazorWebApp/Services/WildcardService.cs` - Backend service

### Example Files
- `Documentation/Examples/sample-expressions.txt` - Text import example
- `Documentation/Examples/sample-colors.json` - JSON import example

### Planning Documents
- `Documentation/Plans/prompt-page-enhancement/PHASE_3.md` - Implementation details
- `Documentation/Plans/prompt-page-enhancement/PHASE_3_COMPLETION_SUMMARY.md` - Features summary

---

## ?? Version History

**v1.0** (Phase 3, Step 13)
- Initial release
- 4 core documentation files
- 1 wizard questionnaire
- ~3,000 total lines of documentation

---

## ?? Support

### Common Questions

**Q: Which verbosity level should I use?**  
A: "Balanced" (3-6 words) is recommended for most use cases. See `WILDCARD_GENERATION_GUIDE.md` > Verbosity Levels.

**Q: How many entries should I create?**  
A: 15-25 entries is ideal. Quality > Quantity. See guide > Best Practices.

**Q: Can I use brand names or specific people?**  
A: Avoid brand names unless essential. Focus on generic, descriptive terms. See guide > Quality Guidelines.

**Q: What's the best way to generate with an LLM?**  
A: Use `WILDCARD_CREATION_WIZARD.md` for guided approach, or `LLM_PROMPTS.json` templates for direct generation.

**Q: My entries are inconsistent in length. How do I fix this?**  
A: Review word counts, use wizard's verbosity check, or manually standardize. See guide > Troubleshooting.

**Q: How do weights work?**  
A: Higher weight = more likely to appear. 1.0 is default. Range: 0.1-2.0. See guide > Best Practices > "Use Weights Strategically".

---

## ?? Learning Path

1. **Beginner:** Read `WILDCARD_GENERATION_GUIDE.md` fully
2. **Intermediate:** Try manual creation with guide as reference
3. **Advanced:** Use `LLM_PROMPTS.json` for AI-assisted generation
4. **Expert:** Guide others using `WILDCARD_CREATION_WIZARD.md`

---

**Maintained by:** Phase 3 Implementation  
**Last Updated:** Current Session  
**Status:** Complete and Production-Ready ?
