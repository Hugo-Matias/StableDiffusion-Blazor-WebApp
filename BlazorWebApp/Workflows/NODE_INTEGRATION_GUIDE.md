# ComfyUI Node Integration Guide

This guide documents the complete process for extracting a ComfyUI node from a workflow and integrating it into the BlazorWebApp as a configurable feature with UI controls.

## Table of Contents

1. [Overview](#overview)
2. [Phase 1: Node Extraction](#phase-1-node-extraction)
3. [Phase 2: Fragment Creation](#phase-2-fragment-creation)
4. [Phase 3: Parameter Model Creation](#phase-3-parameter-model-creation)
5. [Phase 4: DTO Integration](#phase-4-dto-integration)
6. [Phase 5: UI Form Component](#phase-5-ui-form-component)
7. [Phase 6: Workflow Template Integration](#phase-6-workflow-template-integration)
8. [Phase 7: Parameter Flow Integration](#phase-7-parameter-flow-integration)
9. [Critical Initialization Requirements](#critical-initialization-requirements)
10. [Troubleshooting](#troubleshooting)

---

## Overview

The integration process involves these key components:

| Component | Location | Purpose |
|-----------|----------|---------|
| **Fragment** | `Workflows/Fragments/*.sbn` | Scriban template for ComfyUI node JSON |
| **Parameter Model** | `Models/*Parameters.cs` | C# class holding user-configurable values |
| **DTO** | `Data/Dtos/ComfyUI/Workflow/*ComfyUI.cs` | Data Transfer Object passed to workflow service |
| **UI Form** | `Components/*/*.razor` | Blazor form for user interaction |
| **Workflow Template** | `Workflows/Templates/*/*.sbn` | Pipeline definition referencing fragment |

---

## Phase 1: Node Extraction

### 1.1 Export the Workflow from ComfyUI

1. Open ComfyUI and create/load a workflow containing the target node
2. Click **Save (API Format)** to export as JSON
3. Save to `Workflows/` folder for reference

### 1.2 Identify the Target Node

Open the exported JSON and locate the node. Example structure:

```json
"42": {
    "inputs": {
        "randomize_percent": 50,
        "strength": 20,
        "noise_insert": "noise on beginning steps",
        "steps_switchover_percent": 20,
        "seed": 0,
        "mask_starts_at": "beginning",
        "mask_percent": 0,
        "log_to_console": false,
        "noise": ["41", 3],
        "noise_base": ["41", 3]
    },
    "class_type": "SeedVarianceEnhancer",
    "_meta": {
        "title": "Seed Variance Enhancer"
    }
}
```

### 1.3 Document Node Parameters

Create a reference document with:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `randomize_percent` | int | 50 | Percentage of randomization |
| `strength` | int | 20 | Effect strength |
| `noise_insert` | string | "noise on beginning steps" | When to insert noise |
| `seed` | int | 0 | Random seed (-1 for random) |

**Note the input references** - parameters like `["41", 3]` reference other nodes' outputs.

---

## Phase 2: Fragment Creation

### 2.1 Create the Fragment File

Create `Workflows/Fragments/your-node-name.sbn`:

```scriban
#meta
{
    "outputs": {
        "noise": { "node": "{{ scope ?? '' }}seed_variance_enhancer", "index": 0 }
    },
    "conditions": {
        "required": ["SeedVarianceEnhancer.IsActive"]
    }
}
#end

"{{ scope ?? '' }}seed_variance_enhancer": {
    "inputs": {
        "randomize_percent": {{ randomize_percent }},
        "strength": {{ strength }},
        "noise_insert": {{ noise_insert | json }},
        "steps_switchover_percent": {{ steps_switchover_percent }},
        "seed": {{ seed }},
        "mask_starts_at": {{ mask_starts_at | json }},
        "mask_percent": {{ mask_percent }},
        "log_to_console": {{ log_to_console }},
        "noise": {{ get_ref "noise" }},
        "noise_base": {{ get_ref "noise" }}
    },
    "class_type": "SeedVarianceEnhancer",
    "_meta": {
        "title": "{{ scope_title ?? '' }}Seed Variance Enhancer"
    }
}
```

### 2.2 Key Fragment Concepts

#### Meta Block
- **outputs**: Registers node outputs for downstream references
- **conditions.required**: Array of property paths that must evaluate to `true`
- **conditions.excluded_if**: Array of property paths that must evaluate to `false`

#### Parameter Access
- Direct values: `{{ parameter_name }}`
- JSON-escaped strings: `{{ parameter_name | json }}`
- Node references: `{{ get_ref "output_name" }}`
- Defaults: `{{ parameter_name ?? default_value }}`

#### Scoping
Use `{{ scope ?? '' }}` prefix for node IDs to support multiple instances.

---

## Phase 3: Parameter Model Creation

### 3.1 Create the Parameters Class

Create `Models/YourNodeParameters.cs`:

```csharp
namespace BlazorWebApp.Models
{
    public class YourNodeParameters
    {
        /// <summary>
        /// Whether this feature is enabled. CRITICAL: Must default to false.
        /// </summary>
        public bool IsActive { get; set; } = false;
        
        public int RandomizePercent { get; set; } = 50;
        public int Strength { get; set; } = 20;
        public string NoiseInsert { get; set; } = "noise on beginning steps";
        public int StepsSwitchoverPercent { get; set; } = 20;
        public int Seed { get; set; } = 0;
        public string MaskStartsAt { get; set; } = "beginning";
        public int MaskPercent { get; set; } = 0;
        public bool LogToConsole { get; set; } = false;
    }
}
```

### 3.2 Critical Rules for Parameter Models

1. **Always include `IsActive` property** - Controls conditional inclusion
2. **Set sensible defaults** - Match ComfyUI node defaults
3. **Use appropriate types** - Match the ComfyUI input types exactly

---

## Phase 4: DTO Integration

### 4.1 Add Property to Parent Parameters Class

In `Models/Txt2ImgParameters.cs` (or appropriate parent):

```csharp
public class Txt2ImgParameters : SharedParameters
{
    // ... existing properties ...
    
    // CRITICAL: Initialize with new() to prevent null reference errors
    public YourNodeParameters YourNode { get; set; } = new();
    
    // ... rest of class ...
}
```

### 4.2 Add Property to DTO Class

In `Data/Dtos/ComfyUI/Workflow/Txt2ImgComfyUI.cs`:

```csharp
public class Txt2ImgComfyUI : SharedComfyUI
{
    // ... existing properties ...
    
    // CRITICAL: Initialize with new() to prevent null reference errors
    public YourNodeParameters YourNode { get; set; } = new();
}
```

### 4.3 Update Parameter Mapper

In `Extensions/ParameterMapper.cs`, ensure the property is mapped:

```csharp
public static Txt2ImgComfyUI ToTxt2ImgComfyUI(this Txt2ImgParameters param, string model, string vae)
{
    return new Txt2ImgComfyUI
    {
        // ... existing mappings ...
        YourNode = param.YourNode ?? new YourNodeParameters(),
    };
}
```

---

## Phase 5: UI Form Component

### 5.1 Create the Form Component

Create `Components/Txt2Img/YourNodeForm.razor`:

```razor
@using BlazorWebApp.Models

<MudExpansionPanel Text="Your Node Feature">
    <MudStack Spacing="2">
        <MudSwitch @bind-Value="Parameters.IsActive" 
                   Label="Enable Feature" 
                   Color="Color.Primary" />
        
        @if (Parameters.IsActive)
        {
            <MudSlider @bind-Value="Parameters.RandomizePercent" 
                       Min="0" Max="100" Step="1">
                Randomize: @Parameters.RandomizePercent%
            </MudSlider>
            
            <MudSlider @bind-Value="Parameters.Strength" 
                       Min="0" Max="100" Step="1">
                Strength: @Parameters.Strength
            </MudSlider>
            
            <MudSelect @bind-Value="Parameters.NoiseInsert" 
                       Label="Noise Insert">
                <MudSelectItem Value="@("noise on beginning steps")">
                    Noise on Beginning Steps
                </MudSelectItem>
                <MudSelectItem Value="@("noise on ending steps")">
                    Noise on Ending Steps
                </MudSelectItem>
            </MudSelect>
            
            <!-- Add more controls as needed -->
        }
    </MudStack>
</MudExpansionPanel>

@code {
    [Parameter] public YourNodeParameters Parameters { get; set; } = new();
    [Parameter] public EventCallback<YourNodeParameters> ParametersChanged { get; set; }
}
```

### 5.2 Add to Parent Form

In `Components/Txt2Img/GenerateFormTxt2Img.razor`:

```razor
<YourNodeForm @bind-Parameters="Parameters.YourNode" />
```

### 5.3 Handle Enable/Disable Toggle

If using a checkbox separate from the component:

```razor
<MudCheckBox T="bool" 
             Value="@(Parameters.YourNode?.IsActive ?? false)"
             ValueChanged="@(v => HandleYourNodeToggle(v))"
             Label="Your Node" />

@code {
    private void HandleYourNodeToggle(bool isActive)
    {
        // CRITICAL: Create new instance if null
        Parameters.YourNode ??= new YourNodeParameters();
        Parameters.YourNode.IsActive = isActive;
    }
}
```

---

## Phase 6: Workflow Template Integration

### 6.1 Add Fragment to Pipeline

In `Workflows/Templates/your-base/txt2img.sbn`:

```json
{
  "Title": "Txt2Img",
  "Base": "YourBase",
  "Mode": "txt2img",
  "Assets": [...],
  "Pipeline": [
    // ... other fragments ...
    {
      "fragment": "prompts.sbn",
      "parameters": {
        "positive": {{ Prompt | json }},
        "negative": {{ NegativePrompt | json }}
      }
    },
    {
      "fragment": "your-node-name.sbn",
      "parameters": {
        "randomize_percent": {{ YourNode.RandomizePercent ?? 50 | json }},
        "strength": {{ YourNode.Strength ?? 20 | json }},
        "noise_insert": {{ YourNode.NoiseInsert ?? "noise on beginning steps" | json }},
        "steps_switchover_percent": {{ YourNode.StepsSwitchoverPercent ?? 20 | json }},
        "seed": {{ YourNode.Seed ?? 0 | json }},
        "mask_starts_at": {{ YourNode.MaskStartsAt ?? "beginning" | json }},
        "mask_percent": {{ YourNode.MaskPercent ?? 0 | json }},
        "log_to_console": {{ YourNode.LogToConsole ?? false | json }}
      }
    },
    {
      "fragment": "sampler.sbn",
      "parameters": {...}
    }
    // ... more fragments ...
  ]
}
```

### 6.2 Template Parameter Access

The template accesses parameters via:
- `{{ YourNode.PropertyName }}` - Direct access (may fail if null)
- `{{ YourNode.PropertyName ?? default }}` - With fallback (safer)

**CRITICAL**: If `YourNode` is null when the template renders, accessing `YourNode.PropertyName` will fail silently and the entire pipeline step may be skipped!

---

## Phase 7: Parameter Flow Integration

### 7.1 Ensure Parameters Flow Through the Chain

The parameter flow is:

```
UI Form ? Txt2ImgParameters ? ParameterMapper ? Txt2ImgComfyUI ? WorkflowService
```

In `ImageService.BuildTxt2ImgParametersAsync()`:

```csharp
private async Task BuildTxt2ImgParametersAsync(string scriptName)
{
    _parsingParams = await Parser.ParseParametersAsync(...);
    _txt2imgParams = new Txt2ImgParameters(_parsingParams);
    
    // Copy feature parameters from state
    _txt2imgParams.YourNode = _state.ParametersTxt2Img.YourNode;
    
    // ... other parameters ...
}
```

---

## Critical Initialization Requirements

### The Null Reference Problem

**This is the most common cause of integration failures.**

When Scriban templates access `{{ YourNode.PropertyName }}` and `YourNode` is null:
1. The property access fails
2. The entire pipeline step renders as invalid JSON
3. The fragment is silently skipped
4. No error is logged

### Required Initializations

1. **In Parameter Model (`Txt2ImgParameters.cs`)**:
   ```csharp
   public YourNodeParameters YourNode { get; set; } = new();
   ```

2. **In DTO Class (`Txt2ImgComfyUI.cs`)**:
   ```csharp
   public YourNodeParameters YourNode { get; set; } = new();
   ```

3. **In UI Toggle Handler**:
   ```csharp
   Parameters.YourNode ??= new YourNodeParameters();
   ```

4. **In Parameter Mapper**:
   ```csharp
   YourNode = param.YourNode ?? new YourNodeParameters(),
   ```

### Template Files and Caching

**Template files are cached at application startup.**

When you modify `.sbn` files:
1. The source files are in `BlazorWebApp/Workflows/`
2. They are copied to `bin/Debug/net8.0/Workflows/` on build
3. The application loads from the bin folder
4. Changes require **rebuilding** the project

**Ensure `CopyToOutputDirectory` is set in `.csproj`**:
```xml
<ItemGroup>
  <None Update="Workflows\**\*.sbn">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### Workflow Refresh

Workflows are loaded from disk on startup and cached in `State.State.Generation.Workflows`.

To refresh without restarting:
1. Click the **Refresh** button next to the Base dropdown in the toolbar
2. This calls `M.RefreshWorkflowsFromDisk()` which reloads all templates

---

## Troubleshooting

### Fragment Not Being Processed

**Symptoms**: 
- No logs showing fragment processing
- Node not appearing in final workflow

**Causes & Solutions**:

| Cause | Solution |
|-------|----------|
| Parent object is null | Initialize with `= new()` in Parameters and DTO classes |
| Template not copied to bin | Rebuild project, check `.csproj` copy settings |
| Workflow cached with old template | Click Refresh button or restart app |
| Fragment file not found | Check file path and name match exactly |

### Condition Evaluation Failing

**Symptoms**:
- Fragment exists but condition returns false
- `IsActive` is true but node not included

**Causes & Solutions**:

| Cause | Solution |
|-------|----------|
| Property path wrong | Use exact path: `YourNode.IsActive` |
| Object in chain is null | Initialize all objects in the chain |
| Property not a boolean | Ensure `IsActive` is `bool` type |

### Node Included But Has Wrong Values

**Symptoms**:
- Node appears in workflow
- Parameters have default/wrong values

**Causes & Solutions**:

| Cause | Solution |
|-------|----------|
| Parameters not flowing through | Check `ImageService.BuildTxt2ImgParametersAsync()` |
| ParameterMapper not copying | Update mapper to include new property |
| Template using wrong parameter names | Match names exactly (case-sensitive) |

### UI Changes Not Persisting

**Symptoms**:
- Checkbox toggles but value doesn't save
- Value resets on page reload

**Causes & Solutions**:

| Cause | Solution |
|-------|----------|
| Object null when toggling | Create new instance before setting property |
| Not binding correctly | Use `@bind-Value` or `ValueChanged` properly |
| State not saving | Ensure `StateService.SaveState()` is called |

---

## Checklist for New Node Integration

- [ ] Export workflow from ComfyUI (API format)
- [ ] Document node parameters and types
- [ ] Create fragment file with `#meta` block
- [ ] Create Parameters class with `IsActive` property
- [ ] Add property to `Txt2ImgParameters` with `= new()` initializer
- [ ] Add property to `Txt2ImgComfyUI` with `= new()` initializer
- [ ] Update `ParameterMapper.ToTxt2ImgComfyUI()` with null-coalescing
- [ ] Create UI form component
- [ ] Add form to parent (`GenerateFormTxt2Img.razor`)
- [ ] Handle toggle with null check in handler
- [ ] Add fragment reference to workflow template Pipeline
- [ ] Update `ImageService.BuildTxt2ImgParametersAsync()` to copy parameter
- [ ] Set `CopyToOutputDirectory` to `Always` in `.csproj`
- [ ] Rebuild project
- [ ] Test with feature disabled (should not include node)
- [ ] Test with feature enabled (should include node with correct values)

---

## Example: Complete SeedVarianceEnhancer Integration

### Files Modified/Created:

1. `Workflows/Fragments/seed-variance-enhancer.sbn` - Fragment
2. `Models/SeedVarianceEnhancerParameters.cs` - Parameters class
3. `Models/Txt2ImgParameters.cs` - Added property
4. `Data/Dtos/ComfyUI/Workflow/Txt2ImgComfyUI.cs` - Added property
5. `Extensions/ParameterMapper.cs` - Added mapping
6. `Components/Shared/Generation/SeedVarianceEnhancerForm.razor` - UI
7. `Components/Txt2Img/GenerateFormTxt2Img.razor` - Added form
8. `Workflows/Templates/z-image/txt2img.sbn` - Added to Pipeline
9. `Services/ImageService.cs` - Copy parameter in build method

---

*Last Updated: Based on SeedVarianceEnhancer integration experience*
