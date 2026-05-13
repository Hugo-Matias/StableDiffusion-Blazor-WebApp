using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Qwen;

public class QwenImageEditPlusProEncodeFragment : IFragmentBuilder
{
    public const string NodeClassType = "TextEncodeQwenImageEditPlusPro_lrzjason";
    public const int ConditioningWithFullRefOutputIndex = 0;
    public const int LatentOutputIndex = 1;
    public const int ConditioningWithMainRefOutputIndex = 7;

    public FragmentMetadata Metadata => new()
    {
        Id = "encode_qwen_image_edit_plus_pro",
        Type = FragmentType.Conditioning,
        Title = "Encode Qwen Image Edit Plus Pro",
        IsHidden = true
    };

    public class Parameters
    {
        public string NodeId { get; set; } = "qwen_image_edit_plus_pro_encode";
        public string OutputKey { get; set; } = "conditioning_output";
        public string Prompt { get; set; } = string.Empty;
        public string ImageRefKey { get; set; } = "source_image";
        public string VlResizeIndexes { get; set; } = "1,2,3";
        public int MainImageIndex { get; set; } = 1;
        public int TargetSize { get; set; } = 1024;
        public int TargetVlSize { get; set; } = 384;
        public string UpscaleMethod { get; set; } = "lanczos";
        public string CropMethod { get; set; } = "pad";
        public string Instruction { get; set; } = CharacterReferenceDefaults.QwenEditInstruction;
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
        var clipRef = registry.GetRef($"{scope}clip_output");
        var vaeRef = registry.GetRef($"{scope}vae_output");
        var imageRef = registry.GetRef(fragmentParams.ImageRefKey);

        builder.AddNode(fragmentParams.NodeId, node => node
            .Type(NodeClassType)
            .Title($"{scopeTitle}Qwen Image Edit Plus Pro")
            .Input("prompt", fragmentParams.Prompt)
            .Input("vl_resize_indexs", fragmentParams.VlResizeIndexes)
            .Input("main_image_index", fragmentParams.MainImageIndex)
            .Input("target_size", fragmentParams.TargetSize)
            .Input("target_vl_size", fragmentParams.TargetVlSize)
            .Input("upscale_method", fragmentParams.UpscaleMethod)
            .Input("crop_method", fragmentParams.CropMethod)
            .Input("instruction", fragmentParams.Instruction)
            .InputRef("clip", clipRef)
            .InputRef("vae", vaeRef)
            .InputRef("image1", imageRef));

        registry.Register(fragmentParams.OutputKey, fragmentParams.NodeId, ConditioningWithFullRefOutputIndex);
    }
}
