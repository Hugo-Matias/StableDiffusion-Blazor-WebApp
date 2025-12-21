using BlazorWebApp.Models.Fragments.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Models.Fragments
{
    /// <summary>
    /// Fragment for face/detail enhancement using detection and inpainting.
    /// Uses YOLO-based detection to find and enhance faces/details.
    /// </summary>
    public class DetailerFragment : FragmentBase
    {
        public override string FragmentFile => "detailer-core.sbn";

        /// <summary>
        /// The detection model for finding faces/details.
        /// </summary>
        [JsonPropertyName("detailer_detection_model")]
        [DynamicSource("UltralyticsDetectorProvider", "model_name")]
        public string DetectionModel { get; set; } = "face_yolov8m.pt";

        /// <summary>
        /// Sampler for the detailer pass.
        /// </summary>
        [JsonPropertyName("detailer_sampler")]
        [DynamicSource("Backend.Samplers")]
        public string Sampler { get; set; } = "euler";

        /// <summary>
        /// Scheduler for the detailer pass.
        /// </summary>
        [JsonPropertyName("detailer_scheduler")]
        [DynamicSource("Backend.Schedulers")]
        public string Scheduler { get; set; } = "normal";

        /// <summary>
        /// Random seed for detailer (-1 for random).
        /// </summary>
        [JsonPropertyName("detailer_seed")]
        public long Seed { get; set; } = -1;

        /// <summary>
        /// Number of sampling steps.
        /// </summary>
        [JsonPropertyName("detailer_steps")]
        [Range(1, 150)]
        [Step(1)]
        public int Steps { get; set; } = 20;

        /// <summary>
        /// CFG scale for the detailer.
        /// </summary>
        [JsonPropertyName("detailer_cfg")]
        [Range(1, 30)]
        [Step(0.5)]
        public float Cfg { get; set; } = 7.0f;

        /// <summary>
        /// Denoise strength for inpainting.
        /// </summary>
        [JsonPropertyName("detailer_denoise")]
        [Range(0, 1)]
        [Step(0.01)]
        public float Denoise { get; set; } = 0.4f;

        /// <summary>
        /// Feather amount for mask edges.
        /// </summary>
        [JsonPropertyName("detailer_feather")]
        [Range(0, 100)]
        [Step(1)]
        public int Feather { get; set; } = 5;

        /// <summary>
        /// Detection confidence threshold.
        /// </summary>
        [JsonPropertyName("detailer_bbox_threshold")]
        [Range(0, 1)]
        [Step(0.01)]
        public float BBoxThreshold { get; set; } = 0.5f;

        /// <summary>
        /// Dilation amount for detected regions.
        /// </summary>
        [JsonPropertyName("detailer_bbox_dilation")]
        [Range(-512, 512)]
        [Step(1)]
        public int BBoxDilation { get; set; } = 10;

        /// <summary>
        /// Crop factor for detected regions.
        /// </summary>
        [JsonPropertyName("detailer_bbox_crop_factor")]
        [Range(1, 10)]
        [Step(0.1)]
        public float BBoxCropFactor { get; set; } = 3.0f;

        /// <summary>
        /// Minimum size to drop small detections.
        /// </summary>
        [JsonPropertyName("detailer_drop_size")]
        [Range(1, 512)]
        [Step(1)]
        public int DropSize { get; set; } = 10;

        /// <summary>
        /// Guide size for processing.
        /// </summary>
        [JsonPropertyName("detailer_guide_size")]
        [Range(64, 2048)]
        [Step(8)]
        public int GuideSize { get; set; } = 512;

        /// <summary>
        /// Maximum size for processing.
        /// </summary>
        [JsonPropertyName("detailer_max_size")]
        [Range(64, 4096)]
        [Step(8)]
        public int MaxSize { get; set; } = 1024;

        /// <summary>
        /// Number of enhancement cycles.
        /// </summary>
        [JsonPropertyName("detailer_cycle")]
        [Range(1, 10)]
        [Step(1)]
        public int Cycle { get; set; } = 1;

        public override FragmentBase Clone(string newId) => new DetailerFragment
        {
            Id = newId,
            IsActive = IsActive,
            Order = Order,
            DetectionModel = DetectionModel,
            Sampler = Sampler,
            Scheduler = Scheduler,
            Seed = Seed,
            Steps = Steps,
            Cfg = Cfg,
            Denoise = Denoise,
            Feather = Feather,
            BBoxThreshold = BBoxThreshold,
            BBoxDilation = BBoxDilation,
            BBoxCropFactor = BBoxCropFactor,
            DropSize = DropSize,
            GuideSize = GuideSize,
            MaxSize = MaxSize,
            Cycle = Cycle
        };
    }
}
