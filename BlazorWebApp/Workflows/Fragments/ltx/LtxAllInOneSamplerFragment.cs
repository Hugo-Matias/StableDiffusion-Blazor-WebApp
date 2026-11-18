using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

public class LtxAllInOneSamplerFragment : IFragmentBuilder
{
    public Parameters Defaults { get; init; } = new();

    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_all_in_one_sampler",
        Type = FragmentType.Sampler,
        Title = "Sampler",
        Component = "LtxAllInOneSamplerForm",
        Icon = "fa-solid fa-dice",
        Order = 50,
        Collapsible = true,
        Parameters =
        [
            new FragmentParameter { Name = "cfg", Label = "CFG Scale", Type = ParameterType.Slider, Min = 0.0, Max = 10.0, Step = 0.1, DefaultValue = Defaults.Cfg },
            new FragmentParameter { Name = "stage1_seed", Label = "Stage 1 Seed", Type = ParameterType.Number, Min = -1, DefaultValue = Defaults.Stage1Seed },
            new FragmentParameter { Name = "stage2_seed", Label = "Stage 2 Seed", Type = ParameterType.Number, Min = -1, DefaultValue = Defaults.Stage2Seed },
            new FragmentParameter { Name = "stage1_sampler", Label = "Stage 1 Sampler", Type = ParameterType.Select, Options = SamplerOptions, DefaultValue = Defaults.Stage1Sampler },
            new FragmentParameter { Name = "stage2_sampler", Label = "Stage 2 Sampler", Type = ParameterType.Select, Options = SamplerOptions, DefaultValue = Defaults.Stage2Sampler },
            new FragmentParameter { Name = "sigma_mode", Label = "Sigma Mode", Type = ParameterType.Select, Options = SigmaModeOptions, DefaultValue = Defaults.SigmaMode },
            new FragmentParameter { Name = "use_manual_sigmas", Label = "Use Manual Sigmas", Type = ParameterType.Checkbox, DefaultValue = Defaults.UseManualSigmas },
            new FragmentParameter { Name = "scheduler", Label = "Scheduler", Type = ParameterType.Select, Options = SchedulerOptions, DefaultValue = Defaults.Scheduler },
            new FragmentParameter { Name = "stage1_basic_steps", Label = "Stage 1 Basic Steps", Type = ParameterType.Slider, Min = 1, Max = 50, Step = 1, DefaultValue = Defaults.Stage1BasicSteps },
            new FragmentParameter { Name = "stage2_basic_steps", Label = "Stage 2 Basic Steps", Type = ParameterType.Slider, Min = 1, Max = 50, Step = 1, DefaultValue = Defaults.Stage2BasicSteps },
            new FragmentParameter { Name = "stage1_steps", Label = "Stage 1 Steps", Type = ParameterType.Slider, Min = 1, Max = 50, Step = 1, DefaultValue = Defaults.Stage1Steps },
            new FragmentParameter { Name = "stage2_steps", Label = "Stage 2 Steps", Type = ParameterType.Slider, Min = 1, Max = 50, Step = 1, DefaultValue = Defaults.Stage2Steps },
            new FragmentParameter { Name = "stage1_denoise", Label = "Stage 1 Denoise", Type = ParameterType.Slider, Min = 0.0, Max = 1.0, Step = 0.01, DefaultValue = Defaults.Stage1Denoise },
            new FragmentParameter { Name = "stage2_denoise", Label = "Stage 2 Denoise", Type = ParameterType.Slider, Min = 0.0, Max = 1.0, Step = 0.01, DefaultValue = Defaults.Stage2Denoise },
            new FragmentParameter { Name = "stage1_videoflow_terminal", Label = "Stage 1 VideoFlow Terminal", Type = ParameterType.Slider, Min = 0.0, Max = 1.0, Step = 0.01, DefaultValue = Defaults.Stage1VideoFlowTerminal },
            new FragmentParameter { Name = "stage2_videoflow_start", Label = "Stage 2 VideoFlow Start", Type = ParameterType.Slider, Min = 0.0, Max = 1.0, Step = 0.01, DefaultValue = Defaults.Stage2VideoFlowStart },
            new FragmentParameter { Name = "stage2_videoflow_terminal", Label = "Stage 2 VideoFlow Terminal", Type = ParameterType.Slider, Min = 0.0, Max = 1.0, Step = 0.01, DefaultValue = Defaults.Stage2VideoFlowTerminal },
            new FragmentParameter { Name = "videoflow_midpoint", Label = "VideoFlow Midpoint", Type = ParameterType.Slider, Min = 0.0, Max = 1.0, Step = 0.01, DefaultValue = Defaults.VideoFlowMidpoint },
            new FragmentParameter { Name = "stage1_videoflow_shift", Label = "Stage 1 VideoFlow Shift", Type = ParameterType.Slider, Min = 0.0, Max = 2.0, Step = 0.01, DefaultValue = Defaults.Stage1VideoFlowShift },
            new FragmentParameter { Name = "stage2_videoflow_shift", Label = "Stage 2 VideoFlow Shift", Type = ParameterType.Slider, Min = 0.0, Max = 2.0, Step = 0.01, DefaultValue = Defaults.Stage2VideoFlowShift },
            new FragmentParameter { Name = "stage1_sigmas", Label = "Stage 1 Manual Sigmas", Type = ParameterType.TextArea, DefaultValue = Defaults.Stage1Sigmas },
            new FragmentParameter { Name = "stage2_sigmas", Label = "Stage 2 Manual Sigmas", Type = ParameterType.TextArea, DefaultValue = Defaults.Stage2Sigmas }
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
        public double Cfg { get; set; } = 1.0;
        public long Stage1Seed { get; set; } = 1;
        public long Stage2Seed { get; set; } = 100000;
        public string Stage1Sampler { get; set; } = "dpm_2_ancestral";
        public string Stage2Sampler { get; set; } = "dpm_2";
        public string SigmaMode { get; set; } = "videoflow";
        public bool UseManualSigmas { get; set; } = false;
        public string Scheduler { get; set; } = "linear_quadratic";
        public int Stage1BasicSteps { get; set; } = 8;
        public int Stage2BasicSteps { get; set; } = 2;
        public int Stage1Steps { get; set; } = 8;
        public int Stage2Steps { get; set; } = 8;
        public double Stage1Denoise { get; set; } = 1.0;
        public double Stage2Denoise { get; set; } = 0.15;
        public double Stage1VideoFlowTerminal { get; set; } = 0.15;
        public double Stage2VideoFlowStart { get; set; } = 0.80;
        public double Stage2VideoFlowTerminal { get; set; } = 0.0;
        public double VideoFlowMidpoint { get; set; } = 0.54;
        public double Stage1VideoFlowShift { get; set; } = 1.0;
        public double Stage2VideoFlowShift { get; set; } = 0.5;
        public double VideoFlowFlatness { get; set; } = 1.0;
        public string Stage1Sigmas { get; set; } = "1.0, 0.99375, 0.9875, 0.98125, 0.975, 0.909375, 0.725, 0.421875, 0.0";
        public string Stage2Sigmas { get; set; } = "0.8, 0.68, 0.54, 0.1, 0.0";
    }

    public static Parameters CreateTextToVideoDefaults() => new()
    {
        Stage1Steps = 8,
        Stage2Steps = 8,
        Stage1BasicSteps = 8,
        Stage2BasicSteps = 2,
        Stage1Denoise = 1.0,
        Stage2Denoise = 0.15,
        Stage1VideoFlowTerminal = 0.15,
        Stage2VideoFlowStart = 0.80,
        Stage2VideoFlowTerminal = 0.0,
        VideoFlowMidpoint = 0.54,
        Stage1VideoFlowShift = 1.0,
        Stage2VideoFlowShift = 0.5
    };

    public static Parameters CreateImageToVideoDefaults() => new()
    {
        Stage1Steps = 4,
        Stage2Steps = 4,
        Stage1BasicSteps = 8,
        Stage2BasicSteps = 2,
        Stage1Denoise = 1.0,
        Stage2Denoise = 0.29,
        Stage1VideoFlowTerminal = 0.25,
        Stage2VideoFlowStart = 0.68,
        Stage2VideoFlowTerminal = 0.10,
        VideoFlowMidpoint = 0.54,
        Stage1VideoFlowShift = 1.0,
        Stage2VideoFlowShift = 0.5
    };

    public static readonly string[] SigmaModeOptions = ["basic", "videoflow", "manual"];

    private static readonly string[] SamplerOptions =
    [
        "euler", "euler_cfg_pp", "euler_ancestral", "euler_ancestral_cfg_pp", "heun", "heunpp2",
        "dpm_2", "dpm_2_ancestral", "lms", "dpmpp_2s_ancestral", "dpmpp_2m", "dpmpp_3m_sde",
        "ddpm", "lcm", "ipndm", "deis", "res_multistep", "res_multistep_cfg_pp", "ddim", "uni_pc"
    ];

    private static readonly string[] SchedulerOptions =
    [
        "simple", "sgm_uniform", "karras", "exponential", "ddim_uniform", "beta", "normal", "linear_quadratic", "kl_optimal", "bong_tangent", "beta57"
    ];
}