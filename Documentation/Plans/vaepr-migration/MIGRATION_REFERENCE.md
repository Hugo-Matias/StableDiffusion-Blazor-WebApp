# Vaepr Migration Reference Guide

## Overview

This document maps components from **BlazorWebApp** (current project) to **Vaepr** (new solution), identifying what to migrate, adapt, or discard.

**Key Architectural Decisions:**
- ✅ **No .NET Aspire** - Simple layered architecture for single-user deployment
- ✅ **Fresh Database Migration** - Clean `InitialCreate` migration, no legacy baggage
- ✅ **SQLite** - Zero infrastructure, easy backups, portable

---

## Migration Strategy

| Symbol | Meaning |
|--------|---------|
| ✅ **COPY AS-IS** | Copy directly with minimal/no changes |
| 🔄 **ADAPT** | Copy but requires refactoring/updates |
| 🆕 **CREATE NEW** | Build from scratch in Vaepr |
| ❌ **DISCARD** | Do not migrate (tech debt/deprecated) |

---

## Project Mapping

### Vaepr Solution Structure

```
Vaepr/
├── Vaepr.sln
├── src/
│   ├── Vaepr.Core/              # Domain models, interfaces (no dependencies)
│   ├── Vaepr.Infrastructure/    # ComfyUI/Ollama adapters
│   ├── Vaepr.Application/       # Business logic, workflow builders
│   ├── Vaepr.Data/              # Database (SQLite + EF Core)
│   └── Vaepr.Web/               # Blazor Server UI
├── tests/
│   ├── Vaepr.Core.Tests/
│   ├── Vaepr.Application.Tests/
│   └── Vaepr.Web.Tests/
└── docs/
```

---

## 1. Database Layer (Vaepr.Data)

### 📁 Source: `BlazorWebApp/Data/`

| Current File | Migration Status | Target Location | Notes |
|--------------|------------------|-----------------|-------|
| **Entities/** |
| `Image.cs` | ✅ COPY | `Vaepr.Data/Entities/Image.cs` | Keep all properties (historical data) |
| `Project.cs` | ✅ COPY | `Vaepr.Data/Entities/Project.cs` | Gallery organization |
| `Folder.cs` | ✅ COPY | `Vaepr.Data/Entities/Folder.cs` | Gallery organization |
| `Mode.cs` | ✅ COPY | `Vaepr.Data/Entities/Mode.cs` | Txt2Img/Img2Img/etc. |
| `Sampler.cs` | ✅ COPY | `Vaepr.Data/Entities/Sampler.cs` | Sampler definitions |
| `Prompt.cs` | ✅ COPY | `Vaepr.Data/Entities/Prompt.cs` | Saved prompts |
| `State.cs` | 🔄 ADAPT | `Vaepr.Data/Entities/AppState.cs` | Remove generation-specific state |
| `Selection.cs` | ✅ COPY | `Vaepr.Data/Entities/Selection.cs` | Image selections |
| `Resource.cs` | ✅ COPY | `Vaepr.Data/Entities/Resource.cs` | Models/LoRAs/VAEs |
| `ResourceImage.cs` | ✅ COPY | `Vaepr.Data/Entities/ResourceImage.cs` | Example images for resources |
| `ResourceTemplate.cs` | ✅ COPY | `Vaepr.Data/Entities/ResourceTemplate.cs` | Resource presets |
| `WildcardCollection.cs` | ✅ COPY | `Vaepr.Data/Entities/WildcardCollection.cs` | Wildcard management |
| `WildcardEntry.cs` | ✅ COPY | `Vaepr.Data/Entities/WildcardEntry.cs` | Wildcard entries |
| `SystemPromptTemplate.cs` | ✅ COPY | `Vaepr.Data/Entities/SystemPromptTemplate.cs` | LLM prompt templates |
| `WorkflowState.cs` | ❌ DISCARD | - | Being replaced by C# workflow builders |
| **Context** |
| `AppDbContext.cs` | 🔄 ADAPT | `Vaepr.Data/VaeprDbContext.cs` | Clean context with all DbSets |
| `AppDbContextFactory.cs` | 🔄 ADAPT | `Vaepr.Data/VaeprDbContextFactory.cs` | Update for design-time migrations |
| **Migrations/** | ❌ DISCARD | - | **Create fresh InitialCreate instead** |
| **Enums.cs** | ✅ COPY | `Vaepr.Core/Models/Enums.cs` | Move to Core project |

### Migration Steps

1. **Copy entities** to `Vaepr.Data/Entities/` (update namespaces to `Vaepr.Data.Entities`)
2. **Create `VaeprDbContext.cs`** with all DbSets (see example below)
3. **Update connection string** in `Vaepr.Web/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Data Source=data/vaepr.db"
     }
   }
   ```
4. **Create fresh initial migration**:
   ```powershell
   dotnet ef migrations add InitialCreate --project Vaepr.Data --startup-project Vaepr.Web
   dotnet ef database update --project Vaepr.Data --startup-project Vaepr.Web
   ```
5. **Optional: Create data import script** for users migrating from BlazorWebApp (see below)

### Why Fresh Migration?

| Reason | Impact |
|--------|--------|
| **Clean Schema** | No legacy columns from removed features |
| **No WorkflowState Table** | Old migrations create table we immediately delete |
| **Simpler History** | One `InitialCreate` instead of 30+ migrations |
| **No Scriban Artifacts** | Old state table has generation columns we don't need |
| **Easier Debugging** | New developers see clean schema design |

### Optional: Data Import from Old Database

For users with existing BlazorWebApp data, provide migration script:

```sql
-- Vaepr.Data/Scripts/ImportFromBlazorWebApp.sql

-- Attach old database
ATTACH DATABASE 'path/to/old/blazorwebapp.db' AS old_db;

-- Import images (compatible schema)
INSERT INTO Images 
SELECT * FROM old_db.Images;

-- Import projects
INSERT INTO Projects
SELECT * FROM old_db.Projects;

-- Import folders
INSERT INTO Folders
SELECT * FROM old_db.Folders;

-- Import resources
INSERT INTO Resources
SELECT * FROM old_db.Resources;

-- Import prompts
INSERT INTO Prompts
SELECT * FROM old_db.Prompts;

-- Detach old database
DETACH DATABASE old_db;
```

**Usage:**
```sh
sqlite3 data/vaepr.db < Vaepr.Data/Scripts/ImportFromBlazorWebApp.sql
```

---

## 2. Services Layer

### 📁 Source: `BlazorWebApp/Services/`

| Current File | Migration Status | Target Location | Notes |
|--------------|------------------|-----------------|-------|
| **Keep & Adapt** |
| `ComfyUIService.cs` | 🔄 ADAPT | `Vaepr.Infrastructure/ComfyUI/ComfyUIAdapter.cs` | Rename, implement IBackendAdapter |
| `ComfyUIWebsocketService.cs` | ✅ COPY | `Vaepr.Infrastructure/ComfyUI/ComfyUIWebSocketClient.cs` | Minimal changes |
| `ComfyUIEventBus.cs` | ✅ COPY | `Vaepr.Infrastructure/ComfyUI/ComfyUIEventBus.cs` | Event coordination |
| `DatabaseService.cs` | 🔄 ADAPT | `Vaepr.Data/Repositories/ImageRepository.cs` | Split into repositories |
| `CivitaiService.cs` | ✅ COPY | `Vaepr.Application/Services/CivitaiService.cs` | Resource browsing |
| `DanbooruService.cs` | ✅ COPY | `Vaepr.Application/Services/DanbooruService.cs` | Tag browsing |
| `WildcardService.cs` | ✅ COPY | `Vaepr.Application/Services/WildcardService.cs` | Text expansion |
| `OllamaService.cs` | ✅ COPY | `Vaepr.Infrastructure/Ollama/OllamaAdapter.cs` | LLM integration |
| `TokenizerService.cs` | ✅ COPY | `Vaepr.Application/Services/TokenizerService.cs` | Text analysis |
| `IOService.cs` | ✅ COPY | `Vaepr.Infrastructure/Storage/FileSystemStorage.cs` | File operations |
| `EventService.cs` | ✅ COPY | `Vaepr.Application/Services/EventService.cs` | Pub/sub system |
| `SettingsService.cs` | ✅ COPY | `Vaepr.Application/Services/SettingsService.cs` | App settings |
| `ThemeService.cs` | ✅ COPY | `Vaepr.Web/Services/ThemeService.cs` | UI theming |
| `JavascriptService.cs` | ✅ COPY | `Vaepr.Web/Services/JavascriptService.cs` | JS interop |
| **Refactor/Replace** |
| `WorkflowService.cs` | 🆕 CREATE NEW | `Vaepr.Application/Services/WorkflowService.cs` | See Phase 3 of migration plan |
| `ImageService.cs` | 🆕 CREATE NEW | `Vaepr.Application/Services/GenerationService.cs` | Simplified generation logic |
| `OrchestratorService.cs` | ❌ DISCARD | - | Too complex, logic merged into GenerationService |
| `BackendService.cs` | 🔄 ADAPT | `Vaepr.Application/Services/BackendDiscoveryService.cs` | Auto-detect ComfyUI/Ollama |
| `StateService.cs` | 🔄 ADAPT | `Vaepr.Application/Services/StateService.cs` | Remove generation state |
| `GenerationParameterService.cs` | 🆕 CREATE NEW | `Vaepr.Application/Services/ParameterService.cs` | Simplified parameter management |
| **Discard (Scriban-related)** |
| `WorkflowTemplateParser.cs` | ❌ DISCARD | - | Replaced by C# workflow builders |
| `TemplateCacheService.cs` | ❌ DISCARD | - | No longer needed |
| `FragmentSchemaService.cs` | 🆕 CREATE NEW | `Vaepr.Application/Services/FragmentMetadataService.cs` | Read from C# properties |
| `FragmentConditionValidator.cs` | ❌ DISCARD | - | Use C# conditionals |
| `DynamicPromptsService.cs` | ✅ COPY | `Vaepr.Application/Services/DynamicPromptsService.cs` | Keep as-is |
| `RouterService.cs` | ❌ DISCARD | - | Unnecessary abstraction |
| `ProgressService.cs` | ✅ COPY | `Vaepr.Application/Services/ProgressService.cs` | Progress tracking |
| `MagickService.cs` | ✅ COPY | `Vaepr.Infrastructure/ImageProcessing/MagickService.cs` | Image manipulation |
| `ModelService.cs` | ✅ COPY | `Vaepr.Application/Services/ResourceService.cs` | Model/LoRA management |
| `SessionService.cs` | 🔄 ADAPT | `Vaepr.Application/Services/SessionService.cs` | Remove generation session data |
| `GalleryService.cs` | ✅ COPY | `Vaepr.Application/Services/GalleryService.cs` | Gallery logic |
| `ResourcesService.cs` | ✅ COPY | `Vaepr.Application/Services/ResourceService.cs` | Merge with ModelService |
| `CacheService.cs` | ✅ COPY | `Vaepr.Application/Services/CacheService.cs` | Caching infrastructure |
| `CsvService.cs` | ✅ COPY | `Vaepr.Application/Services/CsvService.cs` | CSV parsing |
| `InfoService.cs` | ✅ COPY | `Vaepr.Application/Services/MetadataService.cs` | Image metadata |

---

## 3. Models

### 📁 Source: `BlazorWebApp/Models/`

| Current File | Migration Status | Target Location | Notes |
|--------------|------------------|-----------------|-------|
| **Generation Models** |
| `GenerationParameters.cs` | 🔄 ADAPT | `Vaepr.Core/Models/Generation/GenerationParameters.cs` | Keep structure, update namespace |
| `GeneratedImages.cs` | ✅ COPY | `Vaepr.Core/Models/Generation/GenerationResult.cs` | Rename for clarity |
| `GeneratedVideo.cs` | ✅ COPY | `Vaepr.Core/Models/Generation/VideoResult.cs` | Video generation result |
| `InferenceProgress.cs` | ✅ COPY | `Vaepr.Core/Models/Generation/InferenceProgress.cs` | Progress tracking |
| **Workflow Models** |
| `Workflow.cs` | 🔄 ADAPT | `Vaepr.Core/Models/Workflows/WorkflowDefinition.cs` | Update to use IWorkflowBuilder |
| `WorkflowMetadata.cs` | 🆕 CREATE NEW | `Vaepr.Core/Models/Workflows/WorkflowMetadata.cs` | See migration plan Phase 1 |
| `ComfyWorkflow.cs` | 🆕 CREATE NEW | `Vaepr.Core/Models/Workflows/ComfyWorkflow.cs` | Result of workflow build |
| **Fragment Models** |
| `FragmentParameters.cs` | ✅ COPY | `Vaepr.Core/Models/Fragments/FragmentParameters.cs` | Keep as-is |
| `FragmentMetadata.cs` | 🆕 CREATE NEW | `Vaepr.Core/Models/Fragments/FragmentMetadata.cs` | See migration plan Phase 1 |
| `FragmentParameter.cs` | 🆕 CREATE NEW | `Vaepr.Core/Models/Fragments/FragmentParameter.cs` | Parameter definition |
| `FragmentKeys.cs` | ✅ COPY | `Vaepr.Core/Models/Fragments/FragmentKeys.cs` | String constants |
| **Asset Models** |
| `SDModel.cs` | ✅ COPY | `Vaepr.Core/Models/Resources/Model.cs` | Rename |
| `Lora.cs` | ✅ COPY | `Vaepr.Core/Models/Resources/Lora.cs` | Keep as-is |
| `Sampler.cs` | ✅ COPY | `Vaepr.Core/Models/Resources/Sampler.cs` | Keep as-is |
| `Scheduler.cs` | ✅ COPY | `Vaepr.Core/Models/Resources/Scheduler.cs` | Keep as-is |
| `Upscaler.cs` | ✅ COPY | `Vaepr.Core/Models/Resources/Upscaler.cs` | Keep as-is |
| `SourceAsset.cs` | ✅ COPY | `Vaepr.Core/Models/Resources/SourceAsset.cs` | Input images |
| **UI Models** |
| `AppState.cs` | 🔄 ADAPT | `Vaepr.Core/Models/UI/AppState.cs` | Remove generation state |
| `ImageEditorState.cs` | ✅ COPY | `Vaepr.Core/Models/UI/ImageEditorState.cs` | Canvas state |
| `BaseProgress.cs` | ✅ COPY | `Vaepr.Core/Models/UI/BaseProgress.cs` | Progress base class |
| `JsonTreeNode.cs` | ✅ COPY | `Vaepr.Core/Models/UI/JsonTreeNode.cs` | JSON viewer |
| **Prompt Models** |
| `PromptStyle.cs` | ✅ COPY | `Vaepr.Core/Models/Prompts/PromptStyle.cs` | Prompt templates |
| `PromptHistoryEntry.cs` | ✅ COPY | `Vaepr.Core/Models/Prompts/PromptHistoryEntry.cs` | History tracking |
| `PromptResource.cs` | ✅ COPY | `Vaepr.Core/Models/Prompts/PromptResource.cs` | Prompt resources |
| `PromptButton.cs` | ✅ COPY | `Vaepr.Core/Models/Prompts/PromptButton.cs` | UI buttons |
| `AppendedTags.cs` | ✅ COPY | `Vaepr.Core/Models/Prompts/AppendedTags.cs` | Tag handling |
| **Metadata Models** |
| `ImageInfo.cs` | ✅ COPY | `Vaepr.Core/Models/Metadata/ImageInfo.cs` | Image metadata |
| **Configuration Models** |
| `AppSettings.cs` | 🔄 ADAPT | `Vaepr.Core/Models/Configuration/AppSettings.cs` | Update for Vaepr |
| `OutputPathsOptions.cs` | ✅ COPY | `Vaepr.Core/Models/Configuration/OutputPathsOptions.cs` | Path configuration |
| `DictionaryTheme.cs` | ✅ COPY | `Vaepr.Core/Models/Configuration/DictionaryTheme.cs` | Theme config |
| `DictionaryWord.cs` | ✅ COPY | `Vaepr.Core/Models/Configuration/DictionaryWord.cs` | Dictionary config |

---

## 4. Workflows (New System)

### 📁 Source: `BlazorWebApp/Workflows/`

| Current | Migration Status | Target Location | Notes |
|---------|------------------|-----------------|-------|
| **Templates/** |
| `flux/txt2img.sbn` | 🆕 CONVERT | `Vaepr.Application/Workflows/Templates/Flux/FluxTxt2ImgWorkflow.cs` | See Phase 2 of migration plan |
| `flux/img2img.sbn` | 🆕 CONVERT | `Vaepr.Application/Workflows/Templates/Flux/FluxImg2ImgWorkflow.cs` | See Phase 5 |
| `wan/img2vid.sbn` | 🆕 CONVERT | `Vaepr.Application/Workflows/Templates/Wan/WanImg2VidWorkflow.cs` | See Phase 6 |
| `qwen/*.sbn` | 🆕 CONVERT | `Vaepr.Application/Workflows/Templates/Qwen/*.cs` | See Phase 7 |
| `sd/*.sbn` | 🆕 CONVERT | `Vaepr.Application/Workflows/Templates/SD/*.cs` | See Phase 8 |
| All other `.sbn` | 🆕 CONVERT | Respective folders | See Phase 8 |
| **Fragments/** |
| All `.sbn` files | 🆕 CONVERT | `Vaepr.Application/Workflows/Fragments/**/*.cs` | See Phases 2-8 |
| **Builders/** |
| `ComfyWorkflowBuilder.cs` | 🆕 CREATE NEW | `Vaepr.Application/Workflows/Builders/ComfyWorkflowBuilder.cs` | See Phase 1 |
| `NodeBuilder.cs` | 🆕 CREATE NEW | `Vaepr.Application/Workflows/Builders/NodeBuilder.cs` | See Phase 1 |
| `NodeRegistry.cs` | 🔄 ADAPT | `Vaepr.Application/Workflows/Builders/NodeRegistry.cs` | Update for type safety |
| **Documentation** |
| `TEMPLATE_GUIDE.md` | 🔄 ADAPT | `docs/workflows/CREATING_WORKFLOWS.md` | Update for C# workflows |
| `FRAGMENT_SCHEMA_GUIDE.md` | 🔄 ADAPT | `docs/workflows/FRAGMENT_GUIDE.md` | Update for C# fragments |

---

## 5. UI Components (Blazor)

### 📁 Source: `BlazorWebApp/Components/`

| Current Component | Migration Status | Target Location | Notes |
|-------------------|------------------|-----------------|-------|
| **Gallery Components** (Keep) |
| `Gallery/ImageCard.razor` | ✅ COPY | `Vaepr.Web/Components/Gallery/ImageCard.razor` | Display images |
| `Gallery/ImageViewer.razor` | ✅ COPY | `Vaepr.Web/Components/Gallery/ImageViewer.razor` | Fullscreen viewer |
| `Gallery/ImageCarousel.razor` | ✅ COPY | `Vaepr.Web/Components/Gallery/ImageCarousel.razor` | Carousel |
| `Gallery/ImagesContainer.razor` | ✅ COPY | `Vaepr.Web/Components/Gallery/ImagesContainer.razor` | Grid layout |
| `Gallery/InfiniteScrollMasonry.razor` | ✅ COPY | `Vaepr.Web/Components/Gallery/InfiniteScrollMasonry.razor` | Infinite scroll |
| `Gallery/GallerySettings.razor` | ✅ COPY | `Vaepr.Web/Components/Gallery/GallerySettings.razor` | Settings panel |
| `Gallery/SelectionCard.razor` | ✅ COPY | `Vaepr.Web/Components/Gallery/SelectionCard.razor` | Selections |
| `Gallery/SelectionsDialog.razor` | ✅ COPY | `Vaepr.Web/Components/Gallery/SelectionsDialog.razor` | Selection dialog |
| `Gallery/CreateSelectionDialog.razor` | ✅ COPY | `Vaepr.Web/Components/Gallery/CreateSelectionDialog.razor` | Create selection |
| **Resource Components** (Keep) |
| `Resources/ResourceCard.razor` | ✅ COPY | `Vaepr.Web/Components/Resources/ResourceCard.razor` | Model cards |
| `Resources/ResourcePanel.razor` | ✅ COPY | `Vaepr.Web/Components/Resources/ResourcePanel.razor` | Resource browser |
| `Resources/CivitaiPanel.razor` | ✅ COPY | `Vaepr.Web/Components/Resources/CivitaiPanel.razor` | CivitAI integration |
| `Resources/CivitaiModelsPanel.razor` | ✅ COPY | `Vaepr.Web/Components/Resources/CivitaiModelsPanel.razor` | Model search |
| `Resources/CivitaiImagesPanel.razor` | ✅ COPY | `Vaepr.Web/Components/Resources/CivitaiImagesPanel.razor` | Example images |
| `Resources/CivitaiImageCard.razor` | ✅ COPY | `Vaepr.Web/Components/Resources/CivitaiImageCard.razor` | Image card |
| `Resources/DanbooruSearchesDrawer.razor` | ✅ COPY | `Vaepr.Web/Components/Resources/DanbooruSearchesDrawer.razor` | Danbooru tags |
| `Resources/ResourceInfoDialog.razor` | ✅ COPY | `Vaepr.Web/Components/Resources/ResourceInfoDialog.razor` | Info dialog |
| **Prompt Components** (Keep) |
| `Prompts/PromptsPanel.razor` | ✅ COPY | `Vaepr.Web/Components/Prompts/PromptsPanel.razor` | Prompt management |
| `Prompts/PromptDialog.razor` | ✅ COPY | `Vaepr.Web/Components/Prompts/PromptDialog.razor` | Edit prompts |
| `Prompts/Styles/*.razor` | ✅ COPY | `Vaepr.Web/Components/Prompts/Styles/` | Style components |
| `Prompts/Wildcards/*.razor` | ✅ COPY | `Vaepr.Web/Components/Prompts/Wildcards/` | Wildcard management |
| `Prompts/LLM/*.razor` | ✅ COPY | `Vaepr.Web/Components/Prompts/LLM/` | LLM tools |
| **Generation Components** (Refactor) |
| `Generation/GenerateButton.razor` | ✅ COPY | `Vaepr.Web/Components/Generation/GenerateButton.razor` | Keep as-is |
| `Generation/PromptsFormNew.razor` | ✅ COPY | `Vaepr.Web/Components/Generation/PromptEditor.razor` | Rename |
| `Generation/WorkflowAssetsPanel.razor` | 🔄 ADAPT | `Vaepr.Web/Components/Generation/WorkflowSelector.razor` | Adapt for C# workflows |
| `Generation/LoraCard.razor` | ✅ COPY | `Vaepr.Web/Components/Generation/LoraCard.razor` | LoRA management |
| `Generation/LoraForm.razor` | ✅ COPY | `Vaepr.Web/Components/Generation/LoraForm.razor` | LoRA form |
| `Generation/GeneratedImageTabs.razor` | ✅ COPY | `Vaepr.Web/Components/Generation/GenerationOutput.razor` | Rename |
| `Generation/DynamicFragmentForm.razor` | 🆕 CREATE NEW | `Vaepr.Web/Components/Workflows/NodeFormRenderer.razor` | Auto-generated forms |
| `Generation/FragmentFormContainer.razor` | ❌ DISCARD | - | Replaced by NodeFormRenderer |
| `Generation/Fragments/*.razor` | 🔄 ADAPT | `Vaepr.Web/Components/Generation/Forms/` | Keep useful forms, discard fragment-specific |
| **Shared Components** (Keep) |
| `Shared/MainLayout.razor` | 🔄 ADAPT | `Vaepr.Web/Components/Shared/MainLayout.razor` | Update branding |
| `Shared/NavBar.razor` | 🔄 ADAPT | `Vaepr.Web/Components/Shared/VaeprAppBar.razor` | Rebrand |
| `Shared/TopToolbar.razor` | 🔄 ADAPT | `Vaepr.Web/Components/Shared/VaeprToolbar.razor` | Rebrand |
| `Shared/LoadingSpinner.razor` | ✅ COPY | `Vaepr.Web/Components/Shared/LoadingSpinner.razor` | Generic component |
| `Shared/ProgressContainer.razor` | ✅ COPY | `Vaepr.Web/Components/Shared/ProgressContainer.razor` | Progress display |
| `Shared/ConfirmationDialog.razor` | ✅ COPY | `Vaepr.Web/Components/Shared/ConfirmationDialog.razor` | Dialogs |
| `Shared/StateDialog.razor` | ✅ COPY | `Vaepr.Web/Components/Shared/StateDialog.razor` | State management |
| `Shared/InfoDrawer.razor` | ✅ COPY | `Vaepr.Web/Components/Shared/InfoDrawer.razor` | Info panel |
| `Shared/AssetViewer.razor` | ✅ COPY | `Vaepr.Web/Components/Shared/AssetViewer.razor` | Asset viewer |
| `Shared/JsonTreeView.razor` | ✅ COPY | `Vaepr.Web/Components/Shared/JsonTreeView.razor` | JSON viewer |
| `Shared/Dropdown.razor` | ✅ COPY | `Vaepr.Web/Components/Shared/Dropdown.razor` | Generic dropdown |
| `Shared/PaginationNav.razor` | ✅ COPY | `Vaepr.Web/Components/Shared/PaginationNav.razor` | Pagination |
| **Video Components** (Keep) |
| `Img2Vid/VideoCard.razor` | ✅ COPY | `Vaepr.Web/Components/Generation/VideoCard.razor` | Video display |
| `Img2Vid/VideoViewer.razor` | ✅ COPY | `Vaepr.Web/Components/Generation/VideoViewer.razor` | Video viewer |
| `Img2Vid/GeneratedVideoTabs.razor` | ✅ COPY | `Vaepr.Web/Components/Generation/VideoOutput.razor` | Rename |
| **Img2Img Components** (Adapt) |
| `Img2Img/Img2ImgCanvas.razor` | 🔄 ADAPT | `Vaepr.Web/Components/Generation/ImageCanvas.razor` | Simplify |
| **Image Editor** (Keep) |
| `ImageEditor/ImageEditorModal.razor` | ✅ COPY | `Vaepr.Web/Components/ImageEditor/ImageEditorModal.razor` | Keep as-is |
| `ImageEditor/LayerPanel.razor` | ✅ COPY | `Vaepr.Web/Components/ImageEditor/LayerPanel.razor` | Keep as-is |

---

## 6. Pages

### 📁 Source: `BlazorWebApp/Pages/`

| Current Page | Migration Status | Target Location | Notes |
|--------------|------------------|-----------------|-------|
| `Generate.razor` | 🔄 ADAPT | `Vaepr.Web/Pages/Generate.razor` | Simplify, remove legacy generation code |
| `Prompts.razor` | ✅ COPY | `Vaepr.Web/Pages/Prompts.razor` | Keep as-is |
| `Resources.razor` | ✅ COPY | `Vaepr.Web/Pages/Resources.razor` | Keep as-is |
| `Danbooru.razor` | ✅ COPY | `Vaepr.Web/Pages/Danbooru.razor` | Keep as-is |
| `Index.razor` | 🔄 ADAPT | `Vaepr.Web/Pages/Index.razor` | Gallery page (rebrand) |
| `Error.cshtml` | ✅ COPY | `Vaepr.Web/Pages/Error.cshtml` | Keep as-is |
| `_Host.cshtml` | ✅ COPY | `Vaepr.Web/Pages/_Host.cshtml` | Keep as-is |
| `_Layout.cshtml` | 🔄 ADAPT | `Vaepr.Web/Pages/_Layout.cshtml` | Update branding |

---

## 7. Configuration & Assets

### 📁 Source: `BlazorWebApp/`

| Current File | Migration Status | Target Location | Notes |
|--------------|------------------|-----------------|-------|
| `appsettings.json` | 🔄 ADAPT | `Vaepr.Web/appsettings.json` | Update for SQLite, remove legacy settings |
| `Program.cs` | 🆕 CREATE NEW | `Vaepr.Web/Program.cs` | Rebuild with new service registrations |
| `_Imports.razor` | 🔄 ADAPT | `Vaepr.Web/_Imports.razor` | Update namespaces |
| `wwwroot/` | ✅ COPY | `Vaepr.Web/wwwroot/` | CSS, JS, images |
| `wwwroot/css/` | ✅ COPY | `Vaepr.Web/wwwroot/css/` | Custom styles |
| `wwwroot/js/` | ✅ COPY | `Vaepr.Web/wwwroot/js/` | JavaScript |

---

## 8. Extensions

### 📁 Source: `BlazorWebApp/Extensions/`

| Current File | Migration Status | Target Location | Notes |
|--------------|------------------|-----------------|-------|
| `Parser.cs` | 🔄 ADAPT | `Vaepr.Application/Extensions/ParserExtensions.cs` | Remove Scriban-specific methods |
| `FragmentParametersExtensions.cs` | ✅ COPY | `Vaepr.Application/Extensions/FragmentParametersExtensions.cs` | Type-safe accessors (Phase 1.5) |
| `Generics.cs` | ✅ COPY | `Vaepr.Core/Extensions/GenericExtensions.cs` | Generic helpers |
| `StateNormalizer.cs` | 🔄 ADAPT | `Vaepr.Application/Extensions/StateNormalizer.cs` | Remove generation normalization |

---

## 9. DTOs (Data Transfer Objects)

### 📁 Source: `BlazorWebApp/Data/Dtos/`

| Current File | Migration Status | Target Location | Notes |
|--------------|------------------|-----------------|-------|
| **ComfyUI DTOs** |
| `ComfyUI/ComfyUIPromptResponse.cs` | ✅ COPY | `Vaepr.Infrastructure/ComfyUI/Dtos/PromptResponse.cs` | Keep as-is |
| `ComfyUI/ComfyUIPromptSubmitResponse.cs` | ✅ COPY | `Vaepr.Infrastructure/ComfyUI/Dtos/SubmitResponse.cs` | Keep as-is |
| `ComfyUI/ComfyUIHistoryImageResponse.cs` | ✅ COPY | `Vaepr.Infrastructure/ComfyUI/Dtos/HistoryImageResponse.cs` | Keep as-is |
| `ComfyUI/Health.cs` | ✅ COPY | `Vaepr.Infrastructure/ComfyUI/Dtos/Health.cs` | Keep as-is |
| `ComfyUI/LLMTextGeneration.cs` | ✅ COPY | `Vaepr.Infrastructure/ComfyUI/Dtos/LLMResponse.cs` | Rename |
| **CivitAI DTOs** |
| `CivitaiModelsDto.cs` | ✅ COPY | `Vaepr.Application/Dtos/CivitAI/ModelsDto.cs` | Keep as-is |
| `CivitaiModelDto.cs` | ✅ COPY | `Vaepr.Application/Dtos/CivitAI/ModelDto.cs` | Keep as-is |
| `CivitaiImagesDto.cs` | ✅ COPY | `Vaepr.Application/Dtos/CivitAI/ImagesDto.cs` | Keep as-is |
| `CivitaiImageDto.cs` | ✅ COPY | `Vaepr.Application/Dtos/CivitAI/ImageDto.cs` | Keep as-is |
| All other Civitai DTOs | ✅ COPY | `Vaepr.Application/Dtos/CivitAI/` | Keep as-is |
| **Ollama DTOs** |
| `Ollama/*.cs` | ✅ COPY | `Vaepr.Infrastructure/Ollama/Dtos/` | Keep as-is |
| **Other DTOs** |
| `ImagesDto.cs` | ✅ COPY | `Vaepr.Application/Dtos/ImagesDto.cs` | Pagination |
| `WildcardCollectionDto.cs` | ✅ COPY | `Vaepr.Application/Dtos/WildcardCollectionDto.cs` | Keep as-is |
| `WildcardEntryDto.cs` | ✅ COPY | `Vaepr.Application/Dtos/WildcardEntryDto.cs` | Keep as-is |
| `DanbooruPost.cs` | ✅ COPY | `Vaepr.Application/Dtos/DanbooruPost.cs` | Keep as-is |
| `PaginatedDto.cs` | ✅ COPY | `Vaepr.Application/Dtos/PaginatedDto.cs` | Generic pagination |

---

## 10. Attributes & Metadata

### 📁 Source: `BlazorWebApp/Attributes/`

| Current File | Migration Status | Target Location | Notes |
|--------------|------------------|-----------------|-------|
| `FragmentComponentAttribute.cs` | 🔄 ADAPT | `Vaepr.Core/Attributes/FragmentComponentAttribute.cs` | Update for new system |

---

## 11. Events

### 📁 Source: `BlazorWebApp/Events/`

| Current File | Migration Status | Target Location | Notes |
|--------------|------------------|-----------------|-------|
| `ImagesGeneratedEventArgs.cs` | ✅ COPY | `Vaepr.Core/Events/ImagesGeneratedEventArgs.cs` | Keep as-is |
| `ProgressChangedEventArgs.cs` | ✅ COPY | `Vaepr.Core/Events/ProgressChangedEventArgs.cs` | Keep as-is |
| `StateChangedEventArgs.cs` | ✅ COPY | `Vaepr.Core/Events/StateChangedEventArgs.cs` | Keep as-is |
| `WorkflowChangedEventArgs.cs` | ✅ COPY | `Vaepr.Core/Events/WorkflowChangedEventArgs.cs` | Keep as-is |
| `ResourcesChangedEventArgs.cs` | ✅ COPY | `Vaepr.Core/Events/ResourcesChangedEventArgs.cs` | Keep as-is |
| `ModelsChangedEventArgs.cs` | ✅ COPY | `Vaepr.Core/Events/ModelsChangedEventArgs.cs` | Keep as-is |
| `SelectedImagesChangedEventArgs.cs` | ✅ COPY | `Vaepr.Core/Events/SelectedImagesChangedEventArgs.cs` | Keep as-is |
| All other event args | ✅ COPY | `Vaepr.Core/Events/` | Keep as-is |

---

## 12. What NOT to Migrate

### ❌ Discard List

| Component | Reason |
|-----------|--------|
| **Scriban Templates** (`Workflows/Templates/**/*.sbn`) | Replaced by C# workflow builders |
| **Scriban Fragments** (`Workflows/Fragments/**/*.sbn`) | Replaced by C# fragment classes |
| **TemplateCacheService.cs** | No longer needed without Scriban |
| **WorkflowTemplateParser.cs** | Scriban-specific parsing |
| **FragmentConditionValidator.cs** | Use C# conditionals instead |
| **OrchestratorService.cs** | Over-engineered, logic moved to GenerationService |
| **RouterService.cs** | Unnecessary abstraction |
| **WorkflowState entity** | Being replaced by C# builders |
| **Legacy migration branches** | Clean slate approach |

---

## Migration Workflow

### Phase-by-Phase Approach

Follow the **workflow-fluent-api-migration plan** (MAIN_PLAN.md):

1. ✅ **Phase 0: Setup Vaepr Solution** (You start this in Visual Studio)
2. ✅ **Phase 1: Core Infrastructure** (Create interfaces, builders)
3. ✅ **Phase 1.5: Type-Safe Enhancements** (Add typed outputs, extension methods)
4. ✅ **Phase 2: Proof of Concept** (Convert Flux Txt2Img workflow)
5. ✅ **Phase 3: Service Layer Refactoring** (Remove Scriban dependencies)
6. ... Continue through Phase 10

### Copy Strategy

For files marked **✅ COPY AS-IS**:
1. Copy file directly to target location
2. Update namespace to match Vaepr project
3. Run build, fix any dependency issues
4. No functional changes

For files marked **🔄 ADAPT**:
1. Copy file to target location
2. Update namespace
3. Apply specific adaptations noted in table
4. Test functionality
5. Document changes

For files marked **🆕 CREATE NEW**:
1. Build from scratch following migration plan
2. Use current implementation as reference only
3. Write unit tests first (TDD)

---

## Testing Strategy

### Unit Tests to Copy

From `BlazorWebApp.Tests/`:
- Copy database tests (entities work the same)
- Copy service tests (adapt for new services)
- Discard Scriban-related tests

### New Tests to Create

- Fragment builders (Phase 2)
- Workflow builders (Phase 2)
- `ComfyWorkflowBuilder` (Phase 1)
- `NodeRegistry` type safety (Phase 1.5)
- Service layer refactorings (Phase 3)

---

## Dependencies to Migrate

### NuGet Packages to Keep

```xml
<!-- UI Framework -->
<PackageReference Include="MudBlazor" Version="7.12.1" />

<!-- Database -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.8" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.8" />

<!-- Image Processing -->
<PackageReference Include="Magick.NET-Q16-AnyCPU" Version="14.2.0" />

<!-- Testing -->
<PackageReference Include="xUnit" Version="2.9.2" />
<PackageReference Include="bUnit" Version="1.32.7" />
```

### NuGet Packages to REMOVE

```xml
<!-- Templating (being replaced) -->
<PackageReference Include="Scriban" Version="..." />
```

---

## Namespace Mapping

| Old Namespace | New Namespace |
|---------------|---------------|
| `BlazorWebApp.Data.Entities` | `Vaepr.Data.Entities` |
| `BlazorWebApp.Services` | `Vaepr.Application.Services` (or `Vaepr.Infrastructure`) |
| `BlazorWebApp.Models` | `Vaepr.Core.Models` |
| `BlazorWebApp.Components` | `Vaepr.Web.Components` |
| `BlazorWebApp.Pages` | `Vaepr.Web.Pages` |
| `BlazorWebApp.Events` | `Vaepr.Core.Events` |
| `BlazorWebApp.Extensions` | `Vaepr.Application.Extensions` |
| `BlazorWebApp.Workflows` | `Vaepr.Application.Workflows` |

---

## Checkpoints

After each major section migration:

- [ ] Database layer copied → Run migrations → Verify DB works
- [ ] Services copied → Register in DI → Verify injection
- [ ] Models copied → Update namespaces → Build succeeds
- [ ] Components copied → Update imports → UI renders
- [ ] Phase 1 complete → Unit tests pass → Commit
- [ ] Phase 2 complete → Flux workflow works → Commit

---

## Notes & Gotchas

### SQLite Differences from Current
- Connection string format: `Data Source=data/vaepr.db`
- No server required (single file)
- EF Core migrations work identically

### Service Registration Changes
- Old: `services.AddScoped<IOrchestratorService, OrchestratorService>()`
- New: `services.AddScoped<IGenerationService, GenerationService>()`

### Workflow Discovery
- Old: Scan `Workflows/Templates/` for `.sbn` files
- New: Reflection to find `IWorkflowBuilder` implementations

### Fragment Metadata
- Old: Parse `#meta` JSON blocks from `.sbn` files
- New: Read from `IFragmentBuilder.Metadata` property

---

## Success Criteria

Migration is complete when:

1. ✅ Zero `.sbn` files remain in `Vaepr` codebase
2. ✅ Zero Scriban NuGet package references
3. ✅ All workflows generate valid ComfyUI JSON
4. ✅ All workflows execute successfully in ComfyUI
5. ✅ UI renders correctly with MudBlazor
6. ✅ Database migrations apply cleanly
7. ✅ All unit tests pass (90%+ coverage)
8. ✅ Docker deployment works end-to-end
9. ✅ No compilation warnings or errors
10. ✅ Documentation updated for new system

---

## Quick Reference Commands

### Create New Vaepr Solution
```powershell
dotnet new sln -n Vaepr
dotnet new classlib -n Vaepr.Core -o src/Vaepr.Core
dotnet new classlib -n Vaepr.Infrastructure -o src/Vaepr.Infrastructure
dotnet new classlib -n Vaepr.Application -o src/Vaepr.Application
dotnet new classlib -n Vaepr.Data -o src/Vaepr.Data
dotnet new blazorserver -n Vaepr.Web -o src/Vaepr.Web

dotnet sln add src/**/*.csproj
```

### Add Project References
```powershell
dotnet add src/Vaepr.Infrastructure reference src/Vaepr.Core
dotnet add src/Vaepr.Application reference src/Vaepr.Core
dotnet add src/Vaepr.Application reference src/Vaepr.Infrastructure
dotnet add src/Vaepr.Data reference src/Vaepr.Core
dotnet add src/Vaepr.Web reference src/Vaepr.Core
dotnet add src/Vaepr.Web reference src/Vaepr.Application
dotnet add src/Vaepr.Web reference src/Vaepr.Data
dotnet add src/Vaepr.Web reference src/Vaepr.Infrastructure
```

### Create Migration
```powershell
dotnet ef migrations add InitialCreate --project src/Vaepr.Data --startup-project src/Vaepr.Web
dotnet ef database update --project src/Vaepr.Data --startup-project src/Vaepr.Web
```

---

**Last Updated:** 2024 (Initial creation)
**Migration Plan:** See `Documentation/Plans/workflow-fluent-api-migration/MAIN_PLAN.md`
