using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

public class WanCreateSaveVideoFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "wan_create_save_video",
        Type = FragmentType.Output,
        Title = "Create And Save Video",
        IsHidden = true
    };

    public class Parameters
    {
        public string CreateVideoNodeId { get; set; } = "create_video";
        public string SaveVideoNodeId { get; set; } = "save_video";
        public string ImageInputName { get; set; } = "image_output";
        public double Fps { get; set; } = 16;
        public string FilenamePrefix { get; set; } = "video/ComfyUI";
        public string Format { get; set; } = "auto";
        public string Codec { get; set; } = "auto";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        Build(builder, registry, new Parameters(), scope, scopeTitle);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        var createVideoId = $"{scope}{fragmentParams.CreateVideoNodeId}";
        var saveVideoId = $"{scope}{fragmentParams.SaveVideoNodeId}";

        builder.AddNode(createVideoId, node => node
            .Type("CreateVideo")
            .Title($"{scopeTitle}Create Video")
            .InputRef("images", registry.GetRef(fragmentParams.ImageInputName))
            .Input("fps", fragmentParams.Fps));

        builder.AddNode(saveVideoId, node => node
            .Type("SaveVideo")
            .Title($"{scopeTitle}Save Video")
            .InputFromNode("video", createVideoId, 0)
            .Input("filename_prefix", fragmentParams.FilenamePrefix)
            .Input("format", fragmentParams.Format)
            .Input("codec", fragmentParams.Codec));
    }
}
