# Wan Components Implementation Guide

**Created:** 2024  
**Status:** Planning  
**Goal:** Add Wan-specific UI components for Generate page and update video tab display

---

## **Overview**

This guide covers implementing Blazor components for Wan workflow fragments to enable video generation via the Generate page.

**Key Requirements:**
1. ? Video workflows (`Mode == Img2Vid`) already show `GeneratedVideoTabs` instead of `GeneratedImageTabs`
2. ?? Need UI components for Wan-specific fragments
3. ?? Need to register components in `ComponentRegistry`
4. ?? Optional: Enhance `GeneratedVideoTabs` for better UX

---

## **Phase 1: Create Wan Fragment Components**

Based on the `pose2vid-steadydancer_conversion.md` log, we need components for fragments that have `ui` sections in their `#meta` blocks.

### **Components Needed**

| Fragment | Component Name | Purpose | Priority |
|----------|----------------|---------|----------|
| `load-video.sbn` | `VideoSourceForm` | Video upload & frame selection | ?? High |
| `wan/context-options.sbn` | `WanContextOptionsForm` | Context window settings | ?? Medium |
| `wan/steadydancer-embeds.sbn` | `SteadyDancerForm` | Pose strength controls | ?? Medium |
| `wan/frame-interpolation.sbn` | `FrameInterpolationForm` | RIFE interpolation settings | ?? Low |

**Note:** Other fragments like `load-image.sbn`, `get-image-size.sbn`, `resize-image-kj.sbn`, etc. are utility fragments without UI - they don't need components.

---

## **Phase 2: Component Implementation Details**

### **2.1 VideoSourceForm** (High Priority)

**File:** `BlazorWebApp/Components/Shared/Generation/Fragments/VideoSourceForm.razor`

**Purpose:** 
- Upload video file
- Control frame selection
- Preview frame range

**Parameters (from `load-video.sbn` template):**
```csharp
[Parameter] public FragmentRef Fragment { get; set; }
[Parameter] public EventCallback OnChanged { get; set; }

// Exposed via Fragment.Parameters:
// - force_rate (int, default 16)
// - custom_width (int, default 480)
// - custom_height (int, default 832)
// - frame_load_cap (int, default 176)
// - skip_first_frames (int, default 0)
// - select_every_nth (int, default 1)
```

**UI Layout:**
```razor
<MudPaper Class="pa-4 mb-4">
    <MudStack>
        @* Video Upload *@
        <MudText Typo="Typo.h6">Source Video</MudText>
        <InputFile OnChange="HandleVideoUpload" accept="video/*" />
        
        @if (HasVideo)
        {
            <video width="100%" controls>
                <source src="@_videoSrc" />
            </video>
        }
        
        @* Frame Selection *@
        <MudText Typo="Typo.subtitle2" Class="mt-4">Frame Selection</MudText>
        <MudNumericField Label="Frame Rate" 
                         @bind-Value="ForceRate" 
                         Min="1" Max="60" />
        <MudNumericField Label="Max Frames" 
                         @bind-Value="FrameLoadCap" 
                         Min="1" Max="500" />
        <MudNumericField Label="Skip First Frames" 
                         @bind-Value="SkipFirstFrames" 
                         Min="0" />
        <MudNumericField Label="Select Every Nth" 
                         @bind-Value="SelectEveryNth" 
                         Min="1" />
        
        @* Resolution *@
        <MudText Typo="Typo.subtitle2" Class="mt-4">Video Resolution</MudText>
        <MudNumericField Label="Width" 
                         @bind-Value="CustomWidth" 
                         Min="64" Max="2048" Step="8" />
        <MudNumericField Label="Height" 
                         @bind-Value="CustomHeight" 
                         Min="64" Max="2048" Step="8" />
    </MudStack>
</MudPaper>
```

**Attribute:**
```csharp
[FragmentComponent("VideoSourceForm")]
public partial class VideoSourceForm
{
    // Implementation...
}
```

---

### **2.2 WanContextOptionsForm** (Medium Priority)

**File:** `BlazorWebApp/Components/Shared/Generation/Fragments/WanContextOptionsForm.razor`

**Purpose:**
- Control context window size
- Set overlap for long video generation

**Parameters (from `wan/context-options.sbn`):**
```csharp
// - context_frames (int, default 81)
// - context_overlap (int, default 16)
```

**UI Layout:**
```razor
<MudPaper Class="pa-4 mb-4">
    <MudStack>
        <MudText Typo="Typo.h6">Context Options</MudText>
        <MudText Typo="Typo.caption">Control how long videos are processed in chunks</MudText>
        
        <MudNumericField Label="Context Frames" 
                         @bind-Value="ContextFrames"
                         Min="17" Max="257" Step="8"
                         HelperText="Number of frames processed at once" />
        
        <MudNumericField Label="Context Overlap" 
                         @bind-Value="ContextOverlap"
                         Min="0" Max="64" Step="4"
                         HelperText="Frames to overlap between chunks for smoother transitions" />
    </MudStack>
</MudPaper>
```

**Attribute:**
```csharp
[FragmentComponent("WanContextOptionsForm")]
public partial class WanContextOptionsForm
{
    // Implementation...
}
```

---

### **2.3 SteadyDancerForm** (Medium Priority)

**File:** `BlazorWebApp/Components/Shared/Generation/Fragments/SteadyDancerForm.razor`

**Purpose:**
- Control pose influence strength
- Separate spatial/temporal controls

**Parameters (from `wan/steadydancer-embeds.sbn`):**
```csharp
// - pose_strength_spatial (float, default 1.0)
// - pose_strength_temporal (float, default 1.0)
```

**UI Layout:**
```razor
<MudPaper Class="pa-4 mb-4">
    <MudStack>
        <MudText Typo="Typo.h6">SteadyDancer Pose Control</MudText>
        
        <MudSlider @bind-Value="PoseStrengthSpatial"
                   Min="0.0" Max="2.0" Step="0.1"
                   Color="Color.Primary">
            Spatial Strength: @PoseStrengthSpatial.ToString("F1")
        </MudSlider>
        <MudText Typo="Typo.caption">Controls pose influence on appearance</MudText>
        
        <MudSlider @bind-Value="PoseStrengthTemporal"
                   Min="0.0" Max="2.0" Step="0.1"
                   Color="Color.Secondary">
            Temporal Strength: @PoseStrengthTemporal.ToString("F1")
        </MudSlider>
        <MudText Typo="Typo.caption">Controls pose influence on motion</MudText>
    </MudStack>
</MudPaper>
```

**Attribute:**
```csharp
[FragmentComponent("SteadyDancerForm")]
public partial class SteadyDancerForm
{
    // Implementation...
}
```

---

### **2.4 FrameInterpolationForm** (Low Priority)

**File:** `BlazorWebApp/Components/Shared/Generation/Fragments/FrameInterpolationForm.razor`

**Purpose:**
- Enable/disable RIFE frame interpolation
- Select RIFE model
- Set multiplier

**Parameters (from `wan/frame-interpolation.sbn`):**
```csharp
// - scale_by (double, default 2.0)
// - frame_multiplier (int, default 2)
// - rife_model (string, default "rife49.pth")
```

**UI Layout:**
```razor
<MudPaper Class="pa-4 mb-4">
    <MudStack>
        <MudText Typo="Typo.h6">Frame Interpolation (RIFE)</MudText>
        
        <MudSelect Label="RIFE Model" 
                   @bind-Value="RifeModel"
                   T="string">
            <MudSelectItem Value="@("rife49.pth")">RIFE 4.9</MudSelectItem>
            <MudSelectItem Value="@("rife48.pth")">RIFE 4.8</MudSelectItem>
            <MudSelectItem Value="@("rife47.pth")">RIFE 4.7</MudSelectItem>
            <MudSelectItem Value="@("rife46.pth")">RIFE 4.6</MudSelectItem>
        </MudSelect>
        
        <MudNumericField Label="Frame Multiplier" 
                         @bind-Value="FrameMultiplier"
                         Min="1" Max="8"
                         HelperText="2x = 60fps from 30fps, 4x = 120fps from 30fps" />
        
        <MudNumericField Label="Scale By" 
                         @bind-Value="ScaleBy"
                         Min="1.0" Max="4.0" Step="0.5"
                         HelperText="Upscale factor for interpolation" />
    </MudStack>
</MudPaper>
```

**Attribute:**
```csharp
[FragmentComponent("FrameInterpolationForm")]
public partial class FrameInterpolationForm
{
    // Implementation...
}
```

---

## **Phase 3: Update Fragment Metadata**

Add `ui` sections to Wan fragments that need components:

### **3.1 load-video.sbn**

```scriban
#meta
{
  "outputs": {
    "video_frames": { "node": "load_video", "index": 0 }
  },
  "ui": {
    "type": "source",
    "component": "VideoSourceForm",
    "title": "Source Video",
    "icon": "fa-solid fa-video",
    "order": 10
  }
}
#end
```

### **3.2 wan/context-options.sbn**

```scriban
#meta
{
  "outputs": {},
  "ui": {
    "type": "setting",
    "component": "WanContextOptionsForm",
    "title": "Context Options",
    "icon": "fa-solid fa-sliders",
    "order": 60,
    "collapsible": true
  }
}
#end
```

### **3.3 wan/steadydancer-embeds.sbn**

```scriban
#meta
{
  "outputs": {
    "image_embeds": { "node": "steadydancer_embeds", "index": 0 }
  },
  "ui": {
    "type": "feature",
    "component": "SteadyDancerForm",
    "title": "SteadyDancer Pose",
    "icon": "fa-solid fa-person",
    "order": 50,
    "collapsible": true
  }
}
#end
```

### **3.4 wan/frame-interpolation.sbn**

```scriban
#meta
{
  "outputs": {
    "frames_output": { "node": "frame_interpolation", "index": 0 }
  },
  "ui": {
    "type": "feature",
    "component": "FrameInterpolationForm",
    "title": "Frame Interpolation",
    "icon": "fa-solid fa-film",
    "order": 70,
    "collapsible": true
  }
}
#end
```

---

## **Phase 4: Component Registration**

Components with `[FragmentComponent]` attribute are auto-discovered. Manual registration is fallback.

**No code changes needed** - attribute-based discovery handles it automatically!

---

## **Phase 5: GeneratedVideoTabs Enhancement** (Optional)

**Current State:** Already exists at `BlazorWebApp/Components/Img2Vid/GeneratedVideoTabs.razor`

**Potential Enhancements:**
1. Video player with controls
2. Download button
3. Metadata display (seed, steps, cfg)
4. Frame rate indicator
5. Duration display
6. Thumbnail preview

**Implementation:**
```razor
@* Check current GeneratedVideoTabs.razor and enhance as needed *@
```

---

## **Implementation Checklist**

### **Phase 1: Img2Vid Components** ? COMPLETE
- [x] Create `PainterI2VForm.razor` (Video Settings)
- [x] Create `FrameInterpolationForm.razor` (Frame Interpolation)
- [x] Update `wan/painter-i2v.sbn` #meta
- [x] Update `wan/frame-interpolation.sbn` #meta
- [x] Build successful

**Status:** Basic img2vid workflow components are ready!

### **Phase 2: Pose2Vid Components** (Future)
- [ ] Create `VideoSourceForm.razor` + video upload
- [ ] Create `WanContextOptionsForm.razor`
- [ ] Create `SteadyDancerForm.razor`
- [ ] Update `load-video.sbn` #meta
- [ ] Update `wan/context-options.sbn` #meta
- [ ] Update `wan/steadydancer-embeds.sbn` #meta

### **Phase 3: Testing**
- [ ] Test img2vid workflow
  - [ ] Video settings controls appear
  - [ ] Frame interpolation toggle works
  - [ ] Parameters save correctly
  - [ ] Video generation works
- [ ] Test pose2vid workflow (after Phase 2)

### **Phase 4: Video Tab Enhancement** (Optional)
- [ ] Review current `GeneratedVideoTabs.razor`
- [ ] Add video player enhancements
- [ ] Add metadata display
- [ ] Test video playback

---

## **Component Template**

**Standard Fragment Component Pattern:**

```razor
@using BlazorWebApp.Attributes
@using BlazorWebApp.Models
@using static BlazorWebApp.Models.FragmentKeys
@inject IGenerationParameterService ParameterService

<MudPaper Class="pa-4 mb-4">
    <MudStack>
        <MudText Typo="Typo.h6">@Fragment.Title</MudText>
        
        @* Form controls here *@
        
    </MudStack>
</MudPaper>

@code {
    [Parameter] public FragmentRef Fragment { get; set; } = null!;
    [Parameter] public EventCallback OnChanged { get; set; }
    
    // Bind to Fragment.Parameters properties
    private int SomeValue
    {
        get => Fragment.Parameters.GetValueOrDefault("some_param", 0);
        set
        {
            Fragment.Parameters.SetValue("some_param", value);
            OnChanged.InvokeAsync();
        }
    }
}
```

**Code-behind (.cs):**
```csharp
using BlazorWebApp.Attributes;
using Microsoft.AspNetCore.Components;

namespace BlazorWebApp.Components.Shared.Generation.Fragments;

[FragmentComponent("ComponentName")]
public partial class ComponentName
{
    // Additional logic if needed
}
```

---

## **Next Steps**

1. **Start with VideoSourceForm** (highest priority for video workflows)
2. **Test with img2vid workflow**
3. **Add WanContextOptionsForm and SteadyDancerForm**
4. **Test with pose2vid workflow**
5. **Optional: Add FrameInterpolationForm**
6. **Optional: Enhance GeneratedVideoTabs**

---

## **Resources**

- [Fragment Conventions](./FRAGMENT_CONVENTIONS.md)
- [Component Registry](../Services/ComponentRegistry.cs)
- [FragmentRenderer](../Components/Shared/Generation/FragmentRenderer.razor)
- [Existing Fragment Components](../Components/Shared/Generation/Fragments/)
- [GeneratedVideoTabs](../Components/Img2Vid/GeneratedVideoTabs.razor)

---

## **Notes**

- **Video upload** will need proper handling (likely base64 encoding similar to images)
- **Frame preview** would be nice-to-have but complex (requires video parsing)
- **RIFE models** list should match available models in ComfyUI
- **Context options** affect long video generation - expose with good defaults
- **Pose strength** is specific to SteadyDancer - only show for that workflow

**Estimated Time:** 4-6 hours for all components + testing
