# Service Layer Documentation (Continued)

## Additional Services

This document covers additional supporting services that complement the core services documented in 01-CORE-SERVICES.md.

### Summary of Service Extraction

The application has undergone a comprehensive refactoring where the original "God Object" ManagerService (~1600+ lines) was split into multiple specialized services:

**Extracted Core Services** (See 01-CORE-SERVICES.md):
1. **OrchestratorService** (~460 lines) - Service coordination
2. **StateService** - Application state management
3. **EventService** - Event aggregation and pub/sub
4. **ModelService** - Model and asset management
5. **GalleryService** - Gallery operations
6. **SessionService** - Session state management
7. **SettingsService** - Settings persistence
8. **BackendService** - Backend health and resources
9. **ProgressService** - Progress tracking
10. **WorkflowService** - Workflow management
11. **RouterService** - Backend routing
12. **ImageService** - Image generation orchestration
13. **ResourcesService** - Local resource management

**All services now use interface-based dependency injection** for improved testability and maintainability.

---

### 17. CivitaiService

**File**: `Services/CivitaiService.cs` (429 lines)  
**Lifetime**: HttpClient (Singleton)  
**Role**: CivitAI Integration

#### Purpose
Integrates with CivitAI API for model discovery, search, and download. Operates independently without requiring orchestration service dependencies.

#### Configuration
```csharp
BaseAddress: https://civitai.com/api/
Authorization: Bearer {CivitaiApiToken}
```

#### Key Methods

**Model Search**:
```csharp
Task<CivitaiModelsDto?> GetModels(CivitaiModelsRequest req)
Task<CivitaiModelDto> GetModel(int id)
Task<CivitaiModelDto> GetModelByHash(string hash)
```

**Image Browsing**:
```csharp
Task<CivitaiImagesDto> GetImages(CivitaiImagesRequest req)
Task<CivitaiImageDto?> GetImageById(int id)
```

**Creator Search**:
```csharp
Task<CivitaiCreatorsDto> GetCreators(CivitaiBaseRequest req)
```

**Model Download**:
```csharp
Task<string?> DownloadModel(CivitaiFile file, string filename, string path, CivitaiModelType type)
```
Downloads with progress tracking via ProgressService.

**Image Download**:
```csharp
Task<string?> DownloadImage(CivitaiImageDto image, string filename, string path)
```

**Hash Lookup**:
```csharp
Task<int> GetModelIdByHash(string hash)
```
Retrieves model ID from file hash for version detection.

#### Features
- Progress tracking for downloads
- Automatic file type detection
- Preview image management
- NSFW filtering support
- Pagination support

#### Dependencies
- `HttpClient`: HTTP communication (injected via HttpClientFactory)
- `IProgressService`: Download progress tracking
- `IEventService`: Download completion events
- `IConfiguration`: API token and configuration

---

### 18. IOService

**File**: `Services/IOService.cs` (295 lines)  
**Interface**: `IIOService`  
**Lifetime**: Singleton  
**Role**: File System Operations

#### Purpose
Centralized file I/O operations with path normalization and error handling.

#### Key Methods

**File Operations**:
```csharp
void MoveFile(string sourcePath, string destinationPath)
void DeleteFile(string path)
void DeleteFile(FileInfo file)
FileInfo? GetFileByName(string path, string fileName)
IEnumerable<FileInfo> GetFilesByName(string path, string name)
```

**Directory Operations**:
```csharp
DirectoryInfo CreateDirectory(string path)
void DeleteFolder(DirectoryInfo dir, bool isRecursive)
DirectoryInfo? GetFolderByName(string path, string folderName)
IOrderedEnumerable<FileInfo>? GetOrderedFiles(string path)
IEnumerable<FileInfo> GetFilesRecursive(string path, string? ignorePath, List<string>? extensionsBlacklist, List<string>? extensionsWhitelist)
```

**Image Operations**:
```csharp
Task<List<ImageInfo>?> GetImages(string path)
string GetBase64FromFile(string path)
Task<string?> GetBase64FromFileAsync(string path)
string GetImageStaticFile(string path)
string GetResourceImagePath(string type, string filename)
int GetFileIndex(string path, Outdir dir)
```

**Text Operations**:
```csharp
string GetJsonAsString(string path)
string[]? LoadTextLines(string path)
string? LoadText(string path)
void SaveText(string path, string content, bool overwrite = true)
```

**Metadata**:
```csharp
Task<string> ReadMetadata(string path)
```
Reads PNG metadata using MetadataExtractor.

**Binary Operations**:
```csharp
Task SaveFileToDisk(string path, byte[] data)
```

#### Path Normalization
Handles Windows/Linux path differences automatically.

#### Test Coverage
- **Unit Tests**: 20 tests in `IOServiceTests.cs`
- **Test Areas**: File operations, directory operations, path normalization, metadata reading

---

### 19. CacheService

**File**: `Services/CacheService.cs` (559 lines)  
**Interface**: `ICacheService`  
**Lifetime**: Singleton  
**Role**: Performance Optimization

#### Purpose
Caches frequently accessed data to improve performance.

#### Key Features

**Tag Usage Cache**:
```csharp
Task RefreshTagCache()
Dictionary<string, int> GetTagUsageCache()
Task<int> GetLocalTagUsageCount(string tagName)
Task<List<(string Tag, int Count)>> GetTopLocalTags(int count = 100)
List<string> GetRecentTags(int count = 10)
void IncrementTagUsage(string tagName)
```
Caches tag usage counts for autocomplete and suggestions.

**Dictionary Search**:
```csharp
Task LoadDictionaries()
List<DictionaryWord> SearchDictionaries(string query, int maxResults = 10, params DictionaryTheme[] excludeThemes)
List<DictionaryWord> SearchDictionaryThemes(string query, DictionaryTheme[] themes, int maxResults = 10)
List<string> GetDictionaryWords(DictionaryTheme theme)
```

**Fuzzy Matching**:
```csharp
int CalculateFuzzyScore(string search, string target)
```

**Cache Management**:
```csharp
Task ReloadDictionaries()
bool AreDictionariesLoaded()
Dictionary<string, int> GetDictionaryStats()
```

**Periodic Refresh**:
Refreshes every 30 minutes (configured in Program.cs).

**Resource Cache**:
Caches model and resource metadata.

#### Dependencies
- `IDatabaseService`: Tag usage data
- `IIOService`: Dictionary file loading

#### Test Coverage
- **Unit Tests**: Comprehensive tests in `CacheServiceTests.cs`
- **Test Areas**: Tag caching, dictionary search, fuzzy matching, cache management

---

### 20. ProgressService

**File**: `Services/ProgressService.cs` (30 lines)  
**Lifetime**: Singleton  
**Role**: Progress Notification

#### Purpose
Simple event-based progress tracking for downloads and long-running operations.

#### Events
```csharp
public event Action<BaseProgress>? OnProgressChanged;
```

#### Methods
```csharp
public void NotifyProgressChanged(BaseProgress progress)
```

Used by CivitaiService for download progress.

---

### 21. RouterService

**File**: `Services/RouterService.cs` (81 lines)  
**Lifetime**: Singleton  
**Role**: Backend Routing

#### Purpose
Routes generation requests to appropriate backend based on application state.

#### Key Methods

```csharp
public async Task<GeneratedImages> PostTxt2Img(Txt2ImgWebUI param)
public async Task<GeneratedImages> PostImg2Img(Img2ImgWebUI param)
public async Task<GeneratedVideos> PostImg2Vid(Img2VidParameters param)
```

#### Routing Logic
1. Check if ComfyUI mode is enabled
2. If ComfyUI and workflow is selected:
   - Convert parameters to workflow format
   - Call ComfyUIService
3. Otherwise:
   - Call SDAPIService (Automatic1111)

#### Dependencies
- ManagerService: State and workflow selection
- SDAPIService: WebUI backend
- ComfyUIService: ComfyUI backend
- WorkflowService: Workflow rendering

---

### 22. ResourcesService

**File**: `Services/ResourcesService.cs` (225 lines)  
**Lifetime**: Singleton  
**Role**: Local Resource Management

#### Purpose
Manages local model files and their metadata.

#### Key Methods

**Resource Discovery**:
```csharp
public async Task<List<LocalResource>> ScanResources(string type)
```
Scans filesystem for model files and creates database records.

**Hash Calculation**:
```csharp
public async Task<string> CalculateFileHash(string path)
```
SHA256 hash for version identification.

**Preview Management**:
```csharp
public async Task SetPreviewImage(int resourceId, string imagePath)
```

**Organization**:
```csharp
public async Task MoveResource(int resourceId, string newSubType)
```

Supports organizing resources into sub-folders.

---

### 23. ThemeService

**File**: `Services/ThemeService.cs` (199 lines)  
**Lifetime**: Singleton  
**Role**: UI Theming

#### Purpose
Manages application theme (dark/light mode) and color customization.

#### Key Methods

```csharp
public async Task LoadTheme()
public async Task SaveTheme()
public void SetDarkMode(bool isDark)
public MudTheme GetCurrentTheme()
```

#### Features
- Dark/light mode toggle
- Custom color schemes
- Persistent theme preferences

---

### 24. MagickService

**File**: `Services/MagickService.cs` (61 lines)  
**Lifetime**: Transient  
**Role**: Image Processing

#### Purpose
Image manipulation using ImageMagick (Magick.NET).

#### Key Methods

```csharp
public (int, int) GetImageSize(string base64Image)
public byte[] ResizeImage(string base64Image, int width, int height)
public byte[] ConvertFormat(byte[] imageData, string format)
```

#### Use Cases
- Get dimensions without loading full image
- Resize for thumbnails
- Format conversion

---

### 25. ComfyUIWebsocketService

**File**: `Services/ComfyUIWebsocketService.cs` (191 lines)  
**Lifetime**: Singleton (also Hosted Service)  
**Role**: WebSocket Communication

#### Purpose
Maintains persistent WebSocket connection to ComfyUI for real-time updates.

#### Key Features

**Connection Management**:
```csharp
public async Task StartAsync(CancellationToken cancellationToken)
public async Task StopAsync(CancellationToken cancellationToken)
```

**Event Processing**:
Receives messages and publishes through ComfyUIEventBus:
- Execution start
- Execution progress
- Execution complete
- Execution error

**Auto-Reconnect**:
Automatically reconnects on connection loss.

---

### 26. ComfyUIEventBus

**File**: `Services/ComfyUIEventBus.cs` (14 lines)  
**Lifetime**: Singleton  
**Role**: Event Aggregation

#### Purpose
Simple event bus for ComfyUI WebSocket events.

#### Events
```csharp
public event Action<Guid>? ExecutionSucceeded;
public event Action<Guid, string>? ExecutionFailed;
public event Action<Guid, int, int>? ProgressUpdated;
```

#### Methods
```csharp
public void OnExecutionSucceeded(Guid promptId)
public void OnExecutionFailed(Guid promptId, string error)
public void OnProgressUpdated(Guid promptId, int current, int total)
```

---

### 27. OllamaService

**File**: `Services/OllamaService.cs` (178 lines)  
**Lifetime**: Scoped  
**Role**: LLM Integration

#### Purpose
Integrates with Ollama for local LLM-powered prompt enhancement.

#### Key Methods

```csharp
public async Task<string> GeneratePromptEnhancement(string input, string model, string systemPrompt)
public async Task<List<string>> GetAvailableModels()
```

#### Features
- Streaming responses
- Custom system prompts
- Model selection
- Temperature control

---

### 28. JavascriptService

**File**: `Services/JavascriptService.cs` (22 lines)  
**Lifetime**: Scoped  
**Role**: JS Interop

#### Purpose
Wrapper for JavaScript interop operations.

#### Methods
```csharp
public async ValueTask ScrollToBottom(string elementId)
public async ValueTask FocusElement(string elementId)
public async ValueTask<string> GetLocalStorage(string key)
public async ValueTask SetLocalStorage(string key, string value)
```

---

### 29. AssetResolverService

**File**: `Services/AssetResolverService.cs` (258 lines)  
**Lifetime**: Scoped  
**Role**: Workflow Asset Resolution

#### Purpose
Resolves workflow asset requirements to actual model files.

#### Interface
```csharp
public interface IAssetResolverService
{
    Task<Dictionary<string, string>> ResolveAssetsAsync(Workflow workflow);
    Task<string?> ResolveAssetAsync(WorkflowAsset asset);
}
```

#### Implementation
Maps asset types to model categories and provides UI for selection.

---

### 30. CsvService

**File**: `Services/CsvService.cs` (134 lines)  
**Lifetime**: Singleton  
**Role**: CSV Processing

#### Purpose
Import/export data from CSV files (primarily for Danbooru tags).

#### Key Methods

```csharp
public async Task<List<DictionaryWord>> ImportDanbooruTags(string path)
public async Task ExportData<T>(string path, IEnumerable<T> data)
```

---

### 31. DanbooruService

**File**: `Services/DanbooruService.cs` (43 lines)  
**Lifetime**: HttpClient (Singleton)  
**Role**: Tag Dataset

#### Purpose
Fetches tag data from Danbooru for autocomplete and suggestions.

#### Configuration
```csharp
BaseAddress: https://danbooru.donmai.us/
```

#### Methods
```csharp
public async Task<DanbooruResponseDto> GetPosts(DanbooruRequest req)
```

---

### 32. DynamicPromptsService

**File**: `Services/DynamicPromptsService.cs` (20 lines)  
**Lifetime**: Singleton  
**Role**: Dynamic Prompt Generation

#### Purpose
Python interop for Dynamic Prompts extension functionality.

#### Methods
```csharp
public List<string> GeneratePrompts(string template, int count)
```

Uses Python.Runtime for wildcard and template processing.

---

## Service Dependency Graph (Complete)

```
Application Layer (Blazor Components)
    ↓
─────────────────────────────────────────────────────────────
Orchestration Layer
─────────────────────────────────────────────────────────────
OrchestratorService (IOrchestratorService)
├── IStateService → StateService
│   ├── IDatabaseService
│   ├── IEventService
│   ├── ISettingsService
│   └── IConfiguration
├── IModelService → ModelService
│   ├── IComfyUIService
│   ├── IBackendService
│   ├── IStateService
│   ├── IEventService
│   ├── IProgressService
│   ├── IIOService
│   └── IConfiguration
├── IGalleryService → GalleryService
│   ├── IDatabaseService
│   ├── IStateService
│   └── IEventService
├── ISessionService → SessionService
│   └── IEventService
├── IWorkflowService → WorkflowService
│   ├── IIOService
│   ├── IConfiguration
│   └── ILogger
├── IBackendService → BackendService
│   ├── IComfyUIService
│   ├── IConfiguration
│   └── IEventService
├── ISettingsService → SettingsService
│   ├── IIOService
│   └── IConfiguration
├── IProgressService → ProgressService
│   └── IEventService
├── IResourcesService → ResourcesService
│   ├── IDatabaseService
│   ├── IIOService
│   └── IConfiguration
└── IDatabaseService → DatabaseService
    └── IDbContextFactory<AppDbContext>

─────────────────────────────────────────────────────────────
Generation Layer
─────────────────────────────────────────────────────────────
IImageService → ImageService
├── IRouterService → RouterService
│   ├── IComfyUIService
│   ├── IStateService
│   └── IWorkflowService
├── IIOService
├── IStateService
├── IDatabaseService
├── IProgressService
├── MagickService (transient)
├── IEventService
└── IConfiguration

─────────────────────────────────────────────────────────────
External Integration Layer
─────────────────────────────────────────────────────────────
IComfyUIService → ComfyUIService (HttpClient)
├── ComfyUIEventBus
├── IConfiguration
└── ILogger

CivitaiService (HttpClient)
├── IProgressService
├── IEventService
└── IConfiguration

DanbooruService (HttpClient)
└── IConfiguration

OllamaService (Scoped)
└── HttpClient

─────────────────────────────────────────────────────────────
Infrastructure Layer
─────────────────────────────────────────────────────────────
IEventService → EventService (Central Event Bus)
└── (No dependencies)

IIOService → IOService
└── (No dependencies)

ICacheService → CacheService
├── IDatabaseService
└── IIOService

ComfyUIWebsocketService (Hosted Service)
├── ComfyUIEventBus
└── IConfiguration

ComfyUIEventBus
└── (No dependencies)

─────────────────────────────────────────────────────────────
Utility Services
─────────────────────────────────────────────────────────────
CsvService → Tag search/autocomplete
DynamicPromptsService → Python interop for prompts
ThemeService → UI theming
MagickService (Transient) → Image manipulation
JavascriptService (Scoped) → JS interop
IAssetResolverService → AssetResolverService (Scoped)
```

---

## Service Patterns Summary

### Communication Patterns
1. **Interface-Based Dependency Injection**: All core services use interfaces
2. **Event Aggregation**: Centralized `IEventService` for pub/sub communication
3. **Async/Await**: Throughout for I/O operations
4. **HttpClient Factory**: Typed clients for external APIs
5. **Scoped Services**: User-specific context (AssetResolver, Javascript, Ollama)

### Data Access Patterns
1. **Factory Pattern**: `IDbContextFactory<AppDbContext>` for DbContext
2. **Repository-Like**: DatabaseService methods
3. **DTO Pattern**: Data transfer objects for API communication
4. **State Management**: Centralized via StateService

### Architectural Patterns
1. **Service Coordinator**: OrchestratorService coordinates complex operations
2. **Event-Driven**: EventService mediates component communication
3. **Strategy Pattern**: RouterService selects backend strategy
4. **Template Pattern**: WorkflowService uses Scriban templates
5. **Observer Pattern**: EventService pub/sub implementation

---

## Best Practices ✅

### Achieved
- ✅ **Interface-based DI**: All core services expose interfaces
- ✅ **Single Responsibility**: Each service has focused purpose
- ✅ **Event-driven**: Decoupled communication via EventService
- ✅ **Comprehensive Testing**: 295 tests (280 unit + 15 integration)
- ✅ **Async operations**: Throughout
- ✅ **Type safety**: Generic event handling
- ✅ **HttpClient factory**: Typed clients for APIs
- ✅ **Dependency injection**: All dependencies injected

### Key Improvements from Refactoring
| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| ManagerService lines | ~1600 | ~460 (OrchestratorService) | 71% reduction |
| Services with interfaces | 2 | 17 | 750% increase |
| Unit tests | 0 | 280 | ∞ |
| Integration tests | 0 | 15 | ∞ |
| Action events | 25+ | 0 (replaced with EventService) | 100% removed |
| Direct ManagerService dependencies | Many | 0 (all use specialized services) | Eliminated |

---

## Areas for Continued Improvement ⚠️

### In Progress
- 🔄 **Logging**: Migrating remaining `Console.WriteLine` to `ILogger`
- 🔄 **XML Documentation**: Adding comprehensive XML docs to all methods

### Future Considerations
- ⭕ **Cache Invalidation**: More sophisticated cache strategies
- ⭕ **Circuit Breaker**: Add resilience patterns for external APIs
- ⭕ **Retry Policies**: Automatic retry for transient failures
- ⭕ **Rate Limiting**: Built-in rate limiting for external APIs
- ⭕ **Telemetry**: Add OpenTelemetry for observability
- ⭕ **Health Checks**: ASP.NET Core health check endpoints

---

## Service Lifecycle Summary

| Lifetime | Count | Services | Purpose |
|----------|-------|----------|---------|
| **Singleton** | 21 | Core services, state management, external APIs | Application-wide state and functionality |
| **Scoped** | 3 | AssetResolverService, JavascriptService, OllamaService | Per-user/circuit context |
| **Transient** | 1 | MagickService | Stateless image processing |
| **HttpClient** | 3 | ComfyUIService, CivitaiService, DanbooruService | External API integration |
| **Hosted** | 1 | ComfyUIWebsocketService | Background WebSocket connection |

---

## Testing Standards

### Test Categories
| Category | Count | Coverage |
|----------|-------|----------|
| **Unit Tests** | 280 | All core services |
| **Integration Tests** | 15 | Cross-service workflows |
| **Total** | **295** | **Comprehensive** |

### Test Organization
```
BlazorWebApp.Tests/
├── Services/                 # Unit tests (280 tests)
│   ├── OrchestratorServiceTests.cs (42)
│   ├── StateServiceTests.cs (29)
│   ├── SettingsServiceTests.cs (31)
│   ├── EventServiceTests.cs (15)
│   ├── ModelServiceTests.cs (23)
│   ├── GalleryServiceTests.cs (13)
│   ├── SessionServiceTests.cs (20)
│   ├── BackendServiceTests.cs (15)
│   ├── ProgressServiceTests.cs (28)
│   ├── WorkflowServiceTests.cs (12)
│   ├── RouterServiceTests.cs (10)
│   ├── ImageServiceTests.cs (25)
│   ├── IOServiceTests.cs (20)
│   ├── CacheServiceTests.cs
│   └── ResourcesServiceTests.cs
├── Integration/              # Integration tests (15 tests)
│   └── ServiceIntegrationTests.cs
├── MockBuilders/             # Reusable mocks
│   ├── MockStateServiceBuilder.cs
│   ├── MockComfyUIServiceBuilder.cs
│   ├── MockWorkflowServiceBuilder.cs
│   └── MockBackendServiceBuilder.cs
└── TestFixtures/             # Test data
    ├── ModelTestFixtures.cs
    └── BackendTestFixtures.cs
```

### Testing Best Practices
1. **AAA Pattern**: Arrange-Act-Assert in all tests
2. **FluentAssertions**: Readable, expressive assertions
3. **Mock Builders**: Consistent, reusable mocks
4. **Test Fixtures**: Shared test data
5. **Isolation**: No dependencies between tests
6. **Fast Execution**: Full suite runs in seconds
7. **CI/CD Ready**: All tests automated and reliable

---

## Event-Driven Architecture Details

### EventService Implementation
```csharp
// Thread-safe, type-safe event bus
public interface IEventService
{
    void Publish<TEvent>(TEvent eventArgs) where TEvent : EventArgs;
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : EventArgs;
    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : EventArgs;
}
```

### Event Categories and Types

**State Events**:
- `StateChangedEventArgs`: General state updates
- `ParametersChangedEventArgs`: Generation parameter updates
- `WorkflowChangedEventArgs`: Workflow selection changes

**Model Events**:
- `ModelsChangedEventArgs`: Model list updates
- `SamplersSchedulersChangedEventArgs`: Sampler/scheduler updates

**Gallery Events**:
- `FolderChangedEventArgs`: Folder navigation
- `ProjectChangedEventArgs`: Project selection
- `SelectionChangedEventArgs`: Image selection changes

**Session Events**:
- `InputImageChangedEventArgs`: Input image updates
- `SessionVideosChangedEventArgs`: Video list updates
- `EditorStateChangedEventArgs`: Image editor state

**Progress Events**:
- `ProgressChangedEventArgs`: Generation progress
- `ConvergingStateChangedEventArgs`: Converging state changes

### Event Flow Example
```
User Action (Component)
    ↓
Service Method Call
    ↓
Service Updates Internal State
    ↓
Service.Publish<TEvent>(new EventArgs(...))
    ↓
EventService Broadcasts to All Subscribers
    ↓
Components Receive Event
    ↓
Components Call StateHasChanged()
    ↓
UI Updates
```

---

## Configuration Reference

### Service Registration Pattern
```csharp
// Interface-only (clean pattern) - Most services
builder.Services.AddSingleton<IServiceName, ServiceName>();

// Dual registration (compatibility) - Special cases
builder.Services.AddSingleton<ServiceName>();
builder.Services.AddSingleton<IServiceName>(sp => sp.GetRequiredService<ServiceName>());

// HttpClient services
builder.Services.AddHttpClient<ComfyUIService>();
builder.Services.AddSingleton<IComfyUIService>(sp => sp.GetRequiredService<ComfyUIService>());

// Scoped services
builder.Services.AddScoped<IAssetResolverService, AssetResolverService>();

// Transient services
builder.Services.AddTransient<MagickService>();

// Hosted services
builder.Services.AddSingleton<ComfyUIWebsocketService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ComfyUIWebsocketService>());
```

---

## Migration Notes

### From ManagerService to Specialized Services

**Previously** (ManagerService pattern):
```csharp
@inject ManagerService Manager

var state = Manager.State;
var models = Manager.CheckpointModels;
Manager.OnStateHasChanged += StateHasChanged;
```

**Now** (Specialized services):
```csharp
@inject IStateService State
@inject IModelService Models
@inject IEventService Events

var state = State.State;
var models = Models.CheckpointModels;
Events.Subscribe<StateChangedEventArgs>(e => StateHasChanged());
```

### Benefits of Refactoring
1. **Testability**: Easy to mock individual services
2. **Maintainability**: Focused, single-purpose services
3. **Type Safety**: Generic event handling
4. **Performance**: Better memory management (no event leaks)
5. **Clarity**: Clear service boundaries and responsibilities

---

## Related Documentation

- [Core Services Documentation](./01-CORE-SERVICES.md) - Detailed documentation of all core services
- [Architectural Analysis](../Architecture/01-ARCHITECTURAL-ANALYSIS.md) - Architecture patterns and design decisions
- [Manager Service Refactor Plan](../Plans/manager-service-refactor.md) - Refactoring journey and metrics
- [Service Interface Extraction Plan](../Plans/SERVICE_INTERFACE_EXTRACTION_PLAN.md) - Interface extraction strategy

---

**Documentation Version**: 2.0  
**Last Updated**: 2024-12-14  
**Refactoring Status**: ✅ **Complete** - All 13 specialized services extracted and tested  
**Test Coverage**: 295 tests (280 unit + 15 integration)
