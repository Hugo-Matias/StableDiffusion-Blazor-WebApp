using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

public class WanVaceKjModelLoaderFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "wan_vace_kj_model_loader",
        Type = FragmentType.Loader,
        Title = "Wan VACE KJ Model Loader",
        IsHidden = true
    };

    public class Parameters
    {
        public string Branch { get; set; } = "low";
        public string ModelName { get; set; } = "";
        public string VaceModuleName { get; set; } = "";
        public string WeightDtype { get; set; } = "default";
        public string ComputeDtype { get; set; } = "default";
        public bool PatchCublasLinear { get; set; }
        public string SageAttention { get; set; } = "disabled";
        public bool EnableFp16Accumulation { get; set; }
        public string ModelOutputName { get; set; } = "model_output";
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
        var branch = string.IsNullOrWhiteSpace(fragmentParams.Branch) ? "model" : fragmentParams.Branch;
        var selectorId = $"{scope}{branch}_vace_module_selector";
        var loaderId = $"{scope}{branch}_model_loader";

        builder.AddNode(selectorId, node => node
            .Type("DiffusionModelSelector")
            .Title($"{scopeTitle}{branch} VACE Module")
            .Input("model_name", fragmentParams.VaceModuleName));

        builder.AddNode(loaderId, node => node
            .Type("DiffusionModelLoaderKJ")
            .Title($"{scopeTitle}{branch} Diffusion Model")
            .Input("model_name", fragmentParams.ModelName)
            .Input("weight_dtype", fragmentParams.WeightDtype)
            .Input("compute_dtype", fragmentParams.ComputeDtype)
            .Input("patch_cublaslinear", fragmentParams.PatchCublasLinear)
            .Input("sage_attention", fragmentParams.SageAttention)
            .Input("enable_fp16_accumulation", fragmentParams.EnableFp16Accumulation)
            .InputFromNode("extra_state_dict", selectorId, 0));

        registry.Register(fragmentParams.ModelOutputName, loaderId, 0);
    }
}