# Phase 12: Service Cleanup &amp; Optimization

## Status
**Current Step:** Not Started  
**Phase Complexity:** 21 points  
**Depends On:** Phase 8 (can be started in parallel)

---

## Overview

This phase addresses the technical debt and redundancies identified in [SERVICE_ANALYSIS.md](./SERVICE_ANALYSIS.md). The goal is to simplify the `WorkflowService` and `GenerationParameterService` interaction, eliminate duplicate code, and establish clearer responsibilities.

### Key Problems Being Solved

| Problem | Impact | Solution |
|---------|--------|----------|
| Default values parsed in 3 places | Confusion, inconsistency | Consolidate to single source |
| Duplicate pipeline parsing regex | Code duplication | Single method in WorkflowService |
| Heuristic fragment discovery | Brittle string matching | Schema-based metadata |
| Unused chainable fragment methods | Dead code | Remove or implement |
| Complex legacy parameter bridge | Maintenance burden | Phase 10 will remove |

---

## Steps

### Step 12.1: Add FragmentType to Schema [3 points]
**Status:** [ ]  
**Goal:** Identify fragment purpose via metadata instead of string heuristics

**Changes:**
1. Add `FragmentType` enum to `Models/FragmentSchema.cs`:
   ```csharp
   public enum FragmentType
   {
       Unknown,      // Default
       Loader,       // Model loading (no direct UI)
       Prompts,      // Positive/negative prompts
       Latent,       // Resolution/latent image settings
       Sampler,      // KSampler, sampling settings
       Conditioning, // CLIP text encode, conditioning
       Enhancement,  // Upscale, detailer, etc.
       Output        // Save, preview nodes
   }
   ```

2. Add `FragmentType Type` property to `FragmentSchema`

3. Update `WorkflowService.ParseFragmentSchema()` to parse `"type"` from `#meta`

4. Update fragment `#meta` blocks to include type:
   - `sampler.sbn`: `"type": "sampler"`
   - `empty-latent.sbn`: `"type": "latent"`
   - `prompts.sbn`: `"type": "prompts"`
   - `upscale-seedvr2.sbn`: `"type": "enhancement"`
   - etc.

**Files to Modify:**
- `Models/FragmentSchema.cs`
- `Services/WorkflowService.cs`
- All fragment `.sbn` files with `#meta` blocks

**Verification:**
- Unit test: Parse fragment with type, verify correct enum value
- Generate.razor `DiscoverFragments()` can use `FragmentType` instead of string matching

---

### Step 12.2: Consolidate Pipeline Parsing [5 points]
**Status:** [ ]  
**Goal:** Single source for pipeline step extraction with all needed data

**Current State:**
- `WorkflowService.ParsePipelineStepsFromRawJson()` - returns `(Id, Fragment)` tuples
- `GenerationParameterService.ParsePipelineStepsWithRegex()` - returns `(Id, Fragment, Parameters)` tuples

**Changes:**
1. Create new method in `WorkflowService`:
   ```csharp
   public record ParsedPipelineStep(
       string Id,
       string Fragment,
       Dictionary<string, object?> DefaultValues,
       int Order
   );
   
   public List<ParsedPipelineStep> ParsePipelineSteps(string rawJson);
   ```

2. Move parsing logic from `GenerationParameterService.ParsePipelineStepsWithRegex()` to `WorkflowService.ParsePipelineSteps()`

3. Move `ParseStepParameterDefaults()` to `WorkflowService` (private)

4. Move `ParseScribanDefaultValue()` to `WorkflowService` (private or shared utility)

5. Update `GenerationParameterService.InitializeFragmentsFromPipeline()` to call `WorkflowService.ParsePipelineSteps()`

6. Remove duplicate methods from `GenerationParameterService`

**Files to Modify:**
- `Services/IWorkflowService.cs` - add interface method
- `Services/WorkflowService.cs` - add implementation
- `Services/GenerationParameterService.cs` - remove duplicates, call WorkflowService

**Verification:**
- Existing unit tests still pass
- Workflow initialization works correctly
- No duplicate regex parsing code remains

---

### Step 12.3: Consolidate Default Value Resolution [3 points]
**Status:** [ ]  
**Goal:** Single, clear priority order for default values

**Current Priority (implicit, confusing):**
1. Step parameters: `{{ SeedVR2.Model ?? "default" | json }}`
2. Fragment defaults: `{{ param ?? "default" | json }}`
3. Schema constraints: `ParameterConstraints.Default`
4. Form component hardcoded defaults

**New Priority (explicit):**
1. **Step parameters** (from `ParsedPipelineStep.DefaultValues`)
2. **Fragment defaults** (from `WorkflowService.ParseFragmentDefaults()`)
3. **First dynamic option** (if no default and source is dynamic)

**Changes:**
1. Update `GenerationParameterService.InitializeFragmentsFromPipeline()`:
   ```csharp
   // Already does priority 1 & 2, but document clearly
   // Priority 1: Step parameters (from workflow template)
   foreach (var kvp in step.DefaultValues)
   {
       fragment.Values[kvp.Key] = kvp.Value;
   }
   
   // Priority 2: Fragment template defaults (for values not in step)
   var fragmentDefaults = _workflowService.ParseFragmentDefaults(step.Fragment);
   foreach (var kvp in fragmentDefaults)
   {
       if (!fragment.Values.ContainsKey(kvp.Key))
       {
           fragment.Values[kvp.Key] = kvp.Value;
       }
   }
   ```

2. Remove fallback-to-first-option logic from form components (like `SeedVR2Form.InitializeFromFragment()`)
   - The service should handle this during initialization, not the UI component

3. Add dynamic option fallback to `GenerationParameterService.InitializeFragmentsFromPipeline()`:
   ```csharp
   // Priority 3: First dynamic option (for required fields with no default)
   // This requires async, so might need to be in a separate initialization step
   ```

4. Document the priority order in `IGenerationParameterService.cs` interface comments

**Files to Modify:**
- `Services/GenerationParameterService.cs`
- `Services/IGenerationParameterService.cs` - documentation
- `Components/Shared/Generation/Fragments/SeedVR2Form.razor` - remove fallback logic

**Verification:**
- Default values load correctly from workflow templates
- No fallback logic in form components
- All fragments initialize with correct defaults

---

### Step 12.4: Update Generate.razor to Use FragmentType [3 points]
**Status:** [ ]  
**Depends On:** Step 12.1

**Current State:**
```csharp
// Heuristic-based discovery
if (fragmentFile == "sampler.sbn" || fragmentId.Contains("sampler"))
{
    _samplerFragmentId = fragmentId;
}
```

**Changes:**
1. Update `DiscoverFragments()` to use `FragmentType`:
   ```csharp
   private void DiscoverFragments()
   {
       _latentFragmentId = null;
       _samplerFragmentId = null;
       _promptsFragmentId = null;
       
       foreach (var kvp in Parameters.Fragments)
       {
           var schema = WorkflowService.GetFragmentSchema(kvp.Value.FragmentFile);
           
           switch (schema?.Type)
           {
               case FragmentType.Sampler:
                   _samplerFragmentId ??= kvp.Key; // First sampler found
                   break;
               case FragmentType.Latent:
                   _latentFragmentId ??= kvp.Key;
                   break;
               case FragmentType.Prompts:
                   _promptsFragmentId ??= kvp.Key;
                   break;
           }
       }
   }
   ```

2. Update `GetOptionalFragments()` to use `FragmentType.Enhancement`:
   ```csharp
   private IEnumerable<...> GetOptionalFragments()
   {
       foreach (var kvp in Parameters.Fragments)
       {
           var schema = WorkflowService.GetFragmentSchema(kvp.Value.FragmentFile);
           
           // Enhancement fragments are optional
           if (schema?.Type == FragmentType.Enhancement)
           {
               yield return (kvp.Key, schema.Title, schema.Icon, schema, kvp.Value);
           }
       }
   }
   ```

**Files to Modify:**
- `Pages/Generate.razor`

**Verification:**
- Fragment discovery works correctly
- Optional fragments render correctly
- No string heuristics remain

---

### Step 12.5: Remove Unused Chainable Fragment Methods [2 points]
**Status:** [ ]  
**Goal:** Clean up dead code or mark for Phase 9 implementation

**Current Unused Methods:**
- `GenerationParameterService.AddFragmentInstance()`
- `GenerationParameterService.RemoveFragmentInstance()`
- `GenerationParameterService.ReorderFragments()`

**Decision Required:**
- Option A: Remove these methods (they're for Phase 9)
- Option B: Mark with `// TODO: Phase 9 - Node Chaining` and keep

**Recommended:** Option B - keep with TODO, as Phase 9 needs them

**Changes:**
1. Add `// TODO: Phase 9 - Node Chaining Support` comments
2. Add `[System.ComponentModel.EditorBrowsable(EditorBrowsableState.Never)]` to hide from IntelliSense
3. Or simply document they're not yet implemented

**Files to Modify:**
- `Services/GenerationParameterService.cs`
- `Services/IGenerationParameterService.cs`

**Verification:**
- Code compiles
- Methods are clearly marked as pending Phase 9

---

### Step 12.6: Cache Parsed Workflow Data [3 points]
**Status:** [ ]  
**Goal:** Parse pipeline steps once and cache

**Current State:**
- Pipeline steps parsed every time `InitializeFromWorkflow()` is called
- Schema parsed every time `GetFragmentSchema()` is called (already cached)

**Changes:**
1. Add pipeline step caching to `WorkflowService`:
   ```csharp
   private readonly Dictionary<Guid, List<ParsedPipelineStep>> _pipelineCache = new();
   
   public List<ParsedPipelineStep> GetPipelineSteps(Workflow workflow)
   {
       if (!_pipelineCache.TryGetValue(workflow.Id, out var steps))
       {
           steps = ParsePipelineSteps(workflow.RawJson);
           _pipelineCache[workflow.Id] = steps;
       }
       return steps;
   }
   
   public void ClearPipelineCache()
   {
       _pipelineCache.Clear();
   }
   ```

2. Update `GenerationParameterService.InitializeFromWorkflow()` to use cached method

3. Clear cache when workflows are refreshed

**Files to Modify:**
- `Services/IWorkflowService.cs`
- `Services/WorkflowService.cs`
- `Services/GenerationParameterService.cs`

**Verification:**
- Second workflow selection is faster (no re-parsing)
- Cache clears correctly on workflow refresh
- Memory doesn't grow unbounded

---

### Step 12.7: Add ClearSchemaCache Trigger [2 points]
**Status:** [ ]  
**Goal:** Schema cache clears when it should

**Current State:**
- `WorkflowService.ClearSchemaCache()` exists but is never called

**Changes:**
1. Call `ClearSchemaCache()` in `RefreshWorkflows()`
2. Call `ClearPipelineCache()` in `RefreshWorkflows()`
3. Consider: Should hot-reload of `.sbn` files trigger cache clear?

**Files to Modify:**
- `Services/WorkflowService.cs`

**Verification:**
- Schema changes on disk are picked up after refresh
- No stale cached data

---

## Verification Checklist

After completing all steps:

- [ ] `WorkflowService` is the single source for:
  - [ ] Workflow template parsing
  - [ ] Pipeline step extraction
  - [ ] Fragment schema parsing
  - [ ] Default value extraction

- [ ] `GenerationParameterService` only manages:
  - [ ] Runtime parameter state
  - [ ] Change notifications
  - [ ] Dynamic option resolution

- [ ] No duplicate regex parsing code exists

- [ ] `FragmentType` is used for fragment discovery (no string heuristics)

- [ ] Default value priority is clear and documented

- [ ] Caching is properly implemented with clear triggers

- [ ] All existing tests pass

- [ ] Generation works for all workflow types

---

## Files Summary

### Modified Files
| File | Changes |
|------|---------|
| `Models/FragmentSchema.cs` | Add `FragmentType` enum and property |
| `Services/IWorkflowService.cs` | Add `ParsePipelineSteps()`, `GetPipelineSteps()`, `ClearPipelineCache()` |
| `Services/WorkflowService.cs` | Implement new methods, add caching, update `RefreshWorkflows()` |
| `Services/IGenerationParameterService.cs` | Document default priority, mark chainable methods |
| `Services/GenerationParameterService.cs` | Remove duplicate parsing, use WorkflowService |
| `Pages/Generate.razor` | Use `FragmentType` for fragment discovery |
| `Components/Shared/Generation/Fragments/SeedVR2Form.razor` | Remove fallback-to-first-option logic |
| Fragment `.sbn` files | Add `"type"` to `#meta` blocks |

### No Files Removed
This phase only refactors existing code; no files are deleted.

---

## Success Criteria

1. **Cleaner Separation of Concerns**
   - WorkflowService: template parsing, caching
   - GenerationParameterService: runtime state management

2. **Eliminated Redundancy**
   - Single pipeline parsing method
   - Single default value resolution flow
   - No duplicate regex code

3. **Improved Maintainability**
   - FragmentType metadata instead of string heuristics
   - Clear priority order for defaults (documented)
   - Proper caching with clear invalidation

4. **No Regressions**
   - All existing functionality works
   - All tests pass
   - Performance is same or better

---

## Notes

- This phase can be done in parallel with Phase 8 (Generate Page Layout)
- Phase 10 (Legacy Deprecation) will remove more code, but this phase makes the current code cleaner
- Consider adding unit tests for the consolidated parsing logic
