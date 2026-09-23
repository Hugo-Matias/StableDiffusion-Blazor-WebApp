using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ideogram;

/// <summary>
/// Ideogram 4 sampling pipeline.
///
/// Wires: CFGOverride -> DualModelGuider -> KSamplerSelect + Ideogram4Scheduler + RandomNoise
///        -> SamplerCustomAdvanced -> latent_output
///
/// Reads from registry:
///   {scope}model_output        - main UNet (raw)
///   {scope}uncond_model_output - unconditional UNet
///   {scope}positive_output     - positive conditioning
///   {scope}negative_output     - zeroed conditioning (from ConditioningZeroOut)
///   {scope}latent_output       - input latent
///
/// CFGOverride (cfg=3, start=0.7, end=1.0) is applied to the main model before it enters
/// DualModelGuider. These values are exposed in the UI for experimentation.
/// </summary>
public class IdeogramSamplerFragment : IFragmentBuilder
{
    public Parameters Defaults { get; init; } = new();

    public FragmentMetadata Metadata => new()
    {
        Id = "main_sampler",
        Type = FragmentType.Sampler,
        Title = "Sampler",
        Component = "IdeogramSamplerForm",
        Icon = "fa-solid fa-dice",
        Order = 50,
        Collapsible = false,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "steps",
                Label = "Steps",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 100,
                Step = 1,
                DefaultValue = Defaults.Steps
            },
            new FragmentParameter
            {
                Name = "guidance",
                Label = "Guidance",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 30,
                Step = 0.1,
                DefaultValue = Defaults.Guidance
            },
            new FragmentParameter
            {
                Name = "mu",
                Label = "Mu",
                Type = ParameterType.Slider,
                Min = -10,
                Max = 10,
                Step = 0.05,
                DefaultValue = Defaults.Mu
            },
            new FragmentParameter
            {
                Name = "std",
                Label = "Std",
                Type = ParameterType.Slider,
                Min = 0.1,
                Max = 5,
                Step = 0.05,
                DefaultValue = Defaults.Std
            },
            new FragmentParameter
            {
                Name = "sampler_name",
                Label = "Sampler",
                Type = ParameterType.Select,
                DefaultValue = Defaults.SamplerName,
                Options = new[]
                {
                    "euler", "euler_ancestral", "heun", "dpm_2", "dpm_2_ancestral",
                    "lms", "dpmpp_2s_ancestral", "dpmpp_sde", "dpmpp_2m", "dpmpp_2m_sde",
                    "ddim", "uni_pc"
                }
            },
            new FragmentParameter
            {
                Name = "cfg",
                Label = "CFG Override",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 20,
                Step = 0.1,
                DefaultValue = Defaults.CfgOverrideValue
            },
            new FragmentParameter
            {
                Name = "cfg_start",
                Label = "CFG Start %",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 1,
                Step = 0.01,
                DefaultValue = Defaults.CfgOverrideStart
            },
            new FragmentParameter
            {
                Name = "cfg_end",
                Label = "CFG End %",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 1,
                Step = 0.01,
                DefaultValue = Defaults.CfgOverrideEnd
            },
            new FragmentParameter
            {
                Name = "seed",
                Label = "Seed",
                Type = ParameterType.Number,
                Min = -1,
                DefaultValue = Defaults.Seed
            }
        ]
    };

    public class Parameters
    {
        public string SamplerId { get; set; } = "sampler_main";
        public string SamplerName { get; set; } = "euler";
        public int Steps { get; set; } = 20;
        public double Guidance { get; set; } = 7.0;
        public double Mu { get; set; } = 0.0;
        public double Std { get; set; } = 1.75;
        public long Seed { get; set; } = -1;
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 1024;
        public string Scope { get; set; } = "";
        public double CfgOverrideValue { get; set; } = 3.0;
        public double CfgOverrideStart { get; set; } = 0.7;
        public double CfgOverrideEnd { get; set; } = 1.0;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        var latentFragment = parameters.GetFragment("latent");

        var seed = fragment?.GetLong("seed", Defaults.Seed) ?? Defaults.Seed;
        if (seed < 0) seed = Random.Shared.NextInt64(0, int.MaxValue);

        BuildInternal(builder, registry, new Parameters
        {
            SamplerId = Defaults.SamplerId,
            Steps = fragment?.GetInt("steps", Defaults.Steps) ?? Defaults.Steps,
            Guidance = fragment?.GetDouble("guidance", Defaults.Guidance) ?? Defaults.Guidance,
            Mu = fragment?.GetDouble("mu", Defaults.Mu) ?? Defaults.Mu,
            Std = fragment?.GetDouble("std", Defaults.Std) ?? Defaults.Std,
            SamplerName = fragment?.GetString("sampler_name", Defaults.SamplerName) ?? Defaults.SamplerName,
            CfgOverrideValue = fragment?.GetDouble("cfg", Defaults.CfgOverrideValue) ?? Defaults.CfgOverrideValue,
            CfgOverrideStart = fragment?.GetDouble("cfg_start", Defaults.CfgOverrideStart) ?? Defaults.CfgOverrideStart,
            CfgOverrideEnd = fragment?.GetDouble("cfg_end", Defaults.CfgOverrideEnd) ?? Defaults.CfgOverrideEnd,
            Seed = seed,
            Width = latentFragment?.GetInt("width", Defaults.Width) ?? Defaults.Width,
            Height = latentFragment?.GetInt("height", Defaults.Height) ?? Defaults.Height,
            Scope = scope
        });
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p)
    {
        BuildInternal(builder, registry, p);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p)
    {
        var scope = p.Scope;

        var modelRef = registry.GetRef($"{scope}model_output");
        var uncondModelRef = registry.GetRef($"{scope}uncond_model_output");
        var positiveRef = registry.GetRef($"{scope}positive_output");
        var negativeRef = registry.GetRef($"{scope}negative_output");
        var latentRef = registry.GetRef($"{scope}latent_output");

        var cfgOverrideId = $"{scope}{p.SamplerId}_cfg_override";
        var guiderId = $"{scope}{p.SamplerId}_guider";
        var selectId = $"{scope}{p.SamplerId}_select";
        var schedulerId = $"{scope}{p.SamplerId}_scheduler";
        var noiseId = $"{scope}{p.SamplerId}_noise";

        // 1. CFGOverride — applied to main model only (fixed internal coefficients)
        builder.AddNode(cfgOverrideId, node => node
            .ClassType("CFGOverride")
            .Title("CFGOverride")
            .InputRef("model", modelRef)
            .Input("cfg", p.CfgOverrideValue)
            .Input("start_percent", p.CfgOverrideStart)
            .Input("end_percent", p.CfgOverrideEnd));

        // 2. DualModelGuider — merges main (CFG-overridden) + unconditional models
        builder.AddNode(guiderId, node => node
            .ClassType("DualModelGuider")
            .Title("DualModelGuider")
            .InputFromNode("model", cfgOverrideId, 0)
            .InputRef("model_negative", uncondModelRef)
            .InputRef("positive", positiveRef)
            .InputRef("negative", negativeRef)
            .Input("cfg", p.Guidance));

        // 3. KSamplerSelect
        builder.AddNode(selectId, node => node
            .ClassType("KSamplerSelect")
            .Title("KSamplerSelect")
            .Input("sampler_name", p.SamplerName));

        // 4. Ideogram4Scheduler — custom scheduler with mu/std shape parameters
        builder.AddNode(schedulerId, node => node
            .ClassType("Ideogram4Scheduler")
            .Title("Ideogram4Scheduler")
            .Input("steps", p.Steps)
            .Input("width", p.Width)
            .Input("height", p.Height)
            .Input("mu", p.Mu)
            .Input("std", p.Std));

        // 5. RandomNoise
        builder.AddNode(noiseId, node => node
            .ClassType("RandomNoise")
            .Title("RandomNoise")
            .Input("noise_seed", p.Seed));

        // 6. SamplerCustomAdvanced
        builder.AddNode(p.SamplerId, node => node
            .ClassType("SamplerCustomAdvanced")
            .Title("SamplerCustomAdvanced")
            .InputFromNode("noise", noiseId, 0)
            .InputFromNode("guider", guiderId, 0)
            .InputFromNode("sampler", selectId, 0)
            .InputFromNode("sigmas", schedulerId, 0)
            .InputRef("latent_image", latentRef));

        registry.Register("latent_output", p.SamplerId, 0);
    }
}
