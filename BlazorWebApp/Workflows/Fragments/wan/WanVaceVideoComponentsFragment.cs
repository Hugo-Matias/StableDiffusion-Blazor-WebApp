using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

public class WanVaceVideoComponentsFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "wan_vace_video_components",
        Type = FragmentType.Input,
        Title = "Wan VACE Video Components",
        IsHidden = true
    };

    public class Parameters
    {
        public string FirstVideoPath { get; set; } = "";
        public string SecondVideoPath { get; set; } = "";
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
        AddVideoBranch(
            builder,
            registry,
            $"{scope}load_video_1",
            $"{scope}video_1_components",
            $"{scopeTitle}Load Video 1",
            fragmentParams.FirstVideoPath,
            "video_1_images",
            "video_1_audio",
            "video_1_fps");

        AddVideoBranch(
            builder,
            registry,
            $"{scope}load_video_2",
            $"{scope}video_2_components",
            $"{scopeTitle}Load Video 2",
            fragmentParams.SecondVideoPath,
            "video_2_images",
            "video_2_audio",
            "video_2_fps");
    }

    private static void AddVideoBranch(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string loadNodeId,
        string componentsNodeId,
        string title,
        string videoPath,
        string imageOutputName,
        string audioOutputName,
        string fpsOutputName)
    {
        builder.AddNode(loadNodeId, node => node
            .Type("LoadVideo")
            .Title(title)
            .Input("file", videoPath));

        builder.AddNode(componentsNodeId, node => node
            .Type("GetVideoComponents")
            .Title($"{title} Components")
            .InputFromNode("video", loadNodeId, 0));

        registry.Register(imageOutputName, componentsNodeId, 0);
        registry.Register(audioOutputName, componentsNodeId, 1);
        registry.Register(fpsOutputName, componentsNodeId, 2);
    }
}