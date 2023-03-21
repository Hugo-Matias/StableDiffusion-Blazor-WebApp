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
    public enum ControlNetPreprocessor { none, canny, depth, depth_leres, hed, mlsd, normal_map, openpose, openpose_hand, clip_vision, color, pidinet, scribble, fake_scrible, segmentation, binary }
}
