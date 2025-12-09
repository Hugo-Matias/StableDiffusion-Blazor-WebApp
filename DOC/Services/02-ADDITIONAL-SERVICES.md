# Service Layer Documentation (Continued)

## Additional Services

### 6. WorkflowService

**File**: `Services/WorkflowService.cs` (803 lines)  
**Lifetime**: Singleton  
**Role**: Workflow Template Management

#### Purpose
Manages ComfyUI workflow templates using Scriban templating engine. Parses workflow definitions and renders them with runtime parameters.

#### Key Methods

**Workflow Loading**:
```csharp
public List<Workflow> GetWorkflows()
```
Scans Templates directory for .sbn files and parses workflow definitions.

**Template Parsing**:
```csharp
private Workflow ParseWorkflowTemplate(string templateText)
private List<WorkflowAsset>? ParseAssetsFromTemplate(string templateText)
private WorkflowAsset? ParseSingleAsset(string assetContent)
```

**Workflow Rendering**:
```csharp
public async Task<string> RenderWorkflow(Workflow workflow, Dictionary<string, object> parameters)
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
- IOService: File system access
- ILogger<WorkflowService>: Logging

---

### 7. CivitaiService

**File**: `Services/CivitaiService.cs` (429 lines)  
**Lifetime**: HttpClient (Singleton)  
**Role**: CivitAI Integration

#### Purpose
Integrates with CivitAI API for model discovery, search, and download.

#### Configuration
```csharp
BaseAddress: https://civitai.com/api/
Authorization: Bearer {CivitaiApiToken}
```

#### Key Methods

**Model Search**:
```csharp
public async Task<CivitaiModelsDto?> GetModels(CivitaiModelsRequest req)
public async Task<CivitaiModelDto> GetModel(int id)
public async Task<CivitaiModelDto> GetModelByHash(string hash)
```

**Image Browsing**:
```csharp
public async Task<CivitaiImagesDto> GetImages(CivitaiImagesRequest req)
public async Task<CivitaiImageDto?> GetImageById(int id)
```

**Creator Search**:
```csharp
public async Task<CivitaiCreatorsDto> GetCreators(CivitaiBaseRequest req)
```

**Model Download**:
```csharp
public async Task<string?> DownloadModel(CivitaiFile file, string filename, string path, CivitaiModelType type)
```
Downloads with progress tracking via ProgressService.

**Image Download**:
```csharp
public async Task<string?> DownloadImage(CivitaiImageDto image, string filename, string path)
```

**Hash Lookup**:
```csharp
public async Task<int> GetModelIdByHash(string hash)
```
Retrieves model ID from file hash for version detection.

#### Features
- Progress tracking for downloads
- Automatic file type detection
- Preview image management
- NSFW filtering support
- Pagination support

---

### 8. IOService

**File**: `Services/IOService.cs` (295 lines)  
**Lifetime**: Singleton  
**Role**: File System Operations

#### Purpose
Centralized file I/O operations with path normalization and error handling.

#### Key Methods

**File Operations**:
```csharp
public void MoveFile(string sourcePath, string destinationPath)
public void DeleteFile(string path)
public void DeleteFile(FileInfo file)
public FileInfo? GetFileByName(string path, string fileName)
public IEnumerable<FileInfo> GetFilesByName(string path, string name)
```

**Directory Operations**:
```csharp
public DirectoryInfo CreateDirectory(string path)
public void DeleteFolder(DirectoryInfo dir, bool isRecursive)
public DirectoryInfo? GetFolderByName(string path, string folderName)
public IOrderedEnumerable<FileInfo>? GetOrderedFiles(string path)
public IEnumerable<FileInfo> GetFilesRecursive(string path, string? ignorePath, List<string>? extensionsBlacklist, List<string>? extensionsWhitelist)
```

**Image Operations**:
```csharp
public async Task<List<ImageInfo>?> GetImages(string path)
public string GetBase64FromFile(string path)
public async Task<string?> GetBase64FromFileAsync(string path)
public string GetImageStaticFile(string path)
public string GetResourceImagePath(string type, string filename)
public int GetFileIndex(string path, Outdir dir)
```

**Text Operations**:
```csharp
public string GetJsonAsString(string path)
public string[]? LoadTextLines(string path)
public string? LoadText(string path)
public void SaveText(string path, string content, bool overwrite = true)
```

**Metadata**:
```csharp
public async Task<string> ReadMetadata(string path)
```
Reads PNG metadata using MetadataExtractor.

**Binary Operations**:
```csharp
public async Task SaveFileToDisk(string path, byte[] data)
```

#### Path Normalization
Handles Windows/Linux path differences automatically.

---

### 9. CacheService

**File**: `Services/CacheService.cs` (559 lines)  
**Lifetime**: Singleton  
**Role**: Performance Optimization

#### Purpose
Caches frequently accessed data to improve performance.

#### Key Features

**Tag Usage Cache**:
```csharp
public async Task RefreshTagCache()
public Dictionary<string, int> GetTagUsageCache()
```
Caches tag usage counts for autocomplete and suggestions.

**Periodic Refresh**:
Refreshes every 30 minutes (configured in Program.cs).

**Resource Cache**:
Caches model and resource metadata.

---

### 10. ProgressService

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

### 11. RouterService

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

### 12. ResourcesService

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

### 13. ThemeService

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

### 14. MagickService

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

### 15. ComfyUIWebsocketService

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

### 16. ComfyUIEventBus

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

### 17. OllamaService

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

### 18. JavascriptService

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

### 19. AssetResolverService

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

### 20. CsvService

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

### 21. DanbooruService

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

### 22. DynamicPromptsService

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

## Service Dependency Graph

```
ManagerService (Central Hub)
├── SDAPIService
├── DatabaseService
├── IOService
├── ProgressService
├── ComfyUIService
│   ├── WorkflowService
│   │   └── IOService
│   └── ComfyUIEventBus
│       └── ComfyUIWebsocketService
└── WorkflowService

ImageService
├── SDAPIService
├── IOService
├── ManagerService
├── MagickService
├── DatabaseService
├── ProgressService
└── RouterService
    ├── SDAPIService
    ├── ComfyUIService
    └── ManagerService

CivitaiService
├── HttpClient
├── ImageService
├── IOService
├── ManagerService
├── DatabaseService
└── ProgressService

ResourcesService
├── DatabaseService
└── IOService

CacheService
└── DatabaseService

ThemeService
└── IOService

AssetResolverService (Scoped)
├── ManagerService
└── DatabaseService

OllamaService (Scoped)
└── HttpClient

JavascriptService (Scoped)
└── IJSRuntime
```

## Service Patterns Summary

### Communication Patterns
1. **Events**: Observer pattern for state changes
2. **Dependency Injection**: Constructor injection
3. **Async/Await**: Throughout for I/O operations
4. **HttpClient**: Typed clients for external APIs

### Data Access Patterns
1. **Factory Pattern**: IDbContextFactory for DbContext
2. **Repository-Like**: DatabaseService methods
3. **DTO Pattern**: Data transfer objects for API communication

### Best Practices ✅
- Async operations throughout
- Dependency injection
- Interface segregation (IAssetResolverService)
- Single responsibility (mostly)
- HttpClient factory pattern

### Areas for Improvement ⚠️
- God object (ManagerService)
- Console.WriteLine instead of logging
- No unit tests
- Some synchronous I/O
- Event memory leaks potential
- No cancellation token support in many methods
- Limited error handling in some areas

## Service Lifecycle Summary

| Lifetime | Count | Services |
|----------|-------|----------|
| Singleton | 15 | Core business logic and state |
| Scoped | 3 | Per-user/request context |
| Transient | 1 | Stateless operations |
| HttpClient | 4 | External API clients |

---

This completes the service layer documentation. All 22 services are documented with their purpose, methods, dependencies, and usage patterns.
