# Service Layer Documentation

## Overview

The service layer implements all business logic and external integrations for Blazor Diffusion. Services are registered with dependency injection and follow the Single Responsibility Principle. **All core services now use interface-based dependency injection** for improved testability and maintainability.

## Architectural Principles

- **Interface-Based DI**: All services expose interfaces (`IServiceName`) for loose coupling
- **Single Responsibility**: Each service has a focused, well-defined purpose
- **Event-Driven Communication**: Services communicate through `IEventService` for decoupling
- **Comprehensive Testing**: 280+ tests covering service functionality and integration
- **Dependency Injection**: All dependencies injected through constructors

## Service Registry

### Singleton Services
Services that maintain state across the application lifetime:

| Service | Interface | Purpose | Key Responsibilities |
|---------|-----------|---------|---------------------|
| OrchestratorService | IOrchestratorService | Service coordination | Orchestrates multi-service operations |
| ImageService | IImageService | Image generation orchestration | Generation workflow, image processing |
| DatabaseService | IDatabaseService | Data access | Entity operations, queries |
| IOService | IIOService | File system operations | File I/O, path management |
| StateService | IStateService | Application state | State persistence, parameter management |
| SettingsService | ISettingsService | Settings management | User preferences, configuration |
| EventService | IEventService | Event bus | Pub/sub event aggregation |
| ModelService | IModelService | Model management | Checkpoint/diffusion model loading |
| GalleryService | IGalleryService | Gallery operations | Project/folder/selection management |
| SessionService | ISessionService | Session state | Canvas, editor, video state |
| BackendService | IBackendService | Backend integration | ComfyUI/WebUI health checks |
| ProgressService | IProgressService | Progress tracking | Real-time progress updates |
| ResourcesService | IResourcesService | Resource management | Model file management |
| RouterService | IRouterService | API routing | Backend selection (WebUI/ComfyUI) |
| WorkflowService | IWorkflowService | Workflow management | Template processing, workflow parsing |
| CacheService | ICacheService | Caching | Tag usage cache, performance |
| CsvService | - | CSV file handling | Import/export CSV data |
| DynamicPromptsService | - | Dynamic prompt processing | Python script integration |
| ThemeService | - | UI theming | Dark/light mode, customization |
| ComfyUIWebsocketService | - | WebSocket connection | Real-time ComfyUI communication |
| ComfyUIEventBus | - | Event aggregation | WebSocket event distribution |

### Scoped Services
Services created per user/circuit:

| Service | Interface | Purpose | Lifetime Reason |
|---------|-----------|---------|----------------|
| AssetResolverService | IAssetResolverService | Workflow asset resolution | User-specific workflow context |
| JavascriptService | - | JS interop | Per-user JS runtime |
| OllamaService | - | LLM integration | Per-request scope |

### Transient Services
Services created per request:

| Service | Interface | Purpose | Lifetime Reason |
|---------|-----------|---------|----------------|
| MagickService | - | Image manipulation | Stateless processing |

### HttpClient Services
Services with managed HttpClient instances:

| Service | Base URL | Purpose |
|---------|----------|---------|
| SDAPIService | http://localhost:7860/ | Automatic1111 WebUI API |
| ComfyUIService | http://localhost:8188/ | ComfyUI API |
| CivitaiService | https://civitai.com/api/ | CivitAI resource API |
| DanbooruService | https://danbooru.donmai.us/ | Tag dataset API |

## Service Details

### 1. OrchestratorService (formerly ManagerService)

**File**: `Services/OrchestratorService.cs` (~460 lines)  
**Interface**: `IOrchestratorService`  
**Lifetime**: Singleton  
**Role**: Service Coordinator

#### Purpose
Orchestrates complex multi-service operations and coordinates between specialized services. This service was refactored from the original "God Object" ManagerService, reducing from ~1600+ lines to ~460 lines by extracting responsibilities into specialized services.

#### Key Architectural Changes
1. **No Direct State Storage** - All state delegated to `IStateService`
2. **No Direct Settings** - Settings managed by `ISettingsService`
3. **Event-Driven** - Uses `IEventService` instead of Action events
4. **Interface Dependencies** - All dependencies injected as interfaces
5. **Focused Responsibility** - Coordinates complex workflows only

#### Injected Dependencies
```csharp
public OrchestratorService(
    IDatabaseService db,
    IIOService io,
    IProgressService progress,
    IConfiguration configuration,
    IComfyUIService capi,
    IWorkflowService workflow,
    IStateService state,
    IEventService events,
    ISettingsService settings,
    IBackendService backend,
    IModelService models,
    IGalleryService gallery,
    ISessionService session)
```

#### Key Responsibilities

**1. Event Publishing**
```csharp
void InvokeParametersChanged(bool isImg2Img)
void InvokeSessionVideosChanged()
```
Publishes typed events through `IEventService`.

**2. Parameter Initialization**
```csharp
void InitializeParameters(ModeType[] modes)
```
Initializes generation parameters for specified modes.

**3. Model Management**
```csharp
Task GetWorkflowModels(bool refresh = false)
List<SDModel> GetCurrentWorkflowModels()
Task GetSDVAEs()
Task GetSDADetailerModels()
List<SDModel> GetModelsForAssetType(AssetType assetType)
Task<List<string>> GetAssetOptions(AssetType assetType)
string GetCurrentModel(ModeType? mode = null)
Task SetCurrentModel(string modelTitle, ModeType? mode = null)
Task SetSDModel(string modelTitle)
string? GetCurrentVae(ModeType? mode = null)
Task SetCurrentVae(string vae, ModeType? mode = null)
```
Coordinates model loading and selection across services.

**4. Workflow Management**
```csharp
Workflow? GetCurrentWorkflow()
Workflow GetWorkflowById(Guid id)
List<Workflow> GetWorkflowsForMode(ModeType mode)
void GetComfyWorkflows()
void SetCurrentWorkflow(Guid workflowId, ModeType? mode = null)
Task<bool> SetCurrentWorkflowAsync(Guid workflowId, IAssetResolverService assetResolver, ModeType? mode = null)
void SetWorkflowBase(ModelBase workflowBase)
void ResetCurrentWorkflow()
void SetDefaultBaseModel()
```
Manages workflow selection, asset initialization, and workflow state.

**5. Workflow Assets**
```csharp
string? GetWorkflowAsset(string parameter, ModeType? mode = null)
void SetWorkflowAsset(string parameter, string value, ModeType? mode = null)
Dictionary<string, string>? GetWorkflowAssetsForMode(ModeType? mode)
List<WorkflowAsset>? GetCurrentWorkflowAssets()
```
Manages workflow asset parameters for different generation modes.

**6. Backend Operations**
```csharp
Task LoadBackendDependentResources()
```
Loads resources that depend on backend availability.

**7. Gallery Operations**
```csharp
Task GetFolders()
Task GetProjects()
Task SetCurrentFolder(int id)
Task SetCurrentProject(int id)
void ReplaceSelectedImages(List<int> ids)
void AddSelectedImage(int id)
void RemoveSelectedImage(int id)
void ClearSelectedImages()
```
Delegates to `IGalleryService` with coordination logic.

**8. Session Operations**
```csharp
void ResetImageEditorState()
void SetImg2ImgInputImage(string imageData, bool resetEditorState)
void AddSessionVideo(GeneratedVideo video)
void AddSessionVideos(IEnumerable<GeneratedVideo> videos)
void ClearSessionVideos()
void RemoveSessionVideo(GeneratedVideo video)
```
Delegates to `ISessionService` for session-scoped state.

**9. Styles & Prompts**
```csharp
void SetLoras(IEnumerable<Lora> loras, bool isImg2Img)
string ParseAndCleanCopiedPrompt(string prompt, bool isNegative, bool isImg2Img)
```
Parses and processes prompt text, extracts LoRAs, cleans style text.

**10. Parameter Loading**
```csharp
Task LoadImageInfoParameters(Image image, ModeType mode)
void SetGenerationParameter(Image source, string parameter, bool isImg2Img)
```
Loads parameters from saved images.

**11. State & Settings**
```csharp
void LoadSettings()
void SaveSettings()
Task LoadState(State? state = null)
Task SaveState(State? state = null)
```
Coordinates state and settings operations.

#### Service Coordination Patterns

**Multi-Service Workflows**
```csharp
// Example: Setting a workflow with asset resolution
public async Task<bool> SetCurrentWorkflowAsync(
    Guid workflowId, 
    IAssetResolverService assetResolver, 
    ModeType? mode = null)
{
    // 1. Get workflow from WorkflowService
    var workflow = GetWorkflowById(workflowId);
    
    // 2. Update StateService
    _state.State.Generation.CurrentWorkflowId = workflowId;
    
    // 3. Initialize assets via AssetResolverService
    var assetsInitialized = await assetResolver.InitializeWorkflowAssets(workflow, assets);
    
    // 4. Load models via ModelService
    await GetWorkflowModels();
    
    // 5. Publish events via EventService
    _events.Publish(new WorkflowChangedEventArgs(workflowId, "SetAsync"));
    
    // 6. Save state via StateService
    await SaveState();
    
    return assetsInitialized;
}
```

#### Architectural Improvements
✅ **Achieved**:
- Reduced from 1600+ to ~460 lines (71% reduction)
- Eliminated 25+ Action events (now uses IEventService)
- All dependencies injected as interfaces
- No direct state storage (delegated to specialized services)
- Comprehensive test coverage (42 unit tests + integration tests)
- Clear separation of concerns

#### Test Coverage
- **Unit Tests**: 42 tests in `OrchestratorServiceTests.cs`
- **Integration Tests**: Covered in `ServiceIntegrationTests.cs`
- **Test Areas**: Model management, workflow management, gallery operations, state persistence, event publishing

---

### 2. StateService

**File**: `Services/StateService.cs`  
**Interface**: `IStateService`  
**Lifetime**: Singleton  
**Role**: Application State Management

#### Purpose
Manages application state and generation parameters. Handles loading, saving, initialization, and normalization of state data. This service was extracted from ManagerService to centralize all state-related operations.

#### Key Responsibilities
1. **State Persistence**: Load/save application state from database
2. **Parameter Management**: Initialize and manage generation parameters for all modes
3. **State Normalization**: Ensure state data is valid and consistent
4. **Migration**: Handle legacy state format conversions

#### Key Properties
```csharp
AppState State { get; }
Txt2ImgParameters ParametersTxt2Img { get; }
Img2ImgParameters ParametersImg2Img { get; }
UpscaleParameters ParametersUpscale { get; }
Img2VidParameters ParametersImg2Vid { get; }
```

#### Key Methods
```csharp
void InitializeParameters(ModeType[] modes)
Task LoadState(int? stateId = null)
Task SaveState()
void SetWorkflowBase(ModelBase workflowBase)
void MigrateLegacySettings()
```

#### Dependencies
- `IDatabaseService`: State persistence
- `IConfiguration`: Configuration access
- `IEventService`: Event publishing
- `ISettingsService`: Default settings

#### Test Coverage
- **Unit Tests**: 29 tests in `StateServiceTests.cs`
- **Test Areas**: State initialization, parameter management, save/load cycles, migration

---

### 3. EventService

**File**: `Services/EventService.cs`  
**Interface**: `IEventService`  
**Lifetime**: Singleton  
**Role**: Event Aggregation & Pub/Sub

#### Purpose
Provides a centralized, typed event bus for decoupled component communication. Replaces the 25+ Action events that were previously scattered across ManagerService.

#### Key Features
1. **Type-Safe Events**: Generic `Publish<TEvent>` and `Subscribe<TEvent>` methods
2. **Thread-Safe**: Lock-based synchronization for concurrent access
3. **Error Isolation**: Exceptions in one handler don't affect others
4. **Decoupling**: Components communicate without direct references

#### Event Types
```csharp
// State Events
StateChangedEventArgs
ParametersChangedEventArgs
WorkflowChangedEventArgs

// Model Events
ModelsChangedEventArgs
SamplersSchedulersChangedEventArgs

// Gallery Events
FolderChangedEventArgs
ProjectChangedEventArgs
SelectionChangedEventArgs

// Session Events
InputImageChangedEventArgs
SessionVideosChangedEventArgs
EditorStateChangedEventArgs

// Progress Events
ProgressChangedEventArgs
ConvergingStateChangedEventArgs
```

#### Usage Pattern
```csharp
// Subscribe
_events.Subscribe<ParametersChangedEventArgs>(e => 
{
    if (e.Mode == "Txt2Img")
        StateHasChanged();
});

// Publish
_events.Publish(new ParametersChangedEventArgs("Txt2Img"));

// Unsubscribe
_events.Unsubscribe<ParametersChangedEventArgs>(handler);
```

#### Dependencies
None - pure event aggregation service.

#### Test Coverage
- **Unit Tests**: 15 tests in `EventServiceTests.cs`
- **Integration Tests**: Event flow tested in `ServiceIntegrationTests.cs`
- **Test Areas**: Subscribe, publish, unsubscribe, thread safety, error handling

---

### 4. ModelService

**File**: `Services/ModelService.cs`  
**Interface**: `IModelService`  
**Lifetime**: Singleton  
**Role**: Model & Asset Management

#### Purpose
Manages models and assets (checkpoints, VAEs, samplers, etc.). Handles model loading, selection, and asset resolution. Extracted from ManagerService to centralize model-related operations.

#### Key Responsibilities
1. **Model Loading**: Load checkpoint and diffusion models from backends
2. **Asset Management**: Manage VAEs, CLIP models, ControlNet models
3. **Model Selection**: Track current model selections per mode
4. **Asset Resolution**: Resolve asset types to available options

#### Key Properties
```csharp
List<SDModel> CheckpointModels { get; }
List<SDModel> DiffusionModels { get; }
List<string> VAEModels { get; }
List<string> ClipModels { get; }
List<string> ClipVisionModels { get; }
List<string> ADetailerModels { get; }
List<Sampler> Samplers { get; }  // Delegates to BackendService
List<Scheduler> Schedulers { get; }  // Delegates to BackendService
List<Upscaler> Upscalers { get; }  // Delegates to BackendService
```

#### Key Methods
```csharp
Task GetWorkflowModels(bool refresh = false)
Task GetVAEModels()
Task GetADetailerModels()
List<SDModel> GetModelsForAssetType(AssetType assetType)
Task<List<string>> GetAssetOptions(AssetType assetType)
string GetCurrentModel(ModeType? mode = null)
Task SetCurrentModel(string modelTitle, ModeType? mode = null)
```

#### Asset Type Mapping
```csharp
AssetType.Checkpoint → CheckpointModels
AssetType.DiffusionModel → DiffusionModels
AssetType.Vae → VAEModels
AssetType.ClipModel → ClipModels
AssetType.ClipVisionModel → ClipVisionModels
AssetType.ADetailer → ADetailerModels
```

#### Dependencies
- `IComfyUIService`: ComfyUI model endpoints
- `IBackendService`: Backend model endpoints
- `IStateService`: Current selections
- `IEventService`: Model change events
- `IProgressService`: Loading progress
- `IIOService`: File system model discovery
- `IConfiguration`: Model paths

#### Test Coverage
- **Unit Tests**: 23 tests in `ModelServiceTests.cs`
- **Test Areas**: Model loading, asset resolution, current model tracking

---

### 5. GalleryService

**File**: `Services/GalleryService.cs`  
**Interface**: `IGalleryService`  
**Lifetime**: Singleton  
**Role**: Gallery & Project Management

#### Purpose
Manages gallery operations including folders, projects, and image selection. Provides a clean interface for gallery-related operations that were previously mixed into ManagerService.

#### Key Responsibilities
1. **Folder Management**: Load and manage folder hierarchy
2. **Project Management**: Load, create, and switch projects
3. **Selection Management**: Track selected images
4. **Gallery State**: Maintain gallery-specific UI state

#### Key Properties
```csharp
List<Folder> Folders { get; }
List<Project> Projects { get; }
List<int> SelectedImageIds { get; }
```

#### Key Methods
```csharp
Task GetFolders()
Task GetProjects(int folderId = 0)
Task SetCurrentProject(int projectId)
void ReplaceSelectedImages(List<int> ids)
void AddSelectedImage(int id)
void RemoveSelectedImage(int id)
void ClearSelectedImages()
```

#### Dependencies
- `IDatabaseService`: Folder/project persistence
- `IStateService`: Current project state
- `IEventService`: Selection/project change events

#### Test Coverage
- **Unit Tests**: 13 tests in `GalleryServiceTests.cs`
- **Test Areas**: Folder operations, project operations, selection management

---

### 6. SessionService

**File**: `Services/SessionService.cs`  
**Interface**: `ISessionService`  
**Lifetime**: Singleton  
**Role**: Session State Management

#### Purpose
Manages session-scoped state including canvas data, image editor state, and session videos. Provides isolated state management for UI-specific operations.

#### Key Responsibilities
1. **Canvas State**: Manage canvas image data and transformations
2. **Editor State**: Track image editor state (crops, masks, etc.)
3. **Video Management**: Session-scoped video generation results
4. **Input Images**: Manage Img2Img/Img2Vid input images

#### Key Properties
```csharp
string? CanvasImageData { get; }
string? Img2ImgInputImage { get; }
string? Img2VidInputImage { get; }
List<GeneratedVideo> SessionVideos { get; }
ImageEditorState EditorState { get; }
```

#### Key Methods
```csharp
void SetImg2ImgInputImage(string imageData, bool resetEditorState)
void SetImg2VidInputImage(string imageData)
void SetCanvasImageData(string imageData)
void ResetImageEditorState()
void AddSessionVideo(GeneratedVideo video)
void AddSessionVideos(IEnumerable<GeneratedVideo> videos)
void RemoveSessionVideo(GeneratedVideo video)
void ClearSessionVideos()
```

#### Dependencies
- `IEventService`: Session change events

#### Test Coverage
- **Unit Tests**: 20 tests in `SessionServiceTests.cs`
- **Test Areas**: Canvas state, input images, video management, editor state

---

### 7. SettingsService

**File**: `Services/SettingsService.cs`  
**Interface**: `ISettingsService`  
**Lifetime**: Singleton  
**Role**: Settings Persistence

#### Purpose
Manages application settings and user preferences. Handles loading, saving, and validation of settings data.

#### Key Responsibilities
1. **Settings Persistence**: Save/load settings to JSON file
2. **Default Values**: Provide default settings
3. **Validation**: Ensure settings are valid
4. **Migration**: Handle settings format changes

#### Key Properties
```csharp
AppSettings Settings { get; }
```

#### Key Methods
```csharp
void LoadSettings()
void SaveSettings()
void ResetToDefaults()
bool ValidateSettings()
```

#### Settings Categories
- **Generation Defaults**: Default parameters for each mode
- **UI Preferences**: Theme, layout, display options
- **Backend Configuration**: API endpoints, timeouts
- **File Paths**: Output directories, resource paths

#### Dependencies
- `IIOService`: File operations
- `IConfiguration`: Configuration defaults

#### Test Coverage
- **Unit Tests**: 31 tests in `SettingsServiceTests.cs`
- **Test Areas**: Load/save, defaults, validation, migration

---

### 8. BackendService

**File**: `Services/BackendService.cs`  
**Interface**: `IBackendService`  
**Lifetime**: Singleton  
**Role**: Backend Health & Resources

#### Purpose
Manages backend connectivity and backend-dependent resources (samplers, schedulers, upscalers). Provides health check and resource loading functionality.

#### Key Responsibilities
1. **Health Checks**: Monitor ComfyUI and WebUI connectivity
2. **Resource Loading**: Load backend-specific resources
3. **WebSocket Management**: ComfyUI WebSocket client ID
4. **Output Paths**: Resolve backend output directories

#### Key Properties
```csharp
List<Sampler> Samplers { get; }
List<Scheduler> Schedulers { get; }
List<Upscaler> Upscalers { get; }
string? ComfyWSClientId { get; }
```

#### Key Methods
```csharp
Task<bool> CheckComfyUIConnection()
Task<bool> CheckWebUIConnection()
Task LoadBackendDependentResources()
Task<List<Sampler>> GetSamplers()
Task<List<Scheduler>> GetSchedulers()
Task<List<Upscaler>> GetUpscalers()
Dictionary<Outdir, string> GetOutputPaths()
```

#### Dependencies
- `IComfyUIService`: ComfyUI API
- `IConfiguration`: Backend URLs
- `IEventService`: Connection state events

#### Test Coverage
- **Unit Tests**: 15 tests in `BackendServiceTests.cs`
- **Test Areas**: Health checks, resource loading, path resolution

---

### 9. ProgressService

**File**: `Services/ProgressService.cs`  
**Interface**: `IProgressService`  
**Lifetime**: Singleton  
**Role**: Progress Tracking

#### Purpose
Tracks generation progress and converging state. Provides real-time feedback for long-running operations.

#### Key Responsibilities
1. **Progress Updates**: Track generation progress (current/total)
2. **Converging State**: Track whether generation is in progress
3. **Event Publishing**: Notify subscribers of progress changes

#### Key Properties
```csharp
InferenceProgress? CurrentProgress { get; }
bool IsConverging { get; }
```

#### Key Methods
```csharp
void SetProgress(InferenceProgress progress)
void SetConverging(bool isConverging)
void ClearProgress()
```

#### Dependencies
- `IEventService`: Progress change events

#### Test Coverage
- **Unit Tests**: 28 tests in `ProgressServiceTests.cs`
- **Test Areas**: Progress updates, converging state, event publishing

---

### 10. DatabaseService

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

### 11. ImageService

**File**: `Services/ImageService.cs` (~650 lines)  
**Interface**: `IImageService`  
**Lifetime**: Singleton  
**Role**: Image Generation Orchestrator

#### Purpose
Coordinates the entire image generation workflow from parameter preparation through API calls to result processing and storage. Works closely with RouterService, BackendService, and ProgressService.

#### Key Responsibilities
1. Build generation parameters from user input
2. Route requests to appropriate backend (via RouterService)
3. Process generation results
4. Save images and metadata to disk
5. Store image records in database
6. Track generation progress

#### Key Properties
```csharp
GeneratedVideos GeneratedVideos { get; }
event Action OnChange
```

#### Key Methods

**Generation**:
```csharp
Task<ImagesDto> GetImages(ModeType mode)
```
Main generation method supporting Txt2Img, Img2Img, and Extras modes.

**Video Generation**:
```csharp
Task<GeneratedVideos> GetVideo()
```
Generates videos from images using ComfyUI workflows.

**Image Download**:
```csharp
Task<bool> DownloadImageAsPng(string url, string path, bool overwrite = true)
```

#### Path Patterns
ImageService supports customizable path and filename patterns:
```csharp
string DirectoryPattern { get; }  // e.g., "[model_name]/[sampler]"
string FilenamePattern { get; }   // e.g., "[seed]_[steps]_[cfg]"
```

**Pattern Tokens**:
- `[model_name]`: Current checkpoint/diffusion model
- `[sampler]`: Sampler name
- `[seed]`: Generation seed
- `[steps]`: Number of steps
- `[cfg]`: CFG scale
- `[width]`, `[height]`: Image dimensions

#### Dependencies
- `IRouterService`: Backend routing
- `IIOService`: File operations
- `IStateService`: Generation parameters
- `IMagickService`: Image processing
- `IDatabaseService`: Image persistence
- `IProgressService`: Progress updates
- `IEventService`: Generation events
- `IConfiguration`: Output configuration

#### Workflow

1. **Parameter Preparation**:
   - Read parameters from StateService
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
   - Apply path/filename patterns
   - Save images to configured output directory
   - Save metadata txt files
   - Create database records
   - Update project associations

5. **Notification**:
   - Fire OnChange event
   - Publish events via EventService

#### Test Coverage
- **Unit Tests**: 25 tests in `ImageServiceTests.cs`
- **Test Areas**: Path patterns, folder generation, image saving, progress tracking

---

### 12. SDAPIService

> **⚠️ DEPRECATION NOTICE**: This service is being phased out in favor of ComfyUI-only workflows. It remains for backward compatibility with Automatic1111 WebUI but is not actively maintained for new features.

**File**: `Services/SDAPIService.cs` (171 lines)  
**Lifetime**: HttpClient (Singleton)  
**Role**: WebUI API Client (Legacy)

#### Purpose
Thin wrapper around Automatic1111's Stable Diffusion WebUI API. This service provides compatibility with the legacy WebUI backend.

#### Configuration
```csharp
BaseAddress: http://localhost:7860/
Timeout: 1 day
Route prefix: sdapi/v1/
```

#### Key Methods

**Model Discovery**:
```csharp
Task<List<SDModel>> GetSDModels()
Task<List<Sampler>> GetSamplers()
Task<List<Scheduler>> GetSchedulers()
Task<List<PromptStyle>> GetStyles()
Task<List<Upscaler>> GetUpscalers()
```

**Options**:
```csharp
Task<Options> GetOptions()
Task<string> PostOptions(Options options)
Task<CmdFlags> GetCmdFlags()
```

**Model Management**:
```csharp
Task<string> PostReloadModel()
Task<string> PostRefreshModels()
```

**Generation**:
```csharp
Task<GeneratedImages> PostTxt2Img(Txt2ImgWebUI param)
Task<GeneratedImages> PostImg2Img(Img2ImgWebUI param)
Task<UpscaledImageDto> PostExtraSingle(UpscaleParameters param)
```

**Control**:
```csharp
Task<string> PostInterrupt()
Task<string> PostSkip()
Task<InferenceProgress> GetProgress()
```

**Dynamic Prompts**:
```csharp
Task<List<string>> GeneratePrompts(string dynamicPromptsTitle, string prompt, int amount, ScriptParametersDynamicPrompts scriptParam)
```

**ControlNet Extension**:
```csharp
Task<List<string>> GetControlNetModels()
Task<List<string>> GetControlNetPreprocessors()
```

**Health Check**:
```csharp
Task<bool> CheckWebuiState()
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

### 13. ComfyUIService

**File**: `Services/ComfyUIService.cs` (801 lines)  
**Interface**: `IComfyUIService`  
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
Task<GeneratedImages> ExecuteWorkflow(Workflow workflow, Dictionary<string, object> parameters)
Task<GeneratedVideos> ExecuteVideoWorkflow(Workflow workflow, Dictionary<string, object> parameters)
Task<LLMResponse> ExecuteLLMWorkflow(Workflow workflow, Dictionary<string, object> parameters)
```

**Image Upload**:
```csharp
Task<string> UploadImage(string imageBase64, string filename)
```
Uploads images for img2img workflows with hash-based caching.

**Result Retrieval**:
```csharp
Task<List<string>> GetFilenameFromHistory(Guid promptId)
Task<string> GetTextFromHistory(Guid promptId)
```

**Progress Tracking**:
Through ComfyUIWebsocketService and ComfyUIEventBus.

**Cleanup**:
```csharp
Task CleanupUploadedImagesAsync(Guid promptId)
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
Task<List<string>> GetCheckpoints()
Task<List<string>> GetVAEs()
Task<List<string>> GetLoras()
Task<List<string>> GetUpscalers()
Task<List<string>> GetSamplers()
Task<List<string>> GetSchedulers()
Task<List<string>> GetClipModels()
Task<List<string>> GetClipVisionModels()
Task<List<string>> GetADetailerModels()
```

#### Event-Driven Architecture
Uses TaskCompletionSource for async workflow completion:

```csharp
private readonly ConcurrentDictionary<Guid, object> _pendingJobs;
```

Handles events from ComfyUIEventBus:
- ExecutionSucceeded
- ExecutionFailed

#### Dependencies
- `ComfyUIEventBus`: Event coordination
- `IConfiguration`: API configuration

#### Test Coverage
- **Integration Tests**: Covered via workflow integration tests
- **Mock Builder**: `MockComfyUIServiceBuilder.cs` for testing

---

### 14. WorkflowService

**File**: `Services/WorkflowService.cs` (803 lines)  
**Interface**: `IWorkflowService`  
**Lifetime**: Singleton  
**Role**: Workflow Template Management

#### Purpose
Manages ComfyUI workflow templates using Scriban templating engine. Parses workflow definitions and renders them with runtime parameters.

#### Key Methods

**Workflow Loading**:
```csharp
List<Workflow> GetWorkflows()
(List<Workflow> workflows, ModelBase? suggestedBase, Guid? suggestedId) RefreshWorkflows(ModelBase? currentBase, Guid? currentId)
```
Scans Templates directory for .sbn files and parses workflow definitions.

**Template Parsing**:
```csharp
Workflow ParseWorkflowTemplate(string templateText)
List<WorkflowAsset>? ParseAssetsFromTemplate(string templateText)
WorkflowAsset? ParseSingleAsset(string assetContent)
```

**Workflow Rendering**:
```csharp
Task<string> RenderWorkflow(Workflow workflow, Dictionary<string, object> parameters)
```
Processes Scriban template with provided parameters to generate ComfyUI workflow JSON.

**Asset Management**:
Workflows define required assets (models, LoRAs, etc.) through metadata:
```json
{
  "Assets": [
    {
      "parameter": "checkpoint",
      "label": "Checkpoint",
      "type": "Checkpoint",
      "default": "sd_xl_base_1.0.safetensors",
      "order": 0,
      "columnSize": 6
    }
  ]
}
```

#### Dependencies
- `IIOService`: File system access
- `ILogger<WorkflowService>`: Logging
- `IConfiguration`: Template path configuration

#### Test Coverage
- **Unit Tests**: 12 tests in `WorkflowServiceTests.cs`
- **Test Areas**: Workflow loading, parsing, asset extraction, rendering

---

### 15. RouterService

**File**: `Services/RouterService.cs` (81 lines)  
**Interface**: `IRouterService`  
**Lifetime**: Singleton  
**Role**: Backend Routing

#### Purpose
Routes generation requests to appropriate backend based on application state and workflow selection.

#### Key Methods

```csharp
Task<GeneratedImages> PostTxt2Img(Txt2ImgParameters param)
Task<GeneratedImages> PostImg2Img(Img2ImgParameters param)
Task<GeneratedVideos> PostImg2Vid(Img2VidParameters param)
Task<UpscaledImageDto> PostUpscale(UpscaleParameters param)
```

#### Routing Logic
1. Check workflow selection in StateService
2. If workflow selected:
   - Render workflow via WorkflowService
   - Execute via ComfyUIService
3. Otherwise:
   - Use legacy SDAPIService (being phased out)

#### Dependencies
- `IStateService`: Current workflow state
- `IComfyUIService`: ComfyUI backend
- `IWorkflowService`: Workflow rendering
- `IConfiguration`: Backend configuration

#### Test Coverage
- **Unit Tests**: 10 tests in `RouterServiceTests.cs`
- **Test Areas**: Routing logic, parameter conversion, error handling

---

### 16. ResourcesService

**File**: `Services/ResourcesService.cs` (225 lines)  
**Interface**: `IResourcesService`  
**Lifetime**: Singleton  
**Role**: Local Resource Management

#### Purpose
Manages local model files and their metadata.

#### Key Methods

**Resource Discovery**:
```csharp
Task<List<LocalResource>> ScanResources(string type)
Task<List<LocalResource>> CreateLocalResourcesByType(int typeId)
Task<LocalResource> CreateLocalResourceByEntity(Resource entity)
```
Scans filesystem for model files and creates database records.

**Hash Calculation**:
```csharp
Task<string> CalculateFileHash(string path)
```
SHA256 hash for version identification.

**Preview Management**:
```csharp
Task SetPreviewImage(int resourceId, string imagePath)
```

**Resource Management**:
```csharp
Task UpdateResource(Resource resource, string directory, string filename, int resourceId, bool isEnabled)
Task DeleteResource(Resource resource, bool deleteFiles, string directory, string filename)
Task ToggleResource(LocalResource resource, LocalResourceFile file)
```

**Prompt Loading**:
```csharp
Task LoadPrompt(LocalResourceFile file, string resourceType, ValueTuple<ModeType, bool> target)
```

#### Dependencies
- `IDatabaseService`: Resource persistence
- `IIOService`: File operations
- `IConfiguration`: Resource paths

#### Test Coverage
- **Unit Tests**: Tests in `ResourcesServiceTests.cs`
- **Test Areas**: Resource scanning, management operations, prompt loading

---

## Service Dependency Graph (Updated)

```
OrchestratorService (Service Coordinator)
├── IStateService (State Management)
│   ├── IDatabaseService
│   ├── IEventService
│   └── ISettingsService
├── IModelService (Model Management)
│   ├── IComfyUIService
│   ├── IBackendService
│   ├── IStateService
│   └── IEventService
├── IGalleryService (Gallery Operations)
│   ├── IDatabaseService
│   ├── IStateService
│   └── IEventService
├── ISessionService (Session State)
│   └── IEventService
├── IWorkflowService (Workflow Management)
│   └── IIOService
├── IBackendService (Backend Health)
│   ├── IComfyUIService
│   └── IEventService
└── IDatabaseService (Data Persistence)

ImageService (Generation Orchestrator)
├── IRouterService (Backend Routing)
│   ├── IComfyUIService
│   ├── IStateService
│   └── IWorkflowService
├── IIOService (File Operations)
├── IStateService (Parameters)
├── IDatabaseService (Image Persistence)
├── IProgressService (Progress Tracking)
│   └── IEventService
└── IEventService (Generation Events)

CivitaiService (External API)
├── HttpClient
├── IProgressService
└── IEventService

EventService (Event Bus)
└── (No dependencies - pure aggregation)
```

## Testing Standards

### Test Coverage Summary
| Category | Tests | Status |
|----------|-------|--------|
| Unit Tests | 280 | ✅ Comprehensive |
| Integration Tests | 15 | ✅ Core flows |
| **Total** | **295** | **✅ Excellent** |

### Services with Full Test Coverage
✅ All core services have comprehensive unit tests:
- OrchestratorService (42 tests)
- StateService (29 tests)
- SettingsService (31 tests)
- EventService (15 tests)
- ModelService (23 tests)
- GalleryService (13 tests)
- SessionService (20 tests)
- BackendService (15 tests)
- ProgressService (28 tests)
- WorkflowService (12 tests)
- RouterService (10 tests)
- ImageService (25 tests)
- IOService (20 tests)
- CacheService (tests included)
- ResourcesService (tests included)

### Integration Test Coverage
✅ **ServiceIntegrationTests.cs** (15 tests):
- State persistence round-trips
- Event propagation across services
- Progress integration
- Gallery and session service integration
- Multi-service workflows
- Settings and state interaction

### Testing Best Practices
1. **Arrange-Act-Assert**: All tests follow AAA pattern
2. **Mock Builders**: Reusable mock service builders
3. **Test Fixtures**: Shared test data and helpers
4. **Interface Mocking**: All dependencies injected as interfaces
5. **Isolation**: Tests don't depend on each other
6. **Performance**: Full test suite runs in seconds

---

## Event-Driven Architecture

### EventService Pattern
The application uses a centralized event bus (`IEventService`) for component communication:

**Benefits**:
- **Decoupling**: Components don't need direct references
- **Type Safety**: Generic `Publish<T>` and `Subscribe<T>` methods
- **Thread Safety**: Lock-based synchronization
- **Error Isolation**: Exceptions in handlers don't affect others

**Event Categories**:

| Category | Events | Purpose |
|----------|--------|---------|
| State | StateChangedEventArgs, ParametersChangedEventArgs | State updates |
| Workflow | WorkflowChangedEventArgs | Workflow selection |
| Models | ModelsChangedEventArgs, SamplersSchedulersChangedEventArgs | Model updates |
| Gallery | FolderChangedEventArgs, ProjectChangedEventArgs, SelectionChangedEventArgs | Gallery operations |
| Session | InputImageChangedEventArgs, SessionVideosChangedEventArgs | Session state |
| Progress | ProgressChangedEventArgs, ConvergingStateChangedEventArgs | Generation progress |

**Usage Pattern**:
```csharp
// In a component
@inject IEventService Events

protected override void OnInitialized()
{
    Events.Subscribe<ParametersChangedEventArgs>(OnParametersChanged);
}

private void OnParametersChanged(ParametersChangedEventArgs e)
{
    if (e.Mode == "Txt2Img")
        StateHasChanged();
}

public void Dispose()
{
    Events.Unsubscribe<ParametersChangedEventArgs>(OnParametersChanged);
}
```

---

## Service Patterns Summary

### Communication Patterns
1. **Dependency Injection**: Constructor injection with interfaces
2. **Events**: Observer pattern via EventService
3. **Async/Await**: Throughout for I/O operations
4. **HttpClient Factory**: Typed clients for external APIs

### Data Access Patterns
1. **Factory Pattern**: IDbContextFactory for DbContext
2. **Repository-Like**: DatabaseService methods
3. **DTO Pattern**: Data transfer objects for API communication

### Best Practices ✅
- **Interface-based DI**: All core services use interfaces
- **Single Responsibility**: Each service has focused purpose
- **Event-driven**: Decoupled communication via EventService
- **Comprehensive Testing**: 295 tests covering all services
- **Async operations**: Throughout
- **Type safety**: Generic event handling

### Improvements Achieved ✅
- ~~God object (ManagerService)~~ → OrchestratorService (71% size reduction)
- ~~Console.WriteLine~~ → ILogger (migration in progress)
- ~~No unit tests~~ → 295 tests with excellent coverage
- ~~Action events~~ → EventService (type-safe, decoupled)
- ~~No interfaces~~ → All core services interfaced

---

## Service Lifecycle Summary

| Lifetime | Count | Services |
|----------|-------|----------|
| Singleton | 21 | Core business logic and state |
| Scoped | 3 | Per-user/request context |
| Transient | 1 | Stateless operations |
| HttpClient | 3 | External API clients |

---

## Service Interface Reference

All core services expose interfaces for dependency injection:

```csharp
// Service Registration in Program.cs
builder.Services.AddSingleton<IOrchestratorService, OrchestratorService>();
builder.Services.AddSingleton<IStateService, StateService>();
builder.Services.AddSingleton<IEventService, EventService>();
builder.Services.AddSingleton<IModelService, ModelService>();
builder.Services.AddSingleton<IGalleryService, GalleryService>();
builder.Services.AddSingleton<ISessionService, SessionService>();
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddSingleton<IBackendService, BackendService>();
builder.Services.AddSingleton<IProgressService, ProgressService>();
builder.Services.AddSingleton<IWorkflowService, WorkflowService>();
builder.Services.AddSingleton<IRouterService, RouterService>();
builder.Services.AddSingleton<IImageService, ImageService>();
builder.Services.AddSingleton<IResourcesService, ResourcesService>();
builder.Services.AddSingleton<ICacheService, CacheService>();
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
builder.Services.AddSingleton<IIOService, IOService>();
builder.Services.AddHttpClient<ComfyUIService>();
builder.Services.AddSingleton<IComfyUIService>(sp => sp.GetRequiredService<ComfyUIService>());
builder.Services.AddScoped<IAssetResolverService, AssetResolverService>();
```

---

This completes the core services documentation. All services are now documented with their interfaces, dependencies, test coverage, and architectural patterns. The refactored architecture provides excellent separation of concerns, comprehensive testing, and maintainable code.
