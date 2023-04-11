using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using MudBlazor;

namespace BlazorWebApp.Models
{
    public class AppState
    {

        public bool IsDarkMode { get; set; }
        public AppStateGeneration Generation { get; set; } = new();
        public AppStateGallery Gallery { get; set; } = new();
        public AppStatePrompts Prompts { get; set; } = new();
        public AppStateResources Resources { get; set; } = new();
        public AppStateCivitai Civitai { get; set; } = new();
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
        public IEnumerable<PromptStyle> Styles { get; set; }
        public string SDModel { get; set; } = "Loading...";
        public string Vae { get; set; }
        public long Seed { get; set; }
        public bool IsInterrupted { get; set; } = false;
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

    public class AppStateGallery
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; }
        public int FolderId { get; set; } = 1;
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
        public DateRange DateRange { get; set; } = new(DateTime.Now.Date, DateTime.Now.Date);
        public bool FilterByDateRange { get; set; } = false;
        public bool IsSelectedOnly { get; set; } = false;
    }

    public enum GalleryOrderBy { Date, Sampler, Seed, Steps, CfgScale, Width, Height, Favorite, Mode, Denoising }

    public class AppStateResources
    {
        public int ActiveTabIndex { get; set; } = 0;
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int Limit { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Subtype { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
        public bool IsInclusive { get; set; } = true;
        public bool LoadTriggerWords { get; set; }
        public float Weight { get; set; }
        public bool? ResourceIsEnabledFilter { get; set; }
        public string OrderBy { get; set; }
        public bool OrderByDescending { get; set; }
    }

    public class AppStateCivitai
    {
        public string ResourceSubtype { get; set; }
        public AppStateCivitaiCreators Creators { get; set; } = new();
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
        public int Rating { get; set; } = -1;
        public bool Favorites { get; set; } = false;
        public bool Hidden { get; set; } = false;
        public bool IsPrimaryFileOnly { get; set; } = false;
        public string Hash { get; set; } = string.Empty;
    }

    public class AppStatePrompts
    {
        public int ActiveTabIndex { get; set; } = 0;
        public AppStatePromptsWildcards Wildcards { get; set; } = new();
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
