using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models
{
    public class ScriptParametersControlNet
    {
        [JsonPropertyName("input_image")] public string InputImage { get; set; }
        [JsonPropertyName("mask")] public string Mask { get; set; }
        [JsonPropertyName("module"), JsonConverter(typeof(ControlNetPreprocessorJsonConverter))] public ControlNetPreprocessor Preprocessor { get; set; }
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
        [JsonPropertyName("control_mode")] public string ControlMode { get; set; }
        [JsonPropertyName("pixel_perfect")] public string PixelPerfect { get; set; }
    }
    public enum ControlNetPreprocessor { none, invert, animal_openpose, blur_gaussian, canny, densepose, densepose_parula, depth_anything, depth_hand_refiner, depth_leres, depth_midas, depth_zoe, dw_openpose_full, inpaint_global_harmonious, inpaint_only, instant_id_face_embedding, instant_id_face_keypoints, ipadapter_clip_sd15, ipadapter_clip_sdxl, ipadapter_clip_sdxl_plus_vith, ipadapter_face_id, ipadapter_face_id_plus, lineart_anime, lineart_anime_denoise, lineart_coarse, lineart_realistic, lineart_standard, mediapipe_face, mlsd, normal_bae, normal_midas, openpose, openpose_face, openpose_faceonly, openpose_full, openpose_hand, recolor_intensity, recolor_luminance, reference_adain, reference_only, revision_clipvision, revision_ignore_prompt, scribble_hed, scribble_pidinet, scribble_xdog, seg_anime_face, seg_ofade20k, seg_ofcoco, seg_ufade20k, shuffle, softedge_hed, softedge_hedsafe, softedge_pidinet, softedge_pidisafe, softedge_teed, t2ia_color_grid, t2ia_sketch_pidi, t2ia_style_clipvision, threshold, tile_colorfix, tile_resample }

    public class ControlNetPreprocessorJsonConverter : JsonConverter<ControlNetPreprocessor>
    {
        public override ControlNetPreprocessor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                string enumValue = reader.GetString().Replace("ip-adapter", "ipadapter");
                if (Enum.TryParse<ControlNetPreprocessor>(enumValue, out ControlNetPreprocessor result)) return result;
            }
            throw new JsonException($"Unable to deserialize {typeof(ControlNetPreprocessor).Name} from JSON.");
        }

        public override void Write(Utf8JsonWriter writer, ControlNetPreprocessor value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString().Replace("ipadapter", "ip-adapter"));
        }
    }
}
