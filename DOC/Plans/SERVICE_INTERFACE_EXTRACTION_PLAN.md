# Service Interface Extraction & Testing Plan

## Status
**Current Phase:** Phase A - Critical Service Interfaces
**Last Updated:** 2025-01-14
**Parent Plan:** [manager-service-refactor.md](./manager-service-refactor.md)

---

## ?? Executive Summary

### Objective
Create comprehensive interfaces for **all remaining services** and build a **robust test suite** covering unit tests, integration tests, and end-to-end workflows to ensure bulletproof service architecture.

### Motivation
This initiative is a strategic detour from the main ManagerService refactor to establish a solid foundation of testable, maintainable services before proceeding with Phase 8 (Component Migration). By completing interface extraction and comprehensive testing now, we ensure:

1. **Bulletproof Services** - Every service fully tested and validated
2. **Mockable Architecture** - Easy to test components that depend on services
3. **Future-Proof** - Easy to swap implementations without breaking changes
4. **Clear Contracts** - Interfaces serve as documentation and API contracts
5. **Migration Confidence** - Phase 8 component migration will be smoother with stable services

---

## ?? Current Status Assessment

### Services with Interfaces ? (10/27 = 37%)

| Service | Interface | Tests | Status |
|---------|-----------|-------|--------|
| EventService | IEventService | 15/15 ? | Complete |
| StateService | IStateService | 29/29 ? | Complete |
| SettingsService | ISettingsService | 31/31 ? | Complete |
| BackendService | IBackendService | 15/15 ? | Complete |
| ModelService | IModelService | 23/23 ? | Complete |
| GalleryService | IGalleryService | 13/13 ? | Complete |
| SessionService | ISessionService | 20/20 ? | Complete |
| ComfyUIService | IComfyUIService | 0/0 ?? | Interface only |
| DatabaseService | IDatabaseService | 0/0 ?? | Interface only |
| IOService | IIOService | 20/20 ? | Complete |

**Total Tests: 166/166 passing** ?

---

### Services WITHOUT Interfaces ? (17/27 = 63%)

#### **Critical Services** (Block testing, heavily used)
1. ? **ImageService** - Image generation orchestration
2. ? **WorkflowService** - Workflow management (already exists, needs interface)
3. ? **ResourcesService** - Model/resource management

#### **External API Services** (External integrations)
4. ? **CivitaiService** - CivitAI API integration
5. ? **DanbooruService** - Danbooru API integration
6. ? **OllamaService** - Ollama LLM integration

#### **Utility Services** (Supporting services)
7. ? **CacheService** - Caching layer
8. ? **ProgressService** - Progress bar management
9. ? **CsvService** - Tag search/autocomplete
10. ? **DynamicPromptsService** - Prompt generation
11. ? **ThemeService** - Theme management
12. ? **MagickService** - Image manipulation
13. ? **RouterService** - Routing logic

#### **Infrastructure Services** (Low-level)
14. ? **JavascriptService** - JS interop
15. ? **ComfyUIWebsocketService** - WebSocket communication
16. ? **ComfyUIEventBus** - Event bus for ComfyUI
17. ? **AssetResolverService** - Asset resolution (scoped, needs interface)

---

## ?? Implementation Phases

### **Phase A: Critical Service Interfaces** (Week 1)
**Priority: HIGH** - Services that block testing or are heavily used
**Estimated Duration:** 4 days
**Target Tests:** 60-70 new tests

#### **A1: ImageService Interface** (Day 1-2)
**Complexity:** ?? High - Orchestrates multiple services

##### **Interface Definition**
```csharp
public interface IImageService
{
    // Events
    event Action OnChange;
    
    // Properties
    GeneratedVideos GeneratedVideos { get; }
    
    // Image Generation Methods
    Task<ImagesDto> GetImages(ModeType mode);
    Task<GeneratedVideos> GetVideo();
    
    // Image Download Methods
    Task<bool> DownloadImageAsPng(string url, string path, bool overwrite = true);
}
```

##### **Dependencies**
- `IIOService` - File I/O operations
- `ManagerService` - State and configuration (temporary, will migrate)
- `MagickService` - Image manipulation (will create interface in Phase C)
- `IDatabaseService` - Image persistence
- `ProgressService` - Progress tracking (will create interface in Phase C)
- `RouterService` - API routing (will create interface in Phase C)

##### **Test Requirements**
- **Generation Workflows** (10 tests)
  - Txt2Img generation with various parameters
  - Img2Img generation with mask and inpainting
  - Extras/Upscale generation
  - Img2Vid generation
  - Error handling for invalid parameters
  
- **Image Saving & Persistence** (8 tests)
  - Save to disk with correct file naming
  - Database persistence with metadata
  - Grid image generation
  - File path resolution
  
- **Video Generation** (5 tests)
  - Video generation workflow
  - Video saving and persistence
  - Frame rate and duration calculation
  - Video metadata extraction

- **Error Handling** (2 tests)
  - Network errors during generation
  - Disk write errors

**Estimated Tests:** 25 tests

##### **Implementation Steps**
1. [ ] Create `IImageService` interface in `BlazorWebApp/Services/IImageService.cs`
2. [ ] Update `ImageService` to implement interface
3. [ ] Update DI registration in `Program.cs`
4. [ ] Create test file `BlazorWebApp.Tests/Services/ImageServiceTests.cs`
5. [ ] Create mock builders in `BlazorWebApp.Tests/MockBuilders/MockImageServiceBuilder.cs`
6. [ ] Create test fixtures in `BlazorWebApp.Tests/TestFixtures/ImageTestFixtures.cs`
7. [ ] Write generation workflow tests
8. [ ] Write image saving tests
9. [ ] Write video generation tests
10. [ ] Write error handling tests
11. [ ] Verify all tests passing
12. [ ] Update consuming services to use interface

---

#### **A2: WorkflowService Interface** (Day 2-3)
**Complexity:** ?? Medium - Core workflow management

##### **Interface Definition**
```csharp
public interface IWorkflowService
{
    // Workflow Loading
    List<Workflow> GetWorkflows();
    Workflow? GetWorkflowById(Guid id);
    List<Workflow> GetWorkflowsForMode(ModeType mode);
    
    // Workflow Management
    void RefreshWorkflows();
    Workflow? GetWorkflowByBase(ModelBase workflowBase);
    
    // Validation
    bool ValidateWorkflow(Workflow workflow);
}
```

##### **Dependencies**
- `IConfiguration` - Configuration access
- `IIOService` - File system operations

##### **Test Requirements**
- **Workflow Loading** (8 tests)
  - Load workflows from disk
  - Load workflows from specific directory
  - Handle missing workflow files
  - Handle corrupt workflow JSON
  - Workflow caching behavior
  
- **Workflow Filtering** (5 tests)
  - Filter by ModeType
  - Filter by ModelBase
  - Get workflow by ID
  - Handle empty workflow list
  
- **Workflow Validation** (4 tests)
  - Validate workflow structure
  - Validate required fields
  - Validate asset definitions
  - Validate pipeline nodes

- **Workflow Refresh** (3 tests)
  - Refresh from disk
  - Preserve current workflow selection
  - Handle deleted workflows

**Estimated Tests:** 20 tests

##### **Implementation Steps**
1. [ ] Create `IWorkflowService` interface
2. [ ] Update `WorkflowService` to implement interface
3. [ ] Update DI registration
4. [ ] Create test file
5. [ ] Create test fixtures with sample workflows
6. [ ] Write workflow loading tests
7. [ ] Write workflow filtering tests
8. [ ] Write workflow validation tests
9. [ ] Write workflow refresh tests
10. [ ] Verify all tests passing

---

#### **A3: ResourcesService Interface** (Day 3-4)
**Complexity:** ?? High - Manages models/resources

##### **Interface Definition**
```csharp
public interface IResourcesService
{
    // Resource Loading
    Task<List<LocalResource>> CreateLocalResourcesByType(int typeId);
    Task<LocalResource> CreateLocalResourceByEntity(Resource entity);
    Task<LocalResourceFile?> GetResourceFileInfo(
        string resourceType, 
        string? resourceSubtype, 
        LocalResourceFile file);
    
    // Resource Management
    Task UpdateResource(
        Resource resource, 
        string directory, 
        string filename, 
        int resourceId, 
        bool isEnabled);
    Task DeleteResource(
        Resource resource, 
        bool deleteFiles, 
        string directory, 
        string filename);
    Task ToggleResource(LocalResource resource, LocalResourceFile file);
    
    // Prompt Loading
    Task LoadPrompt(
        LocalResourceFile file, 
        string resourceType, 
        ValueTuple<ModeType, bool> target);
}
```

##### **Dependencies**
- `ManagerService` - State access (temporary)
- `IIOService` - File operations
- `IDatabaseService` - Resource persistence
- `IConfiguration` - Path configuration

##### **Test Requirements**
- **Resource Loading** (8 tests)
  - Load resources by type
  - Load resources with subtypes
  - Handle missing resources
  - Handle corrupt resource files
  - Resource caching
  
- **Resource Management** (7 tests)
  - Update resource state
  - Delete resource with files
  - Delete resource without files
  - Toggle resource enable/disable
  - Move resource between folders
  
- **Prompt Loading** (8 tests)
  - Load TextualInversion prompts
  - Load Hypernetwork prompts
  - Load LORA prompts
  - Load LoCon prompts
  - Load trigger words
  - Handle missing trigger words
  - Positive vs negative prompts
  - Weight application

- **Error Handling** (2 tests)
  - Handle file system errors
  - Handle database errors

**Estimated Tests:** 25 tests

##### **Implementation Steps**
1. [ ] Create `IResourcesService` interface
2. [ ] Update `ResourcesService` to implement interface
3. [ ] Update DI registration
4. [ ] Create test file
5. [ ] Create test fixtures with sample resources
6. [ ] Write resource loading tests
7. [ ] Write resource management tests
8. [ ] Write prompt loading tests
9. [ ] Write error handling tests
10. [ ] Verify all tests passing

---

### **Phase B: External API Service Interfaces** (Week 2)
**Priority: MEDIUM** - External integrations
**Estimated Duration:** 2 days
**Target Tests:** 35-40 new tests

#### **B1: CivitaiService Interface** (Day 5)
**Complexity:** ?? Medium - External API with rate limiting

##### **Interface Definition**
```csharp
public interface ICivitaiService
{
    // Creator Operations
    Task<CivitaiCreatorsDto> GetCreators(CivitaiBaseRequest req);
    
    // Image Operations
    Task<CivitaiImagesDto> GetImages(CivitaiImagesRequest req);
    Task<CivitaiImageDto?> GetImageById(int id);
    Task<CivitaiImageDto> GetImageByModelVersionId(
        int modelVersionId, 
        CivitaiImageDto imageDto);
    
    // Model Operations
    Task<CivitaiModelsDto?> GetModels(CivitaiModelsRequest req);
    Task<CivitaiModelsDto?> GetModelsFromUrl(string url);
    Task<CivitaiModelDto?> GetModel(int id);
    Task<int> GetModelIdByHash(string hash);
    Task<CivitaiModelVersionDto> GetModelVersion(int id);
    
    // Download Operations
    Task<CivitaiDownloadStatus> DownloadResource(
        CivitaiModelDto model, 
        CivitaiModelVersionDto version, 
        CivitaiModelVersionFileDto file, 
        string? subtype = null);
    
    // Maintenance Operations
    Task UpdateResourceDescriptions();
    Task UpdateResourceState();
    Task UpdateResourceBaseModels();
}
```

##### **Test Requirements**
- API endpoint mocking (8 tests)
- Model search and filtering (5 tests)
- Image browsing (3 tests)
- Download workflows (4 tests)
- Error handling (rate limiting, network errors) (3 tests)

**Estimated Tests:** 20 tests

---

#### **B2: DanbooruService Interface** (Day 5)
**Complexity:** ?? Low - Simple API integration

##### **Interface Definition**
```csharp
public interface IDanbooruService
{
    Task<List<Tag>> SearchTags(string query, int limit = 10);
    Task<Tag?> GetTag(string name);
    Task<bool> CheckTagExists(string name);
}
```

##### **Test Requirements**
- API mocking (3 tests)
- Tag search (3 tests)
- Tag retrieval (2 tests)
- Error handling (2 tests)

**Estimated Tests:** 10 tests

---

#### **B3: OllamaService Interface** (Day 6)
**Complexity:** ?? Medium - LLM integration

##### **Interface Definition**
```csharp
public interface IOllamaService
{
    Task<string> GeneratePrompt(string userInput);
    Task<List<string>> GetModels();
    Task<bool> CheckConnection();
}
```

##### **Test Requirements**
- LLM prompt generation mocking (5 tests)
- Model listing (3 tests)
- Connection handling (2 tests)
- Error handling (2 tests)

**Estimated Tests:** 12 tests

---

### **Phase C: Utility Service Interfaces** (Week 3)
**Priority: LOW** - Supporting services
**Estimated Duration:** 4 days
**Target Tests:** 65-75 new tests

#### **C1: CacheService Interface** (Day 7)
**Complexity:** ?? Medium - Caching with expiration

##### **Interface Definition**
```csharp
public interface ICacheService
{
    // Tag Usage Tracking
    Task<int> GetLocalTagUsageCount(string tagName);
    Task<List<(string Tag, int Count)>> GetTopLocalTags(int count = 100);
    List<string> GetRecentTags(int count = 10);
    void IncrementTagUsage(string tagName);
    Task RefreshTagCache();
    
    // Dictionary Search
    Task LoadDictionaries();
    List<DictionaryWord> SearchDictionaries(
        string query, 
        int maxResults = 10, 
        params DictionaryTheme[] excludeThemes);
    List<DictionaryWord> SearchDictionaryThemes(
        string query, 
        DictionaryTheme[] themes, 
        int maxResults = 10);
    List<string> GetDictionaryWords(DictionaryTheme theme);
    
    // Fuzzy Matching
    int CalculateFuzzyScore(string search, string target);
    
    // Cache Management
    Task ReloadDictionaries();
    bool AreDictionariesLoaded();
    Dictionary<string, int> GetDictionaryStats();
}
```

**Estimated Tests:** 18 tests

---

#### **C2: ProgressService Interface** (Day 8)
**Complexity:** ?? Low - Simple progress tracking

##### **Interface Definition**
```csharp
public interface IProgressService
{
    void Add(BaseProgress progress);
    void Remove(Guid id);
    void Update(Guid id, int value);
    BaseProgress? Get(Guid id);
    List<BaseProgress> GetAll();
    void Clear();
}
```

**Estimated Tests:** 10 tests

---

#### **C3: CsvService Interface** (Day 8)
**Complexity:** ?? Low - CSV parsing

##### **Interface Definition**
```csharp
public interface ICsvService
{
    Task<IEnumerable<Tag>> SearchTags(string searchText, bool enableFuzzy = true);
    Tag? GetTag(string name);
    bool CheckTagExists(string name);
}
```

**Estimated Tests:** 15 tests

---

#### **C4: DynamicPromptsService Interface** (Day 9)
**Complexity:** ?? Medium - Template processing

##### **Interface Definition**
```csharp
public interface IDynamicPromptsService
{
    string ProcessPrompt(string prompt);
    List<string> GetWildcards();
    string GetWildcardContent(string wildcardName);
    bool ValidatePrompt(string prompt);
}
```

**Estimated Tests:** 12 tests

---

#### **C5: ThemeService Interface** (Day 9)
**Complexity:** ?? Low - Theme management

##### **Interface Definition**
```csharp
public interface IThemeService
{
    void ApplyTheme(string themeName);
    List<string> GetAvailableThemes();
    string GetCurrentTheme();
}
```

**Estimated Tests:** 8 tests

---

#### **C6: MagickService Interface** (Day 10)
**Complexity:** ?? Medium - Image manipulation

##### **Interface Definition**
```csharp
public interface IMagickService
{
    byte[] ConvertToPng(byte[] imageData);
    (int width, int height) GetImageSize(string base64Image);
    Task<string> SaveGrid(List<string> images, string path);
    byte[] ResizeImage(byte[] imageData, int width, int height);
}
```

**Estimated Tests:** 12 tests

---

#### **C7: RouterService Interface** (Day 10)
**Complexity:** ?? High - API routing orchestration

##### **Interface Definition**
```csharp
public interface IRouterService
{
    Task<GeneratedImages> PostTxt2Img(Txt2ImgParameters parameters);
    Task<GeneratedImages> PostImg2Img(Img2ImgParameters parameters);
    Task<GeneratedVideos> PostImg2Vid(Img2VidParameters parameters);
    Task<UpscaledImageDto> PostUpscale(UpscaleParameters parameters);
}
```

**Estimated Tests:** 18 tests

---

### **Phase D: Integration Testing** (Week 4)
**Priority: HIGH** - End-to-end workflows
**Estimated Duration:** 5 days
**Target Tests:** 70-80 new tests

#### **D1: DatabaseService Integration Tests** (Day 11-12)
**Focus:** Real EF Core operations with in-memory database

##### **Test Groups**
- **State Operations** (5 tests)
  - Full state persistence lifecycle
  - Version filtering
  - WorkflowAssets persistence
  
- **Project/Folder Operations** (5 tests)
  - Project creation with folder associations
  - Folder deletion cascades
  - Project pagination
  
- **Image Operations** (5 tests)
  - Image CRUD with metadata
  - Paging and filtering
  - Sorting by various fields
  
- **Resource Operations** (5 tests)
  - Resource CRUD
  - Resource type filtering
  - Base model filtering

**Total Tests:** 20 tests

---

#### **D2: Cross-Service Integration Tests** (Day 13-14)
**Focus:** Complete workflows across multiple services

##### **Test Groups**
- **Generation Workflows** (10 tests)
  - End-to-end Txt2Img workflow
  - End-to-end Img2Img workflow
  - End-to-end Img2Vid workflow
  - Model switching during generation
  - Settings applied correctly
  
- **Gallery Workflows** (8 tests)
  - Project creation and image saving
  - Image selection and batch operations
  - Folder navigation with projects
  - Image metadata persistence
  
- **State Persistence Workflows** (7 tests)
  - State save/load across services
  - Settings changes reflected in state
  - WorkflowAssets persistence
  - Parameter initialization from settings

**Total Tests:** 25 tests

---

#### **D3: External API Integration Tests** (Day 15)
**Focus:** External API interactions (with mocking)

##### **Test Groups**
- **CivitAI Integration** (10 tests)
  - Model browsing workflow
  - Model download workflow
  - Resource metadata updates
  
- **Tag Search Integration** (5 tests)
  - Tag autocomplete workflow
  - Tag usage tracking
  - Dictionary search
  
- **Ollama Integration** (5 tests)
  - Prompt generation workflow
  - Model selection
  - Error handling

**Total Tests:** 20 tests

---

## ?? Test Coverage Goals

### **Target Test Distribution**

| Test Type | Current | Target | Progress |
|-----------|---------|--------|----------|
| **Unit Tests** | 166 | 320-350 | 47% |
| **Integration Tests** | 0 | 80-100 | 0% |
| **End-to-End Tests** | 0 | 30-40 | 0% |
| **Total** | 166 | 430-490 | 34% |

### **Service Coverage Matrix**

| Service | Interface | Unit Tests | Integration Tests | Status |
|---------|-----------|------------|-------------------|--------|
| EventService | ? | 15 ? | - | Complete |
| StateService | ? | 29 ? | 5 ?? | Needs Integration |
| SettingsService | ? | 31 ? | - | Complete |
| BackendService | ? | 15 ? | - | Complete |
| ModelService | ? | 23 ? | 5 ?? | Needs Integration |
| GalleryService | ? | 13 ? | 8 ?? | Needs Integration |
| SessionService | ? | 20 ? | - | Complete |
| ComfyUIService | ? | 0 ? | - | Needs Tests |
| DatabaseService | ? | 0 ? | 20 ?? | Needs All Tests |
| IOService | ? | 20 ? | - | Complete |
| ImageService | ? | 0 ? | 10 ?? | Needs All |
| WorkflowService | ? | 0 ? | - | Needs All |
| ResourcesService | ? | 0 ? | - | Needs All |
| CivitaiService | ? | 0 ? | 10 ?? | Needs All |
| DanbooruService | ? | 0 ? | - | Needs All |
| OllamaService | ? | 0 ? | 5 ?? | Needs All |
| CacheService | ? | 0 ? | - | Needs All |
| ProgressService | ? | 0 ? | - | Needs All |
| CsvService | ? | 0 ? | - | Needs All |
| DynamicPromptsService | ? | 0 ? | - | Needs All |
| ThemeService | ? | 0 ? | - | Needs All |
| MagickService | ? | 0 ? | - | Needs All |
| RouterService | ? | 0 ? | 10 ?? | Needs All |

Legend: ? Complete | ? Not Started | ?? Planned

---

## ?? Success Criteria

### **Phase A (Week 1):**
- [ ] `IImageService`, `IWorkflowService`, `IResourcesService` created
- [ ] 60-70 new unit tests passing
- [ ] Build passes without errors
- [ ] Application functional
- [ ] Total tests: 236+ passing

### **Phase B (Week 2):**
- [ ] `ICivitaiService`, `IDanbooruService`, `IOllamaService` created
- [ ] 35-40 new unit tests passing
- [ ] External API mocking working correctly
- [ ] Total tests: 276+ passing

### **Phase C (Week 3):**
- [ ] All utility service interfaces created
- [ ] 65-75 new unit tests passing
- [ ] Total tests: 350+ passing

### **Phase D (Week 4):**
- [ ] DatabaseService integration tests (20 tests)
- [ ] Cross-service integration tests (30 tests)
- [ ] External API integration tests (20 tests)
- [ ] **Total tests: 420+ passing** ?
- [ ] Test execution time < 10 seconds
- [ ] Code coverage > 80% for all services

---

## ?? File Structure

```
BlazorWebApp.Tests/
??? Services/                           # Unit Tests
?   ??? ImageServiceTests.cs           # 25 tests
?   ??? WorkflowServiceTests.cs        # 20 tests
?   ??? ResourcesServiceTests.cs       # 25 tests
?   ??? CivitaiServiceTests.cs         # 20 tests
?   ??? DanbooruServiceTests.cs        # 10 tests
?   ??? OllamaServiceTests.cs          # 12 tests
?   ??? CacheServiceTests.cs           # 18 tests
?   ??? ProgressServiceTests.cs        # 10 tests
?   ??? CsvServiceTests.cs             # 15 tests
?   ??? DynamicPromptsServiceTests.cs  # 12 tests
?   ??? ThemeServiceTests.cs           # 8 tests
?   ??? MagickServiceTests.cs          # 12 tests
?   ??? RouterServiceTests.cs          # 18 tests
?
??? Integration/                        # Integration Tests
?   ??? DatabaseServiceIntegrationTests.cs    # 20 tests
?   ??? GenerationWorkflowTests.cs            # 10 tests
?   ??? GalleryIntegrationTests.cs            # 8 tests
?   ??? StatePersistenceTests.cs              # 7 tests
?   ??? ModelLoadingIntegrationTests.cs       # 5 tests
?   ??? CivitaiIntegrationTests.cs            # 10 tests
?   ??? TagSearchIntegrationTests.cs          # 5 tests
?   ??? OllamaIntegrationTests.cs             # 5 tests
?
??? MockBuilders/                       # Mock Builders
?   ??? MockImageServiceBuilder.cs
?   ??? MockWorkflowServiceBuilder.cs
?   ??? MockResourcesServiceBuilder.cs
?   ??? MockCivitaiServiceBuilder.cs
?   ??? MockDanbooruServiceBuilder.cs
?   ??? MockOllamaServiceBuilder.cs
?   ??? MockCacheServiceBuilder.cs
?   ??? MockProgressServiceBuilder.cs
?   ??? MockCsvServiceBuilder.cs
?   ??? MockDynamicPromptsServiceBuilder.cs
?   ??? MockThemeServiceBuilder.cs
?   ??? MockMagickServiceBuilder.cs
?   ??? MockRouterServiceBuilder.cs
?
??? TestFixtures/                       # Test Data
    ??? ImageTestFixtures.cs
    ??? WorkflowTestFixtures.cs
    ??? ResourceTestFixtures.cs
    ??? CivitaiTestFixtures.cs
    ??? TagTestFixtures.cs
    ??? PromptTestFixtures.cs
```

---

## ?? Integration with Main Refactor

### **Before Phase 8 (Component Migration)**
This interface extraction and testing initiative must be **completed** before resuming Phase 8 of the main refactor. This ensures:

1. **Stable Services** - All services tested and validated
2. **Clear Contracts** - Interfaces define expectations
3. **Mockable Dependencies** - Components can be tested in isolation
4. **Confident Migration** - Service stability ensures smooth component migration

### **After Phase D Completion**
1. Update `manager-service-refactor.md` with new test counts
2. Mark Phase 9B as complete
3. Resume Phase 8 (Component Migration) with:
   - Total tests: 420+ passing
   - All services interfaced and tested
   - Integration tests validating cross-service workflows
   - Confidence in service stability

---

## ?? Benefits of This Initiative

### **1. Bulletproof Services**
Every service fully tested with comprehensive unit and integration tests.

### **2. Mockable Architecture**
Easy to test components that depend on services - just mock the interfaces.

### **3. Future-Proof Design**
Easy to swap implementations without breaking changes - interfaces define contracts.

### **4. Living Documentation**
Interfaces serve as clear API contracts and documentation.

### **5. Migration Confidence**
Phase 8 component migration will be smoother with stable, tested services.

### **6. Reduced Technical Debt**
Comprehensive testing prevents bugs and makes refactoring safer.

### **7. Better Code Quality**
Writing tests forces better design and reveals edge cases.

---

## ?? Implementation Notes

### **Testing Best Practices**

1. **Arrange-Act-Assert Pattern**
   ```csharp
   [Fact]
   public async Task MethodName_Condition_ExpectedBehavior()
   {
       // Arrange
       var service = new ServiceUnderTest();
       var input = "test input";
       
       // Act
       var result = await service.Method(input);
       
       // Assert
       result.Should().Be(expectedValue);
   }
   ```

2. **Use FluentAssertions**
   ```csharp
   result.Should().NotBeNull();
   result.Should().BeOfType<ExpectedType>();
   result.Should().HaveCount(5);
   ```

3. **Mock External Dependencies**
   ```csharp
   var mockService = new Mock<IExternalService>();
   mockService.Setup(s => s.Method(It.IsAny<string>()))
              .ReturnsAsync("mocked result");
   ```

4. **Use Test Fixtures for Reusable Data**
   ```csharp
   public static class TestFixtures
   {
       public static Workflow CreateTestWorkflow() => new()
       {
           Id = Guid.NewGuid(),
           Title = "Test Workflow",
           // ...
       };
   }
   ```

### **Interface Design Guidelines**

1. **Keep Interfaces Focused**
   - Single Responsibility Principle
   - Don't expose internal implementation details

2. **Use Async/Await Consistently**
   - All I/O operations should be async
   - Use `Task<T>` return types

3. **Document Intent**
   - Add XML comments to interface methods
   - Explain expected behavior and side effects

4. **Version Carefully**
   - Consider backward compatibility
   - Use optional parameters for new features

---

## ?? Next Steps

### **Immediate Actions**
1. ? Create this plan document
2. ?? Begin Phase A: Critical Service Interfaces
3. ?? Start with `IImageService` interface and tests

### **Weekly Milestones**
- **Week 1:** Complete Phase A (Critical Services)
- **Week 2:** Complete Phase B (External APIs)
- **Week 3:** Complete Phase C (Utility Services)
- **Week 4:** Complete Phase D (Integration Tests)

### **Return to Main Refactor**
After Phase D completion, update main refactor plan and resume Phase 8 with confidence in service stability.

---

## ?? Progress Tracking

### **Weekly Status Updates**
Update this section at the end of each week:

#### **Week 1: [Date] - [Date]**
- [ ] Phase A1: ImageService Interface
- [ ] Phase A2: WorkflowService Interface
- [ ] Phase A3: ResourcesService Interface
- [ ] Tests Added: 0/70
- [ ] Total Tests: 166/236

#### **Week 2: [Date] - [Date]**
- [ ] Phase B1: CivitaiService Interface
- [ ] Phase B2: DanbooruService Interface
- [ ] Phase B3: OllamaService Interface
- [ ] Tests Added: 0/40
- [ ] Total Tests: 236/276

#### **Week 3: [Date] - [Date]**
- [ ] Phase C1: CacheService Interface
- [ ] Phase C2: ProgressService Interface
- [ ] Phase C3: CsvService Interface
- [ ] Phase C4: DynamicPromptsService Interface
- [ ] Phase C5: ThemeService Interface
- [ ] Phase C6: MagickService Interface
- [ ] Phase C7: RouterService Interface
- [ ] Tests Added: 0/75
- [ ] Total Tests: 276/351

#### **Week 4: [Date] - [Date]**
- [ ] Phase D1: DatabaseService Integration Tests
- [ ] Phase D2: Cross-Service Integration Tests
- [ ] Phase D3: External API Integration Tests
- [ ] Tests Added: 0/70
- [ ] Total Tests: 351/421

---

## ?? Related Documentation

- [Manager Service Refactor Plan](./manager-service-refactor.md) - Parent refactoring plan
- [WebUI Deprecation Plan](./WEBUI_DEPRECATION_PLAN.md) - Related cleanup initiative
- [Component Migration Log](./COMPONENT_MIGRATION_LOG.md) - Component migration tracking

---

**Last Updated:** 2025-01-14
**Status:** Ready to begin Phase A
**Next Action:** Create `IImageService` interface
