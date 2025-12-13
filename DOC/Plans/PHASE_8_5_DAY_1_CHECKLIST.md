# Phase 8.5 - Day 1: Event System Migration Checklist

## ?? Day 1 Summary
**Duration:** 4-6 hours  
**Goal:** Remove all Action events, complete EventService migration  
**Status:** ? Not Started

---

## ? Tasks

### 1.1 Audit Action Event Usage (1 hour)

- [ ] **Search for Action event subscriptions**
  ```bash
  # Search for M.On* subscriptions in components
  grep -r "M\.On" BlazorWebApp/Components/
  grep -r "M\.On" BlazorWebApp/Pages/
  ```

- [ ] **Create spreadsheet of events**
  - [ ] List all Action events in ManagerService
  - [ ] Document which components subscribe to each
  - [ ] Note which events are still actively used
  - [ ] Identify events that can be safely removed

- [ ] **Action Events to Audit:**
  - OnSDModelsChange
  - OnOptionsChange
  - OnStyleChange
  - OnConverging
  - OnFolderChange
  - OnProjectsChange
  - OnProjectChange
  - OnProjectChangeTask
  - OnStateHasChanged
  - OnProgressChanged
  - OnDownloadCompleted
  - OnComfyUIStateChanged
  - OnAppStateChanged
  - OnTxt2ImgParametersChanged
  - OnImg2ImgParametersChanged
  - OnUpscaleParametersChanged
  - OnImg2VidParametersChanged
  - OnSelectedImagesChanged
  - OnRefreshImagesContainer
  - OnCanvasImageDataChanged
  - OnImg2VidInputImageChanged
  - OnImg2ImgInputImageChanged
  - OnResourcesStateChanged
  - OnWorkflowBaseChanged
  - OnImageEditorStateChanged
  - OnSamplersSchedulersChanged
  - OnCurrentWorkflowChanged
  - OnCurrentWorkflowChangedAsync
  - OnSessionVideosChanged

---

### 1.2 Create Missing EventArgs (1 hour)

- [ ] **Create OptionsChangedEventArgs**
  ```csharp
  // File: BlazorWebApp/Events/OptionsChangedEventArgs.cs
  namespace BlazorWebApp.Events
  {
      public class OptionsChangedEventArgs
      {
          // Add relevant properties if needed
      }
  }
  ```

- [ ] **Create WorkflowChangedEventArgs**
  ```csharp
  // File: BlazorWebApp/Events/WorkflowChangedEventArgs.cs
  namespace BlazorWebApp.Events
  {
      public class WorkflowChangedEventArgs
      {
          public Guid? WorkflowId { get; set; }
          public string? ChangeType { get; set; } // "Set", "Reset", etc.
      }
  }
  ```

- [ ] **Create SamplersSchedulersChangedEventArgs**
  ```csharp
  // File: BlazorWebApp/Events/SamplersSchedulersChangedEventArgs.cs
  namespace BlazorWebApp.Events
  {
      public class SamplersSchedulersChangedEventArgs
      {
          // Add relevant properties if needed
      }
  }
  ```

- [ ] **Verify all EventArgs classes exist:**
  - [?] StateChangedEventArgs (exists)
  - [?] ModelChangedEventArgs (exists)
  - [?] BackendAvailabilityChangedEventArgs (exists)
  - [?] FolderChangedEventArgs (exists)
  - [?] ProjectChangedEventArgs (exists)
  - [?] ImageSelectionChangedEventArgs (exists)
  - [?] CanvasImageDataChangedEventArgs (exists)
  - [?] Img2ImgInputImageChangedEventArgs (exists)
  - [?] Img2VidInputImageChangedEventArgs (exists)
  - [?] ImageEditorStateChangedEventArgs (exists)
  - [?] SessionVideosChangedEventArgs (exists)
  - [?] ConvergingChangedEventArgs (exists)
  - [?] StylesChangedEventArgs (exists)
  - [?] ResourcesChangedEventArgs (exists)
  - [?] ParametersChangedEventArgs (exists)
  - [?] InputImageChangedEventArgs (exists)
  - [?] SelectedImagesChangedEventArgs (exists)
  - [?] ProgressChangedEventArgs (exists)
  - [?] DownloadCompletedEventArgs (exists)
  - [?] OptionsChangedEventArgs (to create)
  - [?] WorkflowChangedEventArgs (to create)
  - [?] SamplersSchedulersChangedEventArgs (to create)

---

### 1.3 Migrate Orchestration Methods to EventService (2-3 hours)

- [ ] **GetWorkflowModels()**
  ```csharp
  // File: BlazorWebApp/Services/ManagerService.cs
  public async Task GetWorkflowModels(bool refresh = false)
  {
      await _models.GetWorkflowModels(refresh);
      
      // OLD: OnSDModelsChange?.Invoke();
      // NEW:
      _events.Publish(new ModelsChangedEventArgs());
  }
  ```

- [ ] **GetOptions()**
  ```csharp
  public async Task GetOptions()
  {
      await _backend.GetOptions();
      Options = _backend.Options;
      
      // OLD: OnOptionsChange?.Invoke();
      // NEW:
      _events.Publish(new OptionsChangedEventArgs());
  }
  ```

- [ ] **GetStyles()**
  ```csharp
  public async Task GetStyles()
  {
      Styles = new();
      var promptResources = await _db.GetPrompts();
      foreach (var prompt in promptResources)
      {
          Styles.Add(new(prompt));
      }
      
      if (State.Generation.Styles == null) State.Generation.Styles = new List<PromptStyle>();
      else
      {
          var currentStyles = State.Generation.Styles.ToList();
          State.Generation.Styles = Styles.Where(s => currentStyles.Any(cs => cs.Name == s.Name));
      }
      
      // Already uses StylesChangedEventArgs ?
      _events.Publish(new StylesChangedEventArgs { ChangeType = "Loaded" });
  }
  ```

- [ ] **SetCurrentWorkflow()**
  ```csharp
  public void SetCurrentWorkflow(Guid workflowId, ModeType? mode = null)
  {
      var workflow = GetWorkflowById(workflowId);
      if (workflow == null) return;

      State.Generation.CurrentWorkflowId = workflowId;
      State.Generation.WorkflowBase = workflow.Base;

      // OLD: OnWorkflowBaseChanged?.Invoke();
      // OLD: OnCurrentWorkflowChanged?.Invoke();
      
      // NEW:
      _events.Publish(new StateChangedEventArgs());
      _events.Publish(new WorkflowChangedEventArgs 
      { 
          WorkflowId = workflowId, 
          ChangeType = "Set" 
      });
  }
  ```

- [ ] **SetCurrentWorkflowAsync()**
  ```csharp
  public async Task<bool> SetCurrentWorkflowAsync(Guid workflowId, IAssetResolverService assetResolver, ModeType? mode = null)
  {
      var workflow = GetWorkflowById(workflowId);
      if (workflow == null) return false;

      State.Generation.CurrentWorkflowId = workflowId;
      State.Generation.WorkflowBase = workflow.Base;

      bool assetsInitialized = true;
      if (workflow.Assets != null && workflow.Assets.Count > 0)
      {
          var assets = GetOrCreateWorkflowAssetsForMode(mode);
          assetsInitialized = await assetResolver.InitializeWorkflowAssets(workflow, assets);
      }

      await GetWorkflowModels();

      // OLD: OnWorkflowBaseChanged?.Invoke();
      // OLD: OnCurrentWorkflowChanged?.Invoke();
      // OLD: if (OnCurrentWorkflowChangedAsync != null) await OnCurrentWorkflowChangedAsync.Invoke(workflow);
      
      // NEW:
      _events.Publish(new StateChangedEventArgs());
      _events.Publish(new WorkflowChangedEventArgs 
      { 
          WorkflowId = workflowId, 
          ChangeType = "SetAsync" 
      });

      await SaveState();
      return assetsInitialized;
  }
  ```

- [ ] **LoadBackendDependentResources()**
  ```csharp
  public async Task LoadBackendDependentResources()
  {
      await _backend.LoadBackendDependentResources();
      await _models.GetADetailerModels();
      
      // OLD: OnSamplersSchedulersChanged?.Invoke();
      // NEW:
      _events.Publish(new SamplersSchedulersChangedEventArgs());
  }
  ```

- [ ] **SetCurrentFolder()** - Already delegates to GalleryService ?

- [ ] **SetCurrentProject()** - Already delegates to GalleryService ?

---

### 1.4 Remove Action Event Declarations (30 min)

- [ ] **Remove from ManagerService.cs:**
  ```csharp
  // DELETE ALL OF THESE:
  public event Action OnSDModelsChange;
  public event Action OnOptionsChange;
  public event Action OnStyleChange;
  public event Action OnConverging;
  public event Action OnFolderChange;
  public event Action OnProjectsChange;
  public event Action OnProjectChange;
  public event Func<Task> OnProjectChangeTask;
  public event Action OnStateHasChanged;
  public event Action OnProgressChanged;
  public event Action OnDownloadCompleted;
  public event Action OnComfyUIStateChanged;
  public event Action OnAppStateChanged;
  public event Action OnTxt2ImgParametersChanged;
  public event Action OnImg2ImgParametersChanged;
  public event Action OnUpscaleParametersChanged;
  public event Action OnImg2VidParametersChanged;
  public event Action OnSelectedImagesChanged;
  public event Action OnRefreshImagesContainer;
  public event Action OnCanvasImageDataChanged;
  public event Action OnImg2VidInputImageChanged;
  public event Action OnImg2ImgInputImageChanged;
  public event Action OnResourcesStateChanged;
  public event Action OnWorkflowBaseChanged;
  public event Action? OnImageEditorStateChanged;
  public event Action? OnSamplersSchedulersChanged;
  public event Action OnCurrentWorkflowChanged;
  public event Func<Workflow?, Task>? OnCurrentWorkflowChangedAsync;
  public event Action OnSessionVideosChanged;
  ```

- [ ] **Remove Invoke methods:**
  ```csharp
  // DELETE THESE METHODS:
  public void InvokeDownloadComplete()
  public void InvokeRefreshImagesContainer()
  public void InvokeResourcesStateChanged()
  public void InvokeProgressChanged()
  public void InvokeParametersChanged(bool isImg2Img)
  public void InvokeSessionVideosChanged()
  ```

- [ ] **Update any components still subscribing**
  - Search for `M.On*` in components
  - Update to use EventService.Subscribe
  - Verify Dispose methods unsubscribe correctly

---

## ?? Verification

- [ ] **Build passes:**
  ```bash
  dotnet build
  ```

- [ ] **No compiler warnings about unused events**

- [ ] **Search for remaining Action event usage:**
  ```bash
  grep -r "public event Action" BlazorWebApp/Services/ManagerService.cs
  # Should return 0 results
  ```

- [ ] **All orchestration methods use EventService:**
  ```bash
  grep -r "\.Invoke()" BlazorWebApp/Services/ManagerService.cs
  # Should return 0 results (except for nullable checks)
  ```

---

## ?? Success Criteria

- [x] All Action events removed from ManagerService (0 remaining)
- [x] All orchestration methods use EventService.Publish()
- [x] Missing EventArgs classes created (3 new classes)
- [x] Build passes without errors
- [x] No compiler warnings

---

## ?? Next Steps

After Day 1 complete:
- Proceed to Day 2: Orchestration Method Extraction
- Commit changes: `git commit -m "Phase 8.5 Day 1: Remove Action events, complete EventService migration"`

---

**Last Updated:** 2025-01-14  
**Status:** ? Not Started  
**Estimated Duration:** 4-6 hours
