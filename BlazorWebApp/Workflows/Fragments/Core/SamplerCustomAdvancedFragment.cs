using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that runs a SamplerCustomAdvanced pipeline for image generation.
/// Composes: RandomNoise -> KSamplerSelect -> Flux2Scheduler -> CFGGuider -> SamplerCustomAdvanced.
/// Registers latent_output in the node registry (overwrites previous latent).
/// </summary>
public class SamplerCustomAdvancedFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "main_sampler",
        Type = FragmentType.Sampler,
        Title = "Sampler",
        Component = "SamplerForm",
        Icon = "fa-solid fa-dice",
        Order = 50,
        Collapsible = true,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "sampler_name",
                Label = "Sampler",
                Type = ParameterType.Select,
                Source = new DynamicSource("KSamplerSelect", "sampler_name")
            },
            new FragmentParameter
            {
                Name = "steps",
                Label = "Steps",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 150,
                Step = 1,
                DefaultValue = 20
            },
            new FragmentParameter
            {
                Name = "cfg",
                Label = "CFG Scale",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 30,
                Step = 0.5,
                DefaultValue = 5.0
            },
            new FragmentParameter
            {
                Name = "seed",
                Label = "Seed",
                Type = ParameterType.Number,
                Min = -1,
                DefaultValue = -1L
            }
        ]
    };

    /// <summary>
    /// Parameters for the custom advanced sampler fragment.
    /// </summary>
    public class Parameters
    {
        public string SamplerId { get; set; } = "sampler_main";
        public string SamplerName { get; set; } = "euler";
        public int Steps { get; set; } = 20;
        public double Cfg { get; set; } = 5.0;
        public long Seed { get; set; } = 42;
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 1024;
        public string Scope { get; set; } = "";
        /// <summary>
        /// Optional registry reference for width. When set, uses InputRef instead of scalar Input on Flux2Scheduler.
        /// </summary>
        public (string nodeId, int index)? WidthRef { get; set; }
        /// <summary>
        /// Optional registry reference for height. When set, uses InputRef instead of scalar Input on Flux2Scheduler.
        /// </summary>
        public (string nodeId, int index)? HeightRef { get; set; }
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

        BuildInternal(builder, registry, new Parameters
        {
            SamplerId = fragment?.GetString("sampler_id", "sampler_main") ?? "sampler_main",
            SamplerName = fragment?.GetString("sampler_name", "euler") ?? "euler",
            Steps = fragment?.GetInt("steps", 20) ?? 20,
            Cfg = fragment?.GetDouble("cfg", 5.0) ?? 5.0,
            Seed = fragment?.GetLong("seed", 42) ?? 42,
            Width = latentFragment?.GetInt("width", 1024) ?? 1024,
            Height = latentFragment?.GetInt("height", 1024) ?? 1024,
            Scope = scope
        });
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams)
    {
        BuildInternal(builder, registry, fragmentParams);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p)
    {
        var scope = p.Scope;

        var modelRef = registry.GetRef($"{scope}model_output");
        var positiveRef = registry.GetRef($"{scope}positive_output");
        var negativeRef = registry.GetRef($"{scope}negative_output");
        var latentRef = registry.GetRef($"{scope}latent_output");

        var noiseId = $"{scope}{p.SamplerId}_noise";
        var selectId = $"{scope}{p.SamplerId}_select";
        var schedulerId = $"{scope}{p.SamplerId}_scheduler";
        var guiderId = $"{scope}{p.SamplerId}_guider";

        // RandomNoise
        builder.AddNode(noiseId, node => node
            .Type("RandomNoise")
            .Title("RandomNoise")
            .Input("noise_seed", p.Seed));

        // KSamplerSelect
        builder.AddNode(selectId, node => node
            .Type("KSamplerSelect")
            .Title("KSamplerSelect")
            .Input("sampler_name", p.SamplerName));

        // Flux2Scheduler
        builder.AddNode(schedulerId, node =>
        {
            node.Type("Flux2Scheduler")
                .Title("Flux2Scheduler")
                .Input("steps", p.Steps);

            if (p.WidthRef.HasValue)
                node.InputRef("width", p.WidthRef.Value);
            else
                node.Input("width", p.Width);

            if (p.HeightRef.HasValue)
                node.InputRef("height", p.HeightRef.Value);
            else
                node.Input("height", p.Height);
        });

        // CFGGuider
        builder.AddNode(guiderId, node => node
            .Type("CFGGuider")
            .Title("CFGGuider")
            .Input("cfg", p.Cfg)
            .InputRef("model", modelRef)
            .InputRef("positive", positiveRef)
            .InputRef("negative", negativeRef));

        // SamplerCustomAdvanced
        builder.AddNode(p.SamplerId, node => node
            .Type("SamplerCustomAdvanced")
            .Title("SamplerCustomAdvanced")
            .InputRef("noise", (noiseId, 0))
            .InputRef("guider", (guiderId, 0))
            .InputRef("sampler", (selectId, 0))
            .InputRef("sigmas", (schedulerId, 0))
            .InputRef("latent_image", latentRef));

        registry.Register("latent_output", p.SamplerId, 0);
    }
}
