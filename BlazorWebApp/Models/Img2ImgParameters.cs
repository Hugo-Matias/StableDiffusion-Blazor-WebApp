using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    public class Img2ImgParameters : SharedParameters
    {
        [JsonPropertyName("init_images")] public List<string> InitImages { get; set; }
        [JsonPropertyName("mask")] public string Mask { get; set; }
        [JsonPropertyName("mask_blur")] public int MaskBlur { get; set; }
        [JsonPropertyName("resize_mode")] public int ResizeMode { get; set; }
        [JsonPropertyName("inpainting_fill")] public int InpaintingFill { get; set; }
        [JsonPropertyName("inpaint_full_res")] public bool InpaintFullRes { get; set; }
        [JsonPropertyName("inpaint_full_res_padding")] public int InpaintFullResPadding { get; set; }
        [JsonPropertyName("inpainting_mask_invert")] public int InpaintingMaskInvert { get; set; }
        [JsonIgnore] public Img2ImgScriptParameters Scripts { get; set; }

        public Img2ImgParameters() { }
        public Img2ImgParameters(SharedParameters clone)
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
            AlwaysOnScripts = clone.AlwaysOnScripts;
        }
    }

    public class Img2ImgScriptParameters
    {
        public List<ScriptParametersControlNet> ControlNet { get; set; }
        public ScriptParametersCutoff Cutoff { get; set; }
        public ScriptParametersDynamicPrompts DynamicPrompts { get; set; }
    }
}
