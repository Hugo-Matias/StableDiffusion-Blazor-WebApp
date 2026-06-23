# Data Models Documentation

## Overview

The application uses a combination of entity models (database), DTOs (data transfer), and parameter models (configuration) to manage data flow.

## Model Categories

### 1. Entity Models (Database)
Located in `Data/Entities/`

### 2. DTOs (Data Transfer Objects)
Located in `Data/Dtos/`

### 3. Parameter Models
Located in `Models/`

### 4. Enums
Located in `Data/Enums.cs`

---

## Core Entity Models

### Project
```csharp
public class Project
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int? FolderId { get; set; }
    public Folder? Folder { get; set; }
    public string? CoverImage { get; set; }
    public DateTime CreationTime { get; set; }
    public List<Image> Images { get; set; }
}
```

**Purpose**: Organizational unit for generated images  
**Relationships**: Belongs to Folder, has many Images

### Folder
```csharp
public class Folder
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int SortOrder { get; set; }
    public List<Project> Projects { get; set; }
}
```

**Purpose**: Higher-level categorization of projects  
**Relationships**: Has many Projects

### Image
```csharp
public class Image
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; }
    public string Filename { get; set; }
    public string Path { get; set; }
    public string? ThumbnailPath { get; set; }
    public string? Prompt { get; set; }
    public string? NegativePrompt { get; set; }
    public string? Seed { get; set; }
    public string? Model { get; set; }
    public string? Sampler { get; set; }
    public string? Scheduler { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? Steps { get; set; }
    public double? CfgScale { get; set; }
    public DateTime CreationTime { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsCover { get; set; }
    public string? Metadata { get; set; }  // Full JSON
}
```

**Purpose**: Generated image record with full metadata  
**Relationships**: Belongs to Project

### LocalResource
```csharp
public class LocalResource
{
    public int Id { get; set; }
    public int TypeId { get; set; }
    public ResourceType Type { get; set; }
    public int? SubTypeId { get; set; }
    public ResourceSubType? SubType { get; set; }
    public string Title { get; set; }
    public string? Description { get; set; }
    public int? ModelId { get; set; }  // CivitAI ID
    public int? VersionId { get; set; }  // CivitAI version ID
    public List<LocalResourceFile> Files { get; set; }
    public string? PreviewImage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

**Purpose**: Local model file tracking with CivitAI linkage  
**Relationships**: Has ResourceType, optional SubType, has many Files

### LocalResourceFile
```csharp
public class LocalResourceFile
{
    public int Id { get; set; }
    public int LocalResourceId { get; set; }
    public LocalResource LocalResource { get; set; }
    public string Filename { get; set; }
    public string Path { get; set; }
    public string? Hash { get; set; }  // SHA256
    public long SizeBytes { get; set; }
    public DateTime AddedAt { get; set; }
}
```

**Purpose**: Individual file within a resource (for multi-file models)

### Tag
```csharp
public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Category { get; set; }
    public int UsageCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUsed { get; set; }
}
```

**Purpose**: User-defined tags with usage tracking

### PromptResource
```csharp
public class PromptResource
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Content { get; set; }
    public string? Category { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

**Purpose**: Saved prompt templates/presets

### Wildcard
```csharp
public class Wildcard
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Content { get; set; }  // Newline-separated values
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

**Purpose**: Wildcard definitions for dynamic prompts

### Mode
```csharp
public class Mode
{
    public int Id { get; set; }
    public string Name { get; set; }
    public ModeType Type { get; set; }
}
```

**Purpose**: Generation mode presets

### Sampler
```csharp
public class Sampler
{
    public int Id { get; set; }
    public string Name { get; set; }
}
```

**Purpose**: Available sampling methods

---

## Parameter Models

### Txt2ImgParameters
```csharp
public class Txt2ImgParameters : SharedParameters
{
    public bool EnableHr { get; set; }
    public string? HrUpscaler { get; set; }
    public double HrScale { get; set; }
    public int HrSecondPassSteps { get; set; }
    public double DenoisingStrength { get; set; }
    // Inherits: Prompt, NegativePrompt, Width, Height, Steps, etc.
}
```

**Purpose**: Text-to-image generation parameters

### Img2ImgParameters
```csharp
public class Img2ImgParameters : SharedParameters
{
    public List<string> InitImages { get; set; }
    public double DenoisingStrength { get; set; }
    public string? Mask { get; set; }
    public int MaskBlur { get; set; }
    public int InpaintingFill { get; set; }
    public bool InpaintFullRes { get; set; }
    public int InpaintFullResPadding { get; set; }
    // Inherits from SharedParameters
}
```

**Purpose**: Image-to-image generation parameters

### UpscaleParameters
```csharp
public class UpscaleParameters
{
    public string Image { get; set; }
    public int UpscalingResize { get; set; }
    public string? UpscalerMethod1 { get; set; }
    public string? UpscalerMethod2 { get; set; }
    public double? Upscaler2Visibility { get; set; }
    public double? GfpganVisibility { get; set; }
    public double? CodeformerVisibility { get; set; }
    public double? CodeformerWeight { get; set; }
}
```

**Purpose**: Image upscaling parameters

### Img2VidParameters
```csharp
public class Img2VidParameters
{
    public string InputImage { get; set; }
    public int FrameCount { get; set; }
    public int Fps { get; set; }
    public double MotionStrength { get; set; }
    public long Seed { get; set; }
    public string? Checkpoint { get; set; }
    // Additional workflow-specific parameters
}
```

**Purpose**: Image-to-video generation parameters

### SharedParameters
```csharp
public class SharedParameters
{
    public string Prompt { get; set; }
    public string? NegativePrompt { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int Steps { get; set; }
    public string SamplerIndex { get; set; }
    public string? Scheduler { get; set; }
    public double CfgScale { get; set; }
    public long Seed { get; set; }
    public int BatchSize { get; set; }
    public int BatchCount { get; set; }
    public bool RestoreFaces { get; set; }
    public bool Tiling { get; set; }
    public int ClipSkip { get; set; }
    // Extension parameters
    public AlwaysonScripts? AlwaysonScripts { get; set; }
}
```

**Purpose**: Common parameters across generation modes

---

## State Models

### AppState
```csharp
public class AppState
{
    public GenerationState Generation { get; set; }
    public GalleryState Gallery { get; set; }
    public ResourcesState Resources { get; set; }
    public PromptsState Prompts { get; set; }
}
```

**Purpose**: Application-wide state container

### GenerationState
```csharp
public class GenerationState
{
    public bool IsGenerating { get; set; }
    public bool IsInterrupted { get; set; }
    public long Seed { get; set; }
    public ModeType CurrentMode { get; set; }
    public bool UseComfyUI { get; set; }
    public Guid? CurrentWorkflowId { get; set; }
}
```

**Purpose**: Generation workflow state

### GalleryState
```csharp
public class GalleryState
{
    public int FolderId { get; set; }
    public int ProjectId { get; set; }
    public int PageSize { get; set; }
    public bool UseInfiniteScroll { get; set; }
    public DateTime? FilterDateFrom { get; set; }
    public DateTime? FilterDateTo { get; set; }
    public string? FilterModel { get; set; }
    public bool FilterFavoritesOnly { get; set; }
}
```

**Purpose**: Gallery view and filter state

---

## DTOs (External APIs)

### CivitAI DTOs

**CivitaiModelDto**:
```csharp
public class CivitaiModelDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public CivitaiModelType Type { get; set; }
    public bool Nsfw { get; set; }
    public List<string> Tags { get; set; }
    public CivitaiCreatorDto Creator { get; set; }
    public List<CivitaiModelVersionDto> ModelVersions { get; set; }
}
```

**CivitaiModelVersionDto**:
```csharp
public class CivitaiModelVersionDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public string DownloadUrl { get; set; }
    public List<CivitaiFile> Files { get; set; }
    public List<CivitaiImageDto> Images { get; set; }
}
```

### SD WebUI DTOs

**GeneratedImages**:
```csharp
public class GeneratedImages
{
    public List<string> Images { get; set; }  // Base64
    public GeneratedImagesInfo Info { get; set; }
    public string Parameters { get; set; }  // JSON string
}
```

**InferenceProgress**:
```csharp
public class InferenceProgress
{
    public double Progress { get; set; }
    public double EtaRelative { get; set; }
    public string CurrentImage { get; set; }  // Base64
    public string TextInfo { get; set; }
}
```

### ComfyUI DTOs

**ComfyUIWorkflowDto**:
```csharp
public class ComfyUIWorkflowDto
{
    public Dictionary<string, ComfyUINode> Nodes { get; set; }
}
```

**ComfyUINode**:
```csharp
public class ComfyUINode
{
    public string ClassType { get; set; }
    public Dictionary<string, object> Inputs { get; set; }
}
```

---

## Workflow Models

### Workflow
```csharp
public class Workflow
{
    public Guid Id { get; set; }
    public string Title { get; set; }
    public ModelBase Base { get; set; }
    public ModeType Mode { get; set; }
    public string RawJson { get; set; }  // Scriban template
    public List<WorkflowAsset>? Assets { get; set; }
    public object? Pipeline { get; set; }
}
```

**Purpose**: Workflow template definition

### WorkflowAsset
```csharp
public class WorkflowAsset
{
    public string Parameter { get; set; }
    public string Label { get; set; }
    public AssetType Type { get; set; }
    public string? DefaultValue { get; set; }
    public int Order { get; set; }
    public int ColumnSize { get; set; }
}
```

**Purpose**: Required asset definition for workflow

---

## Enums

### ModeType
```csharp
public enum ModeType
{
    Txt2Img,
    Img2Img,
    Extras,  // Upscale
    Img2Vid
}
```

### ModelBase
```csharp
public enum ModelBase
{
    SD15,    // Stable Diffusion 1.5
    SDXL,    // Stable Diffusion XL
    SD3,     // Stable Diffusion 3
    Flux     // Flux models
}
```

### AssetType
```csharp
public enum AssetType
{
    Checkpoint,
    Lora,
    VAE,
    Upscaler,
    ControlNet,
    Embedding,
    Clip,
    ClipVision,
    ADetailer
}
```

### CivitaiModelType
```csharp
public enum CivitaiModelType
{
    Checkpoint,
    Lora,
    TextualInversion,
    Hypernetwork,
    AestheticGradient,
    VAE,
    Controlnet,
    Poses,
    Wildcards,
    Other
}
```

### ResourceType
Similar to AssetType but for database storage.

---

## Extension Parameter Models

### AlwaysonScripts
```csharp
public class AlwaysonScripts
{
    public ControlNetExtension? ControlNet { get; set; }
    public ADetailerExtension? ADetailer { get; set; }
    public DynamicPromptsExtension? DynamicPrompts { get; set; }
    public MultiDiffusionExtension? MultiDiffusion { get; set; }
    // ... other extensions
}
```

### ControlNetExtension
```csharp
public class ControlNetExtension
{
    public List<ControlNetUnit> Args { get; set; }
}

public class ControlNetUnit
{
    public string InputImage { get; set; }
    public string Module { get; set; }  // Preprocessor
    public string Model { get; set; }
    public double Weight { get; set; }
    public string ControlMode { get; set; }
    public bool Enabled { get; set; }
}
```

### ADetailerExtension
```csharp
public class ADetailerExtension
{
    public List<ADetailerModel> Args { get; set; }
}

public class ADetailerModel
{
    public bool AdEnabled { get; set; }
    public string AdModel { get; set; }
    public string AdPrompt { get; set; }
    public double AdConfidence { get; set; }
    public double AdDilateErode { get; set; }
    public double AdDenoisingStrength { get; set; }
}
```

---

## Model Validation

Key models implement validation:

```csharp
// Example validation attributes
public class Txt2ImgParameters
{
    [Range(64, 2048)]
    public int Width { get; set; }
    
    [Range(64, 2048)]
    public int Height { get; set; }
    
    [Range(1, 150)]
    public int Steps { get; set; }
    
    [Range(1.0, 30.0)]
    public double CfgScale { get; set; }
}
```

---

## Model Relationships Diagram

```
Folder 1──→ * Project 1──→ * Image

LocalResource 1──→ * LocalResourceFile
    ↓
    ResourceType
    ↓
    ResourceSubType (optional)

Workflow 1──→ * WorkflowAsset

AppState
    ├─→ GenerationState
    ├─→ GalleryState
    ├─→ ResourcesState
    └─→ PromptsState
```

---

## JSON Serialization

Models use System.Text.Json with options:
```csharp
JsonSerializerOptions
{
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true  // For debugging
}
```

Special converters in `Data/Converters/` handle complex types.

---

## Database Schema

SQLite database created via Entity Framework Code First:

**Tables**:
- Folders
- Projects
- Images
- ResourceTypes
- ResourceSubTypes
- LocalResources
- LocalResourceFiles
- Tags
- PromptResources
- Wildcards
- Modes
- Samplers

**Indexes**:
- Images.ProjectId
- Images.CreationTime
- Images.IsFavorite
- LocalResources.TypeId
- LocalResourceFiles.Hash

**Migrations**:
Located in `Migrations/` directory

---

This completes the data models documentation covering entities, DTOs, parameters, state, and enums used throughout the application.
