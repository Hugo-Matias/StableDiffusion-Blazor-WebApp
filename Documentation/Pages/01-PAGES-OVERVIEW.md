# Frontend Pages Documentation

## Overview

Blazor Diffusion uses Blazor Server with MudBlazor component library. Pages are organized by functionality with a focus on image generation workflows.

## Page Structure

### Navigation Routes

| Route | Page | Purpose |
|-------|------|---------|
| `/` | Index.razor | Project gallery and image browsing |
| `/webui/txt2img` | Txt2ImgWebUI.razor | Text-to-image generation (WebUI) |
| `/webui/img2img` | Img2ImgWebUI.razor | Image-to-image generation (WebUI) |
| `/webui/upscale` | UpscaleWebUI.razor | Image upscaling (WebUI) |
| `/comfyui/txt2img` | Txt2ImgComfyUI.razor | Text-to-image generation (ComfyUI) |
| `/comfyui/img2img` | Img2ImgComfyUI.razor | Image-to-image generation (ComfyUI) |
| `/comfyui/img2vid` | Img2VidComfyUI.razor | Image-to-video generation (ComfyUI) |
| `/resources` | Resources.razor | Model and resource management |
| `/prompts` | Prompts.razor | Prompt templates and wildcards |
| `/danbooru` | Danbooru.razor | Tag dataset browsing |
| `/settings` | Settings.razor | Application settings |

## Page Details

### 1. Index (Gallery) 📸

**File**: `Pages/Index.razor`  
**Route**: `/`

#### Purpose
Main application page displaying the project-based image gallery.

#### Key Features

**Project Selection**:
- Folder/project hierarchy
- Quick project switching
- Recent projects
- Project creation

**Image Display**:
- Two modes:
  1. **Pagination**: Standard page-based navigation
  2. **Infinite Scroll**: Continuous loading with masonry layout

**Filtering**:
- Date range
- Model used
- Favorite status
- Tags
- Resolution
- Sampler/scheduler

**Image Operations**:
- View full details
- Set as project cover
- Delete
- Multi-select for batch operations
- Copy parameters to generation

**Gallery Settings**:
- Page size configuration
- Navigation mode toggle
- Filter presets
- Sort options

#### Components Used
- `GallerySettings`: Filter and navigation controls
- `ImagesContainer`: Paginated image grid
- `InfiniteScrollMasonry`: Infinite scroll layout
- `CreateProjectButton`: Quick project creation

#### State Management
```csharp
M.State.Gallery.FolderId       // Current folder
M.State.Gallery.ProjectId      // Current project
M.State.Gallery.PageSize       // Images per page
M.State.Gallery.UseInfiniteScroll // Navigation mode
```

#### User Flow
```
1. User selects folder/project
   ↓
2. Images load with filters applied
   ↓
3. User can:
   - Browse images (pagination/infinite scroll)
   - Filter by criteria
   - Select images
   - View details
   - Perform batch operations
   - Create new projects
```

---

### 2. Txt2Img (Text-to-Image) 🎨

**Files**: 
- `Pages/WebUI/Txt2ImgWebUI.razor` (WebUI backend)
- `Pages/ComfyUI/Txt2ImgComfyUI.razor` (ComfyUI backend)

**Routes**: `/webui/txt2img`, `/comfyui/txt2img`

#### Purpose
Generate images from text prompts.

#### Key Features

**Prompt Input**:
- Main prompt field with autocomplete
- Negative prompt field
- Tag drawer for quick prompt building
- Prompt styles (presets)
- Dynamic prompts integration
- LLM prompt enhancement (Ollama)

**Basic Parameters**:
- Width/Height (resolution)
- Batch size/count
- Steps (inference steps)
- CFG Scale (guidance)
- Sampler selection
- Scheduler selection
- Seed control
- Clip skip

**Model Selection**:
- Checkpoint model
- VAE override
- LoRA loading with weights
- Textual Inversion embeddings

**Advanced Extensions** (WebUI):
- **ControlNet**: Image conditioning
- **ADetailer**: Automatic face/detail refinement
- **Dynamic Prompts**: Template-based generation
- **MultiDiffusion**: High-resolution tiling
- **Regional Prompter**: Area-specific prompts
- **Cutoff**: Token isolation
- **XYZ Plot**: Parameter grid exploration

**Workflow Selection** (ComfyUI):
- Workflow template picker
- Asset selector (models, LoRAs, etc.)
- Workflow-specific parameters

#### Components Used
- `PromptFields`: Prompt input with tag drawer
- `GenerateFormTxt2Img`: Parameter form
- `GeneratedImageTabs`: Result display
- `LoraForm`: LoRA management
- `ControlNetForm`: ControlNet configuration
- `TagDrawer`: Tag-based prompt building

#### User Flow
```
1. Enter prompt (or use tag drawer)
   ↓
2. Configure parameters
   ↓
3. (Optional) Enable extensions
   ↓
4. (Optional) Load LoRAs/embeddings
   ↓
5. Click Generate
   ↓
6. Monitor progress
   ↓
7. View results in tabs
   ↓
8. Save to project or iterate
```

---

### 3. Img2Img (Image-to-Image) 🖼️

**Files**:
- `Pages/WebUI/Img2ImgWebUI.razor`
- `Pages/ComfyUI/Img2ImgComfyUI.razor`

**Routes**: `/webui/img2img`, `/comfyui/img2img`

#### Purpose
Modify existing images or perform inpainting/outpainting.

#### Key Features

**Image Input**:
- File upload
- Drag and drop
- Load from gallery
- Paste from clipboard

**Canvas Tools** (WebUI):
- Drawing tools for sketching
- Mask painting for inpainting
- Eraser
- Color picker
- Brush size control
- Layer system
- Zoom/pan

**Denoising Strength**:
Controls how much the image changes (0.0 = no change, 1.0 = full regeneration).

**Resize Modes**:
- Just resize
- Crop and resize
- Resize and fill
- Just resize (latent upscale)

**Inpaint Settings**:
- Inpaint masked/not masked
- Mask blur
- Inpaint at full resolution
- Padding

**All Txt2Img Features**:
Plus denoising and image input.

#### Canvas Implementation
Uses HTML Canvas with JSInterop for:
- Real-time drawing
- Layer compositing
- Mask creation
- Image manipulation

#### User Flow
```
1. Upload or select input image
   ↓
2. (Optional) Draw/mask on canvas
   ↓
3. Enter prompt for desired changes
   ↓
4. Set denoising strength
   ↓
5. Configure other parameters
   ↓
6. Generate
   ↓
7. Review results
   ↓
8. Iterate or save
```

---

### 4. Img2Vid (Image-to-Video) 🎬

**File**: `Pages/ComfyUI/Img2VidComfyUI.razor`  
**Route**: `/comfyui/img2vid`

#### Purpose
Generate videos from static images using ComfyUI workflows.

#### Key Features

**Input Image**:
- Upload image
- Select from gallery

**Video Parameters**:
- Frame count
- FPS (frames per second)
- Motion strength
- Interpolation settings

**Workflow-Based**:
Uses ComfyUI workflow templates for video generation pipelines.

#### User Flow
```
1. Select input image
   ↓
2. Choose video workflow
   ↓
3. Configure video parameters
   ↓
4. Generate video
   ↓
5. Preview result
   ↓
6. Download or save to project
```

---

### 5. Upscale 🔍

**File**: `Pages/WebUI/UpscaleWebUI.razor`  
**Route**: `/webui/upscale`

#### Purpose
Upscale images using GAN-based models.

#### Key Features

**Upscaler Selection**:
- Multiple upscaler models
- Model chaining (upscale twice)

**Scaling**:
- Scale factor (2x, 4x, etc.)
- Target width/height

**GFPGAN/CodeFormer**:
- Face restoration
- Visibility/weight control

**Extras Options**:
- Additional processing

#### User Flow
```
1. Select image to upscale
   ↓
2. Choose upscaler model
   ↓
3. Set scale factor
   ↓
4. (Optional) Enable face restoration
   ↓
5. Upscale
   ↓
6. Compare before/after
   ↓
7. Save result
```

---

### 6. Resources 📦

**File**: `Pages/Resources.razor`  
**Route**: `/resources`

#### Purpose
Browse, download, and manage AI models and resources.

#### Tabs

**1. CivitAI Tab**:
- Browse models from CivitAI
- Search by name, creator, tags
- Filter by type, NSFW level
- Preview example images
- Download models with progress tracking
- View model details and metadata

**2. Model Type Tabs**:
One tab per model type:
- Checkpoints
- LoRAs
- Embeddings (Textual Inversion)
- VAEs
- Upscalers
- Hypernetworks
- AestheticGradients

Each tab shows:
- Local model files
- Preview images
- Version information
- Load/activate model
- Edit metadata
- Delete files
- Organize into subtypes

**3. Audit Tab**:
- Scan filesystem for new models
- Match local files to CivitAI
- Update metadata
- Identify orphaned files
- Batch operations

#### Key Features

**CivitAI Integration**:
- Model search and discovery
- Example image browsing
- One-click download
- Automatic organization
- Version detection

**Local Management**:
- File scanning
- Hash-based identification
- Preview management
- Subtype organization
- Metadata editing

**Quick Actions**:
- Load model for generation
- Set as default
- Open in file explorer
- Copy hash
- View on CivitAI

#### User Flow
```
1. Browse CivitAI or local models
   ↓
2. Preview examples/details
   ↓
3. Download or select local file
   ↓
4. Organize into categories
   ↓
5. Load for use in generation
```

---

### 7. Prompts 📝

**File**: `Pages/Prompts.razor`  
**Route**: `/prompts`

#### Purpose
Manage prompt templates and wildcards for dynamic generation.

#### Tabs

**1. Presets Tab**:
- Create prompt templates
- Save commonly used prompts
- Categorize by theme/style
- Quick apply to generation
- Edit existing presets

**2. Wildcards Tab**:
- Define wildcard collections
- Random word/phrase substitution
- Nested wildcards
- Import/export
- Preview randomization

#### Features

**Prompt Templates**:
```
Template: "A {adjective} {subject} in {location}"
Wildcards:
  - adjective: [beautiful, stunning, mystical]
  - subject: [cat, dog, dragon]
  - location: [forest, city, space]
```

**Dynamic Prompts**:
- Combinatorial generation
- Magic prompt enhancement
- Template syntax support

#### User Flow
```
1. Create wildcard collections
   ↓
2. Define prompt templates
   ↓
3. Test randomization
   ↓
4. Save presets
   ↓
5. Use in generation pages
```

---

### 8. Danbooru 🏷️

**File**: `Pages/Danbooru.razor`  
**Route**: `/danbooru`

#### Purpose
Browse Danbooru tag dataset for inspiration and reference.

#### Features
- Search posts by tags
- View example images
- Explore tag relationships
- Import tags to prompt
- Study tag combinations

---

### 9. Settings ⚙️

**File**: `Pages/Settings.razor`  
**Route**: `/settings`

#### Purpose
Configure application behavior and preferences.

#### Settings Categories

**Appearance**:
- Dark/light theme
- Color scheme
- Font size
- Layout density

**Generation Defaults**:
- Default parameters per mode
- Preferred models
- Auto-save settings

**Paths**:
- Output directory
- Resource directories
- Preview locations

**API**:
- WebUI URL
- ComfyUI URL
- CivitAI token
- Connection testing

**Gallery**:
- Default page size
- Thumbnail quality
- Metadata display

**Behavior**:
- Auto-refresh
- Confirmation dialogs
- Keyboard shortcuts

---

## Component Architecture

### Shared Components

#### Generation Components
Located in `Components/Shared/Generation/`:

- `PromptFields`: Main prompt input with tag integration
- `GenerateButton`: Unified generate/stop/skip controls
- `GeneratedImageTabs`: Multi-tab result viewer
- `TagDrawer`: Tag selection UI
- `TagAccordion`: Categorized tag buttons
- `LoraForm`: LoRA management panel
- `ControlNetForm`: ControlNet configuration
- `ControlNetTabs`: Multiple ControlNet units
- `ADetailerForm`: ADetailer settings
- `DynamicPromptsForm`: Dynamic prompts config
- `MultiDiffusionTiledDiffusionForm`: Tiling settings
- `XYZPlotForm`: Parameter grid setup
- `WorkflowAssetSelector`: Model selection for workflows
- `WorkflowAssetsPanel`: Workflow asset management

#### Gallery Components
- `ImagesContainer`: Paginated image grid
- `InfiniteScrollMasonry`: Infinite scroll layout
- `GallerySettings`: Filter and navigation
- `ImageViewer`: Full-size image modal
- `ImageInfo`: Metadata display

#### Resource Components
- `CivitaiPanel`: Model browsing
- `ResourcePanel`: Local file management
- `ResourceVersionsDialog`: Version selector
- `LoadResourceDialog`: Model loading options

#### Prompt Components
Located in `Components/Prompts/`:
- `PromptsPanel`: Template management
- `WildcardsPanel`: Wildcard editor
- `PromptDialog`: Template creation

### Layout Components

**MainLayout**:
- Navigation drawer
- Top app bar
- Backend status indicators
- Progress overlay

**Navigation Menu**:
- Generation modes (WebUI/ComfyUI)
- Resources
- Prompts
- Settings

---

## State Flow Diagram

```
User Interaction (Page)
    ↓
Component Event
    ↓
ManagerService (State Update)
    ↓
Event Notification
    ↓
├─→ Update UI Components
├─→ Update Database
└─→ Update File System
    ↓
StateHasChanged
    ↓
Re-render Affected Components
```

---

This completes the pages documentation. Each page serves a specific purpose in the image generation workflow with clear user flows and feature sets.
