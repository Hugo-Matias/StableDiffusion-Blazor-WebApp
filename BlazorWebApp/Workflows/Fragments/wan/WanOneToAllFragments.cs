using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

public class WanOneToAllControlsFragment : IFragmentBuilder
{
    public const string FragmentId = "wan_one_to_all_controls";

    public FragmentMetadata Metadata => new()
    {
        Id = FragmentId,
        Type = FragmentType.Latent,
        Title = "One-To-All Controls",
        Component = "WanOneToAllControlsForm",
        Icon = "fa-solid fa-sliders",
        Order = 20,
        DefaultActive = true,
        Parameters =
        [
            new() { Name = "width", Label = "Width Ceiling", Type = ParameterType.Slider, Min = 64, Max = 2048, Step = 8, DefaultValue = 480 },
            new() { Name = "height", Label = "Height Ceiling", Type = ParameterType.Slider, Min = 64, Max = 2048, Step = 8, DefaultValue = 832 },
            new() { Name = "window", Label = "Window", Type = ParameterType.Number, Min = 1, Max = 4096, Step = 1, DefaultValue = 81 },
            new() { Name = "overlap", Label = "Overlap", Type = ParameterType.Number, Min = 0, Max = 256, Step = 1, DefaultValue = 5 },
            new() { Name = "fps", Label = "FPS", Type = ParameterType.Number, Min = 1, Max = 120, Step = 1, DefaultValue = 24 },
            new() { Name = "blocks_to_swap", Label = "Blocks To Swap", Type = ParameterType.Number, Min = 0, Max = 40, Step = 1, DefaultValue = 25 }
        ]
    };

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
    }
}

public class WanOneToAllPoseFragment : IFragmentBuilder
{
    public const string FragmentId = "wan_one_to_all_pose";

    public FragmentMetadata Metadata => new()
    {
        Id = FragmentId,
        Type = FragmentType.Conditioning,
        Title = "One-To-All Pose",
        Component = "WanOneToAllPoseForm",
        Icon = "fa-solid fa-person-running",
        Order = 30,
        DefaultActive = true,
        Parameters =
        [
            new() { Name = "vitpose_model", Label = "VitPose Model", Type = ParameterType.Select, DefaultValue = "vitpose-l-wholebody.onnx", Options = ["vitpose-l-wholebody.onnx", "vitpose-b-wholebody.onnx", "vitpose-h-wholebody.onnx", "vitpose-s-wholebody.onnx"] },
            new() { Name = "yolo_model", Label = "YOLO Model", Type = ParameterType.Select, DefaultValue = "yolov10m.onnx", Options = ["yolov10m.onnx"] },
            new() { Name = "onnx_device", Label = "ONNX Device", Type = ParameterType.Select, DefaultValue = "CUDAExecutionProvider", Options = ["CUDAExecutionProvider", "CPUExecutionProvider"] },
            new() { Name = "align_to", Label = "Align To", Type = ParameterType.Select, DefaultValue = "ref", Options = ["ref", "pose", "none"] },
            new() { Name = "draw_face_points", Label = "Face Points", Type = ParameterType.Select, DefaultValue = "full", Options = ["full", "weak", "none"] },
            new() { Name = "draw_head", Label = "Head", Type = ParameterType.Select, DefaultValue = "full", Options = ["full", "weak", "none"] }
        ]
    };

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
    }
}

public class WanOneToAllEmbedsFragment : IFragmentBuilder
{
    public const string FragmentId = "wan_one_to_all_embeds";

    public FragmentMetadata Metadata => new()
    {
        Id = FragmentId,
        Type = FragmentType.Conditioning,
        Title = "One-To-All Embeds",
        Component = "WanOneToAllEmbedsForm",
        Icon = "fa-solid fa-layer-group",
        Order = 40,
        DefaultActive = true,
        Parameters =
        [
            new() { Name = "ref_strength", Label = "Reference Strength", Type = ParameterType.Slider, Min = 0.0, Max = 2.0, Step = 0.05, DefaultValue = 1.0 },
            new() { Name = "ref_start_percent", Label = "Reference Start", Type = ParameterType.Number, Min = 0.0, Max = 1.0, Step = 0.05, DefaultValue = 0.0 },
            new() { Name = "ref_end_percent", Label = "Reference End", Type = ParameterType.Number, Min = 0.0, Max = 1.0, Step = 0.05, DefaultValue = 1.0 },
            new() { Name = "init_pose_strength", Label = "Initial Pose Strength", Type = ParameterType.Slider, Min = 0.0, Max = 2.0, Step = 0.05, DefaultValue = 1.0 },
            new() { Name = "init_pose_cfg_scale", Label = "Initial Pose CFG", Type = ParameterType.Slider, Min = 0.0, Max = 4.0, Step = 0.01, DefaultValue = 1.96 },
            new() { Name = "loop_pose_strength", Label = "Loop Pose Strength", Type = ParameterType.Slider, Min = 0.0, Max = 2.0, Step = 0.05, DefaultValue = 1.0 },
            new() { Name = "loop_pose_cfg_scale", Label = "Loop Pose CFG", Type = ParameterType.Slider, Min = 0.0, Max = 4.0, Step = 0.01, DefaultValue = 1.5 },
            new() { Name = "pose_start_percent", Label = "Pose Start", Type = ParameterType.Number, Min = 0.0, Max = 1.0, Step = 0.05, DefaultValue = 0.0 },
            new() { Name = "pose_end_percent", Label = "Pose End", Type = ParameterType.Number, Min = 0.0, Max = 1.0, Step = 0.05, DefaultValue = 1.0 }
        ]
    };

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
    }
}

public class WanOneToAllSamplerFragment : IFragmentBuilder
{
    public const string FragmentId = "wan_one_to_all_sampler";

    public FragmentMetadata Metadata => new()
    {
        Id = FragmentId,
        Type = FragmentType.Sampler,
        Title = "One-To-All Sampler",
        Component = "WanOneToAllSamplerForm",
        Icon = "fa-solid fa-dice",
        Order = 50,
        DefaultActive = true,
        Parameters =
        [
            new() { Name = "scheduler", Label = "Scheduler", Type = ParameterType.Select, DefaultValue = "unipc", Options = ["unipc", "unipc/beta", "dpm++", "dpm++/beta", "dpm++_sde", "dpm++_sde/beta", "euler", "euler/beta", "longcat_distill_euler", "deis", "lcm", "lcm/beta", "res_multistep", "er_sde", "flowmatch_causvid", "flowmatch_distill", "flowmatch_pusa", "multitalk", "sa_ode_stable", "rcm", "vibt_unipc"] },
            new() { Name = "scheduler_steps", Label = "Scheduler Steps", Type = ParameterType.Slider, Min = 1, Max = 100, Step = 1, DefaultValue = 2 },
            new() { Name = "sampler_steps", Label = "Sampler Steps", Type = ParameterType.Slider, Min = 1, Max = 100, Step = 1, DefaultValue = 8 },
            new() { Name = "cfg", Label = "CFG", Type = ParameterType.Slider, Min = 0.0, Max = 30.0, Step = 0.01, DefaultValue = 1.0 },
            new() { Name = "shift", Label = "Shift", Type = ParameterType.Slider, Min = 0.0, Max = 1000.0, Step = 0.01, DefaultValue = 7.0 },
            new() { Name = "seed", Label = "Seed", Type = ParameterType.Number, Min = -1, DefaultValue = 0L },
            new() { Name = "force_offload", Label = "Force Offload", Type = ParameterType.Checkbox, DefaultValue = true }
        ]
    };

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
    }
}