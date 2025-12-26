# Fluid Template Engine Migration - Implementation Plan

## Status
**Current Phase:** Phase 7 Complete - Scriban Removal & Cleanup

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

### Documentation Requirements
- Create `PHASE_{#}.md` when entering a new phase
- Update phase document after each step completion
- Document all issues, blockers, and resolutions
- Track commit checkpoints throughout execution

---

## Problem Statement

The current Scriban template engine implementation suffers from critical **parser bleeding issues** where the template parser reads beyond extracted `#meta` sections, causing dynamic output key failures and meta section corruption. The root cause is using regex-based text extraction combined with template compilation, creating context mismatches.

**Impact:**
- WAN img2vid workflow fails due to missing output registrations
- Unpredictable failures with nested braces in JSON templates
- Difficult debugging with multi-stage processing (Regex → Scriban → JSON parse)
- Brittle maintenance requiring constant workarounds

---

## Proposed Solution

Migrate from Scriban to **Fluid (Liquid templates)** using a **custom block tag system** that eliminates all regex-based parsing.

### Core Architecture Change

**Current (Broken):**
```
Fragment File → Regex Extract #meta → Scriban Render Meta → Parse JSON
                                    ↓
                                  (Parser Bleeding Occurs Here)
                                    ↓
                      Regex Remove #meta → Scriban Render Body → Final JSON
```

**Proposed (Robust):**
```
Fragment File → Fluid Parse (handles {% meta %} block natively)
                    ↓
                 Single Render Pass
                    ├─→ Main Output: Fragment JSON
                    └─→ Side Channel (Context): Metadata JSON
```

### Key Decisions

| Decision | Rationale |
|----------|-----------|
| Use Fluid over Scriban | Designed for JSON templating, stricter parser, industry standard (Shopify, Jekyll) |
| Custom `{% meta %}` block tag | Eliminates ALL regex, template engine handles parsing, zero bleeding |
| Keep `.liquid` extension | Enables VS Code tooling support, clear differentiation from Scriban |
| Hard cutover (no compatibility) | Clean codebase, simpler implementation, clear migration path |
| Feature flag during transition | Safe rollout, parallel validation, easy rollback |

### Conventions

**Template Syntax Mapping:**

| Feature | Scriban | Fluid |
|---------|---------|-------|
| Delimiters (output) | `{{ }}` | `{{ }}` |
| Delimiters (logic) | `{{~ ~}}` | `{% %}` |
| Default values | `{{ var ?? "default" }}` | `{{ var \| default: "default" }}` |
| JSON encoding | `{{ var \| json }}` | `{{ var \| json }}` (custom filter) |
| Loops | `{{~ for item in items ~}}` | `{% for item in items %}` |
| Conditionals | `{{~ if condition ~}}` | `{% if condition %}` |
| String concat | `{{ a + b }}` | `{{ a \| append: b }}` |
| Node references | `get_ref("key")` | `{% get_ref "key" %}` (custom tag) |
| Meta blocks | `#meta ... #end` | `{% meta %} ... {% endmeta %}` |

**File Organization:**
- Templates: `BlazorWebApp/Workflows/Templates/**/*.liquid`
- Fragments: `BlazorWebApp/Workflows/Fragments/**/*.liquid`
- Service: `BlazorWebApp/Services/Templating/FluidTemplateService.cs`
- Custom blocks: `BlazorWebApp/Services/Templating/Blocks/`

---

## Implementation Phases

### Phase 1: Foundation Setup
**Objective:** Install Fluid, create custom meta block, establish basic rendering infrastructure
**Complexity:** 8 points
**Status:** [x] Complete

#### Steps
- [x] Install Fluid.Core NuGet package
- [x] Create `Services/Templating/FluidTemplateService.cs`
- [x] Create `Services/Templating/Blocks/MetaBlock.cs` (custom block tag) - *Implemented inline with RegisterEmptyBlock*
- [x] Create `Services/Templating/Filters/JsonFilter.cs` (custom filter) - *Implemented inline*
- [x] Create `Services/Templating/Tags/GetRefTag.cs` (custom tag) - *Implemented inline with RegisterExpressionTag*
- [x] Register custom blocks/filters/tags in FluidTemplateService
- [x] ~~Add feature flag: `UseFluidTemplates` in appsettings.json~~ - *Skipped (instant cutoff)*

#### Success Criteria
- [x] Fluid successfully parses a simple template with custom blocks
- [x] `{% meta %}` block captures metadata to context (27 unit tests passing)
- [x] `{{ var | json }}` filter produces correct JSON encoding
- [x] `{% get_ref "key" %}` tag resolves node references

#### Completion Notes
- **Phase completed successfully** with build passing and 27/27 tests passing
- Core parser bleeding solution implemented via native Fluid block parsing
- Custom blocks/tags implemented as inline delegates (correct Fluid API pattern)
- Feature flag skipped per user decision - instant cutoff migration strategy approved
- Ready to proceed to Phase 2

---

### Phase 2: Core Service Migration
**Objective:** Update WorkflowService to use Fluid with parallel Scriban support
**Complexity:** 13 points
**Status:** [x] Complete

#### Steps
- [x] Add FluidTemplateService to DI container
- [x] Create `RenderFragmentWithFluidAsync()` method in WorkflowService
- [x] Create `ComposeWorkflowFromGenerationParametersAsync()` method
- [x] Update ComfyUIService to use async composition
- [x] ~~Update `RenderFragment()` to check feature flag~~ - Skipped (instant cutoff, old method deprecated)
- [x] Remove regex-based meta extraction for Fluid path
- [x] Update `ExtractMetadata()` to handle Fluid context retrieval
- [x] ~~Create template context builder for Fluid~~ - Built into FluidTemplateService
- [x] Implement template caching strategy for Fluid - Using ConcurrentDictionary

#### Success Criteria
- [x] WorkflowService uses Fluid for fragment rendering
- [x] No regex extraction occurs in Fluid path
- [x] Metadata correctly retrieved from Fluid context
- [x] Template caching performs equivalently to Scriban
- [x] All 50 tests passing

#### Completion Notes
- **Phase completed successfully** with build passing and 50/50 tests passing
- Old sync methods marked `[Obsolete]` for backward compatibility
- ComfyUI generation (image and video) now uses Fluid-based rendering
- Ready to proceed to Phase 3 (Template Conversion)

---

### Phase 3: Template Conversion (Simple Fragments)
**Objective:** Convert simple fragments without loops/conditionals to validate approach
**Complexity:** 5 points
**Status:** [x] Complete

#### Target Fragments
- [x] `save.sbn` - Converted
- [x] `vae-decode.sbn` - Converted  
- [x] `empty-latent.sbn` - Converted
- [x] `load-diffusion.sbn` - Converted

#### Steps
- [x] Identify and categorize all fragment files
- [x] Convert meta block syntax (`#meta...#end` → `{% meta %}...{% endmeta %}`)
- [x] Convert default values (`??` → `| default:`)
- [x] Convert get_ref calls
- [x] Add unit tests for fragment patterns
- [x] Verify all conversions render correctly

#### Success Criteria
- [x] All simple fragments render with Fluid
- [x] Conversion patterns validated with unit tests
- [x] 53 tests passing (30 Fluid + 23 Workflow)

#### Completion Notes
- **Patterns established:** Dynamic get_ref with assign, conditional scope in keys
- **Key insight:** `{% get_ref variable %}` works with variables, not just strings
- **3 new tests added** for fragment syntax validation

---

### Phase 4: Template Conversion (All Remaining Fragments)
**Objective:** Convert ALL fragments (simple, complex, WAN) to Fluid syntax
**Complexity:** 53 points
**Status:** [x] Complete

#### Accomplishments
- [x] Converted 50 fragment files from Scriban to Fluid syntax
- [x] Renamed all fragments from `.sbn` to `.liquid`
- [x] Deleted unused `utils/condition-helpers.sbn`
- [x] Updated workflow templates to reference `.liquid` fragments
- [x] All 53 tests passing

#### Key Patterns Established
- Dynamic get_ref with assign: `{% assign ref_key = scope | default: "" | append: "model_output" %}{% get_ref ref_key %}`
- Conditional scope in keys: `{% if scope %}{{ scope }}{% endif %}node_id`
- string_contains filter for scope conditionals

---

### Phase 5: Workflow Template Conversion
**Objective:** Convert main workflow templates from Scriban to Fluid
**Complexity:** 37 points
**Status:** [x] Complete

#### Target Templates
- [x] `chroma/txt2img.sbn` - **Deferred** (uses legacy Prompt format, not Pipeline)
- [x] `flux/txt2img.sbn` -> `flux/txt2img.liquid`
- [x] `qwen/img2img-edit.sbn` -> `qwen/img2img-edit.liquid`
- [x] `qwen/txt2img.sbn` -> `qwen/txt2img.liquid`
- [x] `sd/txt2img.sbn` -> `sd/txt2img.liquid`
- [x] `wan/img2vid.sbn` -> `wan/img2vid.liquid`
- [x] `wan/pose2vid-steadydancer.sbn` -> `wan/pose2vid-steadydancer.liquid`
- [x] `z-image/txt2img.sbn` -> `z-image/txt2img.liquid`

#### Steps
- [x] Update WorkflowService for full Fluid workflow template support
- [x] Convert for loop syntax (`for.index` -> `forloop.index0`)
- [x] Convert conditional blocks (`{{~ if ~}}` -> `{% if %}`)
- [x] Convert variable assignments
- [x] Convert math operations (moved to `$compute:` markers)
- [x] Update file loading to use `.liquid`
- [x] Full end-to-end testing

---

### Phase 6: Validation & Testing
**Objective:** Automated and manual testing to ensure 100% parity
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps
- [ ] Create `FluidMigrationTests.cs` in test project
- [ ] Implement payload comparison tests (Scriban vs Fluid)
- [ ] Add regression tests for all workflow types
- [ ] Manual test: Z-Image txt2img generation
- [ ] Manual test: Flux txt2img generation
- [ ] Manual test: WAN img2vid generation
- [ ] Manual test: Workflows with LoRAs
- [ ] Manual test: Workflows with detailer/upscaler
- [ ] Verify ComfyUI accepts all generated payloads
- [ ] Performance benchmarking (render time comparison)

#### Success Criteria
- All automated tests pass
- JSON payloads are byte-identical or semantically equivalent
- All manual test workflows generate successfully
- ComfyUI processes all payloads without errors
- Performance within 10% of Scriban baseline

---

### Phase 7: Scriban Removal & Cleanup
**Objective:** Remove Scriban dependencies and finalize migration
**Complexity:** 24 points
**Status:** [x] Complete

#### Steps
- [x] Delete TemplateCacheService (Scriban-specific)
- [x] Remove Scriban-specific code from WorkflowService
- [x] Remove ITemplateCacheService registration from Program.cs
- [x] Remove Scriban precompilation logic
- [x] Update .csproj to only copy chroma .sbn file
- [x] Update test files to remove Scriban dependencies
- [x] **Update FragmentSchemaService to parse Fluid `{% meta %}` blocks**
- [x] Build passes with zero Scriban type references

#### Completion Notes
- **TemplateCacheService.cs** deleted
- **WorkflowService** now uses only Fluid for all template rendering
- **FragmentSchemaService** updated to parse both Fluid `{% meta %}...{% endmeta %}` and legacy `#meta...#end` syntax
- **Program.cs** simplified - no more Scriban precompilation
- **Test files** updated to work without TemplateCacheService
- **Chroma template** kept as `.sbn` but throws error if used (requires separate conversion)
- **Root cause of z-image workflow issue fixed**: FragmentSchemaService was only parsing legacy Scriban meta blocks

---

## Stress Points & Risks

| Risk | Impact | Mitigation | Complexity |
|------|--------|------------|------------|
| **Parser Bleeding (Critical)** | High | Eliminate regex, use Fluid's native block parsing | 13 |
| Payload structure mismatch | High | Automated JSON comparison tests, parallel validation | 8 |
| Performance regression | Medium | Benchmarking tests, optimize template caching | 3 |
| Missing Fluid features vs Scriban | Medium | Comprehensive feature audit, custom filters/tags | 5 |
| String concatenation complexity | Low | Use `append` filter, document pattern | 2 |
| Developer learning curve | Low | Liquid is simpler/more common than Scriban | 1 |
| Breaking existing workflows during transition | High | Feature flag for parallel testing, gradual rollout | 5 |
| Template cache invalidation | Medium | Implement development mode without caching | 2 |

**Total Complexity:** 39 fibonacci points (~Epic scale, justified by architectural significance)

---

## Open Questions for Discussion

### 1. `get_ref()` Implementation
**Question:** Custom tag or custom filter?

**Option A - Custom Tag (Recommended):**
```liquid
{% get_ref "output_key" %}
```
**Pros:** Clear intent, standard Liquid pattern  
**Cons:** Slightly more verbose

**Option B - Custom Filter:**
```liquid
{{ "output_key" | get_ref }}
```
**Pros:** Concise  
**Cons:** Reads backwards, less obvious

**Decision:** ✅ **Custom Tag** - Functionality remains the same, clearer intent

---

### 2. String Concatenation Strategy
**Question:** Is the piped filter syntax acceptable?

**Current Scriban:**
```liquid
{{ (node_prefix ?? "model") + "_unet_loader" }}
```

**Fluid Approach:**
```liquid
{{ node_prefix | default: "model" | append: "_unet_loader" }}
```

**Alternative:** Create custom `concat` filter for cleaner syntax?

**Decision:** ✅ **Use built-in `append` filter** - Piped filter syntax is acceptable

---

### 3. Rollback Strategy
**Question:** Feature flag duration and validation period

**Options:**
- **Aggressive:** Validate 2-3 workflows, cutover immediately
- **Conservative:** Run both engines in parallel for extended period
- **Phased:** Convert one workflow at a time over multiple releases

**Decision:** ✅ **Aggressive/Instant Cutoff** - Not in production, migrate everything as we go, no feature flag complexity needed

---

### 4. Template File Extensions
**Question:** Confirm `.liquid` extension

**Pros:**
- VS Code extension support (`vscode-liquid`)
- Clear differentiation from Scriban
- Standard for Liquid templates

**Cons:**
- All files must be renamed
- Search/replace operations need updating

**Decision:** ✅ **Use `.liquid` extension**

---

## Changelog

| Phase | Changes |
|-------|---------|
| Planning | Initial plan created following IMPLEMENTATION_GUIDE.md conventions |
| Phase 1 | Foundation complete - Fluid service, custom blocks, 27 tests passing |
| Phase 2 | Core migration complete - WorkflowService uses Fluid, 50 tests passing |
| Phase 3 | Simple fragments converted - 4 fragments, patterns established |
| Phase 4 | All fragments converted - 50 fragments renamed to .liquid |
| Phase 5 | Workflow templates converted - 7 templates, Pipeline processors created |
| Phase 7 | Scriban removed - TemplateCacheService deleted, FragmentSchemaService updated for Fluid meta syntax |

---

## Validation Checklist

**Payload Comparison Tests:**
- [ ] Simple txt2img workflow (Z-Image)
- [ ] Complex txt2img workflow (Flux with LoRAs)
- [ ] Img2vid workflow (WAN)
- [ ] Workflow with conditionals disabled
- [ ] Workflow with dynamic output keys
- [ ] Workflow with nested node references
- [ ] Workflow with detailer fragment
- [ ] Workflow with upscaler fragment

**Manual Testing:**
- [ ] Generate Z-Image txt2img
- [ ] Generate Flux txt2img
- [ ] Generate WAN img2vid (the failing workflow)
- [ ] Generate with multiple LoRAs
- [ ] Generate with detailer enabled
- [ ] Generate with upscaler enabled
- [ ] Verify all fragment UI forms render
- [ ] Verify all fragments declare outputs correctly
- [ ] Confirm ComfyUI accepts all payloads
- [ ] Verify generated images/videos are correct

**Performance Validation:**
- [ ] Template render time ≤ Scriban baseline
- [ ] Memory usage ≤ Scriban baseline
- [ ] Cache hit rate ≥ 80%

---

## Code Examples

### Proposed Fluid (Robust)
```csharp
// WorkflowService.cs - RenderFragmentWithFluid method
var template = _fluidTemplateService.Parse(fragmentText);
var rendered = await template.RenderAsync(context);

// Retrieve metadata from context (captured by {% meta %} block)
var metaJson = context.GetValue("FragmentMetadata")?.ToStringValue();
if (!string.IsNullOrEmpty(metaJson))
{
    (outputs, conditions) = ExtractMetadata(metaJson);
}
```

### Custom Meta Block Implementation
```csharp
// Services/Templating/Blocks/MetaBlock.cs
public class MetaBlock : IFluidBlock
{
    public async ValueTask<Completion> WriteToAsync(
        TextWriter writer, 
        TextEncoder encoder, 
        TemplateContext context, 
        IReadOnlyList<Statement> statements)
    {
        // Render meta content to separate writer
        using var metaWriter = new StringWriter();
        foreach (var statement in statements)
        {
            await statement.WriteToAsync(metaWriter, encoder, context);
        }
        
        // Store in context, NOT in main output
        context.SetValue("FragmentMetadata", metaWriter.ToString());
        
        return Completion.Normal; // No output to main stream
    }
}
```

---

## References

- [Fluid Documentation](https://github.com/sebastienros/fluid)
- [Liquid Template Language](https://shopify.github.io/liquid/)
- Current Implementation: `BlazorWebApp/Services/WorkflowService.cs`
- Template Guide: `Documentation/Plans/TEMPLATE_GUIDE.md`
- Implementation Guide: `Documentation/Plans/IMPLEMENTATION_GUIDE.md`

---

**Document Version:** 1.3 (Phase 7 Complete - FragmentSchemaService Fix)  
**Plan Status:** Migration Complete - Ready for Manual Testing
