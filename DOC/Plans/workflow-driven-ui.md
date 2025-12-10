# Workflow-Driven UI Refactoring - Implementation Plan

## Status
**Current Phase:** Planning
**Last Updated:** Session Start

---

## Problem Statement

### Current Issues

1. **Hardcoded UI Components**: Generation pages (`Txt2ImgComfyUI.razor`, `Img2ImgComfyUI.razor`) contain hardcoded form fields that don't adapt to workflow requirements:
   - `GenerateFormImg2ImgComfyUI.razor` exposes Qwen-specific settings (Megapixels, LoraStrength, ModelShift) that are irrelevant for other workflows
   - `GenerateFormTxt2ImgComfyUI.razor` shows SeedVR2, Conditioning Variation, ADetailer toggles regardless of whether the workflow supports them

2. **Scalability Concerns**: Adding new workflows (e.g., Z-Image, Chroma, new architectures) requires:
   - Creating new page components
   - Duplicating form logic
   - Manually maintaining feature parity

3. **Future-Proofing**: The current design doesn't accommodate:
   - New inference modes (Text-to-Audio, Video-to-Video)
   - Novel workflow features not yet conceived
   - Architecture-specific optimizations

### Design Tension
The `WorkflowAssets` system already demonstrates the pattern we need: **workflow templates drive UI rendering**. Assets defined in `.sbn` templates populate model selectors dynamically. This pattern should extend to all form controls.

---

## Proposed Solution

### Core Concept: UI Fragments

Extend the workflow template system to define **UI components** alongside pipeline fragments. The generation page renders only components required by the active workflow.

### Key Architectural Changes

1. **UI Metadata in Templates**: Add a `UIFragments` section to workflow templates that declares which UI components to render
2. **Component Registry**: Create a registry mapping fragment/feature names to Blazor components
3. **Dynamic Generation Page**: Replace mode-specific pages with a unified page that assembles UI based on workflow metadata
4. **Parameter Binding**: Establish bidirectional binding between UI components and parameter models

### Example Template Evolution

**Current** (Pipeline-only):
```json
{
  "Title": "Txt2Img",
  "Base": "ZImage",
  "Mode": "txt2img",
  "Assets": [...],
  "Pipeline": [
    { "fragment": "load-diffusion.sbn", ... },
    { "fragment": "upscale-seedvr2.sbn", ... }
  ]
}
```

**Proposed** (With UI metadata):
```json
{
  "Title": "Txt2Img",
  "Base": "ZImage",
  "Mode": "txt2img",
  "Assets": [...],
  "UIComponents": [
    { "component": "PromptFields", "order": 1 },
    { "component": "SamplerSettings", "order": 2, "section": "main" },
    { "component": "SeedVR2Settings", "order": 3, "section": "upscale", "toggleable": true }
  ],
  "Pipeline": [...]
}
```

---

## Key Decisions

| Decision | Rationale |
|----------|-----------|
| UI metadata in templates | Single source of truth; workflow authors control UI |
| Component registry pattern | Decouples template strings from Blazor types |
| Unified Generation page | Reduces duplication; simplifies navigation |
| Keep ModeType enum | Too embedded to change; useful for parameter separation |
| Preserve existing Assets | Already working well; serves as pattern for extension |

---

## Conventions

### Naming Conventions
- UI component names in templates: PascalCase matching component filename (e.g., `SeedVR2Settings`)
- Parameter binding: Use existing parameter model properties (e.g., `Parameters.SeedVR2.IsActive`)
- Section identifiers: lowercase, descriptive (e.g., `main`, `upscale`, `detailer`)

### Component Design Rules
1. Each UI fragment component must be self-contained
2. Components receive parameters via dependency injection (`ManagerService`) or cascading parameters
3. Components must handle null/missing parameter gracefully
4. Toggle-able components manage their own enabled/disabled state

### Template Conventions
- `UIComponents` array is optional (fallback to legacy behavior if absent)
- `order` determines render sequence
- `section` groups related components
- `toggleable: true` wraps component in an expandable chip/panel

---

## Implementation Phases

### Phase 1: Foundation - UI Component Registry
**Objective:** Create infrastructure for mapping template strings to Blazor components

**Status:** [ ] Not Started

#### Tasks
- [ ] Create `IUIComponentRegistry` interface
- [ ] Create `UIComponentRegistry` service
- [ ] Define `UIComponentMetadata` model
- [ ] Register service in DI
- [ ] Create initial mappings for existing components

#### Success Criteria
- Registry can resolve component Type from string name
- Unit tests verify registration and lookup

#### Files to Create/Modify
- `BlazorWebApp/Services/UIComponentRegistry.cs` (new)
- `BlazorWebApp/Models/UIComponentMetadata.cs` (new)
- `BlazorWebApp/Program.cs` (register service)

---

### Phase 2: Template Schema Extension
**Objective:** Extend workflow templates to include UI component definitions

**Status:** [ ] Not Started

#### Tasks
- [ ] Add `UIComponents` property to `Workflow` model
- [ ] Create `UIComponentDefinition` model
- [ ] Update `WorkflowService` to parse new schema
- [ ] Create sample template with UI components

#### Success Criteria
- Template parsing extracts UI component definitions
- Existing templates without `UIComponents` continue to work

#### Files to Create/Modify
- `BlazorWebApp/Models/Workflow.cs` (add property)
- `BlazorWebApp/Models/UIComponentDefinition.cs` (new)
- `BlazorWebApp/Services/WorkflowService.cs` (parsing logic)
- `BlazorWebApp/Workflows/Templates/z-image/txt2img.sbn` (sample)

---

### Phase 3: Modular UI Components
**Objective:** Refactor existing form controls into standalone, registrable components

**Status:** [ ] Not Started

#### Tasks
- [ ] Extract `SamplerSettings` component from GenerateFormTxt2ImgComfyUI
- [ ] Extract `ResolutionSettings` component
- [ ] Extract `SeedControl` component
- [ ] Extract `SeedVR2Settings` component
- [ ] Extract `ConditioningVariationSettings` component
- [ ] Extract `ADetailerSettings` component
- [ ] Register all components in registry

#### Success Criteria
- Each component works independently
- Components bind to appropriate parameter models
- Original forms can use extracted components (backward compatibility)

#### Files to Create
- `BlazorWebApp/Components/Generation/SamplerSettings.razor`
- `BlazorWebApp/Components/Generation/ResolutionSettings.razor`
- `BlazorWebApp/Components/Generation/SeedControl.razor`
- `BlazorWebApp/Components/Generation/SeedVR2Settings.razor`
- `BlazorWebApp/Components/Generation/ConditioningVariationSettings.razor`
- `BlazorWebApp/Components/Generation/ADetailerSettings.razor`

---

### Phase 4: Dynamic Component Renderer
**Objective:** Create a component that renders UI fragments based on workflow metadata

**Status:** [ ] Not Started

#### Tasks
- [ ] Create `DynamicGenerationForm` component
- [ ] Implement component resolution from registry
- [ ] Implement section grouping
- [ ] Implement toggle wrapping for optional features
- [ ] Handle component ordering

#### Success Criteria
- Component renders correct UI based on workflow
- Sections group related controls
- Toggleable components expand/collapse correctly

#### Files to Create
- `BlazorWebApp/Components/Generation/DynamicGenerationForm.razor`

---

### Phase 5: Unified Generation Page
**Objective:** Create a single generation page that adapts to any mode/workflow

**Status:** [ ] Not Started

#### Tasks
- [ ] Create `/comfyui/generate/{mode}/{workflowId}` page
- [ ] Implement mode-based parameter switching
- [ ] Integrate `DynamicGenerationForm`
- [ ] Handle image input for Img2Img mode
- [ ] Preserve backward-compatible routes

#### Success Criteria
- Single page handles Txt2Img, Img2Img, Upscale modes
- Correct parameters load based on mode
- Navigation works from workflow selection

#### Files to Create
- `BlazorWebApp/Pages/ComfyUI/GenerationComfyUI.razor`

#### Files to Modify
- `BlazorWebApp/Components/Shared/NavBar.razor` (routing)

---

### Phase 6: Template Migration
**Objective:** Add UIComponents to existing workflow templates

**Status:** [ ] Not Started

#### Tasks
- [ ] Add UIComponents to z-image/txt2img.sbn
- [ ] Add UIComponents to flux/txt2img.sbn
- [ ] Add UIComponents to qwen/txt2img.sbn
- [ ] Add UIComponents to qwen/img2img-edit.sbn
- [ ] Verify all workflows render correctly

#### Success Criteria
- All templates define their UI requirements
- No regression in existing functionality

---

### Phase 7: Legacy Cleanup & Documentation
**Objective:** Remove duplicated code and update documentation

**Status:** [ ] Not Started

#### Tasks
- [ ] Deprecate old form components (if safe)
- [ ] Update TEMPLATE_GUIDE.md with UIComponents schema
- [ ] Create component development guide
- [ ] Update DOC folder with architecture documentation

#### Success Criteria
- Documentation reflects new architecture
- Developers can add new UI components

---

## Stress Points & Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Breaking existing workflows | High | Maintain backward compatibility; UIComponents is optional |
| Component binding complexity | Medium | Use established ManagerService patterns |
| Performance (dynamic rendering) | Low | DynamicComponent has minimal overhead |
| Template author learning curve | Medium | Comprehensive documentation; sensible defaults |
| Parameter model fragmentation | Medium | Keep unified parameter classes; components read specific properties |

---

## Discussion Points

### Open Questions

1. **Inferred UI from Pipeline**: Should UI components be *inferred* from Pipeline fragments, or *explicitly declared* in UIComponents?
   - **Inferred Pro**: Less duplication; fragments already have conditions
   - **Inferred Con**: Less control; coupling between pipeline and UI
   - **Explicit Pro**: Complete control; can have UI without pipeline fragment
   - **Explicit Con**: Potential drift between Pipeline and UIComponents

2. **Component Granularity**: How fine-grained should components be?
   - Fine (SeedField, StepsSlider) = Maximum flexibility, more template verbosity
   - Coarse (SamplerSettings group) = Less flexibility, simpler templates

3. **Mode-Specific Components**: How to handle Img2Img's image input requirement?
   - Option A: ImageInput is a UIComponent like any other
   - Option B: Mode determines structural layout, UIComponents fill sections

4. **Backward Compatibility Strategy**: How long to maintain old pages?
   - Option A: Keep indefinitely, slowly migrate users
   - Option B: Redirect after X releases
   - Option C: Feature flag to switch between old/new

### User Input Requested
Please share your thoughts on these questions before we proceed to Phase 1.

---

## Changelog

| Date | Phase | Changes |
|------|-------|---------|
| Session Start | Planning | Initial plan created |

---

## Code Examples

### UIComponentRegistry Pattern

```csharp
public interface IUIComponentRegistry
{
    Type? GetComponentType(string componentName);
    UIComponentMetadata? GetMetadata(string componentName);
    void Register<TComponent>(string name, UIComponentMetadata? metadata = null) where TComponent : ComponentBase;
}

public class UIComponentRegistry : IUIComponentRegistry
{
    private readonly Dictionary<string, (Type ComponentType, UIComponentMetadata? Metadata)> _registry = new();

    public UIComponentRegistry()
    {
        // Register built-in components
        Register<SamplerSettings>("SamplerSettings", new() { Section = "main", Order = 10 });
        Register<SeedControl>("SeedControl", new() { Section = "main", Order = 20 });
        Register<ResolutionSettings>("ResolutionSettings", new() { Section = "main", Order = 30 });
        Register<SeedVR2Settings>("SeedVR2Settings", new() { Section = "upscale", Toggleable = true });
        // ... etc
    }

    public Type? GetComponentType(string componentName) 
        => _registry.TryGetValue(componentName, out var entry) ? entry.ComponentType : null;
}
```

### DynamicGenerationForm Pattern

```razor
@inject IUIComponentRegistry Registry
@inject ManagerService M

@foreach (var section in GroupedComponents)
{
    <MudText Typo="Typo.overline">@section.Key</MudText>
    @foreach (var component in section.OrderBy(c => c.Order))
    {
        @if (component.Toggleable)
        {
            <MudExpansionPanel Text="@component.DisplayName">
                <DynamicComponent Type="@Registry.GetComponentType(component.Component)" />
            </MudExpansionPanel>
        }
        else
        {
            <DynamicComponent Type="@Registry.GetComponentType(component.Component)" />
        }
    }
}
```

### Template UIComponents Example

```json
{
  "UIComponents": [
    { 
      "component": "PromptFields", 
      "order": 1,
      "section": "prompts"
    },
    { 
      "component": "SamplerSettings", 
      "order": 10,
      "section": "main"
    },
    { 
      "component": "ResolutionSettings", 
      "order": 20,
      "section": "main"
    },
    { 
      "component": "SeedVR2Settings", 
      "order": 30,
      "section": "upscale",
      "toggleable": true,
      "displayName": "SeedVR2 Upscaler"
    }
  ]
}
```

---

## References

- `BlazorWebApp/Workflows/TEMPLATE_GUIDE.md` - Existing template documentation
- `BlazorWebApp/Components/Shared/Generation/WorkflowAssetsPanel.razor` - Pattern for workflow-driven UI
- `BlazorWebApp/Components/Txt2Img/GenerateFormTxt2ImgComfyUI.razor` - Current form implementation
- `BlazorWebApp/Models/Workflow.cs` - Workflow model definition
- `BlazorWebApp/Services/WorkflowService.cs` - Template parsing service

---

## Assistant Notes

### Current Understanding
- The existing `Assets` system proves the concept works
- Fragment `#meta` conditions already define feature requirements
- The challenge is bridging template metadata to Blazor component rendering
- ModeType enum and parameter separation should remain unchanged

### Suggested First Step
After discussing open questions, Phase 1 (UIComponentRegistry) is self-contained and can be implemented without affecting existing code. This provides a foundation to test assumptions before larger changes.

---

*Document version: 1.0*
*Planning session in progress*
