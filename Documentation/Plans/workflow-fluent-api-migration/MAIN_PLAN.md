# Workflow System Migration to Fluent Builder API - Implementation Plan

## Status
**Current Phase:** Phase 8 - Convert Z-Image Img2Img & Remaining Shared Fragments

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
- **NO BACKWARDS COMPATIBILITY** - remove all Scriban logic completely
- **Clean codebase** - delete deprecated code immediately after replacement

### Documentation Requirements
- Create `PHASE_{#}.md` when entering a new phase
- Update phase document after each step completion
- Document all issues, blockers, and resolutions
- Track commit checkpoints throughout execution

---

## Problem Statement

The current workflow system uses **Scriban templating** to dynamically generate ComfyUI workflows from `.sbn` template files. This approach has several critical issues:

### Current Problems
1. **Regex parsing errors** - Complex JSON structures with Scriban syntax cause unpredictable parsing failures
2. **Runtime-only errors** - Template syntax errors only discovered when workflows are generated
3. **Untestable** - No way to unit test workflow generation without full integration setup
4. **Poor IDE support** - No IntelliSense, refactoring tools, or debugging for templates
5. **Complex maintenance** - Mixed Scriban/JSON syntax is difficult to read and modify
6. **Fragment proliferation** - 100+ fragment `.sbn` files all require Scriban parsing and rendering
7. **Condition evaluation issues** - Nested conditions and parameter resolution is fragile

### Current Architecture
```
Template Files (.sbn)
    ?
Scriban Parser
    ?
Template Rendering (with GenerationParameters)
    ?
Fragment Rendering (RenderFragment)
    ?
JSON String Manipulation (Regex cleanup)
    ?
ComfyUI Workflow JSON
```

### Files Involved (Current System)
- **Templates**: `Workflows/Templates/**/*.sbn` (~15 workflow templates)
- **Fragments**: `Workflows/Fragments/**/*.sbn` (~100+ fragment templates)
- **Services**: 
  - `WorkflowService.cs` - Template parsing and composition
  - `WorkflowTemplateParser.cs` - Scriban template parsing
  - `FragmentSchemaService.cs` - Schema extraction from `#meta` blocks
  - `TemplateCacheService.cs` - Scriban template caching
  - `FragmentConditionValidator.cs` - Condition validation
- **Models**:
  - `Workflow.cs` - Workflow metadata
  - `GenerationParameters.cs` - Runtime parameters
  - `FragmentParameters.cs` - Fragment state
  - `NodeRegistry.cs` - Output reference tracking

---

## Proposed Solution

**Complete migration to a Fluent Builder API** using strongly-typed C# classes for both workflows and fragments, with **complete removal** of all Scriban dependencies.

### Target Architecture
```
Workflow Classes (C#)
    ?
IWorkflowBuilder.Build(GenerationParameters)
    ?
ComfyWorkflowBuilder (fluent API)
    ?
Fragment Classes (IFragmentBuilder)
    ?
NodeBuilder (fluent node construction)
    ?
ComfyUI Workflow JSON
```

### Key Transformations

| Component | Current (Scriban) | New (Fluent API) |
|-----------|------------------|------------------|
| **Workflow Definition** | `.sbn` template files | C# classes implementing `IWorkflowBuilder` |
| **Fragment Definition** | `.sbn` template files | C# classes implementing `IFragmentBuilder` |
| **Metadata** | Parsed from JSON + `#meta` blocks | Strongly-typed properties (`WorkflowMetadata`, `FragmentMetadata`) |
| **Composition** | `Template.Parse()` + `template.Render()` | Direct object construction via `ComfyWorkflowBuilder` |
| **Condition Evaluation** | Scriban `if/else` + regex parsing | C# lambda expressions and LINQ |
| **Output Registration** | String parsing from `#meta.outputs` | `NodeRegistry.Register()` in fragment code |
| **Parameter Injection** | Scriban `{{ variable }}` syntax | C# method parameters and property access |
| **Testing** | Integration tests only | Unit tests + integration tests |

---

## Key Decisions

| Decision | Rationale |
|----------|-----------|
| **Convert ALL fragments to C# classes** | Complete removal of Scriban - no hybrid approach maintains cleaner codebase |
| **NO backwards compatibility** | Clean break allows complete removal of complex Scriban infrastructure |
| **Delete .sbn files immediately after conversion** | Prevents confusion and ensures no accidental usage |
| **Fragment classes in `Workflows/Fragments/`** | Maintain logical folder organization by feature/model |
| **Workflow classes in `Workflows/Templates/`** | Keep existing structure for discoverability |
| **Keep `GenerationParameters` unchanged** | Already well-designed for state management and persistence |
| **Enhance `NodeRegistry` for new system** | Proven pattern for output reference tracking |
| **Metadata as immutable properties** | Use `init` accessors for compile-time validation |
| **Fragment composition via constructor injection** | Workflows declare fragment dependencies explicitly |
| **Unit tests for every fragment and workflow** | Ensure testability before removing Scriban safety net |

---

## Conventions

### Naming Conventions
- **Workflow classes**: `{Base}{Mode}Workflow.cs`
  - Examples: `FluxTxt2ImgWorkflow.cs`, `WanImg2VidWorkflow.cs`
- **Fragment classes**: `{Name}Fragment.cs`
  - Examples: `SamplerFragment.cs`, `LoadFluxFragment.cs`, `DetailerFragment.cs`
- **Builder classes**: `ComfyWorkflowBuilder`, `NodeBuilder`
- **Metadata classes**: `WorkflowMetadata`, `FragmentMetadata`, `FragmentParameter`
- **Interface names**: `IWorkflowBuilder`, `IFragmentBuilder`

### File Organization
```
BlazorWebApp/
??? Workflows/
?   ??? Builders/
?   ?   ??? ComfyWorkflowBuilder.cs       # Fluent API for building workflows
?   ?   ??? NodeBuilder.cs                # Fluent API for building nodes
?   ?   ??? NodeRegistry.cs               # Output reference tracking (updated)
?   ??? Fragments/
?   ?   ??? Core/
?   ?   ?   ??? SamplerFragment.cs
?   ?   ?   ??? LatentFragment.cs
?   ?   ?   ??? PromptsFragment.cs
?   ?   ?   ??? VaeDecodeFragment.cs
?   ?   ?   ??? SaveFragment.cs
?   ?   ??? Flux/
?   ?   ?   ??? LoadFluxFragment.cs
?   ?   ?   ??? FluxGuidanceFragment.cs
?   ?   ??? Wan/
?   ?   ?   ??? I2VEncodeFragment.cs
?   ?   ?   ??? FrameInterpolationFragment.cs
?   ?   ?   ??? DecodeWanFragment.cs
?   ?   ??? Qwen/
?   ?   ?   ??? EncodeEditFragment.cs
?   ?   ??? Enhancements/
?   ?       ??? DetailerFragment.cs
?   ?       ??? UpscaleFragment.cs
?   ?       ??? LoraLoaderFragment.cs
?   ??? Templates/
?   ?   ??? Flux/
?   ?   ?   ??? FluxTxt2ImgWorkflow.cs
?   ?   ?   ??? FluxImg2ImgWorkflow.cs
?   ?   ??? Wan/
?   ?   ?   ??? WanImg2VidWorkflow.cs
?   ?   ??? Qwen/
?   ?   ?   ??? QwenImg2ImgEditWorkflow.cs
?   ?   ??? StableDiffusion/
?   ?       ??? SDTxt2ImgWorkflow.cs
?   ?       ??? SDImg2ImgWorkflow.cs
?   ??? Models/
?       ??? IWorkflowBuilder.cs
?       ??? IFragmentBuilder.cs
?       ??? WorkflowMetadata.cs
?       ??? FragmentMetadata.cs
?       ??? FragmentParameter.cs
?       ??? ComfyWorkflow.cs
??? Services/
    ??? WorkflowService.cs (heavily refactored)
```

### Coding Standards

#### Interface Design
```csharp
// Workflow contract
public interface IWorkflowBuilder
{
    WorkflowMetadata Metadata { get; }
    ComfyWorkflow Build(GenerationParameters parameters);
    IEnumerable<IFragmentBuilder> GetFragments();
}

// Fragment contract
public interface IFragmentBuilder
{
    FragmentMetadata Metadata { get; }
    void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry);
}
```

#### Metadata as Properties
```csharp
public class FluxTxt2ImgWorkflow : IWorkflowBuilder
{
    public WorkflowMetadata Metadata => new()
    {
        Id = Guid.Parse("f1e2d3c4-b5a6-7890-abcd-ef1234567890"),
        Title = "Txt2Img",
        Base = ModelBase.Flux,
        Mode = ModeType.Txt2Img,
        Assets = new[]
        {
            new WorkflowAsset("Model", "Model", AssetType.DiffusionModel, 
                "flux1-krea-dev_fp8_scaled.safetensors", 1, 3),
            // ... more assets
        }
    };
}
```

#### Fluent Builder Pattern
```csharp
builder
    .AddNode("sampler_main", node => node
        .Type("KSampler")
        .Input("steps", 20)
        .InputRef("model", registry.GetRef("model_output"))
        .Output("latent", 0))
    .RegisterOutput(registry, "latent_output", "sampler_main", 0);
```

#### Fragment Composition
```csharp
// Constructor injection of fragment dependencies
public class FluxTxt2ImgWorkflow : IWorkflowBuilder
{
    private readonly LoadFluxFragment _loaderFragment = new();
    private readonly SamplerFragment _samplerFragment = new();
    
    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        
        _loaderFragment.Build(builder, parameters, registry);
        _samplerFragment.Build(builder, parameters, registry);
        
        return builder.ToComfyWorkflow();
    }
}
```

#### Conditional Fragment Inclusion
```csharp
// Lambda expressions for conditions
if (parameters.GetFragment("upscale")?.IsActive == true)
{
    _upscaleFragment.Build(builder, parameters, registry);
}
```

---

## Implementation Phases

### Phase 1: Core Infrastructure
**Objective:** Create foundational interfaces, builders, and metadata classes
**Complexity:** 13 points
**Status:** [x] Complete

#### Steps
- [x] Create `Workflows/Models/IWorkflowBuilder.cs` interface
- [x] Create `Workflows/Models/IFragmentBuilder.cs` interface
- [x] Create `Workflows/Models/WorkflowMetadata.cs` record
- [x] Create `Workflows/Models/FragmentMetadata.cs` record
- [x] Create `Workflows/Models/FragmentParameter.cs` class
- [x] Create `Workflows/Models/ComfyWorkflow.cs` result class
- [x] Create `Workflows/Builders/ComfyWorkflowBuilder.cs` with fluent API
- [x] Create `Workflows/Builders/NodeBuilder.cs` for node construction
- [x] Update `Workflows/Builders/NodeRegistry.cs` for new system
- [x] Create unit test project structure for workflow tests
- [x] Add unit tests for `ComfyWorkflowBuilder`
- [x] Add unit tests for `NodeBuilder`

#### Success Criteria
- All interfaces and classes compile without errors
- `ComfyWorkflowBuilder` can construct simple workflows with 2+ nodes
- `NodeBuilder` can create nodes with inputs, outputs, and references
- Unit tests pass (100% coverage for builders)
- No Scriban dependencies in new infrastructure

---

### Phase 1.5: Type-Safe Enhancements (Hybrid Approach)
**Objective:** Add type-safe features without compromising service flexibility
**Complexity:** 5 points
**Status:** [x] Complete

#### Steps
- [x] Create `Workflows/Models/OutputTypes.cs` - strongly-typed output references
- [x] Create `Workflows/Models/ComfyNode.cs` - typed node representation for JSON serialization
- [x] Update `NodeRegistry.cs` to support generic type-safe output registration
- [x] Add extension methods to `FragmentParameters` for type-safe value access
  - [x] `GetString(string key, string defaultValue)`
  - [x] `GetInt(string key, int defaultValue)`
  - [x] `GetDouble(string key, double defaultValue)`
  - [x] `GetLong(string key, long defaultValue)`
  - [x] `GetBool(string key, bool defaultValue)`
- [x] Add unit tests for typed output references
- [x] Add unit tests for type-safe parameter accessors
- [x] Benchmark performance vs current string-based approach

#### Success Criteria
- `NodeRegistry.Register<TOutput>()` provides compile-time output validation
- `NodeRegistry.GetRef<TOutput>()` prevents invalid output references
- `FragmentParameters.GetInt()` provides type-safe dictionary access
- `ComfyNode` class serializes to valid ComfyUI JSON without regex cleanup
- Services remain 100% generic (no workflow-specific code)
- `GenerationParameters` structure unchanged (dictionary-based)
- All unit tests pass
- Performance equal to or better than current system

#### Implementation Details

**1. Type-Safe Output References (No Service Impact)**
```csharp
// Workflows/Models/OutputTypes.cs
public abstract record NodeOutput(string NodeId, int OutputIndex);

// Specific output types for compile-time validation
public sealed record ModelOutput(string NodeId, int OutputIndex) 
    : NodeOutput(NodeId, OutputIndex);
public sealed record ClipOutput(string NodeId, int OutputIndex) 
    : NodeOutput(NodeId, OutputIndex);
public sealed record VaeOutput(string NodeId, int OutputIndex) 
    : NodeOutput(NodeId, OutputIndex);
public sealed record LatentOutput(string NodeId, int OutputIndex) 
    : NodeOutput(NodeId, OutputIndex);
public sealed record ImageOutput(string NodeId, int OutputIndex) 
    : NodeOutput(NodeId, OutputIndex);
public sealed record ConditioningOutput(string NodeId, int OutputIndex) 
    : NodeOutput(NodeId, OutputIndex);

// Updated NodeRegistry (used only by fragments, not services)
public class NodeRegistry
{
    private readonly Dictionary<Type, List<NodeOutput>> _outputsByType = new();
    
    public void Register<TOutput>(TOutput output) where TOutput : NodeOutput
    {
        var type = typeof(TOutput);
        if (!_outputsByType.ContainsKey(type))
            _outputsByType[type] = new();
        _outputsByType[type].Add(output);
    }
    
    public (string nodeId, int index) GetRef<TOutput>() where TOutput : NodeOutput
    {
        var outputs = GetAll<TOutput>();
        if (outputs.Count == 0)
            throw new InvalidOperationException($"No {typeof(TOutput).Name} registered");
        if (outputs.Count > 1)
            throw new InvalidOperationException($"Multiple {typeof(TOutput).Name} found. Use GetRef with scope parameter.");
        
        var output = outputs[0];
        return (output.NodeId, output.OutputIndex);
    }
    
    // For scoped outputs (detailer, upscale)
    public (string nodeId, int index) GetRef<TOutput>(string scopePrefix) where TOutput : NodeOutput
    {
        var outputs = GetAll<TOutput>();
        var scoped = outputs.FirstOrDefault(o => o.NodeId.StartsWith(scopePrefix));
        if (scoped == null)
            throw new InvalidOperationException($"No {typeof(TOutput).Name} with scope '{scopePrefix}' registered");
        
        return (scoped.NodeId, scoped.OutputIndex);
    }
    
    private List<TOutput> GetAll<TOutput>() where TOutput : NodeOutput
    {
        var type = typeof(TOutput);
        if (_outputsByType.TryGetValue(type, out var outputs))
            return outputs.Cast<TOutput>().ToList();
        return new();
    }
}
```

**2. Type-Safe Parameter Accessors (Extension Methods)**
```csharp
// Models/FragmentParametersExtensions.cs
public static class FragmentParametersExtensions
{
    public static string GetString(this FragmentParameters fragment, string key, string defaultValue = "")
    {
        if (fragment.Values.TryGetValue(key, out var value))
            return value?.ToString() ?? defaultValue;
        return defaultValue;
    }
    
    public static int GetInt(this FragmentParameters fragment, string key, int defaultValue = 0)
    {
        if (fragment.Values.TryGetValue(key, out var value))
        {
            return value switch
            {
                int i => i,
                long l => (int)l,
                double d => (int)d,
                string s when int.TryParse(s, out var i) => i,
                _ => defaultValue
            };
        }
        return defaultValue;
    }
    
    public static double GetDouble(this FragmentParameters fragment, string key, double defaultValue = 0.0)
    {
        if (fragment.Values.TryGetValue(key, out var value))
        {
            return value switch
            {
                double d => d,
                float f => f,
                int i => i,
                long l => l,
                string s when double.TryParse(s, out var d) => d,
                _ => defaultValue
            };
        }
        return defaultValue;
    }
    
    public static long GetLong(this FragmentParameters fragment, string key, long defaultValue = 0L)
    {
        if (fragment.Values.TryGetValue(key, out var value))
        {
            return value switch
            {
                long l => l,
                int i => i,
                double d => (long)d,
                string s when long.TryParse(s, out var l) => l,
                _ => defaultValue
            };
        }
        return defaultValue;
    }
    
    public static bool GetBool(this FragmentParameters fragment, string key, bool defaultValue = false)
    {
        if (fragment.Values.TryGetValue(key, out var value))
        {
            return value switch
            {
                bool b => b,
                string s when bool.TryParse(s, out var b) => b,
                int i => i != 0,
                _ => defaultValue
            };
        }
        return defaultValue;
    }
}

// Usage in fragments (isolated, no service impact)
public class SamplerFragment : IFragmentBuilder
{
    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry)
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        if (!fragment.IsActive) return;
        
        // ? Type-safe accessors with default values
        var samplerName = fragment.GetString("sampler_name", "euler");
        var steps = fragment.GetInt("steps", 20);
        var cfg = fragment.GetDouble("cfg", 7.0);
        var seed = fragment.GetLong("seed", -1);
        
        // Build node...
    }
}
```

**3. Direct Object Construction (No Regex)**
```csharp
// Workflows/Models/ComfyNode.cs
public class ComfyNode
{
    [JsonPropertyName("inputs")]
    public Dictionary<string, object> Inputs { get; init; } = new();
    
    [JsonPropertyName("class_type")]
    public string ClassType { get; init; } = "";
    
    [JsonPropertyName("_meta")]
    public NodeMeta Meta { get; init; } = new();
}

public class NodeMeta
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = "";
}

// ComfyWorkflowBuilder serializes directly
public class ComfyWorkflowBuilder
{
    private readonly Dictionary<string, ComfyNode> _nodes = new();
    
    public string ToJson()
    {
        return JsonSerializer.Serialize(_nodes, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }
}
```

**Key Architectural Decisions:**
- ? **Services stay generic** - Only use `IWorkflowBuilder` interface
- ? **GenerationParameters unchanged** - Dictionary-based for flexibility
- ? **Type safety in fragments** - Accessors prevent runtime casting errors
- ? **Type-safe output references** - Compile-time validation of connections
- ? **No typed parameter classes** - Avoid tight coupling and large objects
- ? **Dynamic UI preserved** - Binds to dictionaries, renders from metadata

---

### Phase 2: Proof of Concept - Convert Z-Image Txt2Img Workflow
**Objective:** End-to-end conversion of one complete workflow to validate approach
**Complexity:** 53 points
**Status:** [x] Complete

#### Steps
- [x] Update `IFragmentBuilder` interface with optional scope parameter
- [x] Create core loader fragments (LoadDiffusion, EmptyLatent, LoraLoader)
- [x] Create prompts fragment
- [x] Create sampler fragment
- [x] Create output fragments (VaeDecode, Save)
- [x] Create enhancement fragments (SeedVarianceEnhancer, ConditioningVariation, SeedVR2Upscale)
- [x] Create detailer fragments (LoadDiffusionWithPrompts, Detailer)
- [x] Create `Workflows/Templates/ZImage/ZImageTxt2ImgWorkflow.cs`
- [x] Update `WorkflowService.GetWorkflows()` to discover C# workflows via reflection
- [x] Update `WorkflowService.ComposeWorkflowFromGenerationParameters()` to use `IWorkflowBuilder.Build()`
- [x] Test workflow generation produces valid ComfyUI JSON
- [x] Test workflow execution in ComfyUI generates images successfully
- [x] Delete `z-image/txt2img.sbn`

#### Deferred to Later
- [ ] Add unit tests for each fragment (deferred to reduce phase scope)

#### Success Criteria
- ? Z-Image Txt2Img workflow generates valid, working ComfyUI JSON
- ? Workflow executes successfully in ComfyUI and produces images
- ? Generated JSON is functionally equivalent to Scriban version
- ? No compilation errors or warnings
- ? Unit tests deferred

---

### Phase 3: Service Layer Refactoring
**Objective:** Complete removal of ALL Scriban dependencies from the codebase
**Complexity:** 35 points (revised from 13 due to broader scope)
**Status:** [x] Complete

#### Steps
- [x] Update `IWorkflowService` interface - remove Scriban-specific methods
- [x] Refactor `WorkflowService.GetWorkflows()` to use reflection discovery only
- [x] Refactor `WorkflowService.ComposeWorkflowFromGenerationParameters()` - C# only
- [x] Remove `RenderFragment()` method (Scriban-specific)
- [x] Remove `RenderTemplate()` method (Scriban-specific)
- [x] Remove `ExtractMetadata()` method (regex parsing)
- [x] Remove `EvaluateConditions()` method (Scriban conditions)
- [x] Update `GenerationParameterService` - remove pipeline code
- [x] Delete `TemplateCacheService.cs` (no longer needed)
- [x] Delete `WorkflowTemplateParser.cs` (Scriban-specific)
- [x] Delete `FragmentConditionValidator.cs`
- [x] Delete `WorkflowValidationService.cs` and `IWorkflowValidationService.cs`
- [x] Delete `FragmentConditionGenerator.cs`
- [x] Remove Scriban NuGet package from project
- [x] Update test files for new API
- [x] Delete `FragmentSchemaService.cs` (dead code - cache never used)

#### Files Deleted
- `TemplateCacheService.cs`
- `WorkflowTemplateParser.cs`
- `FragmentConditionValidator.cs`
- `WorkflowValidationService.cs`
- `IWorkflowValidationService.cs`
- `FragmentConditionGenerator.cs`
- `FragmentSchemaService.cs`

#### Success Criteria
- ? All services compile without Scriban references
- ? Workflow discovery works correctly via reflection
- ? No Scriban code remains in any service
- ? Scriban NuGet package removed
- ? Application builds successfully
- ? Tests updated and passing
- ? No dead services remaining

---

### Phase 4: Convert Core Shared Fragments
**Objective:** Convert commonly-used fragments shared across multiple workflows
**Complexity:** 13 points
**Status:** [x] Complete

#### Steps
- [x] Create `Workflows/Fragments/Enhancements/UpscaleFragment.cs`
- [x] Create `Workflows/Fragments/Enhancements/DetailerFragment.cs`
- [x] Create `Workflows/Fragments/Enhancements/LoraLoaderFragment.cs`
- [x] Create `Workflows/Fragments/Core/ConditioningVariationFragment.cs`
- [x] Create `Workflows/Fragments/Core/ModelSamplingAuraFlowFragment.cs`
- [x] Create `Workflows/Fragments/Core/LoadCheckpointFragment.cs`
- [x] Create `Workflows/Fragments/Core/LoadDiffusionFragment.cs`
- [x] Create `Workflows/Fragments/Core/LoadDiffusionWithPromptsFragment.cs`
- [x] Add unit tests for all 8 fragments
- [x] Integrate fragments into converted workflows
- [x] Delete corresponding `.sbn` files

#### Success Criteria
- All 8 core fragments converted to C#
- All fragments have unit tests with 80%+ coverage
- Fragments successfully integrate into existing converted workflows
- No `.sbn` files remain for converted fragments
- All unit tests pass

---

### Phase 5: Convert Remaining Flux Workflows
**Objective:** Complete Flux workflow family conversion
**Complexity:** 8 points
**Status:** [~] Postponed (no flux/img2img.sbn exists - only Txt2Img was needed, completed in Phase 4)

#### Steps
- [ ] Analyze `flux/img2img.sbn` and identify unique fragments
- [ ] Convert any Flux-specific fragments not yet converted
- [ ] Create `Workflows/Templates/Flux/FluxImg2ImgWorkflow.cs`
- [ ] Add unit tests for Flux Img2Img workflow
- [ ] Test workflow generation produces valid JSON
- [ ] Test workflow execution in ComfyUI
- [ ] Verify all Flux workflows share fragments correctly
- [ ] Delete `flux/img2img.sbn` and related `.sbn` files

#### Success Criteria
- All Flux workflows (Txt2Img, Img2Img) converted to C#
- All Flux workflows generate valid ComfyUI JSON
- All Flux workflows execute successfully in ComfyUI
- Unit tests pass for all Flux workflows
- Fragment reuse works correctly across Flux workflows
- No Flux `.sbn` files remain

---

### Phase 6: Convert Wan (Img2Vid) Workflows and Fragments
**Objective:** Convert video generation workflows and Wan-specific fragments
**Complexity:** 21 points (revised from 13 - two workflows, 16+ fragments, dual-model architecture)
**Status:** [x] Complete

#### Steps
- [ ] Create `Workflows/Fragments/Wan/I2VEncodeFragment.cs`
- [ ] Create `Workflows/Fragments/Wan/ContextOptionsFragment.cs`
- [ ] Create `Workflows/Fragments/Wan/FrameInterpolationFragment.cs`
- [ ] Create `Workflows/Fragments/Wan/DecodeWanFragment.cs`
- [ ] Create `Workflows/Fragments/Wan/LoadClipVaeFragment.cs`
- [ ] Create `Workflows/Fragments/Wan/ClipVisionFragment.cs`
- [ ] Create `Workflows/Templates/Wan/WanImg2VidWorkflow.cs`
- [ ] Add unit tests for all 6 Wan fragments
- [ ] Add unit tests for Wan workflow
- [ ] Test workflow generation produces valid JSON
- [ ] Test workflow execution in ComfyUI generates videos
- [ ] Verify video output quality and metadata
- [ ] Delete `wan/*.sbn` files

#### Success Criteria
- All 6 Wan fragments converted to C#
- Wan Img2Vid workflow converted to C#
- Workflow generates valid ComfyUI JSON
- Workflow executes successfully in ComfyUI
- Video generation works end-to-end
- All unit tests pass
- No Wan `.sbn` files remain

---

### Phase 7: Convert Qwen Workflows and Fragments
**Objective:** Convert image editing workflows for Qwen model
**Complexity:** 8 points
**Status:** [~] Skipped (reordered after Phase 8)

#### Notes
Phase 7 was skipped in execution order. Z-Image Img2Img was prioritized first (Phase 8). Qwen conversion will follow as Phase 9.

---

### Phase 8: Convert Z-Image Img2Img & Remaining Shared Fragments
**Objective:** Create Z-Image Img2Img workflow and convert all remaining shared fragment `.sbn` files to C#
**Complexity:** 17 points
**Status:** [ ] Not Started

#### Steps
- [ ] Create `LoadImageScaledFragment` (`load-image-scaled.sbn`) - LoadImage + ImageScaleToTotalPixels
- [ ] Create `VaeEncodeFragment` (`vae-encode.sbn`) - VAEEncode with registry refs
- [ ] Create `LoadCheckpointFragment` (`load-checkpoint.sbn`) - CheckpointLoaderSimple + PCLazyLoraLoader + PCLazyTextEncode
- [ ] Create `SamplerStandardFragment` (`sampler-standard.sbn`) - Standard KSampler wrapper
- [ ] Create `ZImageImg2ImgWorkflow.cs` with source image, VAE encode, denoise < 1
- [ ] Add unit tests for all 4 fragments and workflow
- [ ] Test workflow execution in ComfyUI
- [ ] Delete converted `.sbn` files

#### Success Criteria
- 4 new shared fragments converted to C#
- Z-Image Img2Img workflow generates valid ComfyUI JSON
- Workflow executes successfully in ComfyUI
- All unit tests pass
- Converted `.sbn` files deleted

---

### Phase 9: Convert Qwen Workflows and Fragments
**Objective:** Convert Qwen-specific fragments and both Qwen workflows (Txt2Img + Img2Img Edit)
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps
- [ ] Create `Workflows/Fragments/Qwen/LoadQwenEditFragment.cs`
- [ ] Create `Workflows/Fragments/Qwen/EncodeEditFragment.cs`
- [ ] Create `Workflows/Templates/Qwen/QwenTxt2ImgWorkflow.cs`
- [ ] Create `Workflows/Templates/Qwen/QwenImg2ImgEditWorkflow.cs`
- [ ] Add unit tests for Qwen fragments and workflows
- [ ] Delete `qwen/*.sbn` files

---

### Phase 10: Convert SD Txt2Img Workflow
**Objective:** Convert StableDiffusion Txt2Img using LoadCheckpointFragment from Phase 8
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps
- [ ] Create `Workflows/Templates/SD/SDTxt2ImgWorkflow.cs`
- [ ] Add unit tests
- [ ] Delete `sd/txt2img.sbn`

---

### Phase 11: Convert Chroma Txt2Img Workflow
**Objective:** Decompose monolithic Chroma template (828 lines) into fragment-based C# workflow
**Complexity:** 8 points
**Status:** [ ] Not Started

#### Steps
- [ ] Analyze monolithic template structure and identify fragments
- [ ] Create `Workflows/Templates/Chroma/ChromaTxt2ImgWorkflow.cs`
- [ ] Add unit tests
- [ ] Delete `chroma/txt2img.sbn`

---

### Phase 12: UI Component Updates
**Objective:** Update Blazor components to work with new metadata system
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps
- [ ] Update `WorkflowAssetSelector.razor` if needed
- [ ] Update `Generate.razor` fragment discovery logic
- [ ] Update any component that reads fragment schemas
- [ ] Test UI displays fragments correctly
- [ ] Test state persistence works with new system
- [ ] Verify asset dropdowns populate correctly

#### Success Criteria
- All UI components work with new metadata
- Fragment UI renders correctly
- State saves and loads properly
- Asset selection functions normally
- No UI regressions

---

### Phase 13: Final Cleanup and Documentation
**Objective:** Remove all deprecated code, delete all remaining `.sbn` files, and update documentation
**Complexity:** 5 points
**Status:** [ ] Not Started

#### Steps
- [ ] Delete all remaining `.sbn` files (shared fragments with C# equivalents)
- [ ] Delete `utils/condition-helpers.sbn` (dead Scriban helper)
- [ ] Delete empty `Workflows/Templates/z-image/` directory
- [ ] Search codebase for any remaining Scriban references
- [ ] Delete empty `Workflows/Fragments/` subdirectories
- [ ] Update `TEMPLATE_GUIDE.md` for new C# workflow system
- [ ] Create migration guide for future workflow additions
- [ ] Add code examples to documentation

#### Success Criteria
- **Zero `.sbn` files remain in codebase**
- All documentation updated
- Migration guide completed
- Codebase is clean and maintainable

---

## Stress Points & Risks

| Risk | Mitigation | Complexity |
|------|------------|------------|
| **Breaking state persistence** | Test state loading/saving after each phase; keep `GenerationParameters` unchanged | 8 |
| **Workflow JSON differences** | Compare generated JSON with Scriban output; functional equivalence is acceptable | 5 |
| **Fragment interdependencies** | Document fragment dependencies; create fragments in dependency order | 8 |
| **UI component breakage** | Update components incrementally; test after each service change | 5 |
| **Missing fragments during conversion** | Audit all `.sbn` files before Phase 8; maintain checklist | 3 |
| **Performance regression** | Benchmark workflow generation before/after; C# should be faster | 2 |
| **Test coverage gaps** | Require 80%+ coverage for all fragments and workflows before deletion | 5 |
| **Loss of dynamic features** | Identify Scriban-specific features early; ensure C# equivalents exist | 8 |

---

## Changelog

| Phase | Changes |
|-------|---------|
| Planning | Initial plan created with 10 phases totaling ~115 complexity points |

---

## Code Examples

### Example: Simple Fragment

```csharp
// Workflows/Fragments/Core/SaveFragment.cs
public class SaveFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "save",
        Type = FragmentType.Output,
        Title = "Save Image",
        IsHidden = true // Utility fragment, no UI
    };
    
    public void Build(
        ComfyWorkflowBuilder builder, 
        GenerationParameters parameters,
        NodeRegistry registry)
    {
        builder.AddNode("save_image", node => node
            .Type("SaveImage")
            .Input("filename_prefix", "BlazorDiffusion")
            .InputRef("images", registry.GetRef("image_output")));
        
        // No outputs to register - terminal node
    }
}
```

### Example: Complex Fragment with Parameters

```csharp
// Workflows/Fragments/Core/SamplerFragment.cs
public class SamplerFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "main_sampler",
        Type = FragmentType.Sampler,
        Title = "Sampler",
        Icon = "fa-solid fa-dice",
        Order = 50,
        Collapsible = true,
        Parameters = new[]
        {
            new FragmentParameter
            {
                Name = "sampler_name",
                Label = "Sampler",
                Type = ParameterType.Select,
                Source = new DynamicSource("KSampler", "sampler_name")
            },
            new FragmentParameter
            {
                Name = "steps",
                Label = "Steps",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 150,
                Step = 1,
                DefaultValue = 20
            },
            new FragmentParameter
            {
                Name = "cfg",
                Label = "CFG Scale",
                Type = ParameterType.Slider,
                Min = 1.0,
                Max = 30.0,
                Step = 0.5,
                DefaultValue = 7.0
            },
            new FragmentParameter
            {
                Name = "seed",
                Label = "Seed",
                Type = ParameterType.Number,
                Min = -1,
                DefaultValue = -1
            }
        }
    };
    
    public void Build(
        ComfyWorkflowBuilder builder, 
        GenerationParameters parameters,
        NodeRegistry registry)
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        if (!fragment.IsActive) return;
        
        builder.AddNode("sampler_main", node => node
            .Type("KSampler")
            .Input("sampler_name", fragment.GetValue("sampler_name", "euler"))
            .Input("scheduler", fragment.GetValue("scheduler", "normal"))
            .Input("steps", fragment.GetValue("steps", 20))
            .Input("cfg", fragment.GetValue("cfg", 7.0))
            .Input("seed", fragment.GetValue("seed", -1L))
            .InputRef("model", registry.GetRef("model_output"))
            .InputRef("positive", registry.GetRef("positive_output"))
            .InputRef("negative", registry.GetRef("negative_output"))
            .InputRef("latent_image", registry.GetRef("latent_output")));
        
        registry.Register("latent_output", "sampler_main", 0);
    }
}
```

### Example: Complete Workflow

```csharp
// Workflows/Templates/Flux/FluxTxt2ImgWorkflow.cs
public class FluxTxt2ImgWorkflow : IWorkflowBuilder
{
    private readonly LoadFluxFragment _loaderFragment = new();
    private readonly SamplerFragment _samplerFragment = new();
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly UpscaleFragment _upscaleFragment = new();
    private readonly DetailerFragment _detailerFragment = new();
    private readonly SaveFragment _saveFragment = new();
    
    public WorkflowMetadata Metadata => new()
    {
        Id = Guid.Parse("f1e2d3c4-b5a6-7890-abcd-ef1234567890"),
        Title = "Txt2Img",
        Base = ModelBase.Flux,
        Mode = ModeType.Txt2Img,
        Assets = new[]
        {
            new WorkflowAsset("Model", "Model", AssetType.DiffusionModel, 
                "flux1-krea-dev_fp8_scaled.safetensors", 1, 3),
            new WorkflowAsset("Clip1", "CLIP T5", AssetType.Clip, 
                "t5xxl_fp8_e4m3fn_scaled.safetensors", 2, 3),
            new WorkflowAsset("Clip2", "CLIP ViT", AssetType.Clip, 
                "vit-l.safetensors", 3, 3),
            new WorkflowAsset("VAE", "VAE", AssetType.Vae, 
                "ae.safetensors", 4, 3)
        }
    };
    
    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _loaderFragment;
        yield return _samplerFragment;
        yield return _vaeDecodeFragment;
        yield return _upscaleFragment;
        yield return _detailerFragment;
        yield return _saveFragment;
    }
    
    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        
        // Core pipeline (always active)
        _loaderFragment.Build(builder, parameters, registry);
        _samplerFragment.Build(builder, parameters, registry);
        _vaeDecodeFragment.Build(builder, parameters, registry);
        
        // Optional enhancements
        if (parameters.GetFragment("upscale")?.IsActive == true)
        {
            _upscaleFragment.Build(builder, parameters, registry);
        }
        
        if (parameters.GetFragment("detailer")?.IsActive == true)
        {
            _detailerFragment.Build(builder, parameters, registry);
        }
        
        // Always save
        _saveFragment.Build(builder, parameters, registry);
        
        return new ComfyWorkflow
        {
            Json = builder.ToJson(),
            Registry = registry
        };
    }
}
```

---

## References

- Current system documentation: `BlazorWebApp/Workflows/TEMPLATE_GUIDE.md`
- Fragment schema guide: `BlazorWebApp/Workflows/FRAGMENT_SCHEMA_GUIDE.md`
- Services involved:
  - `BlazorWebApp/Services/WorkflowService.cs`
  - `BlazorWebApp/Services/WorkflowTemplateParser.cs`
  - `BlazorWebApp/Services/FragmentSchemaService.cs`
  - `BlazorWebApp/Services/WorkflowValidationService.cs`
- Models involved:
  - `BlazorWebApp/Models/Workflow.cs`
  - `BlazorWebApp/Models/GenerationParameters.cs`

---

**Total Estimated Complexity:** ~115 Fibonacci points across 10 phases

**Migration Philosophy:** Complete, clean removal of Scriban with no backwards compatibility. Every phase is independently testable and creates a commit checkpoint.
