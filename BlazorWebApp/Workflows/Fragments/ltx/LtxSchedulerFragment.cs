using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Fragment that produces a sigma curve and registers it as <c>{scope}sigmas_output</c>.
/// Two modes:
///   - "ltxv":   emits LTXVScheduler keyed off a latent reference (registered upstream).
///   - "manual": emits ManualSigmas from a user-provided multiline string.
///
/// Consumed by <see cref="LtxSamplingPassFragment"/> when its
/// <c>UseRegistrySigmas = true</c> flag is set.
/// </summary>
public class LtxSchedulerFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_scheduler",
        Type = FragmentType.Settings,
        Title = "Scheduler",
        Component = "LtxSchedulerForm",
        Icon = "fa-solid fa-wave-square",
        Order = 35,
        Collapsible = true,
        DefaultCollapsed = true,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "mode",
                Label = "Mode",
                Type = ParameterType.Select,
                Options = ["ltxv", "manual"],
                DefaultValue = "ltxv",
                Description = "ltxv: derive sigmas from LTXVScheduler at runtime. manual: user-provided sigma curve."
            },
            new FragmentParameter
            {
                Name = "steps",
                Label = "Steps",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 50,
                Step = 1,
                DefaultValue = 8
            },
            new FragmentParameter
            {
                Name = "max_shift",
                Label = "Max Shift",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 5,
                Step = 0.05,
                DefaultValue = 2.05
            },
            new FragmentParameter
            {
                Name = "base_shift",
                Label = "Base Shift",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 5,
                Step = 0.05,
                DefaultValue = 0.95
            },
            new FragmentParameter
            {
                Name = "stretch",
                Label = "Stretch",
                Type = ParameterType.Checkbox,
                DefaultValue = true
            },
            new FragmentParameter
            {
                Name = "terminal",
                Label = "Terminal",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 1,
                Step = 0.01,
                DefaultValue = 0.1
            },
            new FragmentParameter
            {
                Name = "manual_sigmas",
                Label = "Manual Sigmas (CSV)",
                Type = ParameterType.TextArea,
                DefaultValue = "1.0, 0.99375, 0.9875, 0.98125, 0.975, 0.909375, 0.725, 0.421875, 0.0",
                Description = "Used when Mode = manual. Comma-separated sigma values descending to 0."
            }
        ]
    };

    public class Parameters
    {
        public string Mode { get; set; } = "ltxv";
        public int Steps { get; set; } = 8;
        public double MaxShift { get; set; } = 2.05;
        public double BaseShift { get; set; } = 0.95;
        public bool Stretch { get; set; } = true;
        public double Terminal { get; set; } = 0.1;
        public string ManualSigmas { get; set; } = "1.0, 0.99375, 0.9875, 0.98125, 0.975, 0.909375, 0.725, 0.421875, 0.0";
        /// <summary>
        /// Registry key (relative to <c>scope</c>) of the latent input passed to LTXVScheduler.
        /// Required when Mode = "ltxv".
        /// </summary>
        public string LatentInputName { get; set; } = "av_latent_output";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        var p = new Parameters
        {
            Mode = fragment?.GetString("mode", "ltxv") ?? "ltxv",
            Steps = fragment?.GetInt("steps", 8) ?? 8,
            MaxShift = fragment?.GetDouble("max_shift", 2.05) ?? 2.05,
            BaseShift = fragment?.GetDouble("base_shift", 0.95) ?? 0.95,
            Stretch = fragment?.GetBool("stretch", true) ?? true,
            Terminal = fragment?.GetDouble("terminal", 0.1) ?? 0.1,
            ManualSigmas = fragment?.GetString("manual_sigmas",
                "1.0, 0.99375, 0.9875, 0.98125, 0.975, 0.909375, 0.725, 0.421875, 0.0")
                ?? "1.0, 0.99375, 0.9875, 0.98125, 0.975, 0.909375, 0.725, 0.421875, 0.0"
        };
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
        var nodeId = $"{scope}ltx_sigmas_source";

        if (string.Equals(p.Mode, "ltxv", System.StringComparison.OrdinalIgnoreCase))
        {
            var latentRef = registry.GetRef($"{scope}{p.LatentInputName}");
            builder.AddNode(nodeId, node => node
                .Type("LTXVScheduler")
                .Title($"{scopeTitle}LTXVScheduler")
                .Input("steps", p.Steps)
                .Input("max_shift", p.MaxShift)
                .Input("base_shift", p.BaseShift)
                .Input("stretch", p.Stretch)
                .Input("terminal", p.Terminal)
                .InputRef("latent", latentRef));
        }
        else
        {
            builder.AddNode(nodeId, node => node
                .Type("ManualSigmas")
                .Title($"{scopeTitle}ManualSigmas")
                .Input("sigmas", p.ManualSigmas));
        }

        registry.Register($"{scope}sigmas_output", nodeId, 0);
    }
}
