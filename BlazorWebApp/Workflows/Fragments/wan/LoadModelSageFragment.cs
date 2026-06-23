using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that loads a UNet diffusion model with SageAttention and TorchSettings patches.
/// Chain: UNETLoader -> PathchSageAttentionKJ -> ModelPatchTorchSettings
/// Designed for dual-model workflows - use scope to create high/low variants.
/// Registers {scope}model_output pointing to the final torch-patched model.
/// </summary>
public class LoadModelSageFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "load_model_sage",
        Type = FragmentType.Loader,
        Title = "Load Model (Sage)",
        IsHidden = true
    };

    public class Parameters
    {
        public string ModelName { get; set; } = "";
        public string WeightDtype { get; set; } = "default";
        public string SageAttention { get; set; } = "sageattn_qk_int8_pv_fp16_triton";
        public bool EnableFp16Accumulation { get; set; } = true;
        public string LoaderTitle { get; set; } = "Load Diffusion Model";
        public string SageTitle { get; set; } = "Patch Sage Attention";
        public string TorchTitle { get; set; } = "Model Patch Torch Settings";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, new Parameters(), scope, scopeTitle);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, fragmentParams, scope, scopeTitle);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope,
        string scopeTitle)
    {
        var unetId = $"{scope}unet_loader";
        var sageId = $"{scope}sage";
        var torchId = $"{scope}torch";

        builder.AddNode(unetId, node => node
            .Type("UNETLoader")
            .Title($"{scopeTitle}{p.LoaderTitle}")
            .Input("unet_name", p.ModelName)
            .Input("weight_dtype", p.WeightDtype));

        builder.AddNode(sageId, node => node
            .Type("PathchSageAttentionKJ")
            .Title($"{scopeTitle}{p.SageTitle}")
            .Input("sage_attention", p.SageAttention)
            .InputFromNode("model", unetId, 0));

        builder.AddNode(torchId, node => node
            .Type("ModelPatchTorchSettings")
            .Title($"{scopeTitle}{p.TorchTitle}")
            .Input("enable_fp16_accumulation", p.EnableFp16Accumulation)
            .InputFromNode("model", sageId, 0));

        registry.Register($"{scope}model_output", torchId, 0);
    }
}
