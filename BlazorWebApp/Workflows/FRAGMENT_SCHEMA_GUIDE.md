# Fragment Schema Guide

This guide documents the C# fragment system used for building ComfyUI workflows. Fragments are strongly-typed C# classes implementing `IFragmentBuilder` that generate reusable groups of ComfyUI nodes.

**Last Updated:** April 2026 - aligned with the current fluent workflow metadata and UI bridge

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Data Flow Summary](#data-flow-summary)
3. [Fragment Structure](#fragment-structure)
4. [FragmentMetadata Reference](#fragmentmetadata-reference)
5. [FragmentType Enum](#fragmenttype-enum)
6. [FragmentParameter Reference](#fragmentparameter-reference)
7. [Service Responsibilities](#service-responsibilities)
8. [Component Registry](#component-registry)
9. [Default Value Resolution](#default-value-resolution)
10. [Complete Examples](#complete-examples)
11. [Creating New Fragments](#creating-new-fragments)
12. [Quick Reference](#quick-reference)

---

## Architecture Overview

```
+------------------------------------------------------------------+
|               Workflow Class (IWorkflowBuilder)                   |
|  +----------+ +----------+ +------------------------------------+|
|  |  Assets  | | Sources  | |   Fragment instances (C#)          ||
|  | (models) | |(img/vid) | |   _loader, _sampler, _save, ...   ||
|  +----------+ +----------+ +------------------------------------+|
+------------------------------------------------------------------+
                              |
         +--------------------+--------------------+
         |                    |                    |
         v                    v                    v
+--------------------+ +--------------------+ +--------------------+
| IFragmentBuilder  | | IFragmentBuilder  | | IFragmentBuilder  |
| FragmentMetadata  | | FragmentMetadata  | | FragmentMetadata  |
| - Id, Type, Title | | - Id, Type, Title | | - IsHidden=true   |
| - Component       | | - Parameters[]    | | - No UI           |
| - Parameters[]    | | - (dynamic fields)| |                    |
+--------------------+ +--------------------+ +--------------------+
  (designed comp)       (dynamic fields)       (utility fragment)
         |                    |
         v                    v
+------------------------------------------------------------------+
|                    GenerationParameters                           |
|  +---------------+ +---------------+ +---------------+ +--------+|
|  |  Fragments    | |    Assets     | |   Sources     | | Loras  ||
|  | Dict<id,val>  | | Dict<id,val>  | | Dict<id,val>  | | List   ||
|  +---------------+ +---------------+ +---------------+ +--------+|
+------------------------------------------------------------------+
                              |
                              v
+------------------------------------------------------------------+
|               ComfyWorkflowBuilder + NodeRegistry                 |
|  IWorkflowBuilder.Build(params) -> ComfyUI Workflow JSON          |
+------------------------------------------------------------------+
```

### Key Principles

| Principle                     | Description                                                                    |
| ----------------------------- | ------------------------------------------------------------------------------ |
| **Single Source of Truth**    | Fragment C# class defines both node logic and UI schema via `FragmentMetadata` |
| **Workflow Owns Defaults**    | Workflow `Build()` method provides default values via `GenerationParameters`   |
| **Metadata Owns Constraints** | Min/max/step live in `FragmentParameter`, not AppSettings                      |
| **Type-Based Discovery**      | `FragmentType` enum drives UI layout decisions                                 |
| **Compile-Time Safety**       | All fragment logic is validated at build time                                  |
| **Hybrid Rendering**          | Designed components are the active fluent UI path; metadata-only dynamic rendering is not wired yet |

---

## Data Flow Summary

### 1. Workflow Discovery (Application Start)

```
WorkflowService.GetWorkflows()
    +-- Reflection scans for IWorkflowBuilder implementations
        +-- Each workflow exposes WorkflowMetadata (Id, Title, Base, Mode, Assets, Sources, CompatibleResourceBaseModels)
        +-- Each workflow declares fragment instances -> FragmentMetadata available
```

### 2. Workflow Selection (User navigates to /generate/{id})

```
Generate.razor.OnWorkflowSelected()
    +-- GenerationParameterService.InitializeFromWorkflow(workflow)
        |-- Clear existing Fragments, Assets, Sources
        |-- Initialize Assets from workflow.Assets[].DefaultValue
        |-- Initialize Sources from workflow.Sources[]
        +-- InitializeFragmentsFromMetadata(workflow)
            +-- For each IFragmentBuilder in workflow:
                |-- Create FragmentParameters from FragmentMetadata
                |-- Apply default values from FragmentParameter.DefaultValue
                +-- Set IsActive = !metadata.DefaultCollapsed
```

### 3. Fragment Discovery (GenerationParameterService)

```
DiscoverFragments()
    +-- For each Parameters.Fragments:
        |-- Get FragmentMetadata from workflow's fragment instances
        +-- Switch on metadata.Type:
            |-- FragmentType.Sampler -> _samplerFragmentId
            |-- FragmentType.Latent -> _latentFragmentId
            |-- FragmentType.Prompts -> _promptsFragmentId
            |-- FragmentType.Settings -> optional fragments
            |-- FragmentType.Enhancement -> optional fragments
            |-- FragmentType.Loader/Input -> latent fallback when width/height are present
            +-- FragmentType.Unknown/Conditioning/Output -> optional when collapsible or component-backed
```

### 4. Generation (User clicks Generate)

```
ImageService.GenerateImagesAsync(parameters, workflow)
    +-- WorkflowService.ComposeWorkflow(workflow, parameters)
        +-- IWorkflowBuilder.Build(parameters)
            |-- Creates ComfyWorkflowBuilder + NodeRegistry
            |-- For each fragment:
            |   |-- Check IsActive / InclusionCondition
            |   |-- fragment.Build(builder, parameters, registry, scope)
            |   +-- Fragment adds nodes + registers outputs
            +-- builder.ToJson() -> ComfyUI workflow JSON
    +-- Send to ComfyUI API
    +-- SaveImages() -> Database
```

---

## Fragment Structure

Fragments are C# classes in `Workflows/Fragments/` implementing `IFragmentBuilder`.

### IFragmentBuilder Interface

```csharp
public interface IFragmentBuilder
{
    FragmentMetadata Metadata { get; }

    void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "");
}
```

### Complete Fragment Example

```csharp
public class SamplerFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "main_sampler",
        Type = FragmentType.Sampler,
        Title = "Sampler",
        Component = "SamplerForm",
        Icon = "fa-solid fa-dice",
        Order = 50,
        Collapsible = true,
        Parameters = [
            new() { Name = "sampler_name", Type = ParameterType.Select, Source = new DynamicSource("KSampler", "sampler_name") },
            new() { Name = "scheduler", Type = ParameterType.Select, Source = new DynamicSource("KSampler", "scheduler") },
            new() { Name = "steps", Type = ParameterType.Slider, Min = 1, Max = 150, Step = 1, DefaultValue = 20 },
            new() { Name = "cfg", Type = ParameterType.Slider, Min = 1, Max = 30, Step = 0.5, DefaultValue = 7.0 },
            new() { Name = "seed", Type = ParameterType.Number, Min = -1, DefaultValue = -1L }
        ]
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(scope + Metadata.Id);
        if (fragment == null || !fragment.IsActive) return;

        var samplerId = scope + "sampler_main";

        builder.AddNode(samplerId, node => node
            .Type("KSampler")
            .Input("sampler_name", fragment.GetString("sampler_name", "euler"))
            .Input("scheduler", fragment.GetString("scheduler", "simple"))
            .Input("steps", fragment.GetInt("steps", 20))
            .Input("cfg", fragment.GetDouble("cfg", 7.0))
            .Input("seed", fragment.GetLong("seed", -1))
            .InputRef("model", registry.GetRef<ModelOutput>(scope))
            .InputRef("positive", registry.GetRef<ConditioningOutput>(scope + "positive"))
            .InputRef("negative", registry.GetRef<ConditioningOutput>(scope + "negative"))
            .InputRef("latent_image", registry.GetRef<LatentOutput>(scope))
            .Meta(scopeTitle + "Sampler"));

        registry.Register(new LatentOutput(samplerId, 0));
    }
}
```

---

## FragmentMetadata Reference

Defined in `Workflows/Models/FragmentMetadata.cs`:

| Property             | Type                                    | Default      | Description                                                      |
| -------------------- | --------------------------------------- | ------------ | ---------------------------------------------------------------- |
| `Id`                 | string                                  | **Required** | Unique identifier for parameter storage and UI rendering         |
| `Type`               | FragmentType                            | **Required** | Fragment classification (see [FragmentType](#fragmenttype-enum)) |
| `Title`              | string                                  | **Required** | Display title in UI                                              |
| `Component`          | string?                                 | `null`       | Blazor component name. For fluent fragments, set this on user-visible fragments; metadata-only dynamic rendering is not implemented yet |
| `Icon`               | string?                                 | `null`       | FontAwesome icon class                                           |
| `Order`              | int                                     | `100`        | Display order (lower = higher priority)                          |
| `Collapsible`        | bool                                    | `true`       | Whether fragment can be collapsed in UI                          |
| `DefaultCollapsed`   | bool                                    | `false`      | Initial collapsed state; if `true`, fragment starts inactive     |
| `IsHidden`           | bool                                    | `false`      | Hidden from UI (utility/loader fragments)                        |
| `Parameters`         | IEnumerable&lt;FragmentParameter&gt;    | `[]`         | Parameter definitions for UI and validation                      |
| `InclusionCondition` | Func&lt;GenerationParameters, bool&gt;? | `null`       | Lambda controlling conditional inclusion                         |

### Order Ranges

| Range   | Purpose               | Examples                      |
| ------- | --------------------- | ----------------------------- |
| 0-49    | Core inputs           | Prompts (10), LoRAs (15)      |
| 50-99   | Primary generation    | Latent (20), Sampler (50)     |
| 100-149 | Enhancement           | Upscale (100), Detailer (120) |
| 150+    | Advanced/experimental | Custom nodes                  |

---

## FragmentType Enum

```csharp
public enum FragmentType
{
    Unknown,
    Loader,          // Model loading (typically hidden, no direct UI)
    Input,           // Image/video/source inputs that may also surface size controls
    Latent,          // Resolution/latent image settings
    Prompts,         // Positive/negative prompts
    Conditioning,    // CLIP, pose, edit, or other conditioning stages
    Sampler,         // KSampler and sampler variants
    Settings,        // Supplemental settings panels surfaced as optional UI fragments
    Enhancement,     // Upscale, detailer, frame interpolation, etc.
    Utility,         // Helper fragments with no UI
    Output           // Decode/save/output stages
}
```

### Discovery in GenerationParameterService

```csharp
switch (metadata.Type)
{
    case FragmentType.Prompts:
        _promptsFragmentId ??= fragmentId;
        break;
    case FragmentType.Sampler:
        _samplerFragmentId ??= fragmentId;
        break;
    case FragmentType.Latent:
        _latentFragmentId ??= fragmentId;
        break;
    case FragmentType.Settings:
    case FragmentType.Enhancement:
        // Renders as optional collapsible section
        break;
}
```

---

## FragmentParameter Reference

Defined in `Workflows/Models/FragmentParameter.cs`:

| Property       | Type      | Description                                                |
| -------------- | --------- | ---------------------------------------------------------- |
| `Name`         | string    | Parameter key (matches key in `FragmentParameters.Values`) |
| `Label`        | string?   | Display label (defaults to Name if null)                   |
| `Type`         | ParameterType | UI control type for the parameter                     |
| `DefaultValue` | object?   | Default value for initialization                           |
| `Min`          | double?   | Minimum value (for numeric inputs)                         |
| `Max`          | double?   | Maximum value (for numeric inputs)                         |
| `Step`         | double?   | Step increment (for sliders)                               |
| `Source`       | DynamicSource? | Dynamic option source (`NodeType` + `InputName`)     |
| `Options`      | IEnumerable&lt;string&gt;? | Static option list for select fields    |
| `Description`  | string?   | Optional help text for the UI                              |

### Dynamic Source Configuration

For select fields that query ComfyUI node info:

```csharp
new FragmentParameter
{
    Name = "sampler_name",
    Type = ParameterType.Select,
    Source = new DynamicSource("KSampler", "sampler_name")
}
```

The service calls ComfyUI's `/object_info/{Source.NodeType}` API and extracts options from `input.required.{Source.InputName}` or `input.optional.{Source.InputName}`.

---

## Service Responsibilities

### WorkflowService

**Responsibility:** Workflow discovery, composition, and fragment metadata access

| Method                  | Purpose                                                        |
| ----------------------- | -------------------------------------------------------------- |
| `GetWorkflows()`        | Discover all `IWorkflowBuilder` implementations via reflection |
| `RefreshWorkflows()`    | Re-scan assemblies for workflow classes                        |
| `ComposeWorkflow()`     | Call `IWorkflowBuilder.Build()` to generate ComfyUI JSON       |
| `GetFragmentMetadata()` | Get `FragmentMetadata` for a workflow's fragments              |

### GenerationParameterService

**Responsibility:** Runtime parameter state management

| Method                        | Purpose                                  |
| ----------------------------- | ---------------------------------------- |
| `InitializeFromWorkflow()`    | Set up parameters from workflow metadata |
| `SetFragmentValue()`          | Update a fragment parameter value        |
| `SetFragmentActive()`         | Enable/disable a fragment                |
| `GetFragmentValue<T>()`       | Read a typed parameter value             |
| `ResolveSourceOptionsAsync()` | Query ComfyUI for dynamic select options |
| `CreateSnapshot()`            | Clone current state for persistence      |
| `LoadParameters()`            | Restore state from snapshot              |

### ImageService

**Responsibility:** Generation orchestration, file saving, database persistence

| Method                  | Purpose                   |
| ----------------------- | ------------------------- |
| `GenerateImagesAsync()` | Unified image generation  |
| `GenerateVideoAsync()`  | Unified video generation  |
| `SaveImages()`          | Save to disk and database |

---

## Component Registry

Fragment form components are registered through `[FragmentComponent("...")]` discovery in `ComponentRegistry.cs`, with manual registration used only as a fallback when no attributed components are found.

Current attributed components in `Components/Shared/Generation/Fragments/` include:

```csharp
ConditioningVariationForm
DetailerForm
DoubleSamplerForm
FrameInterpolationForm
LatentForm
LtxSamplerForm
LtxVideoSettingsForm
PainterI2VForm
PromptsForm
ReferenceLatentSettingsForm
SamplerForm
SeedVarianceEnhancerForm
SeedVR2Form
UpscaleForm
```

### Component Naming Convention

- Match fragment class: `SamplerFragment` -> `SamplerForm`
- PascalCase with `Form` suffix
- Located in `Components/Shared/Generation/Fragments/`
- For fluent workflow fragments, declare `Component` when the fragment should render in the UI

---

## Default Value Resolution

**Priority Order** (documented in `IGenerationParameterService`):

| Priority        | Source                         | Description                                                    | Persisted |
| --------------- | ------------------------------ | -------------------------------------------------------------- | --------- |
| **1 (Highest)** | Saved Workflow State           | Previously saved parameters for this workflow (database)       | Yes       |
| **2**           | FragmentParameter.DefaultValue | Default from fragment's metadata `Parameters` collection       | No        |
| **3**           | Dynamic Source Resolution      | First option from ComfyUI API query (for fields with `Source`) | No        |

### Important Conventions

1. **Fragment metadata is the default source**
   - Each `FragmentParameter` can define a `DefaultValue`
   - Workflows can override defaults by pre-populating `GenerationParameters` before fragment init

2. **Dynamic sources set defaults during initialization**
   - When `InitializeFromWorkflowAsync()` runs, dynamic sources are pre-resolved
   - If no value is set from priorities 1-2, the first option from ComfyUI API is used
   - Pre-resolved options are cached in `FragmentParameters.ResolvedOptions` for sync UI access

### Example Priority Resolution

```
Fragment: SamplerFragment (Id = "main_sampler")
Parameter: steps

Priority 1: Database saved state has steps=35 -> Use 35
Priority 2: (skipped)

---

Fragment: SamplerFragment
Parameter: steps (no saved state)

Priority 1: No saved state
Priority 2: FragmentParameter.DefaultValue = 20 -> Use 20

---

Fragment: SamplerFragment
Parameter: sampler_name (no saved state, no default)

Priority 1: No saved state
Priority 2: No DefaultValue
Priority 3: ComfyUI returns ["euler", "euler_ancestral", ...] -> Use "euler"
```

---

## Complete Examples

Current runtime note: fluent fragments with `Component = null` do not automatically render a dynamic form yet. `FragmentRenderer` only renders registered components today, so metadata-only fragments should remain hidden or be paired with a component before being exposed in `GetFragments()`.

### Example 1: Utility Fragment (No UI)

```csharp
public class LoadDiffusionFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "loader",
        Type = FragmentType.Loader,
        Title = "Load Diffusion Model",
        IsHidden = true
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var unetName = parameters.Assets["Model"];
        var clipName = parameters.Assets["Clip"];
        var vaeName = parameters.Assets["Vae"];

        builder.AddNode(scope + "unet_loader", node => node
            .Type("UNETLoader")
            .Input("unet_name", unetName)
            .Input("weight_dtype", "default")
            .Meta(scopeTitle + "Load Diffusion Model"));

        registry.Register(new ModelOutput(scope + "unet_loader", 0));

        // ... CLIP and VAE loaders similarly
    }
}
```

### Example 2: Enhancement Fragment (Optional, with Condition)

```csharp
public class UpscaleFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "upscale",
        Type = FragmentType.Enhancement,
        Title = "SeedVR2 Upscale",
        Component = "SeedVR2Form",
        Icon = "fa-solid fa-expand",
        Order = 100,
        Collapsible = true,
        DefaultCollapsed = true,
        Parameters = [
            new() { Name = "seedvr2_model", Type = ParameterType.Select, Source = new DynamicSource("SeedVR2LoadDiTModel", "model") },
            new() { Name = "seedvr2_resolution", Type = ParameterType.Slider, Min = 512, Max = 4096, Step = 64, DefaultValue = 2048 }
        ]
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        if (fragment == null || !fragment.IsActive) return;

        // Build upscale nodes...
    }
}
```

### Example 3: Workflow Composing Fragments

```csharp
public class FluxTxt2ImgWorkflow : IWorkflowBuilder
{
    private readonly LoadDiffusionFragment _loader = new();
    private readonly PromptsFragment _prompts = new();
    private readonly EmptyLatentFragment _latent = new();
    private readonly SamplerFragment _sampler = new();
    private readonly VaeDecodeFragment _vaeDecode = new();
    private readonly SaveFragment _save = new();
    private readonly UpscaleFragment _upscale = new();

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        _loader.Build(builder, parameters, registry);
        _prompts.Build(builder, parameters, registry);
        _latent.Build(builder, parameters, registry);
        _sampler.Build(builder, parameters, registry);
        _vaeDecode.Build(builder, parameters, registry);
        _upscale.Build(builder, parameters, registry);
        _save.Build(builder, parameters, registry);

        return new ComfyWorkflow { Json = builder.ToJson() };
    }

    public IEnumerable<IFragmentBuilder> GetFragments() =>
        [_prompts, _latent, _sampler, _upscale];
}
```

---

## Creating New Fragments

### Adding a New Fragment (1-2 files)

1. **Create the fragment class** (e.g., `Workflows/Fragments/Enhancements/MyNodeFragment.cs`):

```csharp
public class MyNodeFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "my_node",
        Type = FragmentType.Enhancement,
        Title = "My New Node",
        Component = "MyNodeForm",
        Collapsible = true,
        DefaultCollapsed = true,
        Parameters = [
            new() { Name = "strength", Label = "Strength", Type = ParameterType.Slider, Min = 0, Max = 1, Step = 0.01, DefaultValue = 0.5 }
        ]
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        if (fragment == null || !fragment.IsActive) return;

        var nodeId = scope + "my_node";
        builder.AddNode(nodeId, node => node
            .Type("MyCustomNode")
            .Input("strength", fragment.GetDouble("strength", 0.5))
            .InputRef("image", registry.GetRef<ImageOutput>(scope))
            .Meta(scopeTitle + "My Node"));

        registry.Register(new ImageOutput(nodeId, 0));
    }
}
```

2. **Add to workflow class** - instantiate and call in `Build()`:

```csharp
private readonly MyNodeFragment _myNode = new();

// In Build():
_myNode.Build(builder, parameters, registry);

// In GetFragments():
public IEnumerable<IFragmentBuilder> GetFragments() =>
    [_sampler, _myNode];
```

3. **(Optional) Create designed component** if complex UI needed:
   - Create `Components/Shared/Generation/Fragments/MyNodeForm.razor`
    - Add `[FragmentComponent("MyNodeForm")]` so `ComponentRegistry` can auto-discover it
   - Set `Component = "MyNodeForm"` in `FragmentMetadata`

---

## Quick Reference

### Fragment Class Template

```csharp
public class MyFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "my_fragment",
        Type = FragmentType.Enhancement,
        Title = "Display Title",
        Component = "ComponentName",    // Set this for user-visible fluent fragments
        Icon = "fa-solid fa-icon",
        Order = 100,
        Collapsible = true,
        DefaultCollapsed = true,
        IsHidden = false,
        Parameters = [
            new() { Name = "param_name", Type = ParameterType.Slider, Min = 0, Max = 100, Step = 1, DefaultValue = 50 }
        ],
        InclusionCondition = p => p.GetFragment("my_fragment")?.IsActive == true
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "") { /* ... */ }
}
```

### FragmentType Values

| Type           | Use For                                    |
| -------------- | ------------------------------------------ |
| `Loader`       | Model loading fragments (typically hidden) |
| `Input`        | Source/image/video input fragments         |
| `Prompts`      | Prompt encoding                            |
| `Latent`       | Resolution/latent settings                 |
| `Sampler`      | Sampling settings                          |
| `Conditioning` | CLIP/conditioning                          |
| `Settings`     | Supplemental settings panels               |
| `Enhancement`  | Upscale/detailer/optional features         |
| `Utility`      | Helper fragments, no UI                    |
| `Output`       | Decode/save/output nodes                   |

### Type-Safe Output Types

| Type                 | Use For                   |
| -------------------- | ------------------------- |
| `ModelOutput`        | UNet/DiT model outputs    |
| `ClipOutput`         | Text encoder outputs      |
| `VaeOutput`          | VAE model outputs         |
| `LatentOutput`       | Latent image outputs      |
| `ImageOutput`        | Decoded image outputs     |
| `ConditioningOutput` | CLIP conditioning outputs |

### Type-Safe Parameter Accessors

```csharp
fragment.GetString("key", "default")
fragment.GetInt("key", 0)
fragment.GetDouble("key", 0.0)
fragment.GetLong("key", 0L)
fragment.GetBool("key", false)
```

---

_This guide is the authoritative reference for the fragment system. Update this document when making architectural changes._
