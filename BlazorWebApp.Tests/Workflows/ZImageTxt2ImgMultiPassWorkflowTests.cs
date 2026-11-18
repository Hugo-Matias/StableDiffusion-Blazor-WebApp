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

        _workflow.Metadata.UsesDualModelLoras.Should().BeTrue();
        _workflow.Metadata.DualModelLoraHighLabel.Should().Be("ZIT");
        _workflow.Metadata.DualModelLoraLowLabel.Should().Be("ZIB");
    }

    [Fact]
    public void GetFragments_ShouldExposeCoreSamplersAndOptionalUpscale()
    {
        var fragments = _workflow.GetFragments().ToList();

        var baseSampler = fragments.Should().Contain(f => f.Metadata.Id == "base_sampler").Subject;
        baseSampler.Metadata.Title.Should().Be("Phase 1 - ZIB");
        baseSampler.Metadata.Collapsible.Should().BeFalse();
        baseSampler.Metadata.Type.Should().Be(BlazorWebApp.Workflows.Models.FragmentType.Sampler);
        baseSampler.Metadata.Parameters.Should().Contain(p => p.Name == "sampler_engine");
        baseSampler.Metadata.Parameters.Should().Contain(p => p.Name == "ksampler_sampler_name");
        baseSampler.Metadata.Parameters.Should().Contain(p => p.Name == "res4lyf_sampler_name");

        var zitSampler = fragments.Should().Contain(f => f.Metadata.Id == "zit_sampler").Subject;
        zitSampler.Metadata.Title.Should().Be("Phase 2 - ZIT");
        zitSampler.Metadata.Collapsible.Should().BeFalse();
        zitSampler.Metadata.Type.Should().Be(BlazorWebApp.Workflows.Models.FragmentType.Settings);
        zitSampler.Metadata.Parameters.Should().Contain(p => p.Name == "sampler_engine");

        var upscale = fragments.Should().Contain(f => f.Metadata.Id == "zimage_upscale").Subject;
        upscale.Metadata.Collapsible.Should().BeTrue();
        upscale.Metadata.Type.Should().Be(BlazorWebApp.Workflows.Models.FragmentType.Enhancement);
        upscale.Metadata.Parameters.Should().Contain(p => p.Name == "upscale_sampler_name");
        upscale.Metadata.Parameters.Should().Contain(p => p.Name == "controlnet_node_type");
        upscale.Metadata.Parameters.Should().Contain(p => p.Name == "seam_fix_mode");

        var upscaleWithModel = fragments.Should().Contain(f => f.Metadata.Id == "upscale_with_model").Subject;
        upscaleWithModel.Metadata.Title.Should().Be("Upscale With Model");
        upscaleWithModel.Metadata.Type.Should().Be(BlazorWebApp.Workflows.Models.FragmentType.Enhancement);
        upscaleWithModel.Metadata.Parameters.Should().Contain(p => p.Name == "upscale_model");
        upscaleWithModel.Metadata.Parameters.Should().Contain(p => p.Name == "blend_factor");
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
        json.RootElement.GetProperty("sampler_base").GetProperty("inputs").GetProperty("noise_seed").GetInt64().Should().Be(42L);
        json.RootElement.GetProperty("sampler_zit").GetProperty("class_type").GetString().Should().Be("ClownsharKSampler_Beta");
        json.RootElement.GetProperty("sampler_zit").GetProperty("inputs").GetProperty("latent_image")[0].GetString().Should().Be("sampler_base");
        json.RootElement.GetProperty("sampler_zit").GetProperty("inputs").GetProperty("seed").GetInt64().Should().Be(41075146839208L);
        json.RootElement.GetProperty("vae_decoder").GetProperty("inputs").GetProperty("samples")[0].GetString().Should().Be("sampler_zit");
        json.RootElement.TryGetProperty("ultimate_sd_upscale", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_WithSamplerEngineOverrides_ShouldSwitchBothSamplerNodeTypes()
    {
        var parameters = CreateParameters(upscaleActive: false);
        var baseSampler = parameters.GetOrCreateFragment("base_sampler");
        baseSampler.SetValue("sampler_engine", "RES4LYF");
        baseSampler.SetValue("res4lyf_sampler_name", "multistep/res_2m");
        baseSampler.SetValue("res4lyf_scheduler", "beta");
        baseSampler.SetValue("denoise", 1.0);
        baseSampler.SetValue("eta", 0.5);

        var zitSampler = parameters.GetOrCreateFragment("zit_sampler");
        zitSampler.SetValue("sampler_engine", "KSamplerAdvanced");
        zitSampler.SetValue("ksampler_sampler_name", "dpmpp_2m_sde");
        zitSampler.SetValue("ksampler_scheduler", "sgm_uniform");
        zitSampler.SetValue("start_at_step", 5);
        zitSampler.SetValue("end_at_step", 999);

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        var baseInputs = json.RootElement.GetProperty("sampler_base").GetProperty("inputs");
        json.RootElement.GetProperty("sampler_base").GetProperty("class_type").GetString().Should().Be("ClownsharKSampler_Beta");
        baseInputs.GetProperty("sampler_name").GetString().Should().Be("multistep/res_2m");
        baseInputs.GetProperty("scheduler").GetString().Should().Be("beta");
        baseInputs.TryGetProperty("noise_seed", out _).Should().BeFalse();

        var zitInputs = json.RootElement.GetProperty("sampler_zit").GetProperty("inputs");
        json.RootElement.GetProperty("sampler_zit").GetProperty("class_type").GetString().Should().Be("KSamplerAdvanced");
        zitInputs.GetProperty("sampler_name").GetString().Should().Be("dpmpp_2m_sde");
        zitInputs.GetProperty("scheduler").GetString().Should().Be("sgm_uniform");
        zitInputs.GetProperty("noise_seed").GetInt64().Should().Be(41075146839208L);
        zitInputs.GetProperty("start_at_step").GetInt32().Should().Be(5);
        zitInputs.GetProperty("end_at_step").GetInt32().Should().Be(999);
        zitInputs.TryGetProperty("denoise", out _).Should().BeFalse();
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
    public void Build_WithLegacyLora_ShouldApplyToPhase2ZitOnly()
    {
        var parameters = CreateParameters(upscaleActive: false);
        parameters.Loras.Add(new Lora
        {
            Name = "legacy_lora",
            Path = "loras/legacy_lora.safetensors",
            Strength = 0.8f,
            IsEnabled = true
        });

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        var loraInputs = json.RootElement.GetProperty("lora_loader_0").GetProperty("inputs");
        json.RootElement.GetProperty("lora_loader_0").GetProperty("class_type").GetString().Should().Be("LoraLoader");
        loraInputs.GetProperty("lora_name").GetString().Should().Be("loras/legacy_lora.safetensors");
        loraInputs.GetProperty("model")[0].GetString().Should().Be("unet_loader");
        loraInputs.GetProperty("clip")[0].GetString().Should().Be("clip_loader");
        json.RootElement.GetProperty("sampler_zit").GetProperty("inputs").GetProperty("model")[0].GetString().Should().Be("lora_loader_0");
        json.RootElement.GetProperty("sampler_base").GetProperty("inputs").GetProperty("model")[0].GetString().Should().Be("base_unet_loader");
        json.RootElement.TryGetProperty("base_lora_loader_0", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_WithZibLora_ShouldApplyModelOnlyToPhase1Base()
    {
        var parameters = CreateParameters(upscaleActive: false);
        parameters.Loras.Add(new Lora
        {
            Name = "zib_lora",
            Path = "loras/zib_lora.safetensors",
            LowPath = "loras/zib_lora.safetensors",
            Strength = 0.6f,
            IsEnabled = true
        });

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        var baseLoraInputs = json.RootElement.GetProperty("base_lora_loader_0").GetProperty("inputs");
        json.RootElement.GetProperty("base_lora_loader_0").GetProperty("class_type").GetString().Should().Be("LoraLoaderModelOnly");
        baseLoraInputs.GetProperty("lora_name").GetString().Should().Be("loras/zib_lora.safetensors");
        baseLoraInputs.GetProperty("model")[0].GetString().Should().Be("base_unet_loader");
        json.RootElement.GetProperty("sampler_base").GetProperty("inputs").GetProperty("model")[0].GetString().Should().Be("base_lora_loader_0");
        json.RootElement.GetProperty("sampler_zit").GetProperty("inputs").GetProperty("model")[0].GetString().Should().Be("unet_loader");
        json.RootElement.TryGetProperty("lora_loader_0", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_WithBothBranchLora_ShouldApplyToPhase1AndPhase2()
    {
        var parameters = CreateParameters(upscaleActive: false);
        parameters.Loras.Add(new Lora
        {
            Name = "both_lora",
            Path = "loras/both_lora.safetensors",
            HighPath = "loras/both_lora.safetensors",
            LowPath = "loras/both_lora.safetensors",
            Strength = 0.7f,
            IsEnabled = true
        });

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("lora_loader_0").GetProperty("class_type").GetString().Should().Be("LoraLoader");
        json.RootElement.GetProperty("base_lora_loader_0").GetProperty("class_type").GetString().Should().Be("LoraLoaderModelOnly");
        json.RootElement.GetProperty("sampler_zit").GetProperty("inputs").GetProperty("model")[0].GetString().Should().Be("lora_loader_0");
        json.RootElement.GetProperty("sampler_base").GetProperty("inputs").GetProperty("model")[0].GetString().Should().Be("base_lora_loader_0");
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
    public void Build_WithUpscaleWithModelActive_ShouldEnhanceCurrentImageOutput()
    {
        var parameters = CreateParameters(upscaleActive: false);
        var upscaleWithModel = parameters.GetOrCreateFragment("upscale_with_model");
        upscaleWithModel.IsActive = true;
        upscaleWithModel.SetValue("upscale_model", "1xSkinContrast-High-SuperUltraCompact.pth");
        upscaleWithModel.SetValue("blend_with_input", true);
        upscaleWithModel.SetValue("blend_factor", 0.4);
        upscaleWithModel.SetValue("blend_mode", "normal");

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("upscale_with_model_loader").GetProperty("class_type").GetString().Should().Be("UpscaleModelLoader");
        json.RootElement.GetProperty("upscale_with_model_loader").GetProperty("inputs").GetProperty("model_name").GetString().Should().Be("1xSkinContrast-High-SuperUltraCompact.pth");
        json.RootElement.GetProperty("upscale_with_model_image").GetProperty("class_type").GetString().Should().Be("ImageUpscaleWithModel");
        json.RootElement.GetProperty("upscale_with_model_image").GetProperty("inputs").GetProperty("image")[0].GetString().Should().Be("vae_decoder");
        json.RootElement.GetProperty("upscale_with_model_blend").GetProperty("class_type").GetString().Should().Be("ImageBlend");
        json.RootElement.GetProperty("upscale_with_model_blend").GetProperty("inputs").GetProperty("image1")[0].GetString().Should().Be("vae_decoder");
        json.RootElement.GetProperty("upscale_with_model_blend").GetProperty("inputs").GetProperty("image2")[0].GetString().Should().Be("upscale_with_model_image");
        json.RootElement.GetProperty("save").GetProperty("inputs").GetProperty("images")[0].GetString().Should().Be("upscale_with_model_blend");
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
        baseSampler.SetValue("sampler_engine", "KSamplerAdvanced");
        baseSampler.SetValue("ksampler_sampler_name", "res_multistep");
        baseSampler.SetValue("ksampler_scheduler", "simple");
        baseSampler.SetValue("sampler_name", "res_multistep");
        baseSampler.SetValue("scheduler", "simple");
        baseSampler.SetValue("steps", 8);
        baseSampler.SetValue("cfg", 4.0);
        baseSampler.SetValue("seed", 42L);

        var zitSampler = parameters.GetOrCreateFragment("zit_sampler");
        zitSampler.SetValue("sampler_engine", "RES4LYF");
        zitSampler.SetValue("res4lyf_sampler_name", "linear/ralston_2s");
        zitSampler.SetValue("res4lyf_scheduler", "beta");
        zitSampler.SetValue("ksampler_sampler_name", "dpmpp_2m_sde");
        zitSampler.SetValue("ksampler_scheduler", "sgm_uniform");
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

        var upscaleWithModel = parameters.GetOrCreateFragment("upscale_with_model");
        upscaleWithModel.IsActive = false;
        upscaleWithModel.SetValue("upscale_model", "1xSkinContrast-High-SuperUltraCompact.pth");
        upscaleWithModel.SetValue("blend_with_input", true);
        upscaleWithModel.SetValue("blend_factor", 0.4);
        upscaleWithModel.SetValue("blend_mode", "normal");

        return parameters;
    }
}