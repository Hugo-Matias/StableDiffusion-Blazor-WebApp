# Detailer Prompts & LoRA Support - Implementation Plan (FINAL v4)

## Overview

This plan implements detailer prompts and LoRA support through **abstraction, simplification, and unification**:

- **Extend `prompts.sbn`** with scope parameter (reuse existing)
- **Create `SimplePromptsForm`** component (lightweight, reusable for ANY scope)
- **Add `Scope` to Lora model** (single list, filtered by scope)
- **Refactor checkpoint fragments** (`load-checkpoint.sbn` = minimal, `load-checkpoint-w-prompts.sbn` = with prompts)
- **Update `lora-loader.sbn`** outputs to be scoped
- **Zero duplication** - maximum simplification

---

## Core Principles

### 1. **SIMPLIFY**
- Single `Lora` list with `Scope` property
- Filter by scope in templates, not separate lists
- Minimal fragments that do ONE thing

### 2. **ABSTRACT**
- `SimplePromptsForm` works for ANY scope
- Fragments are model-agnostic
- LoRAs are scoped entities throughout the pipeline

### 3. **UNIFY**
- `load-checkpoint.sbn` (minimal) ? `load-diffusion.sbn` (minimal)
- `load-checkpoint-w-prompts.sbn` (with prompts) ? `load-diffusion-w-prompts.sbn` (with prompts)
- Same LoRA loading pattern for all model types

---

## Current State Analysis

### Existing `Lora` Model
```csharp
public class Lora
{
    public string Name { get; set; }
    public string Path { get; set; }
    public float Strength { get; set; }
    public bool IsNegative { get; set; }
    public bool IsEnabled { get; set; }
    public string HighPath { get; set; }  // For dual-model workflows
    public string LowPath { get; set; }   // For dual-model workflows
    // MISSING: Scope property
}
```

### Existing `lora-loader.sbn`
```scriban
#meta
{
  "outputs": {
    "model_output": { "node": "{{ lora_loader_id }}", "index": 0 },  // NOT scoped!
    "clip_output": { "node": "{{ lora_loader_id }}", "index": 1 }    // NOT scoped!
  }
}
#end

{
  "{{ lora_loader_id }}": {
    "inputs": {
      "model": {{ get_ref ((scope ?? "") + "model_output") }},  // Reads from scope ?
      "clip": {{ get_ref ((scope ?? "") + "clip_output") }}      // Reads from scope ?
    }
  }
}
```

**Problem:** Outputs are NOT scoped, so LoRA chaining doesn't work properly for scoped contexts.

### Existing `load-checkpoint.sbn`
- Has prompts bundled
- Uses `PCLazyLoraLoader` (prompt-based LoRA loading)
- NOT minimal

---

## Implementation Plan

### Phase 1: Extend `prompts.sbn` with Scope Support

**File:** `BlazorWebApp/Workflows/Fragments/prompts.sbn`

**Changes:**
1. Scope outputs and node IDs
2. Parameterize UI component
3. Add conditional activation

**Updated Fragment:**
```scriban
#meta
{
  "outputs": {
    "{{ scope ?? '' }}positive_output": { "node": "{{ scope ?? '' }}positive_encode", "index": 0 },
    "{{ scope ?? '' }}negative_output": { "node": "{{ scope ?? '' }}negative_encode", "index": 0 }
  }{{~ if scope && scope | string.contains "detailer" ~}},
  "conditions": {
    "required": ["Detailer.IsActive"]
  }{{~ end ~}},
  "ui": {
    "type": "prompts",
    "component": "{{ ui_component ?? 'PromptsForm' }}",
    "title": "{{ scope_title ?? '' }}Prompts",
    "icon": "fa-solid fa-comment",
    "order": {{ ui_order ?? 10 }},
    "collapsible": {{ ui_collapsible ?? false }},
    "defaultCollapsed": {{ ui_defaultCollapsed ?? false }},
    "parameters": {
      "positive": { },
      "negative": { }
    }
  }
}
#end

{
  "{{ scope ?? '' }}positive_encode": {
    "inputs": {
      "text": {{ positive | json }},
      "clip": {{ get_ref ((scope ?? "") + "clip_output") }}
    },
    "class_type": "CLIPTextEncode",
    "_meta": {
      "title": "{{ scope_title ?? '' }}CLIP Text Encode-Positive"
    }
  },
  "{{ scope ?? '' }}negative_encode": {
    "inputs": {
      "text": {{ negative ?? "" | json }},
      "clip": {{ get_ref ((scope ?? "") + "clip_output") }}
    },
    "class_type": "CLIPTextEncode",
    "_meta": {
      "title": "{{ scope_title ?? '' }}CLIP Text Encode-Negative"
    }
  }
}
```

**Backward Compatible:**
- No scope = main pipeline (unchanged behavior)
- With scope = scoped outputs

---

### Phase 2: Create `SimplePromptsForm` Component

**File:** `BlazorWebApp/Components/Shared/Generation/Fragments/SimplePromptsForm.razor`

**Purpose:** Lightweight prompt form for scoped contexts (detailer, upscale, etc.)

**Key Features:**
- ? NO styles, NO autocomplete, NO LLM tools
- ? Uses `@bind-Value` / `@bind-Value:after` pattern for proper propagation
- ? Reusable for ANY scope
- ? Auto-extracts scope title from FragmentId

**Implementation:**
```razor
@*
    SimplePromptsForm - Lightweight scoped prompts component
    
    For scoped contexts (detailer, upscale, etc.)
    Does NOT include: styles, autocomplete, LLM tools
*@
@using BlazorWebApp.Attributes
@using BlazorWebApp.Events
@attribute [FragmentComponent("SimplePromptsForm")]
@inject IGenerationParameterService ParameterService
@inject IEventService EventService
@inject ILogger<SimplePromptsForm> Logger
@implements IDisposable

<MudGrid Spacing="2">
    <MudItem xs="12">
        <MudTextField T="string" 
                      @bind-Value="_positive"
                      @bind-Value:after="OnPositiveChanged"
                      Label="@($"{_scopeTitle}Positive Prompt")" 
                      Lines="3" 
                      Variant="Variant.Outlined"
                      Immediate="false"
                      DebounceInterval="300"
                      HelperText="@_positiveHelperText" />
    </MudItem>
    
    <MudItem xs="12">
        <MudTextField T="string" 
                      @bind-Value="_negative"
                      @bind-Value:after="OnNegativeChanged"
                      Label="@($"{_scopeTitle}Negative Prompt")" 
                      Lines="3" 
                      Variant="Variant.Outlined"
                      Immediate="false"
                      DebounceInterval="300"
                      HelperText="@_negativeHelperText" />
    </MudItem>
</MudGrid>

@code {
    [Parameter] public string FragmentId { get; set; } = "detailer_prompts";
    
    private string _positive = "";
    private string _negative = "";
    private string _scopeTitle = "";
    private string _positiveHelperText = "Describe what to enhance";
    private string _negativeHelperText = "Describe what to avoid";
    
    protected override void OnInitialized()
    {
        EventService.Subscribe<GenerationParametersChangedEventArgs>(OnParametersChanged);
        ExtractScopeTitle();
    }
    
    protected override void OnParametersSet()
    {
        LoadFromFragment();
    }
    
    private void ExtractScopeTitle()
    {
        // Extract scope title from fragment ID for UI labels
        // e.g., "detailer_prompts" ? "Detailer "
        if (FragmentId.Contains("_"))
        {
            var scopeName = FragmentId.Split('_')[0];
            _scopeTitle = char.ToUpper(scopeName[0]) + scopeName.Substring(1) + " ";
        }
    }
    
    private void OnParametersChanged(GenerationParametersChangedEventArgs args)
    {
        if (args.ChangeType == GenerationParameterChangeType.WorkflowChanged ||
            args.ChangeType == GenerationParameterChangeType.ParametersLoaded)
        {
            LoadFromFragment();
            InvokeAsync(StateHasChanged);
        }
    }
    
    private void LoadFromFragment()
    {
        var fragment = ParameterService.Current.GetFragment(FragmentId);
        if (fragment == null)
        {
            Logger.LogDebug("SimplePromptsForm: Fragment '{FragmentId}' not found", FragmentId);
            return;
        }
        
        _positive = fragment.GetValueOrDefault<string>("positive", "");
        _negative = fragment.GetValueOrDefault<string>("negative", "");
    }
    
    private void OnPositiveChanged()
    {
        ParameterService.SetFragmentValue(FragmentId, "positive", _positive);
    }
    
    private void OnNegativeChanged()
    {
        ParameterService.SetFragmentValue(FragmentId, "negative", _negative);
    }
    
    public void Dispose()
    {
        EventService.Unsubscribe<GenerationParametersChangedEventArgs>(OnParametersChanged);
    }
}
```

**Benefits:**
- ? Uses `@bind-Value:after` pattern (proper Blazor 8+ idiom)
- ? Debounced input (300ms)
- ? ~80 lines of code
- ? Zero dependencies on complex services

---

### Phase 3: Add `Scope` to `Lora` Model

**File:** `BlazorWebApp/Models/Lora.cs`

**Addition:**
```csharp
public class Lora
{
    // Existing properties
    public string Name { get; set; }
    public string Path { get; set; }
    public float Strength { get; set; }
    public bool IsNegative { get; set; }
    public bool IsEnabled { get; set; }
    public string HighPath { get; set; }
    public string LowPath { get; set; }
    
    // NEW: Scope property
    /// <summary>
    /// Scope for multi-model workflows.
    /// null or "" = main pipeline (default)
    /// "detailer_" = detailer scope
    /// "upscale_" = upscale scope (future)
    /// </summary>
    public string? Scope { get; set; }
    
    // Helper properties
    public bool IsMainScope => string.IsNullOrEmpty(Scope);
    public bool IsDetailerScope => Scope == "detailer_";
    
    // Update clone constructor
    public Lora(Lora clone)
    {
        Name = clone.Name;
        Path = clone.Path;
        Strength = clone.Strength;
        IsNegative = clone.IsNegative;
        IsEnabled = clone.IsEnabled;
        HighPath = clone.HighPath;
        LowPath = clone.LowPath;
        Scope = clone.Scope;  // NEW
    }
}
```

**No separate lists needed!** Single `GenerationParameters.Loras` list, filtered by `Scope`.

---

### Phase 4: Update `lora-loader.sbn` - Scope Outputs

**File:** `BlazorWebApp/Workflows/Fragments/lora-loader.sbn`

**Changes:** Scope the outputs to match the scope of inputs.

**Updated Fragment:**
```scriban
#meta
{
  "outputs": {
    "{{ scope ?? '' }}model_output": { "node": "{{ lora_loader_id }}", "index": 0 },
    "{{ scope ?? '' }}clip_output": { "node": "{{ lora_loader_id }}", "index": 1 }
  }{{~ if scope && scope | string.contains "detailer" ~}},
  "conditions": {
    "required": ["Detailer.IsActive"]
  }{{~ end ~}}
}
#end

{
  "{{ lora_loader_id }}": {
    "inputs": {
      "lora_name": {{ (lora_path ?? lora_name) | json }},
      "strength_model": {{ lora_strength | json }},
      "strength_clip": {{ lora_strength_clip ?? lora_strength ?? 1.0 | json }},
      "model": {{ get_ref ((scope ?? "") + "model_output") }},
      "clip": {{ get_ref ((scope ?? "") + "clip_output") }}
    },
    "class_type": "LoraLoader",
    "_meta": {
      "title": "{{ scope_title ?? '' }}LoRA: {{ lora_name }}"
    }
  }
}
```

**Key Changes:**
- ? Outputs are now scoped: `{{ scope ?? '' }}model_output`
- ? Conditional activation for detailer scope
- ? Better node title with LoRA name

---

### Phase 5: Refactor Checkpoint Fragments

**Objective:** Split `load-checkpoint.sbn` into minimal and with-prompts versions.

#### 5.1 Create `load-checkpoint.sbn` (Minimal)

**File:** `BlazorWebApp/Workflows/Fragments/load-checkpoint.sbn` (REFACTOR)

**New minimal version (matches `load-diffusion.sbn` pattern):**
```scriban
#meta
{
  "outputs": {
    "{{ scope ?? '' }}model_output": { "node": "{{ scope ?? '' }}{{ loader_id ?? 'checkpoint' }}", "index": 0 },
    "{{ scope ?? '' }}clip_output": { "node": "{{ scope ?? '' }}{{ loader_id ?? 'checkpoint' }}", "index": 1 },
    "{{ scope ?? '' }}vae_output": { "node": "{{ scope ?? '' }}{{ loader_id ?? 'checkpoint' }}", "index": 2 }
  }{{~ if scope && scope | string.contains "detailer" ~}},
  "conditions": {
    "required": ["Detailer.IsActive"]
  }{{~ end ~}}
}
#end

{
  "{{ scope ?? '' }}{{ loader_id ?? 'checkpoint' }}": {
    "inputs": {
      "ckpt_name": {{ ckpt_name | json }}
    },
    "class_type": "CheckpointLoaderSimple",
    "_meta": {
      "title": "{{ scope_title ?? '' }}Load Checkpoint"
    }
  }
}
```

#### 5.2 Create `load-checkpoint-w-prompts.sbn` (With Prompts)

**File:** `BlazorWebApp/Workflows/Fragments/load-checkpoint-w-prompts.sbn` (NEW or rename existing)

**Purpose:** Legacy checkpoint loader with bundled prompts (for backward compatibility or specific use cases)

**Note:** This fragment can still exist for templates that want all-in-one loading, but new templates should use the modular approach.

---

### Phase 6: Update `LoraForm` - Add Scope Support

**File:** `BlazorWebApp/Components/Shared/Generation/LoraForm.razor`

**Add optional `Scope` parameter:**

```razor
@code {
    [Parameter] public Backend Backend { get; set; }
    [Parameter] public List<Lora> Loras { get; set; }
    [Parameter] public EventCallback OnLorasUpdated { get; set; }
    
    // NEW: Optional scope for scoped LoRA forms
    [Parameter] public string? Scope { get; set; }

    // ... existing code ...

    public async Task AddLora()
    {
        if (string.IsNullOrWhiteSpace(_lora)) return;

        Loras.Add(new()
        {
            Name = Path.GetFileNameWithoutExtension(_lora),
            Path = _lora,
            IsNegative = false,
            Strength = 1,
            IsEnabled = true,
            Scope = Scope  // NEW: Apply scope to new LoRAs
        });

        await OnLorasUpdated.InvokeAsync();
    }
}
```

**Usage:**
```razor
@* Main LoRAs (no scope) *@
<LoraForm Loras="@_mainLoras" OnLorasUpdated="HandleMainLorasUpdated" />

@* Detailer LoRAs (scoped) *@
<LoraForm Loras="@_detailerLoras" Scope="detailer_" OnLorasUpdated="HandleDetailerLorasUpdated" />
```

---

### Phase 7: Update `DetailerForm` - Add LoRA Section

**File:** `BlazorWebApp/Components/Shared/Generation/Fragments/DetailerForm.razor`

**Add LoRA management section:**

```razor
@* Add after existing controls, before closing </MudGrid> *@

@* LoRA Section *@
<MudItem xs="12">
    <MudText Typo="Typo.subtitle1" Class="mb-2">Detailer LoRAs</MudText>
    <LoraForm Loras="@_detailerLoras" 
              Scope="detailer_"
              OnLorasUpdated="HandleDetailerLorasUpdated" />
</MudItem>
```

**Code additions:**
```csharp
private List<Lora> _detailerLoras = new();

protected override void OnParametersSet()
{
    LoadFromFragment();
    LoadResolvedOptions();
    LoadDetailerLoras();
}

private void LoadDetailerLoras()
{
    // Filter LoRAs by scope from the single centralized list
    _detailerLoras = ParameterService.Current.Loras?
        .Where(l => l.Scope == "detailer_")
        .ToList() ?? new List<Lora>();
}

private void HandleDetailerLorasUpdated()
{
    // Update the centralized LoRA list
    // Remove old detailer LoRAs, add updated ones
    var mainLoras = ParameterService.Current.Loras?
        .Where(l => l.Scope != "detailer_")
        .ToList() ?? new List<Lora>();
    
    ParameterService.Current.Loras = mainLoras
        .Concat(_detailerLoras)
        .ToList();
    
    // Notify of change
    EventService.Publish(new GenerationParametersChangedEventArgs 
    { 
        ChangeType = GenerationParameterChangeType.LorasUpdated 
    });
}
```

---

### Phase 8: Template Structure - LoRA Scoping

**Templates filter LoRAs by scope using Scriban:**

```scriban
{
  "Pipeline": [
    // ==================
    // MAIN GENERATION
    // ==================
    
    // 1. Load model (minimal)
    { "id": "loader_main", "fragment": "load-checkpoint.sbn", "parameters": { "ckpt_name": {{ Model | json }} } },
    
    // 2. Load main LoRAs (scope = null or "")
    {{~ if Loras && Loras.size > 0 ~}}
    {{~ for lora in Loras ~}}
    {{~ if !lora.Scope || lora.Scope == "" ~}}
    {
      "id": "lora_main_{{ for.index }}",
      "fragment": "lora-loader.sbn",
      "parameters": {
        "lora_loader_id": "lora_main_{{ for.index }}",
        "lora_name": {{ lora.Name | json }},
        "lora_path": {{ lora.Path | json }},
        "lora_strength": {{ lora.Strength | json }}
      }
    },
    {{~ end ~}}
    {{~ end ~}}
    {{~ end ~}}
    
    // 3. Encode main prompts
    { "id": "prompts", "fragment": "prompts.sbn", "parameters": { "positive": {{ Positive | json }}, "negative": {{ Negative | json }} } },
    
    // ... sampler, decode, etc. ...
    
    // ==================
    // DETAILER (CONDITIONAL)
    // ==================
    
    // 4. Load detailer model
    {
      "id": "loader_detailer",
      "fragment": "load-checkpoint.sbn",
      "parameters": {
        "scope": "detailer_",
        "scope_title": "Detailer ",
        "loader_id": "detailer_checkpoint",
        "ckpt_name": {{ DetailerModel ?? Model | json }}
      }
    },
    
    // 5. Load detailer LoRAs (scope = "detailer_")
    {{~ if Loras && Loras.size > 0 ~}}
    {{~ for lora in Loras ~}}
    {{~ if lora.Scope == "detailer_" ~}}
    {
      "id": "lora_detailer_{{ for.index }}",
      "fragment": "lora-loader.sbn",
      "parameters": {
        "lora_loader_id": "lora_detailer_{{ for.index }}",
        "scope": "detailer_",
        "scope_title": "Detailer ",
        "lora_name": {{ lora.Name | json }},
        "lora_path": {{ lora.Path | json }},
        "lora_strength": {{ lora.Strength | json }}
      }
    },
    {{~ end ~}}
    {{~ end ~}}
    {{~ end ~}}
    
    // 6. Encode detailer prompts
    {
      "id": "detailer_prompts",
      "fragment": "prompts.sbn",
      "parameters": {
        "scope": "detailer_",
        "scope_title": "Detailer ",
        "ui_component": "SimplePromptsForm",
        "ui_order": 115,
        "ui_collapsible": true,
        "positive": {{ DetailerPositive ?? "" | json }},
        "negative": {{ DetailerNegative ?? "" | json }}
      }
    },
    
    // 7. Detailer core
    { "id": "detailer", "fragment": "detailer-core.sbn", "parameters": { "scope": "detailer_" } },
    
    // 8. Save
    { "id": "save", "fragment": "save.sbn", "parameters": {} }
  ]
}
```

**Key Points:**
- ? Single `Loras` array with `Scope` property
- ? Filter with `{{~ if lora.Scope == "detailer_" ~}}`
- ? Same `prompts.sbn` fragment for both main and detailer
- ? Same `lora-loader.sbn` fragment for both scopes

---

## File Changes Summary

### New Files

| File | Purpose | Lines |
|------|---------|-------|
| `Components/Shared/Generation/Fragments/SimplePromptsForm.razor` | Lightweight scoped prompts | ~80 |

### Modified Files

| File | Changes |
|------|---------|
| `Workflows/Fragments/prompts.sbn` | Add scope to outputs, parameterize UI component |
| `Workflows/Fragments/lora-loader.sbn` | Add scope to outputs |
| `Workflows/Fragments/load-checkpoint.sbn` | Refactor to minimal (remove prompts/LoRAs) |
| `Models/Lora.cs` | Add `Scope` property |
| `Components/Shared/Generation/LoraForm.razor` | Add optional `Scope` parameter |
| `Components/Shared/Generation/Fragments/DetailerForm.razor` | Add LoRA section |
| `Workflows/Templates/*/txt2img.sbn` | Update LoRA iteration with scope filtering |
| `Workflows/Templates/*/img2img.sbn` | Update LoRA iteration with scope filtering |

### Renamed/Deprecated Files

| Original | New | Notes |
|----------|-----|-------|
| `load-checkpoint.sbn` (old) | `load-checkpoint-w-prompts.sbn` | Rename existing for backward compat |
| N/A | `load-checkpoint.sbn` (new) | New minimal version |

---

## Implementation Checklist

### Phase 1: Fragment Updates
- [ ] Update `prompts.sbn` with scope support
- [ ] Update `lora-loader.sbn` with scoped outputs
- [ ] Create minimal `load-checkpoint.sbn`
- [ ] Rename old to `load-checkpoint-w-prompts.sbn` (optional)

### Phase 2: Model Updates
- [ ] Add `Scope` property to `Lora.cs`
- [ ] Update `Lora` clone constructor
- [ ] Verify JSON serialization

### Phase 3: Component Updates
- [ ] Create `SimplePromptsForm.razor`
- [ ] Add `Scope` parameter to `LoraForm.razor`
- [ ] Add LoRA section to `DetailerForm.razor`

### Phase 4: Template Updates
- [ ] Update Z-Image template
- [ ] Update Flux templates
- [ ] Update checkpoint-based templates
- [ ] Test LoRA scope filtering

### Phase 5: Testing
- [ ] Test main LoRAs load correctly
- [ ] Test detailer LoRAs load correctly
- [ ] Test LoRA chaining within scopes
- [ ] Test prompts for both scopes
- [ ] Test state persistence

---

## Success Criteria

1. ? Single `Loras` list with `Scope` property
2. ? Templates filter LoRAs by scope (no separate lists)
3. ? `prompts.sbn` reused for main and scoped contexts
4. ? `lora-loader.sbn` outputs are scoped
5. ? `load-checkpoint.sbn` is minimal (no prompts/LoRAs)
6. ? `SimplePromptsForm` works for any scope
7. ? Backward compatibility maintained
8. ? Zero code duplication

---

## Questions Resolved

### 1. **Should we have separate LoRA lists?**
**Answer:** NO. Single `GenerationParameters.Loras` list, filter by `Scope` in templates.

### 2. **Should we create a new prompt fragment?**
**Answer:** NO. Extend existing `prompts.sbn` with scope support.

### 3. **What about `load-checkpoint.sbn`?**
**Answer:** Refactor to minimal (like `load-diffusion.sbn`). Optionally keep `load-checkpoint-w-prompts.sbn` for legacy.

### 4. **Naming: `SimplePromptsForm` vs `ScopedPromptsForm`?**
**Answer:** `SimplePromptsForm` - emphasizes simplicity (no autocomplete, no styles).

### 5. **How does LoRA chaining work with scope?**
**Answer:** `lora-loader.sbn` reads from `{scope}model_output` and writes to `{scope}model_output`. Chaining just works.

---

*Plan version: 4.0 (FINAL - Simplification Focus)*
*Status: Ready for Implementation*
*Principles: SIMPLIFY, ABSTRACT, UNIFY*
