using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Models
{
    public class AppSettingsModel
    {
        public GallerySettingsModel Gallery { get; set; } = new();
        public SharedSettingsModel Shared { get; set; } = new();
        public Txt2ImgSettingsModel Txt2Img { get; set; } = new();
        public Img2ImgSettingsModel Img2Img { get; set; } = new();
        public UpscaleSettingsModel Upscale { get; set; } = new();
    }

    #region Gallery
    public class GallerySettingsModel
    {
        public int MaxImageWidth { get; set; } = 2048;
        public int MaxImageHeight { get; set; } = 2048;
        public GalleryOrderBy OrderBy { get; set; } = GalleryOrderBy.Date;
        public bool OrderDescending { get; set; } = true;
        public bool GalleriesOrderDescending { get; set; } = true;
        public int PageSize { get; set; } = 10;
        public ModeType Mode { get; set; } = ModeType.Txt2Img;
        public string SearchPrompt { get; set; } = "";
        public string SearchNegativePrompt { get; set; } = "";
        public bool IsFavoritesOnly { get; set; } = false;
        public bool IsModeTxt2Img { get; set; } = true;
        public bool IsModeImg2Img { get; set; } = true;
        public bool IsModeUpscale { get; set; } = true;
    }

    public enum GalleryOrderBy { Date, Sampler, Seed, Steps, CfgScale, Width, Height, Favorite, Mode, Denoising }
    #endregion

    #region Shared
    public class SharedSettingsModel
    {
        public string Sampler { get; set; } = "DPM++ 2M Karras";
        public int Seed { get; set; } = -1;
        public bool FaceRestoration { get; set; } = true;
        public bool Tilling { get; set; } = false;
        public StepsSettingsModel Steps { get; set; } = new();
        public ResolutionSettingsModel Resolution { get; set; } = new();
        public BatchSettingsModel Batch { get; set; } = new();
        public CfgScaleSettingsModel CfgScale { get; set; } = new();
        public DenoisingSettingsModel Denoising { get; set; } = new();
    }

    public class StepsSettingsModel
    {
        public int DefaultValue { get; set; } = 40;
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 150;
        public int Step { get; set; } = 1;
    }

    public class ResolutionSettingsModel
    {
        public int Width { get; set; } = 512;
        public int Height { get; set; } = 512;
        public int Min { get; set; } = 64;
        public int Max { get; set; } = 2048;
        public int Step { get; set; } = 64;
    }

    public class BatchSettingsModel
    {
        public BatchSizeSettingsModel Size { get; set; } = new();
        public BatchCountSettingsModel Count { get; set; } = new();
    }

    public class BatchCountSettingsModel
    {
        public int DefaultValue { get; set; } = 1;
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 8;
        public int Step { get; set; } = 1;
    }

    public class BatchSizeSettingsModel
    {
        public int DefaultValue { get; set; } = 4;
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 8;
        public int Step { get; set; } = 1;
    }

    public class CfgScaleSettingsModel
    {
        public float DefaultValue { get; set; } = 9.5f;
        public float Min { get; set; } = 1.0f;
        public float Max { get; set; } = 30.0f;
        public float Step { get; set; } = 0.5f;
    }

    public class DenoisingSettingsModel
    {
        public double DefaultValue { get; set; } = 0.52;
        public double Min { get; set; } = 0;
        public double Max { get; set; } = 1;
        public double Step { get; set; } = 0.01;
    }
    #endregion

    #region Txt2Img
    public class Txt2ImgSettingsModel
    {
        public HighresSettingsModel HighRes { get; set; } = new();
    }
    public class HighresSettingsModel
    {
        public bool Enabled { get; set; } = false;
        public FirstPassSettingsModel FirstPass { get; set; } = new();
    }

    public class FirstPassSettingsModel
    {
        public int Width { get; set; } = 512;
        public int Height { get; set; } = 512;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 2048;
        public int Step { get; set; } = 64;
    }
    #endregion

    #region Img2Img
    public class Img2ImgSettingsModel
    {
        public BrushSettingsModel BrushSetttings { get; set; } = new();
        public string Mode { get; set; } = "Mask";
        public MaskBlurSettingsModel MaskBlurSettings { get; set; } = new();
        public int ResizeMode { get; set; } = 1;
        public InpaintingSettingsModel Inpainting { get; set; } = new();
    }

    public class BrushSettingsModel
    {
        public int DefaultValue { get; set; } = 50;
        public int Min { get; set; } = 5;
        public int Max { get; set; } = 70;
        public string Color { get; set; } = "black";
        public string PointerOutline { get; set; } = "white";
    }

    public class MaskBlurSettingsModel
    {
        public int DefaultValue { get; set; } = 2;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 64;
        public int Step { get; set; } = 1;
    }

    public class InpaintingSettingsModel
    {
        public int Fill { get; set; } = 1;
        public int MaskInvert { get; set; } = 0;
        public InpaintingFullResSettingsModel FullRes { get; set; } = new();
    }

    public class InpaintingFullResSettingsModel
    {
        public bool DefaultValue { get; set; } = true;
        public InpaintingFullResPaddingSettingsModel Padding { get; set; } = new();
    }
    public class InpaintingFullResPaddingSettingsModel
    {
        public int DefaultValue { get; set; } = 12;
        public int Min { get; set; } = 0;
        public int Max { get; set; } = 40;
        public int Step { get; set; } = 1;
    }
    #endregion

    #region Upscale
    public class UpscaleSettingsModel
    {
        public int ResizeMode { get; set; } = 0;
        public bool ShowResults { get; set; } = true;
        public string UpscalerPrimary { get; set; } = "Remacri (foolhardy)";
        public UpscalerSecondarySettingsModel UpscalerSecondary { get; set; } = new();
        public UpscalingMultiplierSettingsModel UpscalingMultiplier { get; set; } = new();
        public UpscalingResolutionSettingsModel UpscalingResolution { get; set; } = new();
        public FaceRestorationSettingsModel FaceRestoration { get; set; } = new();

    }

    public class UpscalerSecondarySettingsModel
    {
        public string Name { get; set; } = "None";
        public double DefaultValue { get; set; } = 0.5;
        public double Min { get; set; } = 0;
        public double Max { get; set; } = 1;
        public double Step { get; set; } = 0.01;
    }

    public class UpscalingMultiplierSettingsModel
    {
        public double DefaultValue { get; set; } = 2;
        public double Min { get; set; } = 1;
        public double Max { get; set; } = 8;
        public double Step { get; set; } = 0.05;
    }

    public class UpscalingResolutionSettingsModel
    {
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 1024;
        public bool CropToFit { get; set; } = false;
    }

    public class FaceRestorationSettingsModel
    {
        public double GfpganVisibility { get; set; } = 0;
        public double CodeformerVisibility { get; set; } = 0;
        public double CodeformerWeight { get; set; } = 0.85;
        public bool UpscaleBeforeRestoration { get; set; } = true;
        public double Min { get; set; } = 0;
        public double Max { get; set; } = 1;
        public double Step { get; set; } = 0.01;
    }
    #endregion
}
