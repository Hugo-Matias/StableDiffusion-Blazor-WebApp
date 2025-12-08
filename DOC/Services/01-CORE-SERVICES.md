# Service Layer Documentation

## Overview

The service layer implements all business logic and external integrations for Blazor Diffusion. Services are registered with dependency injection and follow the Single Responsibility Principle (mostly).

## Service Registry

### Singleton Services
Services that maintain state across the application lifetime:

| Service | Purpose | Key Responsibilities |
|---------|---------|---------------------|
| ManagerService | Central state management | Application state, events, coordination |
| ImageService | Image generation orchestration | Generation workflow, image processing |
| DatabaseService | Data access | Entity operations, queries |
| IOService | File system operations | File I/O, path management |
| CsvService | CSV file handling | Import/export CSV data |
| ProgressService | Progress tracking | Real-time progress updates |
| ResourcesService | Resource management | Model file management |
| RouterService | API routing | Backend selection (WebUI/ComfyUI) |
| WorkflowService | Workflow management | Template processing, workflow parsing |
| DynamicPromptsService | Dynamic prompt processing | Python script integration |
| CacheService | Caching | Tag usage cache, performance |
| ThemeService | UI theming | Dark/light mode, customization |
| ComfyUIWebsocketService | WebSocket connection | Real-time ComfyUI communication |
| ComfyUIEventBus | Event aggregation | WebSocket event distribution |

### Scoped Services
Services created per user/circuit:

| Service | Purpose | Lifetime Reason |
|---------|---------|----------------|
| IAssetResolverService | Workflow asset resolution | User-specific workflow context |
| AssetResolverService | Asset resolver implementation | Scoped to workflow session |
| JavascriptService | JS interop | Per-user JS runtime |
| OllamaService | LLM integration | Per-request scope |

### Transient Services
Services created per request:

| Service | Purpose | Lifetime Reason |
|---------|---------|----------------|
| MagickService | Image manipulation | Stateless processing |

### HttpClient Services
Services with managed HttpClient instances:

| Service | Base URL | Purpose |
|---------|----------|---------|
| SDAPIService | http://localhost:7860/ | Automatic1111 WebUI API |
| ComfyUIService | http://localhost:8188/ | ComfyUI API |
| CivitaiService | https://civitai.com/api/ | CivitAI resource API |
| DanbooruService | https://danbooru.donmai.us/ | Tag dataset API |

## Service Details

### 1. ManagerService

**File**: `Services/ManagerService.cs` (1803 lines)  
**Lifetime**: Singleton  
**Role**: God Object (⚠️ Architectural smell)

#### Purpose
Central coordination point for the entire application. Manages state, parameters, events, and cross-cutting concerns.

#### Key Properties

**State Management**:
- `AppState State`: Current application state (project, filters, UI state)
- `AppSettings Settings`: User preferences and configuration
- `Options Options`: SD WebUI options

**Generation Parameters**:
- `Txt2ImgParameters ParametersTxt2Img`: Text-to-image settings
- `Img2ImgParameters ParametersImg2Img`: Image-to-image settings
- `UpscaleParameters ParametersUpscale`: Upscaling settings
- `Img2VidParameters ParametersImg2Vid`: Image-to-video settings

**Generation Results**:
- `GeneratedImages Images`: Latest image generation results
- `GeneratedImagesInfo ImagesInfo`: Metadata about generated images
- `InferenceProgress Progress`: Real-time generation progress

**Models and Resources**:
- `List<SDModel> CheckpointModels`: Available checkpoint models
- `List<SDModel> DiffusionModels`: Diffusion models (ComfyUI)
- `List<Sampler> Samplers`: Available sampling methods
- `List<Scheduler> Schedulers`: Scheduling algorithms
- `List<PromptStyle> Styles`: Prompt style presets
- `List<Upscaler> Upscalers`: Upscaler models

**Project Management**:
- `List<Folder> Folders`: Project folders
- `List<Project> Projects`: Available projects
- `List<int> SelectedImageIds`: Currently selected images

#### Events

The service exposes 25+ events for component synchronization:

```csharp
public event Action OnSDModelsChange;           // Model list changed
public event Action OnOptionsChange;            // Options updated
public event Action OnStyleChange;              // Styles changed
public event Action OnConverging;               // Generation started
public event Action OnFolderChange;             // Folders updated
public event Action OnProjectsChange;           // Projects list changed
public event Action OnProjectChange;            // Active project changed
public event Func<Task> OnProjectChangeTask;    // Async project change
public event Action OnStateHasChanged;          // General state change
public event Action OnProgressChanged;          // Progress update
public event Action OnDownloadCompleted;        // Resource downloaded
public event Action OnWebuiStateChanged;        // WebUI connection status
public event Action OnComfyUIStateChanged;      // ComfyUI connection status
public event Action OnAppStateChanged;          // App state changed
public event Action OnTxt2ImgParametersChanged; // Txt2Img params changed
public event Action OnImg2ImgParametersChanged; // Img2Img params changed
public event Action OnUpscaleParametersChanged; // Upscale params changed
public event Action OnImg2VidParametersChanged; // Img2Vid params changed
public event Action OnSelectedImagesChanged;    // Selection changed
public event Action OnRefreshImagesContainer;   // Gallery refresh
public event Action OnCanvasImageDataChanged;   // Canvas updated
public event Action OnImg2VidInputImageChanged; // Input image changed
public event Action OnImg2ImgInputImageChanged; // Input image changed
public event Action OnResourcesStateChanged;    // Resources changed
public event Action OnWorkflowBaseChanged;      // Workflow base changed
public event Action OnCurrentWorkflowChanged;   // Current workflow changed
public event Func<Workflow?, Task>? OnCurrentWorkflowChangedAsync; // Async workflow change
```

#### Key Methods

**Initialization**:
```csharp
public async Task Init()
```
Initializes application state, loads settings, refreshes models and styles.

**Model Management**:
```csharp
public async Task RefreshCheckpointModels()
public async Task RefreshDiffusionModels()
public async Task RefreshSamplers()
public async Task RefreshSchedulers()
public async Task RefreshUpscalers()
public async Task RefreshStyles()
```

**Options Management**:
```csharp
public async Task GetOptions()
public async Task SetOptions(Options options)
public void SetModel(string modelName, ModeType mode)
```

**Project Management**:
```csharp
public async Task SetCurrentProject(int projectId)
public async Task RefreshProjects()
public async Task RefreshFolders()
```

**State Management**:
```csharp
public async Task LoadState()
public async Task SaveState()
public async Task LoadSettings()
public async Task SaveSettings()
```

**Parameter Serialization**:
```csharp
public void SerializeInfo()
public void DeserializeInfo(ImageInfo info)
```

**Canvas Management**:
```csharp
public void SetCanvasImageData(string data)
public void SetImg2ImgInputImage(string data)
public void SetImg2VidInputImage(string data)
```

**Workflow Management**:
```csharp
public void SetCurrentWorkflow(Workflow? workflow)
public string? GetCurrentModel(ModeType mode)
```

#### Dependencies
- SDAPIService: WebUI API communication
- DatabaseService: Data persistence
- IOService: File operations
- ProgressService: Progress tracking
- IConfiguration: Application configuration
- ComfyUIService: ComfyUI API communication
- WorkflowService: Workflow processing

#### Architectural Issues
1. **Too many responsibilities** (violates SRP)
2. **High complexity** (1803 lines)
3. **Tight coupling** (25+ events)
4. **Difficult to test**
5. **Memory retention** (singleton with events)

#### Recommended Refactoring
Split into specialized managers:
- ProjectManager
- ParameterManager
- ModelManager
- StateManager
- EventCoordinator

---

### 2. DatabaseService

**File**: `Services/DatabaseService.cs` (988 lines)  
**Lifetime**: Singleton  
**Role**: Data Access Layer

#### Purpose
Provides centralized access to the SQLite database through Entity Framework Core. Implements repository-like patterns for all entities.

#### Key Responsibilities
1. Database initialization and migrations
2. CRUD operations for all entities
3. Complex queries and filtering
4. Data pagination
5. Relationship management

#### Key Methods

**Initialization**:
```csharp
public async Task InitializeDatabase()
```
Ensures database exists and applies migrations.

**Folder Operations**:
```csharp
public async Task<List<Folder>> GetFolders()
public async Task<Folder> GetFolder(int id)
public async Task<Folder> GetFolder(string name)
public async Task AddFolder(Folder folder)
public async Task UpdateFolder(Folder folder)
public async Task DeleteFolder(int id)
```

**Project Operations**:
```csharp
public async Task<List<Project>> GetProjects(int folderId = 0)
public async Task<Project> GetProject(int id)
public async Task<Project> GetProject(string name)
public async Task<Project> GetLatestProject()
public async Task<List<Project>?> GetLastUsedProjects(int amount, int[] ignoreIds)
public async Task AddProject(Project project)
public async Task UpdateProject(Project project)
public async Task DeleteProject(int id)
```

**Image Operations**:
```csharp
public async Task<ImagesDto> GetImages(ImageSearchOptions options)
public async Task<Image> GetImage(int id)
public async Task<List<Image>> GetImagesByIds(List<int> ids)
public async Task AddImage(Image image)
public async Task UpdateImage(Image image)
public async Task DeleteImages(List<int> ids)
public async Task SetImageAsCover(int imageId, int projectId)
```

**Tag Operations**:
```csharp
public async Task<List<Tag>> GetTags()
public async Task<Tag> GetTag(int id)
public async Task<List<Tag>> SearchTags(string query)
public async Task IncrementTagUsage(string tagName)
public async Task AddTag(Tag tag)
public async Task UpdateTag(Tag tag)
public async Task DeleteTag(int id)
```

**Resource Operations**:
```csharp
public async Task<List<LocalResource>> GetLocalResources(string type)
public async Task<LocalResource> GetLocalResource(int id)
public async Task AddLocalResource(LocalResource resource)
public async Task UpdateLocalResource(LocalResource resource)
public async Task DeleteLocalResource(int id)
```

**Wildcard Operations**:
```csharp
public async Task<List<Wildcard>> GetWildcards()
public async Task<Wildcard> GetWildcard(int id)
public async Task AddWildcard(Wildcard wildcard)
public async Task UpdateWildcard(Wildcard wildcard)
public async Task DeleteWildcard(int id)
```

**Prompt Operations**:
```csharp
public async Task<List<PromptResource>> GetPromptResources()
public async Task<PromptResource> GetPromptResource(int id)
public async Task AddPromptResource(PromptResource prompt)
public async Task UpdatePromptResource(PromptResource prompt)
public async Task DeletePromptResource(int id)
```

**Mode Operations**:
```csharp
public void PopulateModes()
public async Task<List<Mode>> GetModes()
```

**Sampler Operations**:
```csharp
public void PopulateSamplers()
public async Task<List<Sampler>> GetSamplers()
```

#### Pagination Support
```csharp
public int PageSize { get; set; }  // Default: 5
```

Images and other collections support pagination through skip/take patterns.

#### Dependencies
- IDbContextFactory<AppDbContext>: Database context factory
- SDAPIService: Model data from WebUI
- ComfyUIService: Model data from ComfyUI
- IConfiguration: Configuration access
- ILogger<DatabaseService>: Logging

#### Best Practices
✅ **Good**:
- Uses `CreateDbContextAsync()` for context creation
- Proper async/await usage
- LINQ query optimization
- Projection to reduce data transfer

⚠️ **Areas for Improvement**:
- No Unit of Work pattern
- No transaction management in some operations
- Could benefit from specification pattern for complex queries
- Missing cancellation token support

---

### 3. ImageService

**File**: `Services/ImageService.cs` (606 lines)  
**Lifetime**: Singleton  
**Role**: Image Generation Orchestrator

#### Purpose
Coordinates the entire image generation workflow from parameter preparation through API calls to result processing and storage.

#### Key Responsibilities
1. Build generation parameters from user input
2. Route requests to appropriate backend (WebUI/ComfyUI)
3. Process generation results
4. Save images and metadata to disk
5. Store image records in database
6. Track generation progress

#### Key Methods

**Generation**:
```csharp
public async Task<ImagesDto> GetImages(ModeType mode)
```
Main generation method supporting Txt2Img, Img2Img, and Extras modes.

**Video Generation**:
```csharp
public async Task<GeneratedVideos> GetVideo()
```
Generates videos from images using ComfyUI workflows.

**Parameter Building**:
```csharp
private Txt2ImgWebUI BuildTxt2ImgParameters(ref string scriptName)
private Img2ImgWebUI BuildImg2ImgParameters(ref string scriptName)
```
Constructs API request objects from application parameters.

**Image Saving**:
```csharp
private async Task<ImagesDto> SaveImages(Outdir samplesDir, Outdir? gridDir, string scriptName)
private async Task<ImagesDto> SaveUpscaleImage()
private async Task<ImagesDto> SaveVideos()
```

**Progress Tracking**:
```csharp
public async Task TrackProgress()
public void StopTracking()
```
Monitors generation progress via API polling.

**Canvas Operations**:
```csharp
public void SetCanvasSourceSize(int width, int height)
```

#### Event
```csharp
public event Action OnChange;
```

#### Dependencies
- SDAPIService: WebUI communication
- IOService: File operations
- ManagerService: State and parameters
- MagickService: Image processing
- DatabaseService: Data persistence
- ProgressService: Progress updates
- RouterService: Backend routing

#### Workflow

1. **Parameter Preparation**:
   - Read parameters from ManagerService
   - Apply styles and prompts
   - Build extension parameters (ControlNet, ADetailer, etc.)

2. **API Call**:
   - Route through RouterService
   - Call appropriate backend API
   - Handle interruptions

3. **Result Processing**:
   - Parse response
   - Extract images and metadata
   - Get image dimensions

4. **Storage**:
   - Save images to configured output directory
   - Save metadata txt files
   - Create database records
   - Update project associations

5. **Notification**:
   - Fire OnChange event
   - Update ManagerService state

#### Error Handling
```csharp
catch (Exception e)
{
    await Console.Out.WriteLineAsync(e.ToString());  // ⚠️ Should use ILogger
}
```

---

### 4. SDAPIService

**File**: `Services/SDAPIService.cs` (171 lines)  
**Lifetime**: HttpClient (Singleton)  
**Role**: WebUI API Client

#### Purpose
Thin wrapper around Automatic1111's Stable Diffusion WebUI API.

#### Configuration
```csharp
BaseAddress: http://localhost:7860/
Timeout: 1 day
Route prefix: sdapi/v1/
```

#### Key Methods

**Model Discovery**:
```csharp
public async Task<List<SDModel>> GetSDModels()
public async Task<List<Sampler>> GetSamplers()
public async Task<List<Scheduler>> GetSchedulers()
public async Task<List<PromptStyle>> GetStyles()
public async Task<List<Upscaler>> GetUpscalers()
```

**Options**:
```csharp
public async Task<Options> GetOptions()
public async Task<string> PostOptions(Options options)
public async Task<CmdFlags> GetCmdFlags()
```

**Model Management**:
```csharp
public async Task<string> PostReloadModel()
public async Task<string> PostRefreshModels()
```

**Generation**:
```csharp
public async Task<GeneratedImages> PostTxt2Img(Txt2ImgWebUI param)
public async Task<GeneratedImages> PostImg2Img(Img2ImgWebUI param)
public async Task<UpscaledImageDto> PostExtraSingle(UpscaleParameters param)
```

**Control**:
```csharp
public async Task<string> PostInterrupt()
public async Task<string> PostSkip()
public async Task<InferenceProgress> GetProgress()
```

**Dynamic Prompts**:
```csharp
public async Task<List<string>> GeneratePrompts(string dynamicPromptsTitle, string prompt, int amount, ScriptParametersDynamicPrompts scriptParam)
```

**ControlNet Extension**:
```csharp
public async Task<List<string>> GetControlNetModels()
public async Task<List<string>> GetControlNetPreprocessors()
```

**Health Check**:
```csharp
public async Task<bool> CheckWebuiState()
```

#### JSON Serialization
```csharp
private readonly JsonSerializerOptions _jsonIgnoreNull = new() 
{ 
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull 
};
```

#### Debugging
Writes payload.json for each generation request (useful for troubleshooting).

---

### 5. ComfyUIService

**File**: `Services/ComfyUIService.cs` (801 lines)  
**Lifetime**: HttpClient (Singleton)  
**Role**: ComfyUI API Client

#### Purpose
Manages communication with ComfyUI including workflow execution, progress tracking, and result retrieval.

#### Configuration
```csharp
BaseAddress: http://localhost:8188/
Timeout: 1 day
```

#### Key Features

**Workflow Execution**:
```csharp
public async Task<GeneratedImages> ExecuteWorkflow(Workflow workflow, Dictionary<string, object> parameters)
public async Task<GeneratedVideos> ExecuteVideoWorkflow(Workflow workflow, Dictionary<string, object> parameters)
public async Task<LLMResponse> ExecuteLLMWorkflow(Workflow workflow, Dictionary<string, object> parameters)
```

**Image Upload**:
```csharp
private async Task<string> UploadImage(string imageBase64, string filename)
```
Uploads images for img2img workflows with hash-based caching.

**Result Retrieval**:
```csharp
private async Task<List<string>> GetFilenameFromHistory(Guid promptId)
private async Task<string> GetTextFromHistory(Guid promptId)
```

**Progress Tracking**:
Through ComfyUIWebsocketService and ComfyUIEventBus.

**Cleanup**:
```csharp
private async Task CleanupUploadedImagesAsync(Guid promptId)
```
Removes temporary uploaded images.

#### Workflow Processing
1. Parse Scriban template
2. Inject parameters
3. Upload input images if needed
4. Queue prompt
5. Wait for completion via WebSocket
6. Retrieve results
7. Cleanup

#### Model Discovery**:
```csharp
public async Task<List<string>> GetCheckpoints()
public async Task<List<string>> GetVAEs()
public async Task<List<string>> GetLoras()
public async Task<List<string>> GetUpscalers()
public async Task<List<string>> GetSamplers()
public async Task<List<string>> GetSchedulers()
public async Task<List<string>> GetClipModels()
public async Task<List<string>> GetClipVisionModels()
public async Task<List<string>> GetADetailerModels()
```

#### Event-Driven Architecture
Uses TaskCompletionSource for async workflow completion:

```csharp
private readonly ConcurrentDictionary<Guid, object> _pendingJobs;
```

Handles events from ComfyUIEventBus:
- ExecutionSucceeded
- ExecutionFailed

---

_Continued in next section..._
