using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that loads a WanVideo model with block swap and LoRA support.
/// Pipeline: CompileSettings -> ModelLoader -> BlockSwap -> SetBlockSwap -> LoRASelect -> SetLoRAs
/// Registers model_output (from set_loras node).
/// </summary>
public class LoadWanModelFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "load_wan_model",
        Type = FragmentType.Loader,
        Title = "Load WanVideo Model",
        IsHidden = true
    };

    public class Parameters
    {
        public string ModelName { get; set; } = "Wan21_SteadyDancer_fp8_e4m3fn_scaled_KJ.safetensors";
        public string LoraName { get; set; } = "Speed/lightx2v_I2V_14B_480p_cfg_step_distill_rank64_bf16.safetensors";
        public double LoraStrength { get; set; } = 1;
        public string BasePrecision { get; set; } = "fp16_fast";
        public string AttentionMode { get; set; } = "sageattn";
        public int BlocksToSwap { get; set; } = 35;
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
        var compileId = $"{scope}compile_settings";
        var loaderId = $"{scope}model_loader";
        var blockSwapId = $"{scope}block_swap";
        var setBlockSwapId = $"{scope}set_block_swap";
        var loraSelectId = $"{scope}lora_select";
        var setLorasId = $"{scope}set_loras";

        // Torch Compile Settings
        builder.AddNode(compileId, node => node
            .Type("WanVideoTorchCompileSettings")
            .Title($"{scopeTitle}Torch Compile Settings")
            .Input("backend", "inductor")
            .Input("fullgraph", false)
            .Input("mode", "default")
            .Input("dynamic", false)
            .Input("dynamo_cache_size_limit", 64)
            .Input("compile_transformer_blocks_only", true)
            .Input("dynamo_recompile_limit", 128)
            .Input("force_parameter_static_shapes", false)
            .Input("allow_unmerged_lora_compile", false));

        // WanVideo Model Loader
        builder.AddNode(loaderId, node => node
            .Type("WanVideoModelLoader")
            .Title($"{scopeTitle}WanVideo Model Loader")
            .Input("model", p.ModelName)
            .Input("base_precision", p.BasePrecision)
            .Input("quantization", "disabled")
            .Input("load_device", "offload_device")
            .Input("attention_mode", p.AttentionMode)
            .Input("rms_norm_function", "default")
            .InputFromNode("compile_args", compileId, 0));

        // Block Swap settings
        builder.AddNode(blockSwapId, node => node
            .Type("WanVideoBlockSwap")
            .Title($"{scopeTitle}Block Swap")
            .Input("blocks_to_swap", p.BlocksToSwap)
            .Input("offload_img_emb", false)
            .Input("offload_txt_emb", false)
            .Input("use_non_blocking", true)
            .Input("vace_blocks_to_swap", 0)
            .Input("prefetch_blocks", 1)
            .Input("block_swap_debug", false));

        // Set Block Swap on model
        builder.AddNode(setBlockSwapId, node => node
            .Type("WanVideoSetBlockSwap")
            .Title($"{scopeTitle}Set BlockSwap")
            .InputFromNode("model", loaderId, 0)
            .InputFromNode("block_swap_args", blockSwapId, 0));

        // LoRA Select
        builder.AddNode(loraSelectId, node => node
            .Type("WanVideoLoraSelect")
            .Title($"{scopeTitle}LoRA Select")
            .Input("lora", p.LoraName)
            .Input("strength", p.LoraStrength)
            .Input("low_mem_load", false)
            .Input("merge_loras", false));

        // Set LoRAs on model
        builder.AddNode(setLorasId, node => node
            .Type("WanVideoSetLoRAs")
            .Title($"{scopeTitle}Set LoRAs")
            .InputFromNode("model", setBlockSwapId, 0)
            .InputFromNode("lora", loraSelectId, 0));

        registry.Register($"{scope}model_output", setLorasId, 0);
    }
}
