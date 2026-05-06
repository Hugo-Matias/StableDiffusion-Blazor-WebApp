using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Templates.ZImage;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows;

public class ZImageTxt2ImgMultiPassWorkflowTests
{
    private readonly ZImageTxt2ImgMultiPassWorkflow _workflow = new();

    [Fact]
    public void Metadata_ShouldUseSingleVaeAsset()
    {
        var assets = _workflow.Metadata.Assets.ToList();

        assets.Should().Contain(a => a.Parameter == "Vae" && a.DefaultValue == "ae.safetensors");
        assets.Should().Contain(a => a.Parameter == "ModelPatch" && a.Type == BlazorWebApp.Workflows.Models.AssetType.ModelPatch);
        assets.Should().Contain(a => a.Parameter == "BaseModel" && a.Label == "Phase 1 - ZIB Model");
        assets.Should().Contain(a => a.Parameter == "Model" && a.Label == "Phase 2 - ZIT Model");
        assets.Should().NotContain(a => a.Parameter == "VaeMergeA");
        assets.Should().NotContain(a => a.Parameter == "VaeMergeB");
    }

    [Fact]
    public void GetFragments_ShouldExposeCoreSamplersAndOptionalUpscale()
    {
        var fragments = _workflow.GetFragments().ToList();

        var baseSampler = fragments.Should().Contain(f => f.Metadata.Id == "base_sampler").Subject;
        baseSampler.Metadata.Title.Should().Be("Phase 1 - ZIB");
        baseSampler.Metadata.Collapsible.Should().BeFalse();
        baseSampler.Metadata.Type.Should().Be(BlazorWebApp.Workflows.Models.FragmentType.Sampler);

        var zitSampler = fragments.Should().Contain(f => f.Metadata.Id == "zit_sampler").Subject;
        zitSampler.Metadata.Title.Should().Be("Phase 2 - ZIT");
        zitSampler.Metadata.Collapsible.Should().BeFalse();
        zitSampler.Metadata.Type.Should().Be(BlazorWebApp.Workflows.Models.FragmentType.Settings);

        var upscale = fragments.Should().Contain(f => f.Metadata.Id == "zimage_upscale").Subject;
        upscale.Metadata.Collapsible.Should().BeTrue();
        upscale.Metadata.Type.Should().Be(BlazorWebApp.Workflows.Models.FragmentType.Enhancement);
        upscale.Metadata.Parameters.Should().Contain(p => p.Name == "upscale_sampler_name");
        upscale.Metadata.Parameters.Should().Contain(p => p.Name == "controlnet_node_type");
        upscale.Metadata.Parameters.Should().Contain(p => p.Name == "seam_fix_mode");
    }

    [Fact]
    public void Build_WithoutUpscale_ShouldGenerateRequiredTwoPassGraph()
    {
        var parameters = CreateParameters(upscaleActive: false);
        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("vae_merge", out _).Should().BeFalse();
        json.RootElement.GetProperty("base_unet_loader").GetProperty("class_type").GetString().Should().Be("UNETLoader");
        json.RootElement.GetProperty("sampler_base").GetProperty("class_type").GetString().Should().Be("KSamplerAdvanced");
        json.RootElement.GetProperty("sampler_base").GetProperty("inputs").GetProperty("model")[0].GetString().Should().Be("base_unet_loader");
        json.RootElement.GetProperty("sampler_zit").GetProperty("class_type").GetString().Should().Be("ClownsharKSampler_Beta");
        json.RootElement.GetProperty("sampler_zit").GetProperty("inputs").GetProperty("latent_image")[0].GetString().Should().Be("sampler_base");
        json.RootElement.GetProperty("vae_decoder").GetProperty("inputs").GetProperty("samples")[0].GetString().Should().Be("sampler_zit");
        json.RootElement.TryGetProperty("ultimate_sd_upscale", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_WithUpscaleActive_ShouldChainTwoPassOutputIntoUpscaleGraph()
    {
        var parameters = CreateParameters(upscaleActive: true);
        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("tile_preprocessor").GetProperty("class_type").GetString().Should().Be("AIO_Preprocessor");
        json.RootElement.GetProperty("tile_preprocessor").GetProperty("inputs").GetProperty("image")[0].GetString().Should().Be("vae_decoder");
        json.RootElement.GetProperty("upscale_model_loader").GetProperty("class_type").GetString().Should().Be("UpscaleModelLoader");
        json.RootElement.GetProperty("qwen_controlnet").GetProperty("class_type").GetString().Should().Be("QwenImageDiffsynthControlnet");
        json.RootElement.GetProperty("ultimate_sd_upscale").GetProperty("class_type").GetString().Should().Be("UltimateSDUpscale");
        json.RootElement.GetProperty("ultimate_sd_upscale").GetProperty("inputs").GetProperty("image")[0].GetString().Should().Be("vae_decoder");
    }

    [Fact]
    public void Build_UltimateSdUpscale_ShouldUseLiveSchemaInputs()
    {
        var parameters = CreateParameters(upscaleActive: true);
        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        var inputs = json.RootElement.GetProperty("ultimate_sd_upscale").GetProperty("inputs");
        inputs.GetProperty("upscale_by").GetDouble().Should().Be(2.0);
        inputs.GetProperty("steps").GetInt32().Should().Be(6);
        inputs.GetProperty("cfg").GetDouble().Should().Be(1.0);
        inputs.GetProperty("sampler_name").GetString().Should().Be("deis_2m");
        inputs.GetProperty("scheduler").GetString().Should().Be("beta");
        inputs.GetProperty("denoise").GetDouble().Should().Be(0.21);
        inputs.GetProperty("mode_type").GetString().Should().Be("Linear");
        inputs.GetProperty("tile_padding").GetInt32().Should().Be(32);
        inputs.GetProperty("force_uniform_tiles").GetBoolean().Should().BeTrue();
        inputs.TryGetProperty("denoise_strength", out _).Should().BeFalse();
        inputs.TryGetProperty("padding", out _).Should().BeFalse();
        inputs.TryGetProperty("force_tile", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_UltimateSdUpscale_ShouldCalculateTileSizeInCSharp()
    {
        var parameters = CreateParameters(upscaleActive: true);
        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        var inputs = json.RootElement.GetProperty("ultimate_sd_upscale").GetProperty("inputs");
        inputs.GetProperty("tile_width").GetInt32().Should().Be(904);
        inputs.GetProperty("tile_height").GetInt32().Should().Be(1280);
    }

    [Fact]
    public void Build_WithZImageControlNetToggle_ShouldSwapControlNetNodeType()
    {
        var parameters = CreateParameters(upscaleActive: true);
        parameters.GetOrCreateFragment("zimage_upscale")
            .SetValue("controlnet_node_type", "ZImageFunControlnet");

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("qwen_controlnet").GetProperty("class_type").GetString().Should().Be("ZImageFunControlnet");
    }

    [Fact]
    public void Build_WithDetailerActive_ShouldUsePhase2ZitModel()
    {
        var parameters = CreateParameters(upscaleActive: false);
        parameters.Assets["Model"] = "phase-2-zit-model.safetensors";
        parameters.Assets["BaseModel"] = "phase-1-zib-model.safetensors";
        parameters.GetOrCreateFragment("detailer").IsActive = true;

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("detailer_unet_loader")
            .GetProperty("inputs")
            .GetProperty("unet_name")
            .GetString()
            .Should().Be("phase-2-zit-model.safetensors");
    }

    [Fact]
    public void Build_WithModelPatchAssetInSubfolder_ShouldPassSubfolderPathToModelPatchLoader()
    {
        var parameters = CreateParameters(upscaleActive: true);
        parameters.Assets["ModelPatch"] = "Z-Image-Turbo-Fun/Z-Image-Turbo-Fun-Controlnet-Tile-2.1-2601-8steps.safetensors";

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("model_patch_loader")
            .GetProperty("inputs")
            .GetProperty("name")
            .GetString()
                .Should().Be("Z-Image-Turbo-Fun/Z-Image-Turbo-Fun-Controlnet-Tile-2.1-2601-8steps.safetensors");
    }

    private static GenerationParameters CreateParameters(bool upscaleActive)
    {
        var parameters = new GenerationParameters();

        parameters.Assets["Model"] = "z_image_turbo_bf16.safetensors";
        parameters.Assets["BaseModel"] = "z_image_bf16.safetensors";
        parameters.Assets["Clip"] = "qwen_3_4b.safetensors";
        parameters.Assets["Vae"] = "ae.safetensors";
        parameters.Assets["ModelPatch"] = "Z-Image-Turbo-Fun-Controlnet-Tile-2.1-8steps.safetensors";
        parameters.Assets["UpscaleModel"] = "x1_ITF_SkinDiffDetail_Lite_v1.pth";

        var prompts = parameters.GetOrCreateFragment("prompts");
        prompts.SetValue("positive", "a beautiful landscape");
        prompts.SetValue("negative", "");

        var latent = parameters.GetOrCreateFragment("latent");
        latent.SetValue("width", 872);
        latent.SetValue("height", 1248);
        latent.SetValue("batch_size", 1);

        var baseSampler = parameters.GetOrCreateFragment("base_sampler");
        baseSampler.SetValue("sampler_name", "res_multistep");
        baseSampler.SetValue("scheduler", "simple");
        baseSampler.SetValue("steps", 8);
        baseSampler.SetValue("cfg", 4.0);
        baseSampler.SetValue("seed", 42L);

        var zitSampler = parameters.GetOrCreateFragment("zit_sampler");
        zitSampler.SetValue("sampler_name", "linear/ralston_2s");
        zitSampler.SetValue("scheduler", "beta");
        zitSampler.SetValue("steps", 10);
        zitSampler.SetValue("cfg", 1.0);
        zitSampler.SetValue("denoise", 0.56);
        zitSampler.SetValue("eta", 0.23);
        zitSampler.SetValue("seed", 41075146839208L);

        var upscale = parameters.GetOrCreateFragment("zimage_upscale");
        upscale.IsActive = upscaleActive;
        upscale.SetValue("upscale_by", 2.0);
        upscale.SetValue("strength", 0.2);
        upscale.SetValue("controlnet_node_type", "QwenImageDiffsynthControlnet");
        upscale.SetValue("upscale_sampler_name", "deis_2m");
        upscale.SetValue("upscale_scheduler", "beta");
        upscale.SetValue("upscale_steps", 6);
        upscale.SetValue("upscale_cfg", 1.0);
        upscale.SetValue("upscale_denoise", 0.21);
        upscale.SetValue("upscale_seed", 41075146839208L);
        upscale.SetValue("mode_type", "Linear");
        upscale.SetValue("auto_tile_size", true);
        upscale.SetValue("mask_blur", 8);
        upscale.SetValue("tile_padding", 32);
        upscale.SetValue("seam_fix_mode", "None");
        upscale.SetValue("seam_fix_denoise", 1.0);
        upscale.SetValue("seam_fix_width", 64);
        upscale.SetValue("seam_fix_mask_blur", 8);
        upscale.SetValue("seam_fix_padding", 16);
        upscale.SetValue("force_uniform_tiles", true);
        upscale.SetValue("tiled_decode", false);
        upscale.SetValue("batch_size", 1);

        return parameters;
    }
}