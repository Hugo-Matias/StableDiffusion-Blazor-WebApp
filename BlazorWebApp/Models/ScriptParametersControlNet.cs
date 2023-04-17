using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    public class ScriptParametersControlNet
    {
        [JsonPropertyName("input_image")] public string InputImage { get; set; }
        [JsonPropertyName("mask")] public string Mask { get; set; }
        [JsonPropertyName("module"), JsonConverter(typeof(JsonStringEnumConverter))] public ControlNetPreprocessor Preprocessor { get; set; }
        [JsonPropertyName("model")] public string Model { get; set; }
        [JsonPropertyName("weight")] public float Weight { get; set; }
        [JsonPropertyName("resize_mode")] public string ResizeMode { get; set; }
        [JsonPropertyName("lowvram")] public bool IsLowVRam { get; set; }
        [JsonPropertyName("processor_res")] public int? ProcessorResolution { get; set; }
        [JsonPropertyName("threshold_a")] public float? ThresholdA { get; set; }
        [JsonPropertyName("threshold_b")] public float? ThresholdB { get; set; }
        [JsonPropertyName("guidance")] public float Guidance { get; set; }
        [JsonPropertyName("guidance_start")] public float GuidanceStart { get; set; }
        [JsonPropertyName("guidance_end")] public float GuidanceEnd { get; set; }
        [JsonPropertyName("guessmode")] public bool IsGuessMode { get; set; }
    }
    public enum ControlNetPreprocessor { none, canny, depth_leres, depth_midas, depth_zoe, inpaint_global_harmonious, lineart, lineart_anime, lineart_coarse, mlsd, normal_bae, normal_midas, openpose, openpose_face, openpose_faceonly, openpose_full, openpose_hand, scribble_hed, scribble_pidinet, scribble_xdog, seg_ofade20k, seg_ofcoco, seg_ufade20k, shuffle, softedge_hed, softedge_hedsafe, softedge_pidinet, softedge_pidisafe, t2ia_color_grid, t2ia_sketch_pidi, t2ia_style_clipvision, threshold, tile_gaussian }
}
