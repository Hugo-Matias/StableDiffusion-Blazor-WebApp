using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    public class UpscaleParameters : SharedParameters
    {
        [JsonPropertyName("resize_mode")]
        public int ResizeMode { get; set; }
        [JsonPropertyName("show_extras_results")]
        public bool ShowResults { get; set; }
        [JsonPropertyName("gfpgan_visibility")]
        public double GfpganVisibility { get; set; }
        [JsonPropertyName("codeformer_visibility")]
        public double CodeformerVisibility { get; set; }
        [JsonPropertyName("codeformer_weight")]
        public double CodeformerWeight { get; set; }
        [JsonPropertyName("upscaling_resize")]
        public double UpscalingMultiplier { get; set; }
        [JsonPropertyName("upscaling_resize_w")]
        public int UpscalingWidth { get; set; }
        [JsonPropertyName("upscaling_resize_h")]
        public int UpscalingHeight { get; set; }
        [JsonPropertyName("upscaling_crop")]
        public bool UpscalingCrop { get; set; }
        [JsonPropertyName("upscaler_1")]
        public string UpscalerPrimary { get; set; }
        [JsonPropertyName("upscaler_2")]
        public string UpscalerSecondary { get; set; }
        [JsonPropertyName("extras_upscaler_2_visibility")]
        public double UpscalerSecondaryVisibility { get; set; }
        [JsonPropertyName("upscale_first")]
        public bool UpscalePriority { get; set; }
        [JsonPropertyName("image")]
        public string Image { get; set; }

        public UpscaleParameters() { }

        public UpscaleParameters(SharedParameters clone)
        {
            DenoisingStrength = clone.DenoisingStrength;
            Prompt = clone.Prompt;
            Styles = clone.Styles;
            Seed = clone.Seed;
            Subseed = clone.Subseed;
            SubseedStrength = clone.SubseedStrength;
            SeedResizeFromH = clone.SeedResizeFromH;
            SeedResizeFromW = clone.SeedResizeFromW;
            BatchSize = clone.BatchSize;
            NIter = clone.NIter;
            Steps = clone.Steps;
            CfgScale = clone.CfgScale;
            Width = clone.Width;
            Height = clone.Height;
            RestoreFaces = clone.RestoreFaces;
            Tiling = clone.Tiling;
            NegativePrompt = clone.NegativePrompt;
            Eta = clone.Eta;
            SChurn = clone.SChurn;
            STmax = clone.STmax;
            STmin = clone.STmin;
            SNoise = clone.SNoise;
            SamplerIndex = clone.SamplerIndex;
        }
    }
}
