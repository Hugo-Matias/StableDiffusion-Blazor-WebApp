# Generation System Architecture

## Overview

This document provides a comprehensive walkthrough of the image/video generation system from application startup through to completed output. It's designed to help developers understand the complete data flow and service interactions.

---

## Table of Contents

1. [High-Level Architecture](#high-level-architecture)
2. [Application Startup](#application-startup)
3. [Core Data Models](#core-data-models)
4. [Service Layer](#service-layer)
5. [Workflow Templates](#workflow-templates)
6. [UI Components](#ui-components)
7. [Generation Flow](#generation-flow)
8. [Event System](#event-system)
9. [State Persistence](#state-persistence)
10. [Error Handling](#error-handling)

---

## High-Level Architecture

```
???????????????????????????????????????????????????????????????????????
?                         Generate.razor (UI)                         ?
?   ???????????????? ???????????????? ?????????????????????????????????
?   ? PromptsForm  ? ? SamplerForm  ? ?  Optional Feature Sections   ??
?   ? LatentForm   ? ? SourcesPanel ? ?  (Upscale, SeedVR2, etc.)   ??
?   ???????????????? ???????????????? ?????????????????????????????????
???????????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????????
?                   IGenerationParameterService                        ?
?  ????????????????????????????????????????????????????????????????????
?  ? GenerationParameters { Fragments, Assets, Sources, Loras }      ??
?  ?   ??? FragmentParameters["prompts"] ? { positive, negative }    ??
?  ?   ??? FragmentParameters["main_sampler"] ? { steps, cfg, seed } ??
?  ?   ??? FragmentParameters["empty_latent"] ? { width, height }    ??
?  ?   ??? FragmentParameters["upscale"] ? { model, scale, IsActive }??
?  ????????????????????????????????????????????????????????????????????
???????????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????????
?                         IImageService                                ?
?  ????????????????????????????????????????????????????????????????????
?  ? 1. PrepareGenerationParametersAsync()                           ??
?  ?    - Expand wildcards in prompts                                ??
?  ?    - Apply styles                                               ??
?  ?    - Randomize seed if -1                                       ??
?  ? 2. _router.PostGenerationAsync(parameters, workflow)            ??
?  ? 3. SaveImagesFromGenerationParams()                             ??
?  ????????????????????????????????????????????????????????????????????
???????????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????????
?                        IRouterService                                ?
?  Routes to ComfyUI via: _capi.PostGenerationAsync(params, workflow) ?
???????????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????????
?                       IComfyUIService                                ?
?  ????????????????????????????????????????????????????????????????????
?  ? 1. UploadSourceImagesAsync() - Upload input images if needed    ??
?  ? 2. _workflow.ComposeWorkflowFromGenerationParameters()          ??
?  ?    ? Renders Scriban templates with fragment values             ??
?  ?    ? Merges all fragments into final ComfyUI JSON               ??
?  ? 3. POST /prompt to ComfyUI API                                  ??
?  ? 4. Wait for WebSocket ExecutionSucceeded event                  ??
?  ? 5. Fetch result images from /history/{promptId}                 ??
?  ????????????????????????????????????????????????????????????????????
???????????????????????????????????????????????????????????????????????
                              ?
                              ?
???????????????????????????????????????????????????????????????????????
?                      ComfyUI Backend                                 ?
?  Receives workflow JSON ? Executes nodes ? Returns images/videos    ?
???????????????????????????????????????????????????????????????????????
```

---

## Application Startup

### 1. Service Registration (`Program.cs`)

All services are registered with dependency injection during startup:

```csharp
// Core services - Singletons
builder.Services.AddSingleton<IEventService, EventService>();
builder.Services.AddSingleton<IStateService, StateService>();
builder.Services.AddSingleton<IBackendService, BackendService>();
builder.Services.AddSingleton<IWorkflowService, WorkflowService>();
builder.Services.AddSingleton<IImageService, ImageService>();
builder.Services.AddSingleton<IRouterService, RouterService>();
builder.Services.AddSingleton<IComfyUIService, ComfyUIService>();

// Scoped services (per-circuit for Blazor Server)
builder.Services.AddScoped<IGenerationParameterService, GenerationParameterService>();
builder.Services.AddScoped<IAssetResolverService, AssetResolverService>();

// Template services
builder.Services.AddSingleton<ITemplateCacheService, TemplateCacheService>();
builder.Services.AddSingleton<IWorkflowValidationService, WorkflowValidationService>();
builder.Services.AddSingleton<WorkflowTemplateParser>();
builder.Services.AddSingleton<IFragmentSchemaService, FragmentSchemaService>();
builder.Services.AddSingleton<IComponentRegistry, ComponentRegistry>();
```

### 2. Template Pre-compilation and Validation

At startup, all Scriban templates are pre-compiled and cached:

```csharp
// Pre-compile all templates (always, for performance)
var templateCacheService = app.Services.GetRequiredService<ITemplateCacheService>();
var workflowPath = Path.Combine(AppContext.BaseDirectory, "Workflows");

var templatesCompiled = templateCacheService.PrecompileAll(Path.Combine(workflowPath, "Templates"));
var fragmentsCompiled = templateCacheService.PrecompileAll(Path.Combine(workflowPath, "Fragments"));

// Validate templates in Development mode only
if (app.Environment.IsDevelopment())
{
    var validationService = app.Services.GetRequiredService<IWorkflowValidationService>();
    var validationResult = validationService.ValidateAllTemplates();
    // Logs errors/warnings for invalid templates
}
```

### 3. Component Auto-Discovery

The `ComponentRegistry` automatically discovers fragment form components:

```csharp
// ComponentRegistry constructor scans for [FragmentComponent] attribute
var componentTypes = assembly.GetTypes()
    .Where(t => t.IsClass && !t.IsAbstract && typeof(IComponent).IsAssignableFrom(t))
    .Where(t => t.GetCustomAttribute<FragmentComponentAttribute>() != null);

// Example fragment component:
[FragmentComponent("SamplerForm")]
public partial class SamplerForm : ComponentBase { }
```

---

## Core Data Models

### GenerationParameters

The central model for all generation data:

```csharp
public class GenerationParameters
{
    /// <summary>
    /// All parameters organized by fragment instance ID.
    /// Keys match Pipeline[].id in the workflow template.
    /// </summary>
    public Dictionary<string, FragmentParameters> Fragments { get; set; } = new();
    
    /// <summary>
    /// Workflow assets (models, VAEs, CLIPs).
    /// Keys match workflow Asset.Parameter names.
    /// </summary>
    public Dictionary<string, string> Assets { get; set; } = new();
    
    /// <summary>
    /// Input images/videos for the workflow.
    /// Keys match workflow Sources[].id.
    /// </summary>
    public Dictionary<string, SourceAsset> Sources { get; set; } = new();
    
    /// <summary>
    /// LoRAs active for this generation.
    /// </summary>
    public List<Lora> Loras { get; set; } = new();
    
    /// <summary>
    /// Current workflow reference.
    /// </summary>
    public Guid? WorkflowId { get; set; }
}
```

### FragmentParameters

Holds values for a single workflow fragment:

```csharp
public class FragmentParameters
{
    /// <summary>
    /// The fragment file this instance uses (e.g., "sampler.sbn").
    /// </summary>
    public string FragmentFile { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this fragment is active (for optional features).
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Parameter values. Keys match fragment parameter names.
    /// </summary>
    public Dictionary<string, object?> Values { get; set; } = new();
    
    /// <summary>
    /// Pre-resolved dynamic options (e.g., model lists from ComfyUI).
    /// Not serialized - populated during initialization.
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, List<string>> ResolvedOptions { get; set; } = new();
}
```

### FragmentSchema

Defines UI constraints and metadata from fragment `#meta` blocks:

```csharp
public class FragmentSchema
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public FragmentType Type { get; set; } = FragmentType.Utility;
    public string? Component { get; set; }
    public string? Icon { get; set; }
    public bool DefaultCollapsed { get; set; } = false;
    
    /// <summary>
    /// Parameter constraints (min, max, step, default, options).
    /// </summary>
    public Dictionary<string, ParameterConstraints>? Parameters { get; set; }
    
    /// <summary>
    /// Dynamic field definitions for schema-driven rendering.
    /// </summary>
    public List<FieldSchema>? Fields { get; set; }
}

public enum FragmentType
{
    Utility,      // Loader, clip, vae - no UI
    Prompts,      // Positive/negative prompts
    Sampler,      // KSampler, scheduling
    Latent,       // Empty latent, resolution
    Enhancement,  // Upscale, detailer, optional features
    Output,       // Save image/video nodes
    Video,        // Video-specific fragments
    Pose          // Pose estimation
}
```

### Workflow

Represents a complete workflow template:

```csharp
public class Workflow
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public ModelBase Base { get; set; }
    public ModeType Mode { get; set; }
    public string RawJson { get; set; } = "";
    public List<WorkflowAsset>? Assets { get; set; }
    public List<SourceDefinition>? Sources { get; set; }
}
```

---

## Service Layer

### Service Responsibilities

| Service | Responsibility |
|---------|----------------|
| `IGenerationParameterService` | CRUD for `GenerationParameters`, workflow initialization, source resolution |
| `IImageService` | Orchestrates generation, handles wildcards/seeds, saves results |
| `IRouterService` | Routes requests to ComfyUI backend |
| `IComfyUIService` | HTTP/WebSocket communication with ComfyUI |
| `IWorkflowService` | Loads templates, composes final workflow JSON |
| `IFragmentSchemaService` | Parses and caches fragment `#meta` schemas |
| `IWorkflowStateService` | Persists per-workflow parameters to database |
| `IEventService` | Pub/sub event system for decoupled communication |
| `IStateService` | Global app state (GenerationParameters, UI preferences) |
| `IBackendService` | ComfyUI connection status, model lists |

### GenerationParameterService

The central service for managing generation parameters:

```csharp
public interface IGenerationParameterService
{
    /// <summary>
    /// Gets the current generation parameters.
    /// </summary>
    GenerationParameters Current { get; }

    /// <summary>
    /// Initializes parameters from a workflow template.
    /// - Loads saved state from database if available
    /// - Falls back to template defaults + schema defaults
    /// - Pre-resolves dynamic source options from ComfyUI
    /// </summary>
    Task<GenerationParameters> InitializeFromWorkflowAsync(Workflow workflow);

    /// <summary>
    /// Saves the current workflow's parameters to the database.
    /// </summary>
    Task SaveCurrentWorkflowStateAsync();

    /// <summary>
    /// Sets whether a fragment is active (for optional features).
    /// </summary>
    void SetFragmentActive(string fragmentId, bool isActive);

    /// <summary>
    /// Resolves dynamic options for a parameter from ComfyUI.
    /// </summary>
    Task<List<string>> ResolveSourceOptionsAsync(ParameterConstraints constraints);
}
```

#### Default Value Priority

When initializing fragments, values are resolved in this priority order:

1. **Saved State (Database)** - Previously saved parameters for this workflow
2. **Step Parameters** - Values from workflow template's Pipeline step
3. **Schema Defaults** - Values from fragment `#meta.ui.parameters.*.default`
4. **Dynamic Sources** - First option from ComfyUI for dynamic dropdowns

### ImageService

Orchestrates the generation process:

```csharp
public class ImageService : IImageService
{
    /// <summary>
    /// Generates images using GenerationParameters.
    /// </summary>
    public async Task<ImagesDto> GenerateImagesAsync(
        GenerationParameters parameters, 
        Workflow workflow)
    {
        // 1. Prepare parameters (wildcards, seeds, styles)
        await PrepareGenerationParametersAsync(parameters);
        
        // 2. Route to ComfyUI
        Images = await _router.PostGenerationAsync(parameters, workflow);
        
        // 3. Save results to disk and database
        if (_backend.OutputPaths.SaveSamples)
        {
            images = await SaveImagesFromGenerationParams(outdir, parameters, workflow);
        }
        
        // 4. Publish event
        _events.Publish(new ImagesGeneratedEventArgs(success: true, count: images.Count));
        
        return images;
    }
    
    private async Task PrepareGenerationParametersAsync(GenerationParameters parameters)
    {
        // Get prompts fragment
        var promptsFragment = parameters.GetFragment("prompts");
        
        // Expand wildcards: "a {cat|dog} on a couch" ? "a cat on a couch"
        prompt = await _wildcardService.ParseWildcards(prompt);
        
        // Apply styles from State.State.Generation.Styles
        foreach (var style in _state.State.Generation.Styles)
        {
            prompt = style.Prompt.Replace("{prompt}", prompt);
        }
        
        // Randomize seed if -1
        var samplerFragment = parameters.GetFragment("main_sampler");
        if (samplerFragment.GetValue<long>("seed") == -1)
        {
            samplerFragment.SetValue("seed", new Random().Next());
        }
    }
}
```

### ComfyUIService

Handles all communication with ComfyUI backend:

```csharp
public class ComfyUIService : IComfyUIService
{
    /// <summary>
    /// Executes generation and waits for completion via WebSocket.
    /// </summary>
    public async Task<GeneratedImages> PostGenerationAsync(
        GenerationParameters parameters, 
        string clientId, 
        Workflow workflow)
    {
        // 1. Upload source images if base64 data present
        await UploadSourceImagesAsync(parameters, tempId);
        
        // 2. Compose workflow from templates
        var workflowJson = _workflow.ComposeWorkflowFromGenerationParameters(workflow, parameters);
        
        // 3. POST to ComfyUI /prompt endpoint
        var payload = new { prompt = workflowObject, client_id = clientId };
        var response = await _httpClient.PostAsJsonAsync("/prompt", payload);
        var promptId = Guid.Parse(submit.PromptId);
        
        // 4. Wait for WebSocket completion event
        var tcs = new TaskCompletionSource<GeneratedImages>();
        _pendingJobs[promptId] = tcs;
        return await tcs.Task;  // Resolved when ExecutionSucceeded fires
    }
    
    // WebSocket event handler
    private async Task HandleExecutionSucceededAsync(Guid promptId)
    {
        // Fetch images from /history/{promptId}
        var files = await GetFilenameFromHistory(promptId);
        var images = new GeneratedImages();
        
        foreach (var file in files)
        {
            var filepath = Path.Combine(comfyOutputPath, file);
            images.Images.Add(await _io.GetBase64FromFileAsync(filepath));
        }
        
        // Complete the TaskCompletionSource
        tcs.SetResult(images);
    }
}
```

---

## Workflow Templates

### Template Structure

Workflows are Scriban templates (`.sbn` files) in `Workflows/Templates/`:

```json
{
  "title": "Flux Dev - Txt2Img",
  "base": "Flux",
  "mode": "Txt2Img",
  
  "Assets": [
    {
      "label": "Model",
      "parameter": "Model",
      "type": "Checkpoint",
      "default": "flux1-dev-fp8.safetensors"
    }
  ],
  
  "Pipeline": [
    {
      "id": "prompts",
      "fragment": "prompts.sbn",
      "parameters": { "positive": "", "negative": "" }
    },
    {
      "id": "main_sampler",
      "fragment": "sampler.sbn",
      "parameters": { "steps": 20, "cfg": 7.0, "seed": -1 }
    },
    {
      "id": "empty_latent",
      "fragment": "empty-latent.sbn",
      "parameters": { "width": 1024, "height": 1024, "batch_size": 1 }
    },
    {
      "id": "upscale",
      "fragment": "upscale.sbn",
      "parameters": { "upscale_model": "RealESRGAN_x4plus" }
    }
  ]
}
```

### Fragment Structure

Fragments are reusable ComfyUI node groups in `Workflows/Fragments/`:

```javascript
// sampler.sbn
#meta
{
  "ui": {
    "title": "Sampler",
    "component": "SamplerForm",
    "type": "Sampler",
    "parameters": {
      "steps": { "min": 1, "max": 150, "default": 20 },
      "cfg": { "min": 1, "max": 30, "step": 0.5, "default": 7.0 },
      "seed": { "min": -1, "max": 9999999999, "default": -1 },
      "sampler_name": { "source": "ClownsharKSampler_Beta", "input_name": "sampler_name" },
      "scheduler": { "source": "ClownsharKSampler_Beta", "input_name": "scheduler" }
    }
  },
  "outputs": {
    "LATENT": { "node": "sampler", "index": 0 }
  },
  "conditions": {
    "required": []
  }
}
#end

"sampler": {
  "inputs": {
    "model": {{ get_ref "MODEL" }},
    "positive": {{ get_ref "POSITIVE" }},
    "negative": {{ get_ref "NEGATIVE" }},
    "latent_image": {{ get_ref "LATENT" }},
    "seed": {{ seed | json }},
    "steps": {{ steps | json }},
    "cfg": {{ cfg | json }},
    "sampler_name": {{ sampler_name | json }},
    "scheduler": {{ scheduler | json }},
    "denoise": {{ denoise ?? 1.0 | json }}
  },
  "class_type": "ClownsharKSampler_Beta"
}
```

### Workflow Composition

`WorkflowService.ComposeWorkflowFromGenerationParameters()`:

1. **Flatten parameters** - Convert `GenerationParameters` to flat dictionary
2. **Render main template** - Execute Scriban on workflow template
3. **Process Pipeline steps** - For each step:
   - Check if fragment is active
   - Merge global params + step params
   - Render fragment template
   - Extract outputs for next fragments
4. **Build final JSON** - Merge all rendered fragments

```csharp
public string ComposeWorkflowFromGenerationParameters(Workflow template, GenerationParameters parameters)
{
    // Flatten all fragment values + assets + sources into global params
    var globalParams = parameters.FlattenForTemplateRendering();
    
    // For each pipeline step...
    foreach (var stepEl in pipelineEl.EnumerateArray())
    {
        var fragmentName = stepEl.GetProperty("fragment").GetString();
        var fragmentId = GetFragmentIdFromStep(stepEl, fragmentName);
        
        // Skip inactive optional fragments
        if (!parameters.Fragments[fragmentId].IsActive)
            continue;
        
        // Merge global + step parameters
        var mergedParams = new Dictionary<string, object>(globalParams);
        // ... add step-specific params
        
        // Render fragment with Scriban
        var (rendered, outputs) = RenderFragment(fragmentText, context, mergedParams);
        
        // Register outputs for next fragments to reference
        composer.AddRenderedFragment(rendered, outputs);
    }
    
    return composer.BuildFinalWorkflow();
}
```

---

## UI Components

### Generate.razor

The main generation page:

```razor
@page "/generate/{WorkflowId}"

@* Workflow selection via URL parameter *@

@* Asset panel - models, VAEs, etc. *@
<WorkflowAssetsPanel Mode="@_selectedWorkflow.Mode" Workflow="@_selectedWorkflow" />

@* Prompts + Generate button *@
<PromptsForm 
    Prompt="@_prompt"
    PromptChanged="HandlePromptChanged"
    OnGenerate="GenerateAsync" />

@* Two-column layout *@
<MudGrid>
    @* Left: Parameters *@
    <MudItem xs="6">
        <LoraForm Loras="Parameters.Loras" />
        
        @if (HasSources)
        {
            <SourcesPanel Sources="@Parameters.Sources" />
        }
        
        <LatentForm Width="@_width" Height="@_height" ... />
        <SamplerForm Steps="@_steps" Seed="@_seed" ... />
        
        @* Optional features *@
        @foreach (var optFrag in GetOptionalFragments())
        {
            <CollapsibleFeatureSection 
                Title="@optFrag.Title"
                IsActive="@optFrag.Fragment.IsActive"
                IsActiveChanged="@(a => HandleFragmentActiveChanged(optFrag.Id, a))">
                @RenderOptionalFragmentForm(optFrag.Id, optFrag.Fragment, optFrag.Schema)
            </CollapsibleFeatureSection>
        }
    </MudItem>
    
    @* Right: Output *@
    <MudItem xs="6">
        <GeneratedImageTabs />
    </MudItem>
</MudGrid>

@code {
    private async Task OnWorkflowSelected(Workflow workflow)
    {
        _selectedWorkflow = workflow;
        
        // Initialize parameters from workflow template
        await ParameterService.InitializeFromWorkflowAsync(workflow);
        
        // Discover fragment IDs from pipeline
        DiscoverFragments();
        
        // Initialize local state from fragment values
        InitializeLocalStateFromFragments();
    }
    
    private async Task GenerateAsync()
    {
        // Save state before generation
        await ParameterService.SaveCurrentWorkflowStateAsync();
        
        // Generate
        var images = await ImageService.GenerateImagesAsync(Parameters, _selectedWorkflow);
        
        // Save state after (captures seed updates)
        await ParameterService.SaveCurrentWorkflowStateAsync();
    }
}
```

### Fragment Form Components

Each fragment has a dedicated form component:

```razor
// SamplerForm.razor
@using BlazorWebApp.Attributes
@attribute [FragmentComponent("SamplerForm")]

<MudGrid Spacing="2">
    <MudItem xs="6">
        <MudSelect @bind-Value="_localSamplerName" @bind-Value:after="OnSamplerChanged" Label="Sampler">
            @foreach (var sampler in Backend.Samplers)
            {
                <MudSelectItem Value="@sampler.Name" />
            }
        </MudSelect>
    </MudItem>
    
    <MudItem xs="6">
        <MudSlider @bind-Value="_localSteps" @bind-Value:after="OnStepsChanged"
                   Min="1" Max="150" Step="1" ValueLabel>
            Steps: @_localSteps
        </MudSlider>
    </MudItem>
    
    <!-- More fields... -->
</MudGrid>

@code {
    [Parameter] public string? SamplerName { get; set; }
    [Parameter] public EventCallback<string> SamplerNameChanged { get; set; }
    [Parameter] public int Steps { get; set; } = 20;
    [Parameter] public EventCallback<int> StepsChanged { get; set; }
    
    // Local state for MudBlazor binding
    private string? _localSamplerName;
    private int _localSteps;
    
    protected override void OnParametersSet()
    {
        _localSamplerName = SamplerName;
        _localSteps = Steps;
    }
    
    private async Task OnSamplerChanged()
    {
        await SamplerNameChanged.InvokeAsync(_localSamplerName);
    }
}
```

### Dynamic Field Rendering

For fragments without custom components, `DynamicField` renders from schema:

```razor
// DynamicField.razor
@switch (Field.Type?.ToLowerInvariant())
{
    case "slider":
        <MudSlider T="double" @bind-Value="_localDoubleValue" Min="@Field.Min" Max="@Field.Max" />
        break;
    case "select":
        <MudSelect @bind-Value="_localStringValue">
            @foreach (var option in GetSelectOptions())
            {
                <MudSelectItem Value="@option" />
            }
        </MudSelect>
        break;
    case "seed":
        <MudNumericField @bind-Value="_localLongValue" />
        <MudIconButton OnClick="RandomizeSeed" Icon="@Icons.Material.Filled.Casino" />
        break;
    // ... more field types
}
```

---

## Generation Flow

### Complete Flow Diagram

```
??????????????????????????????????????????????????????????????????????????
?                     USER CLICKS "GENERATE"                              ?
??????????????????????????????????????????????????????????????????????????
                                    ?
                                    ?
??????????????????????????????????????????????????????????????????????????
? Generate.razor ? GenerateAsync()                                       ?
?   1. await ParameterService.SaveCurrentWorkflowStateAsync()            ?
?   2. await ImageService.GenerateImagesAsync(Parameters, workflow)      ?
??????????????????????????????????????????????????????????????????????????
                                    ?
                                    ?
??????????????????????????????????????????????????????????????????????????
? ImageService.GenerateImagesAsync()                                     ?
?   1. _progress.IsConverging = true                                     ?
?   2. await PrepareGenerationParametersAsync()                          ?
?      ??? Expand wildcards in prompts                                   ?
?      ??? Apply styles from State.Generation.Styles                     ?
?      ??? Randomize seed if -1                                          ?
?   3. Images = await _router.PostGenerationAsync(parameters, workflow)  ?
??????????????????????????????????????????????????????????????????????????
                                    ?
                                    ?
??????????????????????????????????????????????????????????????????????????
? RouterService.PostGenerationAsync()                                    ?
?   ? _capi.PostGenerationAsync(parameters, clientId, workflow)          ?
??????????????????????????????????????????????????????????????????????????
                                    ?
                                    ?
??????????????????????????????????????????????????????????????????????????
? ComfyUIService.PostGenerationAsync()                                   ?
?   1. await UploadSourceImagesAsync(parameters, tempId)                 ?
?      ??? Upload base64 images to ComfyUI /upload/image                 ?
?   2. workflowJson = _workflow.ComposeWorkflowFromGenerationParameters()?
?      ??? Render all Scriban templates, merge fragments                 ?
?   3. POST /prompt { prompt: workflowJson, client_id: ... }             ?
?   4. _pendingJobs[promptId] = new TaskCompletionSource<GeneratedImages>?
?   5. return await tcs.Task  // Waits for WebSocket event               ?
??????????????????????????????????????????????????????????????????????????
                                    ?
              ?????????????????????????????????????????????
              ?                                           ?
              ?                                           ?
???????????????????????????????           ????????????????????????????????
? ComfyUI executes workflow   ?           ? ComfyUIWebsocketService      ?
? (external process)          ?           ? receives progress events     ?
?                             ? ??????????? publishes ConvergingChanged  ?
???????????????????????????????           ????????????????????????????????
              ?
              ? Execution complete
              ?
??????????????????????????????????????????????????????????????????????????
? ComfyUIEventBus.ExecutionSucceeded event fires                         ?
?   ? ComfyUIService.HandleExecutionSucceededAsync(promptId)             ?
?     1. GET /history/{promptId}                                         ?
?     2. Parse output filenames                                          ?
?     3. Read images from ComfyUI output folder                          ?
?     4. tcs.SetResult(images) ? Completes await in PostGenerationAsync  ?
??????????????????????????????????????????????????????????????????????????
                                    ?
                                    ?
??????????????????????????????????????????????????????????????????????????
? Back in ImageService.GenerateImagesAsync()                             ?
?   4. await SaveImagesFromGenerationParams()                            ?
?      ??? Create save directory based on patterns                       ?
?      ??? Save each image to disk                                       ?
?      ??? Create Image entity in database                               ?
?      ??? Return ImagesDto                                              ?
?   5. _progress.IsConverging = false                                    ?
?   6. _events.Publish(new ImagesGeneratedEventArgs(...))                ?
??????????????????????????????????????????????????????????????????????????
                                    ?
                                    ?
??????????????????????????????????????????????????????????????????????????
? GeneratedImageTabs subscribes to ImagesGeneratedEventArgs              ?
?   ? Refreshes UI with new images                                       ?
??????????????????????????????????????????????????????????????????????????
```

### Workflow Composition Detail

```
GenerationParameters                   Workflow Template
???????????????????????????           ???????????????????????????
? Fragments:              ?           ? Pipeline: [             ?
?   "prompts": {          ?           ?   { id: "prompts",      ?
?     positive: "cat",    ?           ?     fragment: "prompts" ?
?     negative: "blurry"  ?           ?     params: {...}       ?
?   }                     ?           ?   },                    ?
?   "main_sampler": {     ?           ?   { id: "main_sampler", ?
?     steps: 25,          ?           ?     fragment: "sampler" ?
?     cfg: 7.5,           ?           ?     params: {...}       ?
?     seed: 12345         ?           ?   },                    ?
?   }                     ?           ?   ...                   ?
?   "upscale": {          ?           ? ]                       ?
?     IsActive: false     ?           ?                         ?
?   }                     ?           ?                         ?
? Assets:                 ?           ? Assets: [               ?
?   Model: "flux.sft"     ?           ?   { parameter: "Model" }?
? Sources:                ?           ? ]                       ?
?   source_image: {...}   ?           ?                         ?
???????????????????????????           ???????????????????????????
          ?                                       ?
          ?????????????????????????????????????????
                          ?
                          ?
           ComposeWorkflowFromGenerationParameters()
                          ?
                          ?
           ????????????????????????????????????
           ? Flatten to globalParams:          ?
           ?   positive: "cat"                 ?
           ?   negative: "blurry"              ?
           ?   steps: 25                       ?
           ?   cfg: 7.5                        ?
           ?   seed: 12345                     ?
           ?   Model: "flux.sft"               ?
           ?   source_image: "uploaded.png"    ?
           ????????????????????????????????????
                          ?
                          ?
           For each Pipeline step:
           ????????????????????????????????????
           ? 1. Load fragment file             ?
           ? 2. Check conditions               ?
           ? 3. Render Scriban template        ?
           ? 4. Register outputs               ?
           ????????????????????????????????????
                          ?
                          ?
           ????????????????????????????????????
           ? Final ComfyUI Workflow JSON       ?
           ? {                                 ?
           ?   "1": { "class_type": "..." },   ?
           ?   "2": { "class_type": "..." },   ?
           ?   ...                             ?
           ? }                                 ?
           ????????????????????????????????????
```

---

## Event System

All inter-service communication uses `IEventService` (pub/sub pattern):

### Key Events

| Event | Published By | Subscribers |
|-------|--------------|-------------|
| `ImagesGeneratedEventArgs` | `ImageService` | `GeneratedImageTabs` |
| `ConvergingChangedEventArgs` | `ProgressService` | `GenerateButton`, progress UI |
| `GenerationParametersChangedEventArgs` | `GenerationParameterService` | `Generate.razor` |
| `StateChangedEventArgs` | `StateService` | Various components |
| `WorkflowChangedEventArgs` | `GenerationParameterService` | `Generate.razor` |

### Usage Pattern

```csharp
// Publishing
_events.Publish(new ImagesGeneratedEventArgs(success: true, count: 4));

// Subscribing (in component)
protected override void OnInitialized()
{
    Events.Subscribe<ImagesGeneratedEventArgs>(OnImagesGenerated);
}

private void OnImagesGenerated(ImagesGeneratedEventArgs args)
{
    if (args.Success)
    {
        _ = InvokeAsync(StateHasChanged);
    }
}

public void Dispose()
{
    Events.Unsubscribe<ImagesGeneratedEventArgs>(OnImagesGenerated);
}
```

---

## State Persistence

### Per-Workflow State (`WorkflowStateService`)

Each workflow's parameters are saved independently:

```csharp
// Saved to WorkflowStates table with WorkflowId as key
public class WorkflowState
{
    public Guid WorkflowId { get; set; }           // Primary key
    public string? GenerationParametersJson { get; set; }  // JSON blob
    public DateTime LastModified { get; set; }
}
```

### Global App State (`StateService`)

Application-wide settings persisted to `State` table:

```csharp
public class AppState
{
    public GenerationState Generation { get; set; } = new();
    public GalleryState Gallery { get; set; } = new();
    // ... more sections
}

public class GenerationState
{
    public Guid? CurrentWorkflowId { get; set; }
    public ModelBase? WorkflowBase { get; set; }
    public IEnumerable<PromptStyle>? Styles { get; set; }
    public long Seed { get; set; } = -1;
    // ...
}
```

### State Loading Flow

```
App Startup
    ?
    ?
OrchestratorService.LoadState()
    ?
    ??? StateService.LoadState() ? Load from State table
    ?
    ?
User navigates to /generate/{workflowId}
    ?
    ?
Generate.razor.OnWorkflowSelected()
    ?
    ?
ParameterService.InitializeFromWorkflowAsync(workflow)
    ?
    ??? WorkflowStateService.GetStateAsync(workflowId)
    ?   ??? Load from WorkflowStates table if exists
    ?
    ??? If saved state exists:
    ?   ??? Restore GenerationParameters from JSON
    ?
    ??? If no saved state:
        ??? Parse workflow Pipeline for defaults
        ??? Apply fragment schema defaults
        ??? Pre-resolve dynamic options from ComfyUI
```

---

## Error Handling

### Template Validation (Startup)

```csharp
// WorkflowValidationService validates at startup:
- Template file existence
- JSON syntax in #meta blocks
- Pipeline structure (required fields)
- Fragment references exist
- Component registrations
- Parameter constraints (min < max, step > 0)
- Dynamic source configurations
```

### Generation Errors

```csharp
// ImageService.GenerateImagesAsync()
try
{
    Images = await _router.PostGenerationAsync(parameters, workflow);
}
catch (Exception e)
{
    _logger.LogError(e, "Error during generation");
}
finally
{
    _progress.IsConverging = false;
    NotifyStateChanged();  // Always update UI
}

// ComfyUIService handles execution failures
_bus.ExecutionFailed += async (promptId, error) =>
{
    if (_pendingJobs.TryRemove(promptId, out var tcs))
    {
        tcs.SetException(new Exception(error));
    }
    await CleanupUploadedImagesAsync(promptId);
};
```

### JSON Type Preservation

Custom converters handle JSON round-trip issues:

```csharp
// GenerationParametersJsonConverter
- Preserves int vs long vs double during deserialization
- Handles JsonElement values from database
- Converts string numbers to numeric types

// FragmentParameters.GetValue<T>()
- Handles JsonElement to T conversion
- Parses string values to numbers/bools
- Falls back to full deserialization for complex types
```

---

## Summary

The generation system follows this flow:

1. **User selects workflow** ? `GenerationParameterService` initializes from template + saved state
2. **User modifies parameters** ? Form components update `GenerationParameters.Fragments`
3. **User clicks Generate** ? `ImageService` prepares and routes to ComfyUI
4. **`ComfyUIService` composes workflow** ? Scriban renders templates with parameter values
5. **ComfyUI executes** ? WebSocket provides progress updates
6. **Execution completes** ? Images fetched, saved, events published
7. **UI updates** ? `GeneratedImageTabs` displays results

Key architectural decisions:
- **Fragment-based composition** - Reusable workflow building blocks
- **Schema-driven UI** - Constraints and metadata in fragment `#meta` blocks
- **Event-driven communication** - Decoupled services via `IEventService`
- **Per-workflow persistence** - Each workflow's parameters saved independently
- **Pre-compiled templates** - Scriban templates cached at startup for performance
