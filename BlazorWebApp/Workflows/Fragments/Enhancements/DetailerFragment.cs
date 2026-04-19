using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Enhancements;

/// <summary>
/// Fragment that applies face/detail enhancement using the FaceDetailer node.
/// Overwrites image_output in the node registry.
/// Conditional: Only builds when detailer.IsActive is true.
/// </summary>
public class DetailerFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "detailer",
        Type = FragmentType.Enhancement,
        Title = "Detailer",
        Component = "DetailerForm",
        Icon = "fa-solid fa-face-smile",
        Order = 120,
        Collapsible = true,
        DefaultCollapsed = true,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "detailer_detection_model",
                Label = "Detection Model",
                Type = ParameterType.Select,
                Source = new DynamicSource("UltralyticsDetectorProvider", "model_name"),
                DefaultValue = "bbox/face_yolov8m.pt"
            },
            new FragmentParameter
            {
                Name = "detailer_sampler",
                Label = "Sampler",
                Type = ParameterType.Select,
                Source = new DynamicSource("FaceDetailer", "sampler_name"),
                DefaultValue = "dpmpp_2m"
            },
            new FragmentParameter
            {
                Name = "detailer_scheduler",
                Label = "Scheduler",
                Type = ParameterType.Select,
                Source = new DynamicSource("FaceDetailer", "scheduler"),
                DefaultValue = "beta"
            },
            new FragmentParameter
            {
                Name = "detailer_seed",
                Label = "Seed",
                Type = ParameterType.Number,
                Min = -1,
                DefaultValue = 42L
            },
            new FragmentParameter
            {
                Name = "detailer_steps",
                Label = "Steps",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 100,
                Step = 1,
                DefaultValue = 20
            },
            new FragmentParameter
            {
                Name = "detailer_cfg",
                Label = "CFG",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 30,
                Step = 0.5,
                DefaultValue = 8.0
            },
            new FragmentParameter
            {
                Name = "detailer_denoise",
                Label = "Denoise",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 1,
                Step = 0.01,
                DefaultValue = 0.65
            },
            new FragmentParameter
            {
                Name = "detailer_feather",
                Label = "Feather",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 100,
                Step = 1,
                DefaultValue = 5
            },
            new FragmentParameter
            {
                Name = "detailer_bbox_threshold",
                Label = "BBox Threshold",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 1,
                Step = 0.01,
                DefaultValue = 0.7
            },
            new FragmentParameter
            {
                Name = "detailer_bbox_dilation",
                Label = "BBox Dilation",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 100,
                Step = 1,
                DefaultValue = 10
            },
            new FragmentParameter
            {
                Name = "detailer_bbox_crop_factor",
                Label = "BBox Crop Factor",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 10,
                Step = 0.1,
                DefaultValue = 3.0
            },
            new FragmentParameter
            {
                Name = "detailer_drop_size",
                Label = "Drop Size",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 200,
                Step = 1,
                DefaultValue = 70
            },
            new FragmentParameter
            {
                Name = "detailer_guide_size",
                Label = "Guide Size",
                Type = ParameterType.Slider,
                Min = 64,
                Max = 2048,
                Step = 64,
                DefaultValue = 512
            },
            new FragmentParameter
            {
                Name = "detailer_max_size",
                Label = "Max Size",
                Type = ParameterType.Slider,
                Min = 256,
                Max = 4096,
                Step = 64,
                DefaultValue = 1024
            },
            new FragmentParameter
            {
                Name = "detailer_cycle",
                Label = "Cycle",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 10,
                Step = 1,
                DefaultValue = 1
            }
        ]
    };

    /// <summary>
    /// Parameters for the detailer fragment.
    /// </summary>
    public class Parameters
    {
        public string Scope { get; set; } = "detailer_";
        public string DetectionModel { get; set; } = "bbox/face_yolov8m.pt";
        public string Sampler { get; set; } = "dpmpp_2m";
        public string Scheduler { get; set; } = "beta";
        public long Seed { get; set; } = 42;
        public int Steps { get; set; } = 20;
        public double Cfg { get; set; } = 8.0;
        public double Denoise { get; set; } = 0.65;
        public int Feather { get; set; } = 5;
        public double BboxThreshold { get; set; } = 0.7;
        public int BboxDilation { get; set; } = 10;
        public double BboxCropFactor { get; set; } = 3.0;
        public int DropSize { get; set; } = 70;
        public int GuideSize { get; set; } = 512;
        public int MaxSize { get; set; } = 1024;
        public int Cycle { get; set; } = 1;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        
        // Check if fragment is active
        if (fragment?.IsActive != true)
            return;

        // Get the scope for model references (defaults to "detailer_")
        var modelScope = fragment.GetString("scope", "detailer_");

        var p = new Parameters
        {
            Scope = modelScope,
            DetectionModel = fragment.GetString("detailer_detection_model", "bbox/face_yolov8m.pt"),
            Sampler = fragment.GetString("detailer_sampler", "dpmpp_2m"),
            Scheduler = fragment.GetString("detailer_scheduler", "beta"),
            Seed = fragment.GetLong("detailer_seed", 42),
            Steps = fragment.GetInt("detailer_steps", 20),
            Cfg = fragment.GetDouble("detailer_cfg", 8.0),
            Denoise = fragment.GetDouble("detailer_denoise", 0.65),
            Feather = fragment.GetInt("detailer_feather", 5),
            BboxThreshold = fragment.GetDouble("detailer_bbox_threshold", 0.7),
            BboxDilation = fragment.GetInt("detailer_bbox_dilation", 10),
            BboxCropFactor = fragment.GetDouble("detailer_bbox_crop_factor", 3.0),
            DropSize = fragment.GetInt("detailer_drop_size", 70),
            GuideSize = fragment.GetInt("detailer_guide_size", 512),
            MaxSize = fragment.GetInt("detailer_max_size", 1024),
            Cycle = fragment.GetInt("detailer_cycle", 1)
        };

        BuildInternal(builder, registry, p);
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters fragmentParams)
    {
        BuildInternal(builder, registry, fragmentParams);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters p)
    {
        // Get references - image from main generation, model/clip/vae/conditioning from scoped loader
        var imageRef = registry.GetRef("image_output");
        var modelRef = registry.GetRef($"{p.Scope}model_output");
        var clipRef = registry.GetRef($"{p.Scope}clip_output");
        var vaeRef = registry.GetRef($"{p.Scope}vae_output");
        var positiveRef = registry.GetRef($"{p.Scope}positive_output");
        var negativeRef = registry.GetRef($"{p.Scope}negative_output");

        // BBox detector provider
        builder.AddNode("detailer_bbox_provider", node => node
            .Type("UltralyticsDetectorProvider")
            .Title("Detailer BBox Detector Provider")
            .Input("model_name", p.DetectionModel));

        // FaceDetailer node
        builder.AddNode("detailer", node => node
            .Type("FaceDetailer")
            .Title("FaceDetailer")
            .Input("guide_size", p.GuideSize)
            .Input("guide_size_for", true)
            .Input("max_size", p.MaxSize)
            .Input("seed", p.Seed)
            .Input("steps", p.Steps)
            .Input("cfg", p.Cfg)
            .Input("sampler_name", p.Sampler)
            .Input("scheduler", p.Scheduler)
            .Input("denoise", p.Denoise)
            .Input("feather", p.Feather)
            .Input("noise_mask", true)
            .Input("force_inpaint", true)
            .Input("bbox_threshold", p.BboxThreshold)
            .Input("bbox_dilation", p.BboxDilation)
            .Input("bbox_crop_factor", p.BboxCropFactor)
            .Input("sam_detection_hint", "center-1")
            .Input("sam_dilation", 0)
            .Input("sam_threshold", 0.93)
            .Input("sam_bbox_expansion", 0)
            .Input("sam_mask_hint_threshold", 0.7)
            .Input("sam_mask_hint_use_negative", "False")
            .Input("drop_size", p.DropSize)
            .Input("wildcard", "")
            .Input("cycle", p.Cycle)
            .Input("inpaint_model", false)
            .Input("noise_mask_feather", 20)
            .Input("tiled_encode", false)
            .Input("tiled_decode", false)
            .InputRef("image", imageRef)
            .InputRef("model", modelRef)
            .InputRef("clip", clipRef)
            .InputRef("vae", vaeRef)
            .InputRef("positive", positiveRef)
            .InputRef("negative", negativeRef)
            .InputRef("bbox_detector", ("detailer_bbox_provider", 0)));

        // Overwrite image_output with detailed version
        registry.Register("image_output", "detailer", 0);
    }
}
