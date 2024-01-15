using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    public class ScriptParametersADetailer : BaseScriptParameters
    {
        public bool SkipImg2Img { get; set; }
        public ScriptParametersADetailerModel Model1 { get; set; }
        public ScriptParametersADetailerModel Model2 { get; set; }
        public ScriptParametersADetailerModel Model3 { get; set; }
        public ScriptParametersADetailerModel Model4 { get; set; }
        public ScriptParametersADetailerModel Model5 { get; set; }
    }

    public class ScriptParametersADetailerModel
    {
        [JsonPropertyName("ad_model")] public string Model { get; set; }
        [JsonPropertyName("ad_prompt")] public string Prompt { get; set; }
        [JsonPropertyName("ad_negative_prompt")] public string NegativePrompt { get; set; }
        [JsonPropertyName("ad_confidence")] public float Confidence { get; set; }
        [JsonPropertyName("ad_mask_k_largest")] public int MaskKLargest { get; set; }
        [JsonPropertyName("ad_mask_min_ratio")] public float MaskMinRatio { get; set; }
        [JsonPropertyName("ad_mask_max_ratio")] public float MaskMaxRatio { get; set; }
        [JsonPropertyName("ad_dilate_erode")] public int DilateErode { get; set; }
        [JsonPropertyName("ad_x_offset")] public int XOffset { get; set; }
        [JsonPropertyName("ad_y_offset")] public int YOffset { get; set; }
        [JsonPropertyName("ad_mask_merge_invert")] public string MaskMergeInvert { get; set; }
        [JsonPropertyName("ad_mask_blur")] public int MaskBlur { get; set; }
        [JsonPropertyName("ad_denoising_strength")] public float DenoisingStrength { get; set; }
        [JsonPropertyName("ad_inpaint_only_masked")] public bool InpaintOnlyMasked { get; set; }
        [JsonPropertyName("ad_inpaint_only_masked_padding")] public int InpaintOnlyMaskedPadding { get; set; }
        [JsonPropertyName("ad_use_inpaint_width_height")] public bool UseInpaintWidthHeight { get; set; }
        [JsonPropertyName("ad_inpaint_width")] public int InpaintWidth { get; set; }
        [JsonPropertyName("ad_inpaint_height")] public int InpaintHeight { get; set; }
        [JsonPropertyName("ad_use_steps")] public bool UseSteps { get; set; }
        [JsonPropertyName("ad_steps")] public int Steps { get; set; }
        [JsonPropertyName("ad_use_cfg_scale")] public bool UseCFGScale { get; set; }
        [JsonPropertyName("ad_cfg_scale")] public float CFGScale { get; set; }
        [JsonPropertyName("ad_use_checkpoint")] public bool UseCheckpoint { get; set; }
        [JsonPropertyName("ad_checkpoint")] public string Checkpoint { get; set; }
        [JsonPropertyName("ad_use_vae")] public bool UseVAE { get; set; }
        [JsonPropertyName("ad_vae")] public string VAE { get; set; }
        [JsonPropertyName("ad_use_sampler")] public bool UseSampler { get; set; }
        [JsonPropertyName("ad_sampler")] public string Sampler { get; set; }
        [JsonPropertyName("ad_use_noise_multiplier")] public bool UseNoiseMultiplier { get; set; }
        [JsonPropertyName("ad_noise_multiplier")] public float NoiseMultiplier { get; set; }
        [JsonPropertyName("ad_use_clip_skip")] public bool UseClipSkip { get; set; }
        [JsonPropertyName("ad_clip_skip")] public int ClipSkip { get; set; }
        [JsonPropertyName("ad_restore_face")] public bool RestoreFace { get; set; }
        [JsonPropertyName("ad_controlnet_model")] public string ControlNetModel { get; set; }
        [JsonPropertyName("ad_controlnet_module")] public string ControlNetModule { get; set; }
        [JsonPropertyName("ad_controlnet_weight")] public float ControlNetWeight { get; set; }
        [JsonPropertyName("ad_controlnet_guidance_start")] public float ControlNetGuidanceStart { get; set; }
        [JsonPropertyName("ad_controlnet_guidance_end")] public float ControlNetGuidanceEnd { get; set; }
    }
}
