using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Converters;
using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Extensions;
using MudBlazor;
using System.Text.Json;
using System.Text.Json.Serialization;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Models
{
    public class AppState
    {

        public bool IsDarkMode { get; set; }
        public string CurrentTheme { get; set; } = "Default";
        public AppStateGeneration Generation { get; set; } = new();
        public AppStateGallery Gallery { get; set; } = new();
        public AppStatePrompts Prompts { get; set; } = new();
        public AppStateResources Resources { get; set; } = new();
        public AppStateCivitai Civitai { get; set; } = new();
        public AppStateDanbooru Danbooru { get; set; } = new();
        public AppStateScripts Scripts { get; set; } = new();

        public AppState() { }
        public AppState(AppSettings settings)
        {
            IsDarkMode = settings.IsDarkMode;
            Generation = new()
            {
                RandomImagesAmount = settings.Generation.RandomImages.Value,
                RandomImagesSource = settings.Generation.RandomImages.Source,
                Img2Img = new()
                {
                    BrushSize = settings.Generation.Img2Img.Brush.Value,
                    BrushColor = settings.Generation.Img2Img.Brush.Color,
                    BrushOutlineColor = settings.Generation.Img2Img.Brush.PointerOutline,
                    Mode = settings.Generation.Img2Img.Mode,
                    DownsizeInput = settings.Generation.Img2Img.DownsizeInput,
                    MaxInputWidth = settings.Generation.Img2Img.InputResolution.Width,
                    MaxInputHeight = settings.Generation.Img2Img.InputResolution.Height,
                },
                Autocomplete = new()
                {
                    IsEnabled = true,
                    EnableFuzzySearch = true
                },
                LLM = new()
                {
                    Options = new()
                    {
                        Seed = settings.Generation.Shared.LLMEnhancer.Seed.Value,
                        Temperature = settings.Generation.Shared.LLMEnhancer.Temperature.Value,
                        TopK = settings.Generation.Shared.LLMEnhancer.TopK.Value,
                        TopP = settings.Generation.Shared.LLMEnhancer.TopP.Value,
                        MinP = settings.Generation.Shared.LLMEnhancer.MinP.Value,
                        NumCtx = settings.Generation.Shared.LLMEnhancer.NumCtx.Value,
                        NumPredict = settings.Generation.Shared.LLMEnhancer.NumPredict.Value
                    }
                }
            };
            Resources = new()
            {
                Limit = settings.Resources.Search.Limit.Value,
                LoadTriggerWords = settings.Resources.LoadTriggerWords,
                Weight = settings.Resources.Weight.Value,
                OrderBy = settings.Resources.OrderByOptions[0],
                OrderByDescending = settings.Resources.OrderByDescending
            };
            Prompts = new()
            {
                Wildcards = new()
                {
                    GenerationAmount = settings.Prompts.Wildcards.Generation.Value
                }
            };
        }
    }

    public class AppStateGeneration
    {
        public int RandomImagesAmount { get; set; }
        public string RandomImagesSource { get; set; }
        public AppStateGenerationImg2Img Img2Img { get; set; }
        public IEnumerable<PromptStyle> Styles { get; set; } = new List<PromptStyle>();
        public long Seed { get; set; }
        public bool IsInterrupted { get; set; } = false;
        public List<Workflow> Workflows { get; set; }
        public ModelBase WorkflowBase { get; set; }
        public Guid? CurrentWorkflowId { get; set; }
        /// <summary>
        /// Last-selected workflow id per base. Used by the global Generate nav button
        /// to restore the user's previous selection when returning to the generation page,
        /// and when switching bases to prefer the previously-used workflow under that base.
        /// </summary>
        public Dictionary<ModelBase, Guid> LastWorkflowByBase { get; set; } = new();
        public List<Lora> Loras { get; set; }
        public AppStateLLMEnhancer LLM { get; set; } = new();
        public AppStateGenerationAutocomplete Autocomplete { get; set; } = new();

        /// <summary>
        /// Page size for the session-wide Results gallery on the Generate page.
        /// Persisted with the rest of the app state.
        /// </summary>
        public int ResultsPageSize { get; set; } = 12;

        // Legacy properties - kept for state migration, will be removed in future versions
        [Obsolete("Use ParametersTxt2Img.Model or ParametersImg2Img.Model instead")]
        public string? SDModel { get; set; }
        [Obsolete("Use ParametersTxt2Img.Vae or ParametersImg2Img.Vae instead")]
        public string? Vae { get; set; }
    }

    public class AppStateGenerationImg2Img
    {
        private int _brushSize;
        private string _brushColor;

        public event Action OnBrushSizeChange;
        public event Action OnBrushColorChange;

        public int BrushSize
        {
            get => _brushSize; set
            {
                _brushSize = value;
                OnBrushSizeChange?.Invoke();
            }
        }
        public string BrushColor
        {
            get => _brushColor; set
            {
                _brushColor = value;
                OnBrushColorChange?.Invoke();
            }
        }
        public string BrushOutlineColor { get; set; }
        public string Mode { get; set; }
        public bool DownsizeInput { get; set; }
        public int MaxInputWidth { get; set; }
        public int MaxInputHeight { get; set; }
    }

    public class AppStateLLMEnhancer
    {
        public string Prompt { get; set; } = string.Empty;
        public string NegativePrompt { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public string NegativeInstructions { get; set; } = string.Empty;
        public string EnhancedPrompt { get; set; } = string.Empty;
        public string EnhancedNegativePrompt { get; set; } = string.Empty;
        public string LastPromptId { get; set; } = string.Empty;
        public string LastNegativePromptId { get; set; } = string.Empty;
        public AppStateOllamaOptions Options { get; set; } = new();
    }

    public class AppStateOllamaOptions
    {
        public int Seed { get; set; } = -1;
        public float Temperature { get; set; } = 1.0f;
        public int TopK { get; set; } = 50;
        public float TopP { get; set; } = 0.9f;
        public float MinP { get; set; } = 0.05f;
        public int NumCtx { get; set; } = 8192;
        public int NumPredict { get; set; } = 500;

        public OllamaOptions ToOllamaOptions()
        {
            return new OllamaOptions
            {
                Seed = Seed >= 0 ? Seed : null,
                Temperature = Temperature,
                TopK = TopK,
                TopP = TopP,
                NumPredict = NumPredict,
                MinP = MinP > 0 ? MinP : null,
                NumCtx = NumCtx > 0 ? NumCtx : null
            };
        }
    }

    public class AppStateGenerationAutocomplete
    {
        public bool IsEnabled { get; set; } = true;
        public bool EnableFuzzySearch { get; set; } = true;
    }

    public class AppStateGallery
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public int FolderId { get; set; } = 0;
        public string FolderName { get; set; } = string.Empty;
        public string Prompt { get; set; } = "";
        public string NegativePrompt { get; set; } = "";
        public GalleryOrderBy OrderBy { get; set; } = GalleryOrderBy.Date;
        public bool OrderDescending { get; set; } = true;
        public bool GalleriesOrderDescending { get; set; } = true;
        public int PageSize { get; set; } = 10;
        public ModeType Mode { get; set; } = ModeType.Txt2Img;
        public bool IsFavoritesOnly { get; set; } = false;
        public bool IsModeTxt2Img { get; set; } = true;
        public bool IsModeImg2Img { get; set; } = true;
        public bool IsModeUpscale { get; set; } = true;
        public bool IsModeImg2Vid { get; set; } = true;
        public DateRange DateRange { get; set; } = new(DateTime.Now.Date, DateTime.Now.Date);
        public bool FilterByDateRange { get; set; } = false;
        public bool IsSelectedOnly { get; set; } = false;
        public int Score { get; set; } = 0;
        public bool IsScore { get; set; } = false;
        public bool UseInfiniteScroll { get; set; } = true;

        /// <summary>
        /// Seed for consistent random ordering across page navigations.
        /// Reset when filters are applied or OrderBy changes.
        /// </summary>
        public int? RandomSeed { get; set; }
    }

    public enum GalleryOrderBy { Random, Date, Sampler, Seed, Steps, CfgScale, Width, Height, Favorite, Mode, Denoising }

    public enum ResourceCardSize { Small, Medium, Large }

    public class AppStateResources
    {
        public int ActiveTabIndex { get; set; } = 0;
        public bool SidebarCollapsed { get; set; } = false;
        public ResourceCardSize CardSize { get; set; } = ResourceCardSize.Medium;
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int Limit { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subtype { get; set; } = string.Empty;
        public string BaseModel { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public bool IsInclusive { get; set; } = true;
        public bool LoadTriggerWords { get; set; }
        public float Weight { get; set; }
        [PreserveNull]
        public bool? ResourceIsEnabledFilter { get; set; }
        public string OrderBy { get; set; }
        public bool OrderByDescending { get; set; }
    }

    public class AppStateCivitai
    {
        public int ActiveTabIndex { get; set; } = 0;
        public bool SidebarCollapsed { get; set; } = false;
        public ResourceCardSize CardSize { get; set; } = ResourceCardSize.Medium;
        public string ResourceSubtype { get; set; }
        public string ResourceTypeOverride { get; set; } = "Checkpoint";
        public AppStateCivitaiCreators Creators { get; set; } = new();
        public AppStateCivitaiImages Images { get; set; } = new();
        public AppStateCivitaiModels Models { get; set; } = new();
    }

    public class AppStateCivitaiShared
    {
        public string Query { get; set; }
        public int Limit { get; set; }
        public int Page { get; set; }
    }

    public class AppStateCivitaiCreators
    {
        public AppStateCivitaiShared Shared { get; set; } = new();
    }

    public class AppStateCivitaiModels
    {
        public AppStateCivitaiShared Shared { get; set; } = new();
        public string Username { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public CivitaiModelType? Type { get; set; } = null;
        public CivitaiSort Sort { get; set; } = CivitaiSort.Highest_Rated;
        public CivitaiPeriod Period { get; set; } = CivitaiPeriod.AllTime;
        [JsonConverter(typeof(StringOrListConverter))]
        public List<string> BaseModels { get; set; } = new List<string>();
        public int Rating { get; set; } = -1;
        public bool Favorites { get; set; } = false;
        public bool Hidden { get; set; } = false;
        public bool IsPrimaryFileOnly { get; set; } = false;
        public string Hash { get; set; } = string.Empty;
    }

    public class AppStateCivitaiImages
    {
        public int Limit { get; set; } = 100;
        public int PostId { get; set; } = 0;
        public int ModelId { get; set; } = 0;
        public int ModelVersionId { get; set; } = 0;
        public string Username { get; set; } = string.Empty;
        public CivitaiNsfw Nsfw { get; set; } = CivitaiNsfw.None;
        public CivitaiImageSort Sort { get; set; } = CivitaiImageSort.Newest;
        public CivitaiPeriod Period { get; set; } = CivitaiPeriod.AllTime;
        public int Page { get; set; } = 1;
    }

    public class AppStateDanbooru
    {
        public int ActiveTabIndex { get; set; } = 0;
        public string SearchString { get; set; } = "order:rank";
    }

    public class AppStatePrompts
    {
        public int ActiveTabIndex { get; set; } = 0;
        public AppStatePromptsWildcards Wildcards { get; set; } = new();
        public AppStatePromptsLLM LLM { get; set; } = new();
        public List<string> FavoriteArtists { get; set; } = new();
    }

    public class AppStatePromptsLLM
    {
        public string SelectedModel { get; set; } = string.Empty;
        public string ActiveViewId { get; set; } = "process";
        public bool IsNavCollapsed { get; set; } = false;
        public AppStatePromptsLLMTagBuilder TagBuilder { get; set; } = new();
        public AppStatePromptsLLMMixer Mixer { get; set; } = new();
        public AppStatePromptsLLMInspiration Inspiration { get; set; } = new();
        public AppStatePromptsLLMSceneBuilder SceneBuilder { get; set; } = new();
        public AppStatePromptsLLMWorkshop Workshop { get; set; } = new();
    }

    public class AppStatePromptsLLMWorkshop
    {
        public int? ActiveSessionId { get; set; }
        public int AncestorDepth { get; set; } = 3;
        public int SpawnCount { get; set; } = 5;
        public bool AutoRender { get; set; } = true;
        public bool RightPanelCollapsed { get; set; } = false;
    }

    public class AppStatePromptsLLMSceneBuilder
    {
        public string Subject { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
        public string Lighting { get; set; } = string.Empty;
        public string Mood { get; set; } = string.Empty;
        public string Style { get; set; } = string.Empty;
        public string? LastAssembled { get; set; }
    }

    public class AppStatePromptsLLMInspiration
    {
        public string Mode { get; set; } = "inspire"; // "inspire" | "roulette"
        public string Genre { get; set; } = string.Empty;
        public string Mood { get; set; } = string.Empty;
        public string Complexity { get; set; } = "Standard"; // "Simple" | "Standard" | "Rich"
        public bool WeightedRoulette { get; set; } = true;
    }

    public class AppStatePromptsLLMMixer
    {
        public string PromptA { get; set; } = string.Empty;
        public string PromptB { get; set; } = string.Empty;
        public int Ratio { get; set; } = 50; // 0 = all A, 100 = all B
    }

    public class AppStatePromptsLLMTagBuilder
    {
        public TagVerbosity Verbosity { get; set; } = TagVerbosity.Standard;
        public TagModelPreset Preset { get; set; } = TagModelPreset.Pony;
        public Dictionary<TagCategory, bool> CategoryToggles { get; set; } = new()
        {
            [TagCategory.General] = true,
            [TagCategory.Artist] = false,
            [TagCategory.Copyright] = true,
            [TagCategory.Character] = true,
            [TagCategory.Meta] = true
        };
    }

    public class AppStatePromptsWildcards
    {
        public List<string> GeneratedPrompts { get; set; } = new();
        public int GenerationAmount { get; set; }
        public string Template { get; set; } = string.Empty;
        public int ActivePromptTabIndex { get; set; } = 0;
        public int ActiveActionTabIndex { get; set; } = 0;
    }

    public class AppStateScripts
    {
        public AppStateScriptsDynamicPrompts DynamicPrompts { get; set; } = new();
    }

    public class AppStateScriptsDynamicPrompts
    {
        public bool EnablePromptMagic { get; set; } = false;
    }
}
