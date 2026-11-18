using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Loads a Lightricks IC-LoRA via <c>LTXICLoRALoaderModelOnly</c>. The node
/// reads <c>reference_downscale_factor</c> from the safetensors metadata and
/// exposes it as the <c>latent_downscale_factor</c> output, which downstream
/// IC-LoRA guide nodes consume so the dilation factor stays in sync with the
/// LoRA's training configuration.
///
/// Reads <c>{scope}model_output</c> (rebound from the upstream loader chain)
/// and re-registers it on the patched output so subsequent fragments see the
/// IC-LoRA-patched model. Also registers
/// <c>{scope}latent_downscale_factor</c>.
///
/// Reads <c>parameters.Assets["IcLora"]</c> for the LoRA filename.
/// </summary>
public class LtxIcLoraLoaderFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_ic_lora_loader",
        Type = FragmentType.Loader,
        Title = "LTX IC-LoRA Loader",
        IsHidden = true
    };

    public class Parameters
    {
        public string LoraName { get; set; } = "ltx-2.3-22b-v1.1-ic-lora-union-control-ref0.5.safetensors";
        public double StrengthModel { get; set; } = 0.71;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var p = new Parameters();
        var name = parameters.Assets?.GetValueOrDefault("IcLora");
        if (!string.IsNullOrWhiteSpace(name)) p.LoraName = name;
        BuildInternal(builder, registry, p, scope, scopeTitle);
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
        var nodeId = $"{scope}ltx_ic_lora_loader";
        var modelRef = registry.GetRef($"{scope}model_output");

        builder.AddNode(nodeId, node => node
            .Type("LTXICLoRALoaderModelOnly")
            .Title($"{scopeTitle}LTX IC-LoRA Loader")
            .InputRef("model", modelRef)
            .Input("lora_name", p.LoraName)
            .Input("strength_model", p.StrengthModel));

        registry.Register($"{scope}model_output", nodeId, 0);
        registry.Register($"{scope}latent_downscale_factor", nodeId, 1);
    }
}
