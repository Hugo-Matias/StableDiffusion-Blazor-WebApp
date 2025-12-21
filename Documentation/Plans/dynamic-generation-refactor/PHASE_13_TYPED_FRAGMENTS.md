# Phase 13: Strongly-Typed Fragment Classes

## Status
**Current Step:** Planning Complete - Ready for Execution
**Complexity:** 42 points (revised)
**Priority:** HIGHEST - Execute before any other work

---

## Decisions Made

| Question | Decision |
|----------|----------|
| Constraint Source | **C# attributes** (`[Range]`, `[MinLength]`, etc.) - safer and easier to manage |
| Dynamic Sources | **Custom `[DynamicSource]` attribute** for ComfyUI-populated dropdowns |
| Wan/Video Fragments | **Yes, all fragments** - complete integration for all pipeline templates |
| Backward Compatibility | **None** - clean removal of old `FragmentParameters` and dictionary approach |
| Parallel Execution | **Highest priority** - execute before anything else |

---

## Problem Statement

The current `FragmentParameters` model uses `Dictionary&lt;string, object?&gt;` for values:

```csharp
public class FragmentParameters
{
    public string FragmentFile { get; set; }
    public bool IsActive { get; set; }
    public int Order { get; set; }
    public Dictionary&lt;string, object?&gt; Values { get; set; } = new();
}
```

### Pain Points

1. **No compile-time safety** - Typos in parameter names only surface at runtime
2. **Scattered string constants** - `FragmentKeys.Params` must be kept in sync with `.sbn` files and forms
3. **Complex condition evaluation** - Walking dictionaries with reflection and case conversion
4. **Type casting everywhere** - `fragment.GetValueOrDefault&lt;int&gt;("steps", 20)` 
5. **Hard to debug** - Dictionary contents not visible without expanding in debugger
6. **Maintenance burden** - Every parameter access point needs the same string

### What We Want

```csharp
// Instead of this:
var steps = fragment.GetValueOrDefault&lt;int&gt;("steps", 20);
ParameterService.SetFragmentValue(fragmentId, "steps", value);

// We want this:
var steps = sampler.Steps;
sampler.Steps = value;
```

---

## Proposed Solution

### Architecture Overview

```
                        FragmentBase (abstract)
                               |
    +-------------+------------+------------+-------------+
    |             |            |            |             |
 Sampler      Prompts      Latent      SeedVR2        Wan/*
 Fragment     Fragment     Fragment    Fragment     Fragments
    |             |            |            |             |
 - Steps      - Positive   - Width     - Resolution  - VideoLength
 - Seed       - Negative   - Height    - BatchSize   - FrameRate
 - Cfg        (no neg for  - BatchSize - BlocksToSwap- MotionAmp
 - etc...      Flux)       - etc...    - etc...      - etc...
```

### Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Abstract `FragmentBase` class | Common properties (Id, IsActive, Order, FragmentFile) + template method |
| One class per fragment type | Full IntelliSense, refactoring support, compile-time errors |
| `.sbn` files for ComfyUI rendering only | C# classes define structure and constraints |
| `[Range]` attributes for constraints | Standard .NET validation, works with Blazor forms |
| `[DynamicSource]` custom attribute | Declarative ComfyUI source binding |
| `[JsonPropertyName]` for serialization | Maps C# PascalCase to snake_case for Scriban |
| Clean removal of old code | No `FragmentParameters`, no `FragmentKeys.Params`, no dictionary access |

---

## Custom Attributes

### DynamicSourceAttribute

```csharp
/// &lt;summary&gt;
/// Indicates this property's options should be populated from a ComfyUI node.
/// &lt;/summary&gt;
[AttributeUsage(AttributeTargets.Property)]
public class DynamicSourceAttribute : Attribute
{
    /// &lt;summary&gt;
    /// The ComfyUI node class_type to query for options.
    /// &lt;/summary&gt;
    public string NodeType { get; }
    
    /// &lt;summary&gt;
    /// The input name on the node to get options from.
    /// &lt;/summary&gt;
    public string InputName { get; }
    
    /// &lt;summary&gt;
    /// Alternative: Use a well-known backend collection (e.g., "Backend.Samplers").
    /// &lt;/summary&gt;
    public string? BackendCollection { get; }
    
    public DynamicSourceAttribute(string nodeType, string inputName)
    {
        NodeType = nodeType;
        InputName = inputName;
    }
    
    public DynamicSourceAttribute(string backendCollection)
    {
        BackendCollection = backendCollection;
    }
}
```

### StepAttribute (for sliders)

```csharp
/// &lt;summary&gt;
/// Defines the step increment for numeric slider controls.
/// &lt;/summary&gt;
[AttributeUsage(AttributeTargets.Property)]
public class StepAttribute : Attribute
{
    public double Value { get; }
    
    public StepAttribute(double value)
    {
        Value = value;
    }
}
```

---

## Implementation Plan

### Step 13.1: Create Custom Attributes (2 points)
**Goal:** Define attributes for constraints and dynamic sources

**Files:**
- [ ] Create `BlazorWebApp/Models/Fragments/Attributes/DynamicSourceAttribute.cs`
- [ ] Create `BlazorWebApp/Models/Fragments/Attributes/StepAttribute.cs`

---

### Step 13.2: Create FragmentBase Abstract Class (5 points)
**Goal:** Define the base class all fragments inherit from

```csharp
public abstract class FragmentBase
{
    /// &lt;summary&gt;
    /// Unique identifier for this fragment instance in the pipeline.
    /// E.g., "main_sampler", "refiner_sampler"
    /// &lt;/summary&gt;
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    
    /// &lt;summary&gt;
    /// The fragment template file (e.g., "sampler.sbn").
    /// &lt;/summary&gt;
    [JsonIgnore]
    public abstract string FragmentFile { get; }
    
    /// &lt;summary&gt;
    /// Whether this fragment is active and should be rendered.
    /// &lt;/summary&gt;
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;
    
    /// &lt;summary&gt;
    /// Order in the pipeline for chainable fragments.
    /// &lt;/summary&gt;
    [JsonPropertyName("order")]
    public int Order { get; set; }
    
    /// &lt;summary&gt;
    /// Flattens all properties to a dictionary for Scriban rendering.
    /// Uses JsonPropertyName attributes for key names.
    /// &lt;/summary&gt;
    public Dictionary&lt;string, object?&gt; ToDictionary()
    {
        var result = new Dictionary&lt;string, object?&gt;(StringComparer.OrdinalIgnoreCase);
        var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        
        foreach (var prop in properties)
        {
            if (prop.GetCustomAttribute&lt;JsonIgnoreAttribute&gt;() != null)
                continue;
                
            var jsonName = prop.GetCustomAttribute&lt;JsonPropertyNameAttribute&gt;()?.Name ?? prop.Name;
            result[jsonName] = prop.GetValue(this);
        }
        
        // Also add IsActive for condition checking
        result["IsActive"] = IsActive;
        
        return result;
    }
    
    /// &lt;summary&gt;
    /// Creates a clone of this fragment with a new ID.
    /// &lt;/summary&gt;
    public abstract FragmentBase Clone(string newId);
    
    /// &lt;summary&gt;
    /// Gets constraint metadata for a property (for UI rendering).
    /// &lt;/summary&gt;
    public static (double? Min, double? Max, double? Step) GetConstraints&lt;T&gt;(Expression&lt;Func&lt;T&gt;&gt; propertyExpression)
    {
        // Extract property info from expression and read attributes
        // ... implementation
    }
}
```

**Files:**
- [ ] Create `BlazorWebApp/Models/Fragments/FragmentBase.cs`

---

### Step 13.3: Create Core Fragment Classes (8 points)
**Goal:** Implement typed classes for essential fragments

#### SamplerFragment
```csharp
public class SamplerFragment : FragmentBase
{
    public override string FragmentFile =&gt; "sampler.sbn";
    
    [JsonPropertyName("sampler_name")]
    [DynamicSource("Backend.Samplers")]
    public string SamplerName { get; set; } = "euler";
    
    [JsonPropertyName("scheduler")]
    [DynamicSource("Backend.Schedulers")]
    public string Scheduler { get; set; } = "normal";
    
    [JsonPropertyName("steps")]
    [Range(1, 150)]
    [Step(1)]
    public int Steps { get; set; } = 20;
    
    [JsonPropertyName("seed")]
    public long Seed { get; set; } = -1;
    
    [JsonPropertyName("cfg")]
    [Range(1, 30)]
    [Step(0.5)]
    public float Cfg { get; set; } = 7.0f;
    
    [JsonPropertyName("denoise")]
    [Range(0, 1)]
    [Step(0.01)]
    public float Denoise { get; set; } = 1.0f;
    
    [JsonPropertyName("guidance")]
    [Range(1, 10)]
    [Step(0.5)]
    public float Guidance { get; set; } = 3.5f;
    
    public override FragmentBase Clone(string newId) =&gt; new SamplerFragment
    {
        Id = newId,
        IsActive = IsActive,
        Order = Order,
        SamplerName = SamplerName,
        Scheduler = Scheduler,
        Steps = Steps,
        Seed = Seed,
        Cfg = Cfg,
        Denoise = Denoise,
        Guidance = Guidance
    };
}
```

#### PromptsFragment
```csharp
public class PromptsFragment : FragmentBase
{
    public override string FragmentFile =&gt; "prompts.sbn";
    
    [JsonPropertyName("positive")]
    public string Positive { get; set; } = string.Empty;
    
    [JsonPropertyName("negative")]
    public string Negative { get; set; } = string.Empty;
    
    public override FragmentBase Clone(string newId) =&gt; new PromptsFragment
    {
        Id = newId,
        IsActive = IsActive,
        Order = Order,
        Positive = Positive,
        Negative = Negative
    };
}
```

#### LatentFragment
```csharp
public class LatentFragment : FragmentBase
{
    public override string FragmentFile =&gt; "empty-latent.sbn";
    
    [JsonPropertyName("width")]
    [Range(64, 4096)]
    [Step(8)]
    public int Width { get; set; } = 1024;
    
    [JsonPropertyName("height")]
    [Range(64, 4096)]
    [Step(8)]
    public int Height { get; set; } = 1024;
    
    [JsonPropertyName("batch_size")]
    [Range(1, 16)]
    [Step(1)]
    public int BatchSize { get; set; } = 1;
    
    public override FragmentBase Clone(string newId) =&gt; new LatentFragment
    {
        Id = newId,
        IsActive = IsActive,
        Order = Order,
        Width = Width,
        Height = Height,
        BatchSize = BatchSize
    };
}
```

**Files:**
- [ ] Create `BlazorWebApp/Models/Fragments/SamplerFragment.cs`
- [ ] Create `BlazorWebApp/Models/Fragments/PromptsFragment.cs`
- [ ] Create `BlazorWebApp/Models/Fragments/LatentFragment.cs`

---

### Step 13.4: Create Enhancement Fragment Classes (5 points)
**Goal:** Implement typed classes for optional/enhancement fragments

**Files:**
- [ ] Create `BlazorWebApp/Models/Fragments/SeedVR2Fragment.cs`
- [ ] Create `BlazorWebApp/Models/Fragments/UpscaleFragment.cs`
- [ ] Create `BlazorWebApp/Models/Fragments/DetailerFragment.cs`
- [ ] Create `BlazorWebApp/Models/Fragments/ConditioningVariationFragment.cs`
- [ ] Create `BlazorWebApp/Models/Fragments/SeedVarianceEnhancerFragment.cs`

---

### Step 13.5: Create Wan/Video Fragment Classes (5 points)
**Goal:** Implement typed classes for video generation fragments

**Files:**
- [ ] Create `BlazorWebApp/Models/Fragments/WanSamplerFragment.cs`
- [ ] Create `BlazorWebApp/Models/Fragments/WanLoadModelFragment.cs`
- [ ] Create `BlazorWebApp/Models/Fragments/FrameInterpolationFragment.cs`
- [ ] Create `BlazorWebApp/Models/Fragments/WanPromptsFragment.cs`

---

### Step 13.6: Create FragmentRegistry (3 points)
**Goal:** Central registry that maps fragment files to their C# types

```csharp
public interface IFragmentRegistry
{
    FragmentBase CreateFragment(string fragmentFile, string id);
    Type? GetFragmentType(string fragmentFile);
    IEnumerable&lt;string&gt; GetRegisteredFiles();
}

public class FragmentRegistry : IFragmentRegistry
{
    private readonly Dictionary&lt;string, Type&gt; _registry = new(StringComparer.OrdinalIgnoreCase);
    
    public FragmentRegistry()
    {
        // Core fragments
        Register&lt;SamplerFragment&gt;("sampler.sbn");
        Register&lt;SamplerFragment&gt;("sampler-standard.sbn");
        Register&lt;PromptsFragment&gt;("prompts.sbn");
        Register&lt;LatentFragment&gt;("empty-latent.sbn");
        
        // Enhancement fragments
        Register&lt;SeedVR2Fragment&gt;("upscale-seedvr2.sbn");
        Register&lt;UpscaleFragment&gt;("upscale.sbn");
        Register&lt;DetailerFragment&gt;("detailer-core.sbn");
        Register&lt;ConditioningVariationFragment&gt;("conditioning-variation.sbn");
        Register&lt;SeedVarianceEnhancerFragment&gt;("seed-variance-enhancer.sbn");
        
        // Wan/Video fragments
        Register&lt;WanSamplerFragment&gt;("wan/sampler-wan.sbn");
        Register&lt;WanLoadModelFragment&gt;("wan/load-wan-model.sbn");
        Register&lt;FrameInterpolationFragment&gt;("wan/frame-interpolation.sbn");
        Register&lt;WanPromptsFragment&gt;("wan/prompts.sbn");
    }
    
    private void Register&lt;T&gt;(string fragmentFile) where T : FragmentBase, new()
    {
        _registry[fragmentFile] = typeof(T);
    }
    
    public FragmentBase CreateFragment(string fragmentFile, string id)
    {
        if (!_registry.TryGetValue(fragmentFile, out var type))
            throw new InvalidOperationException($"No fragment type registered for '{fragmentFile}'");
            
        var fragment = (FragmentBase)Activator.CreateInstance(type)!;
        fragment.Id = id;
        return fragment;
    }
    
    public Type? GetFragmentType(string fragmentFile)
        =&gt; _registry.GetValueOrDefault(fragmentFile);
        
    public IEnumerable&lt;string&gt; GetRegisteredFiles()
        =&gt; _registry.Keys;
}
```

**Files:**
- [ ] Create `BlazorWebApp/Services/FragmentRegistry.cs`
- [ ] Register in `Program.cs`

---

### Step 13.7: Update GenerationParameters (5 points)
**Goal:** Replace `Dictionary&lt;string, FragmentParameters&gt;` with `Dictionary&lt;string, FragmentBase&gt;`

```csharp
public class GenerationParameters
{
    /// &lt;summary&gt;
    /// All fragments organized by instance ID.
    /// Uses polymorphic JSON serialization.
    /// &lt;/summary&gt;
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(SamplerFragment), "sampler")]
    [JsonDerivedType(typeof(PromptsFragment), "prompts")]
    [JsonDerivedType(typeof(LatentFragment), "latent")]
    [JsonDerivedType(typeof(SeedVR2Fragment), "seed_vr2")]
    [JsonDerivedType(typeof(UpscaleFragment), "upscale")]
    [JsonDerivedType(typeof(DetailerFragment), "detailer")]
    [JsonDerivedType(typeof(ConditioningVariationFragment), "conditioning_variation")]
    [JsonDerivedType(typeof(SeedVarianceEnhancerFragment), "seed_variance_enhancer")]
    [JsonDerivedType(typeof(WanSamplerFragment), "wan_sampler")]
    [JsonDerivedType(typeof(WanLoadModelFragment), "wan_load_model")]
    [JsonDerivedType(typeof(FrameInterpolationFragment), "frame_interpolation")]
    [JsonDerivedType(typeof(WanPromptsFragment), "wan_prompts")]
    public Dictionary&lt;string, FragmentBase&gt; Fragments { get; set; } = new();
    
    // Typed accessors for common fragments
    [JsonIgnore] public PromptsFragment? Prompts =&gt; GetFragment&lt;PromptsFragment&gt;("prompts");
    [JsonIgnore] public SamplerFragment? MainSampler =&gt; GetFragment&lt;SamplerFragment&gt;("main_sampler");
    [JsonIgnore] public LatentFragment? Latent =&gt; GetFragment&lt;LatentFragment&gt;("empty_latent");
    [JsonIgnore] public SeedVR2Fragment? SeedVR2 =&gt; GetFragment&lt;SeedVR2Fragment&gt;("seed_vr2");
    
    public T? GetFragment&lt;T&gt;(string id) where T : FragmentBase
        =&gt; Fragments.TryGetValue(id, out var fragment) ? fragment as T : null;
    
    public T GetOrCreateFragment&lt;T&gt;(string id) where T : FragmentBase, new()
    {
        if (!Fragments.TryGetValue(id, out var fragment) || fragment is not T typed)
        {
            typed = new T { Id = id };
            Fragments[id] = typed;
        }
        return typed;
    }
    
    /// &lt;summary&gt;
    /// Flattens all fragments for Scriban template rendering.
    /// &lt;/summary&gt;
    public Dictionary&lt;string, object&gt; FlattenForTemplateRendering()
    {
        var result = new Dictionary&lt;string, object&gt;(StringComparer.OrdinalIgnoreCase);
        
        foreach (var (id, fragment) in Fragments)
        {
            // Add the fragment itself (for IsActive checks)
            result[id] = fragment;
            
            // Add flattened properties
            foreach (var (key, value) in fragment.ToDictionary())
            {
                if (value != null)
                    result[key] = value;
            }
        }
        
        // Add assets, sources, loras as before
        foreach (var (key, value) in Assets)
            result[key] = value;
            
        // ... etc
        
        return result;
    }
    
    // Keep existing Assets, Sources, Loras properties
    public Dictionary&lt;string, string&gt; Assets { get; set; } = new();
    public Dictionary&lt;string, SourceAsset&gt; Sources { get; set; } = new();
    public List&lt;Lora&gt; Loras { get; set; } = new();
    public Guid? WorkflowId { get; set; }
}
```

**Files:**
- [ ] Update `BlazorWebApp/Models/GenerationParameters.cs`
- [ ] Remove `BlazorWebApp/Models/FragmentParameters.cs`

---

### Step 13.8: Update GenerationParameterService (5 points)
**Goal:** Simplify service - remove all string-based parameter access methods

```csharp
public interface IGenerationParameterService
{
    GenerationParameters Current { get; }
    
    /// &lt;summary&gt;
    /// Gets a typed fragment by ID.
    /// &lt;/summary&gt;
    T? GetFragment&lt;T&gt;(string fragmentId) where T : FragmentBase;
    
    /// &lt;summary&gt;
    /// Gets or creates a typed fragment.
    /// &lt;/summary&gt;
    T GetOrCreateFragment&lt;T&gt;(string fragmentId) where T : FragmentBase, new();
    
    /// &lt;summary&gt;
    /// Sets fragment active state. Creates fragment if it doesn't exist.
    /// &lt;/summary&gt;
    void SetFragmentActive&lt;T&gt;(string fragmentId, bool active) where T : FragmentBase, new();
    
    /// &lt;summary&gt;
    /// Initializes fragments from a workflow's pipeline.
    /// &lt;/summary&gt;
    void InitializeFromWorkflow(Workflow workflow);
    
    /// &lt;summary&gt;
    /// Gets dynamic options for a property with [DynamicSource] attribute.
    /// &lt;/summary&gt;
    Task&lt;List&lt;string&gt;&gt; GetDynamicOptionsAsync&lt;T&gt;(Expression&lt;Func&lt;T, object&gt;&gt; propertyExpression);
    
    // REMOVED - No longer needed:
    // - SetFragmentValue(string fragmentId, string parameterName, object? value)
    // - GetFragmentValue&lt;T&gt;(string fragmentId, string parameterName)
    // - GetFragment(string fragmentId) - replaced by typed version
}
```

**Files:**
- [ ] Update `BlazorWebApp/Services/IGenerationParameterService.cs`
- [ ] Update `BlazorWebApp/Services/GenerationParameterService.cs`

---

### Step 13.9: Update Fragment Forms (8 points)
**Goal:** Rewrite forms to bind directly to typed fragment properties

#### Example: SeedVR2Form (After)
```razor
@using static BlazorWebApp.Models.FragmentKeys
@inject IGenerationParameterService ParameterService

&lt;MudGrid&gt;
    &lt;MudItem xs="6"&gt;
        &lt;MudSelect T="string" Label="DiT Model" 
                   @bind-Value="Fragment.Model"
                   AnchorOrigin="Origin.BottomLeft"&gt;
            @foreach (var model in _modelOptions)
            {
                &lt;MudSelectItem Value="@model" /&gt;
            }
        &lt;/MudSelect&gt;
    &lt;/MudItem&gt;
    &lt;MudItem xs="6"&gt;
        &lt;MudSlider T="int" 
                   @bind-Value="Fragment.Resolution"
                   Min="512" Max="4096" Step="64"
                   Variant="Variant.Filled" ValueLabel&gt;
            &lt;small&gt;Resolution:&lt;/small&gt; @(Fragment.Resolution)px
        &lt;/MudSlider&gt;
    &lt;/MudItem&gt;
    @* ... other fields bind directly to Fragment.PropertyName ... *@
&lt;/MudGrid&gt;

@code {
    [Parameter] public string FragmentId { get; set; } = Fragments.SeedVR2;
    
    // Direct typed access - no dictionary, no string keys
    private SeedVR2Fragment Fragment =&gt; ParameterService.GetOrCreateFragment&lt;SeedVR2Fragment&gt;(FragmentId);
    
    private List&lt;string&gt; _modelOptions = new();
    
    protected override async Task OnInitializedAsync()
    {
        // Load dynamic options using attribute metadata
        _modelOptions = await ParameterService.GetDynamicOptionsAsync&lt;SeedVR2Fragment&gt;(f =&gt; f.Model);
    }
}
```

**Files to Update:**
- [ ] Rewrite `SeedVR2Form.razor`
- [ ] Rewrite `UpscaleForm.razor`
- [ ] Rewrite `SamplerForm.razor`
- [ ] Rewrite `LatentForm.razor`
- [ ] Rewrite `PromptsForm.razor`
- [ ] Rewrite `ConditioningVariationForm.razor`
- [ ] Rewrite `SeedVarianceEnhancerForm.razor`

---

### Step 13.10: Update WorkflowService (3 points)
**Goal:** Simplify condition evaluation to single line

```csharp
// Before: 50+ lines of reflection and dictionary walking
private bool EvaluateCondition(string? conditionPath, Dictionary&lt;string, object&gt; parameters)
{
    // Complex path splitting, case conversion, dictionary lookups...
}

// After: Direct property access
private bool ShouldRenderFragment(string fragmentId, GenerationParameters parameters)
{
    return parameters.Fragments.TryGetValue(fragmentId, out var fragment) &amp;&amp; fragment.IsActive;
}
```

**Files:**
- [ ] Simplify `WorkflowService.cs` - remove `EvaluateCondition`, `EvaluateConditions`
- [ ] Update `RenderFragment` to use `ShouldRenderFragment`
- [ ] Remove schema parsing from `WorkflowService.cs`
- [ ] Remove `GetValueOrDefault`, `SetValue` extension methods

---

### Step 13.11: Update State Persistence (3 points)
**Goal:** Ensure typed fragments serialize/deserialize correctly with polymorphic JSON

**Files:**
- [ ] Update `StateService.cs` serialization options
- [ ] Test round-trip serialization for all fragment types

---

### Step 13.12: Remove Old Code (5 points)
**Goal:** Clean removal of deprecated code

**Files to Remove:**
- [ ] Delete `BlazorWebApp/Models/FragmentParameters.cs`
- [ ] Delete `BlazorWebApp/Models/ParameterConstraints.cs`
- [ ] Delete `BlazorWebApp/Models/FragmentSchema.cs`
- [ ] Delete `BlazorWebApp/Services/FragmentSchemaService.cs`
- [ ] Remove `FragmentKeys.Params` from `FragmentKeys.cs`
- [ ] Remove `GetValueOrDefault`, `SetValue` extension methods

**Code to Remove from Services:**
- [ ] `IGenerationParameterService.SetFragmentValue`
- [ ] `IGenerationParameterService.GetFragmentValue`
- [ ] `WorkflowService.ParseFragmentSchema`
- [ ] `WorkflowService.GetFragmentSchema`
- [ ] `WorkflowService.GetWorkflowFragmentSchemas`
- [ ] `WorkflowService.ParseFragmentDefaults`

---

## Files Summary

### New Files (15)
```
BlazorWebApp/Models/Fragments/
??? Attributes/
?   ??? DynamicSourceAttribute.cs
?   ??? StepAttribute.cs
??? FragmentBase.cs
??? SamplerFragment.cs
??? PromptsFragment.cs
??? LatentFragment.cs
??? SeedVR2Fragment.cs
??? UpscaleFragment.cs
??? DetailerFragment.cs
??? ConditioningVariationFragment.cs
??? SeedVarianceEnhancerFragment.cs
??? WanSamplerFragment.cs
??? WanLoadModelFragment.cs
??? FrameInterpolationFragment.cs
??? WanPromptsFragment.cs

BlazorWebApp/Services/
??? FragmentRegistry.cs
```

### Files to Update (12)
```
BlazorWebApp/Models/GenerationParameters.cs
BlazorWebApp/Models/FragmentKeys.cs (remove Params class)
BlazorWebApp/Services/IGenerationParameterService.cs
BlazorWebApp/Services/GenerationParameterService.cs
BlazorWebApp/Services/WorkflowService.cs
BlazorWebApp/Services/StateService.cs
BlazorWebApp/Components/Shared/Generation/Fragments/SeedVR2Form.razor
BlazorWebApp/Components/Shared/Generation/Fragments/UpscaleForm.razor
BlazorWebApp/Components/Shared/Generation/Fragments/SamplerForm.razor
BlazorWebApp/Components/Shared/Generation/Fragments/LatentForm.razor
BlazorWebApp/Components/Shared/Generation/Fragments/PromptsForm.razor
BlazorWebApp/Components/Shared/Generation/Fragments/ConditioningVariationForm.razor
BlazorWebApp/Components/Shared/Generation/Fragments/SeedVarianceEnhancerForm.razor
BlazorWebApp/Program.cs (register FragmentRegistry)
```

### Files to Delete (5+)
```
BlazorWebApp/Models/FragmentParameters.cs
BlazorWebApp/Models/ParameterConstraints.cs
BlazorWebApp/Models/FragmentSchema.cs
BlazorWebApp/Services/FragmentSchemaService.cs
BlazorWebApp/Services/IFragmentSchemaService.cs
```

---

## Success Criteria

- [ ] All fragment forms bind directly to typed properties
- [ ] No string parameter names anywhere in UI code
- [ ] Compile-time errors for property name typos
- [ ] IntelliSense works for all fragment properties
- [ ] `[Range]` and `[Step]` attributes provide UI constraints
- [ ] `[DynamicSource]` attribute populates dropdowns from ComfyUI
- [ ] State persistence works with polymorphic JSON
- [ ] Condition evaluation is &lt;5 lines of code
- [ ] All old dictionary-based code removed
- [ ] Build passes with zero warnings about obsolete code
- [ ] All existing tests updated or removed

---

## New Fragment Workflow (Post-Implementation)

Adding a new fragment requires only:

1. **Create Fragment Class** (1 file):
   ```csharp
   public class MyNewFragment : FragmentBase
   {
       public override string FragmentFile =&gt; "my-new-fragment.sbn";
       
       [JsonPropertyName("my_param")]
       [Range(0, 100)]
       [Step(5)]
       public int MyParam { get; set; } = 50;
       
       [JsonPropertyName("my_model")]
       [DynamicSource("MyNodeType", "model")]
       public string MyModel { get; set; } = string.Empty;
       
       public override FragmentBase Clone(string newId) =&gt; new MyNewFragment { /* copy props */ };
   }
   ```

2. **Register in FragmentRegistry** (1 line):
   ```csharp
   Register&lt;MyNewFragment&gt;("my-new-fragment.sbn");
   ```

3. **Add JsonDerivedType to GenerationParameters** (1 line):
   ```csharp
   [JsonDerivedType(typeof(MyNewFragment), "my_new")]
   ```

4. **Create Form Component** (1 file):
   ```razor
   @code {
       private MyNewFragment Fragment =&gt; ParameterService.GetOrCreateFragment&lt;MyNewFragment&gt;(FragmentId);
   }
   &lt;MudSlider @bind-Value="Fragment.MyParam" Min="0" Max="100" Step="5" /&gt;
   ```

**Total: 3 files, 1 line registration** vs. the old approach of updating FragmentKeys.Params, matching strings everywhere, updating schema parsing, etc.

---

## Total Complexity: 42 points

| Step | Points | Description |
|------|--------|-------------|
| 13.1 | 2 | Custom attributes |
| 13.2 | 5 | FragmentBase abstract class |
| 13.3 | 8 | Core fragment classes |
| 13.4 | 5 | Enhancement fragment classes |
| 13.5 | 5 | Wan/Video fragment classes |
| 13.6 | 3 | FragmentRegistry service |
| 13.7 | 5 | Update GenerationParameters |
| 13.8 | 5 | Update GenerationParameterService |
| 13.9 | 8 | Update all fragment forms |
| 13.10 | 3 | Simplify WorkflowService |
| 13.11 | 3 | Update state persistence |
| 13.12 | 5 | Remove old code |

---

## Execution Order

1. **Step 13.1-13.2**: Create base infrastructure (attributes + FragmentBase)
2. **Step 13.3-13.5**: Create all fragment classes (can parallelize)
3. **Step 13.6**: Create FragmentRegistry
4. **Step 13.7-13.8**: Update GenerationParameters and service
5. **Step 13.9**: Update forms (biggest effort)
6. **Step 13.10-13.11**: Update WorkflowService and StateService
7. **Step 13.12**: Remove old code (final cleanup)

Ready to begin execution?
