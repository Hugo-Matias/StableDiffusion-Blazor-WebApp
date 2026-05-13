using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

public class WanLowModelLoaderFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "wan_low_model_loader",
        Type = FragmentType.Loader,
        Title = "Wan Low Model Loader",
        IsHidden = true
    };

    public class Parameters
    {
        public string ModelName { get; set; } = "Wan2_2-T2V-A14B-LOW_fp8_e4m3fn_scaled_KJ.safetensors";
        public string VaeName { get; set; } = "wan_2.1_vae.safetensors";
        public string LoraName { get; set; } = "Speed/wan2.2_t2v_lightx2v_4steps_lora_v1.1_low_noise.safetensors";
        public string BasePrecision { get; set; } = "fp16_fast";
        public string Quantization { get; set; } = "fp8_e4m3fn_scaled";
        public string LoadDevice { get; set; } = "offload_device";
        public string AttentionMode { get; set; } = "sageattn";
        public string VaePrecision { get; set; } = "bf16";
        public int BlocksToSwap { get; set; } = 27;
        public bool UseNonBlocking { get; set; }
        public double LoraStrength { get; set; } = 0.7;
    }

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
        Build(builder, registry, new Parameters(), scope, scopeTitle);
    }

    public void Build(ComfyWorkflowBuilder builder, NodeRegistry registry, Parameters p, string scope = "", string scopeTitle = "")
    {
        var compileId = $"{scope}compile_settings";
        var loaderId = $"{scope}model_loader";
        var blockSwapId = $"{scope}block_swap";
        var setBlockSwapId = $"{scope}set_block_swap";
        var loraSelectId = $"{scope}lora_select";
        var setLorasId = $"{scope}set_loras";
        var vaeLoaderId = $"{scope}vae_loader";

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

        builder.AddNode(loaderId, node => node
            .Type("WanVideoModelLoader")
            .Title($"{scopeTitle}Wan Low Model Loader")
            .Input("model", p.ModelName)
            .Input("base_precision", p.BasePrecision)
            .Input("quantization", p.Quantization)
            .Input("load_device", p.LoadDevice)
            .Input("attention_mode", p.AttentionMode)
            .Input("rms_norm_function", "default")
            .InputFromNode("compile_args", compileId, 0));

        builder.AddNode(blockSwapId, node => node
            .Type("WanVideoBlockSwap")
            .Title($"{scopeTitle}Block Swap")
            .Input("blocks_to_swap", p.BlocksToSwap)
            .Input("offload_img_emb", false)
            .Input("offload_txt_emb", false)
            .Input("use_non_blocking", p.UseNonBlocking)
            .Input("vace_blocks_to_swap", 0)
            .Input("prefetch_blocks", 0)
            .Input("block_swap_debug", false));

        builder.AddNode(setBlockSwapId, node => node
            .Type("WanVideoSetBlockSwap")
            .Title($"{scopeTitle}Set Block Swap")
            .InputFromNode("model", loaderId, 0)
            .InputFromNode("block_swap_args", blockSwapId, 0));

        builder.AddNode(loraSelectId, node => node
            .Type("WanVideoLoraSelect")
            .Title($"{scopeTitle}Speed LoRA")
            .Input("lora", p.LoraName)
            .Input("strength", p.LoraStrength)
            .Input("low_mem_load", false)
            .Input("merge_loras", false));

        builder.AddNode(setLorasId, node => node
            .Type("WanVideoSetLoRAs")
            .Title($"{scopeTitle}Set LoRAs")
            .InputFromNode("model", setBlockSwapId, 0)
            .InputFromNode("lora", loraSelectId, 0));

        builder.AddNode(vaeLoaderId, node => node
            .Type("WanVideoVAELoader")
            .Title($"{scopeTitle}Wan VAE Loader")
            .Input("model_name", p.VaeName)
            .Input("precision", p.VaePrecision)
            .Input("use_cpu_cache", false)
            .Input("verbose", false));

        registry.Register("model_output", setLorasId, 0);
        registry.Register("vae_output", vaeLoaderId, 0);
    }
}