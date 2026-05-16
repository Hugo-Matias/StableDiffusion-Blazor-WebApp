using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

public class LtxAllInOneLoaderSettingsFragment : IFragmentBuilder
{
    public Parameters Defaults { get; init; } = new();

    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_all_in_one_loader",
        Type = FragmentType.Settings,
        Title = "Model Loader",
        Component = "LtxLoaderSettingsForm",
        Icon = "fa-solid fa-cubes",
        Order = 25,
        Collapsible = false,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "loader_mode",
                Label = "Loader Mode",
                Type = ParameterType.Select,
                Options = ["checkpoint", "diffusion"],
                DefaultValue = Defaults.LoaderMode
            },
            new FragmentParameter
            {
                Name = "use_separate_video_vae",
                Label = "Use Separate Video VAE",
                Type = ParameterType.Checkbox,
                DefaultValue = Defaults.UseSeparateVideoVae
            },
            new FragmentParameter
            {
                Name = "unet_weight_dtype",
                Label = "Diffusion Weight DType",
                Type = ParameterType.Select,
                Options = ["default", "fp8_e4m3fn", "fp8_e4m3fn_fast", "fp8_e5m2"],
                DefaultValue = Defaults.UnetWeightDtype
            },
            new FragmentParameter
            {
                Name = "vae_weight_dtype",
                Label = "VAE Weight DType",
                Type = ParameterType.Select,
                Options = ["bf16", "fp16", "fp32"],
                DefaultValue = Defaults.VaeWeightDtype
            }
        ]
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
    }

    public class Parameters
    {
        public string LoaderMode { get; set; } = "checkpoint";
        public bool UseSeparateVideoVae { get; set; } = false;
        public string UnetWeightDtype { get; set; } = "default";
        public string VaeWeightDtype { get; set; } = "bf16";
    }
}