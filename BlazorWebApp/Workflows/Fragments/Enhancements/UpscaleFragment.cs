using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;

namespace BlazorWebApp.Workflows.Fragments.Enhancements;

/// <summary>
/// Fragment for image upscaling using a model-based upscaler followed by 
/// unsample/resample chain for quality enhancement.
/// </summary>
public class UpscaleFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "upscale",
        Type = FragmentType.Enhancement,
        Title = "Upscale",
        Icon = "fa-solid fa-expand",
        Order = 90,
        Collapsible = true,
        DefaultCollapsed = true,
        Component = "UpscaleForm",
        Parameters =
        [
            new FragmentParameter
            {
                Name = "upscale_model",
                Label = "Upscale Model",
                Type = ParameterType.Select,
                Source = new DynamicSource("UpscaleModelLoader", "model_name"),
                DefaultValue = "4x-UltraSharpV2.safetensors"
            },
            new FragmentParameter
            {
                Name = "upscale_width",
                Label = "Width",
                Type = ParameterType.Slider,
                Min = 64,
                Max = 4096,
                Step = 8,
                DefaultValue = 1744
            },
            new FragmentParameter
            {
                Name = "upscale_height",
                Label = "Height",
                Type = ParameterType.Slider,
                Min = 64,
                Max = 4096,
                Step = 8,
                DefaultValue = 2496
            },
            new FragmentParameter
            {
                Name = "upscale_steps",
                Label = "Steps",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 150,
                Step = 1,
                DefaultValue = 20
            },
            new FragmentParameter
            {
                Name = "upscale_denoise",
                Label = "Denoise",
                Type = ParameterType.Slider,
                Min = 0.0,
                Max = 1.0,
                Step = 0.01,
                DefaultValue = 1.0
            },
            new FragmentParameter
            {
                Name = "upscale_scale",
                Label = "Scale",
                Type = ParameterType.Slider,
                Min = 1.0,
                Max = 4.0,
                Step = 0.25,
                DefaultValue = 2.0
            }
        ]
    };

    public record Parameters
    {
        public string UpscaleModel { get; init; } = "4x-UltraSharpV2.safetensors";
        public int UpscaleWidth { get; init; } = 1744;
        public int UpscaleHeight { get; init; } = 2496;
        public int UpscaleSteps { get; init; } = 20;
        public double UpscaleDenoise { get; init; } = 1.0;
        public string SamplerName { get; init; } = "multistep/res_2m";
        public string Scheduler { get; init; } = "beta";
        public double Cfg { get; init; } = 1.0;
        public long Seed { get; init; } = 42;
        public string Scope { get; init; } = "";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters parameters)
    {
        var scope = parameters.Scope;
        var stepsPerPhase = Math.Max(1, parameters.UpscaleSteps / 4);

        // 1. Load upscale model
        builder.AddNode("upscale_model_loader", node => node
            .ClassType("UpscaleModelLoader")
            .Title("Load Upscale Model")
            .Input("model_name", parameters.UpscaleModel));

        // 2. VAE decode current latent for upscaling
        builder.AddNode("upscale_decode_for_upscale", node => node
            .ClassType("VAEDecode")
            .Title("VAE Decode (for Upscale)")
            .InputFromRegistry("samples", registry, "latent_output")
            .InputFromRegistry("vae", registry, $"{scope}vae_output"));

        // 3. Upscale with model
        builder.AddNode("upscale_with_model", node => node
            .ClassType("ImageUpscaleWithModel")
            .Title("Upscale Image (using Model)")
            .InputFromNode("upscale_model", "upscale_model_loader", 0)
            .InputFromNode("image", "upscale_decode_for_upscale", 0));

        // 4. Scale to target resolution
        builder.AddNode("upscale_scale", node => node
            .ClassType("ImageScale")
            .Title("Upscale Image")
            .Input("upscale_method", "lanczos")
            .Input("width", parameters.UpscaleWidth)
            .Input("height", parameters.UpscaleHeight)
            .Input("crop", "disabled")
            .InputFromNode("image", "upscale_with_model", 0));

        // 5. VAE encode back to latent
        builder.AddNode("upscale_encode", node => node
            .ClassType("VAEEncode")
            .Title("VAE Encode (Upscaled)")
            .InputFromNode("pixels", "upscale_scale", 0)
            .InputFromRegistry("vae", registry, $"{scope}vae_output"));

        // 6. Unsample step
        builder.AddNode("upscale_unsample", node => node
            .ClassType("ClownsharKSampler_Beta")
            .Title("Upscale Unsample")
            .Input("eta", 0.5)
            .Input("sampler_name", parameters.SamplerName)
            .Input("scheduler", parameters.Scheduler)
            .Input("steps", parameters.UpscaleSteps)
            .Input("steps_to_run", stepsPerPhase)
            .Input("denoise", parameters.UpscaleDenoise)
            .Input("cfg", parameters.Cfg)
            .Input("seed", parameters.Seed)
            .Input("sampler_mode", "unsample")
            .Input("bongmath", true)
            .InputFromRegistry("model", registry, $"{scope}model_output")
            .InputFromRegistry("positive", registry, $"{scope}positive_output")
            .InputFromRegistry("negative", registry, $"{scope}negative_output")
            .InputFromNode("latent_image", "upscale_encode", 0));

        // 7. Resample step 1
        builder.AddNode("upscale_resample_1", node => node
            .ClassType("ClownsharkChainsampler_Beta")
            .Title("Upscale Resample 1")
            .Input("eta", 0.5)
            .Input("sampler_name", parameters.SamplerName)
            .Input("steps_to_run", stepsPerPhase)
            .Input("cfg", parameters.Cfg)
            .Input("sampler_mode", "resample")
            .Input("bongmath", true)
            .InputFromNode("latent_image", "upscale_unsample", 0));

        // 8. Resample step 2 (final)
        builder.AddNode("upscale_resample_2", node => node
            .ClassType("ClownsharkChainsampler_Beta")
            .Title("Upscale Resample 2")
            .Input("eta", 0.5)
            .Input("sampler_name", parameters.SamplerName)
            .Input("steps_to_run", -1) // -1 = run remaining steps
            .Input("cfg", parameters.Cfg)
            .Input("sampler_mode", "resample")
            .Input("bongmath", true)
            .InputFromNode("latent_image", "upscale_resample_1", 0));

        // Update latent output to point to upscaled result
        registry.Register("latent_output", "upscale_resample_2", 0);
    }

    // IFragmentBuilder implementation
    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        if (fragment?.IsActive != true) return;

        Build(builder, registry, new Parameters
        {
            UpscaleModel = fragment.GetString("upscale_model", "4x-UltraSharpV2.safetensors"),
            UpscaleWidth = fragment.GetInt("upscale_width", 1744),
            UpscaleHeight = fragment.GetInt("upscale_height", 2496),
            UpscaleSteps = fragment.GetInt("upscale_steps", 20),
            UpscaleDenoise = fragment.GetDouble("upscale_denoise", 1.0),
            SamplerName = parameters.GetFragment("main_sampler")?.GetString("sampler_name", "multistep/res_2m") ?? "multistep/res_2m",
            Scheduler = parameters.GetFragment("main_sampler")?.GetString("scheduler", "beta") ?? "beta",
            Cfg = parameters.GetFragment("main_sampler")?.GetDouble("cfg", 1.0) ?? 1.0,
            Seed = parameters.GetFragment("main_sampler")?.GetLong("seed", 42) ?? 42,
            Scope = scope
        });
    }
}
