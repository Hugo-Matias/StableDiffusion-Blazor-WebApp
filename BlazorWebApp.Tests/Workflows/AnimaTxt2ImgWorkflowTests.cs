using BlazorWebApp.Models;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Workflows.Templates.Anima;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows;

public class AnimaTxt2ImgWorkflowTests
{
    private readonly AnimaTxt2ImgWorkflow _workflow = new();

    [Fact]
    public void Metadata_ShouldDescribeWorkflowAndAssets()
    {
        _workflow.Metadata.Description.Should().NotBeNullOrWhiteSpace();
        _workflow.Metadata.Base.Should().Be(BlazorWebApp.Data.Enums.ModelBase.Anima);
        _workflow.Metadata.Mode.Should().Be(ModeType.Txt2Img);

        var assets = _workflow.Metadata.Assets.ToList();
        assets.Should().Contain(a => a.Parameter == "Model" && a.Type == BlazorWebApp.Workflows.Models.AssetType.DiffusionModel);
        assets.Should().Contain(a => a.Parameter == "Clip" && a.Type == BlazorWebApp.Workflows.Models.AssetType.Clip);
        assets.Should().Contain(a => a.Parameter == "Vae" && a.Type == BlazorWebApp.Workflows.Models.AssetType.Vae);
    }

    [Fact]
    public void GetFragments_ShouldExposeUpscaleEnhancements()
    {
        var fragments = _workflow.GetFragments().ToList();

        var ultimate = fragments.Should().Contain(f => f.Metadata.Id == "ultimate_sd_upscale").Subject;
        ultimate.Metadata.Title.Should().Be("Ultimate SD Upscale");
        ultimate.Metadata.Type.Should().Be(FragmentType.Enhancement);
        ultimate.Metadata.Component.Should().Be("UltimateSdUpscaleForm");
        ultimate.Metadata.Parameters.Should().Contain(p => p.Name == "upscale_model" && p.Source != null && p.Source.NodeType == "UpscaleModelLoader");
        ultimate.Metadata.Parameters.Should().Contain(p => p.Name == "upscale_by" && p.Min == 0.05 && p.Max == 4);
        ultimate.Metadata.Parameters.Should().Contain(p => p.Name == "upscale_sampler_name" && p.Source != null && p.Source.NodeType == "UltimateSDUpscale");
        ultimate.Metadata.Parameters.Should().NotContain(p => p.Name == "controlnet_node_type");
        ultimate.Metadata.Parameters.Should().NotContain(p => p.Name == "strength");

        var upscaleWithModel = fragments.Should().Contain(f => f.Metadata.Id == "upscale_with_model").Subject;
        upscaleWithModel.Metadata.Title.Should().Be("Upscale With Model");
        upscaleWithModel.Metadata.Component.Should().Be("UpscaleWithModelForm");
        upscaleWithModel.Metadata.Description.Should().Contain("1x models");
    }

    [Fact]
    public void Build_WithoutOptionalUpscale_ShouldGenerateBaseGraph()
    {
        var parameters = CreateParameters();
        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("unet_loader").GetProperty("class_type").GetString().Should().Be("UNETLoader");
        json.RootElement.GetProperty("sampler_main").GetProperty("class_type").GetString().Should().Be("KSampler");
        json.RootElement.GetProperty("vae_decoder").GetProperty("inputs").GetProperty("samples")[0].GetString().Should().Be("sampler_main");
        json.RootElement.TryGetProperty("ultimate_sd_upscale", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("upscale_with_model_image", out _).Should().BeFalse();
        json.RootElement.GetProperty("save").GetProperty("inputs").GetProperty("images")[0].GetString().Should().Be("vae_decoder");
    }

    [Fact]
    public void Build_WithUltimateSdUpscaleActive_ShouldUpscaleImageThenRunTiledRefinement()
    {
        var parameters = CreateParameters();
        var ultimate = parameters.GetOrCreateFragment("ultimate_sd_upscale");
        ultimate.IsActive = true;
        ultimate.SetValue("upscale_model", "4x-UltraSharpV2.safetensors");
        ultimate.SetValue("upscale_by", 2.0);
        ultimate.SetValue("upscale_sampler_name", "dpmpp_2m");
        ultimate.SetValue("upscale_scheduler", "beta");
        ultimate.SetValue("upscale_steps", 20);
        ultimate.SetValue("upscale_cfg", 8.0);
        ultimate.SetValue("upscale_denoise", 0.2);
        ultimate.SetValue("upscale_seed", 123L);
        ultimate.SetValue("auto_tile_size", true);
        ultimate.SetValue("mode_type", "Linear");
        ultimate.SetValue("mask_blur", 8);
        ultimate.SetValue("tile_padding", 32);
        ultimate.SetValue("seam_fix_mode", "None");
        ultimate.SetValue("seam_fix_denoise", 1.0);
        ultimate.SetValue("seam_fix_width", 64);
        ultimate.SetValue("seam_fix_mask_blur", 8);
        ultimate.SetValue("seam_fix_padding", 16);
        ultimate.SetValue("force_uniform_tiles", true);
        ultimate.SetValue("tiled_decode", false);
        ultimate.SetValue("batch_size", 1);

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("ultimate_sd_upscale_model_loader").GetProperty("class_type").GetString().Should().Be("UpscaleModelLoader");
        json.RootElement.GetProperty("ultimate_sd_upscale_model_loader").GetProperty("inputs").GetProperty("model_name").GetString().Should().Be("4x-UltraSharpV2.safetensors");

        var inputs = json.RootElement.GetProperty("ultimate_sd_upscale").GetProperty("inputs");
        json.RootElement.GetProperty("ultimate_sd_upscale").GetProperty("class_type").GetString().Should().Be("UltimateSDUpscale");
        inputs.GetProperty("image")[0].GetString().Should().Be("vae_decoder");
        inputs.GetProperty("model")[0].GetString().Should().Be("unet_loader");
        inputs.GetProperty("positive")[0].GetString().Should().Be("positive_encode");
        inputs.GetProperty("negative")[0].GetString().Should().Be("negative_encode");
        inputs.GetProperty("vae")[0].GetString().Should().Be("vae_loader");
        inputs.GetProperty("upscale_model")[0].GetString().Should().Be("ultimate_sd_upscale_model_loader");
        inputs.GetProperty("upscale_by").GetDouble().Should().Be(2.0);
        inputs.GetProperty("seed").GetInt64().Should().Be(123L);
        inputs.GetProperty("tile_width").GetInt32().Should().Be(1056);
        inputs.GetProperty("tile_height").GetInt32().Should().Be(1056);

        json.RootElement.TryGetProperty("qwen_controlnet", out _).Should().BeFalse();
        json.RootElement.GetProperty("save").GetProperty("inputs").GetProperty("images")[0].GetString().Should().Be("ultimate_sd_upscale");
    }

    [Fact]
    public void Build_WithUpscaleWithModelActive_ShouldUseSelectedModelAndOverwriteImageOutput()
    {
        var parameters = CreateParameters();
        var upscaleWithModel = parameters.GetOrCreateFragment("upscale_with_model");
        upscaleWithModel.IsActive = true;
        upscaleWithModel.SetValue("upscale_model", "2x_AnimeSharp.pth");
        upscaleWithModel.SetValue("blend_with_input", false);

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("upscale_with_model_loader").GetProperty("class_type").GetString().Should().Be("UpscaleModelLoader");
        json.RootElement.GetProperty("upscale_with_model_loader").GetProperty("inputs").GetProperty("model_name").GetString().Should().Be("2x_AnimeSharp.pth");
        json.RootElement.GetProperty("upscale_with_model_image").GetProperty("class_type").GetString().Should().Be("ImageUpscaleWithModel");
        json.RootElement.GetProperty("upscale_with_model_image").GetProperty("inputs").GetProperty("image")[0].GetString().Should().Be("vae_decoder");
        json.RootElement.TryGetProperty("upscale_with_model_blend", out _).Should().BeFalse();
        json.RootElement.GetProperty("save").GetProperty("inputs").GetProperty("images")[0].GetString().Should().Be("upscale_with_model_image");
    }

    [Fact]
    public void Build_WithBothUpscalesActive_ShouldChainUltimateSdIntoModelUpscale()
    {
        var parameters = CreateParameters();
        var ultimate = parameters.GetOrCreateFragment("ultimate_sd_upscale");
        ultimate.IsActive = true;
        ultimate.SetValue("upscale_seed", 123L);

        var upscaleWithModel = parameters.GetOrCreateFragment("upscale_with_model");
        upscaleWithModel.IsActive = true;
        upscaleWithModel.SetValue("upscale_model", "1xSkinContrast-High-SuperUltraCompact.pth");
        upscaleWithModel.SetValue("blend_with_input", true);
        upscaleWithModel.SetValue("blend_factor", 0.4);
        upscaleWithModel.SetValue("blend_mode", "normal");

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("upscale_with_model_image").GetProperty("inputs").GetProperty("image")[0].GetString().Should().Be("ultimate_sd_upscale");
        json.RootElement.GetProperty("upscale_with_model_blend").GetProperty("inputs").GetProperty("image1")[0].GetString().Should().Be("ultimate_sd_upscale");
        json.RootElement.GetProperty("save").GetProperty("inputs").GetProperty("images")[0].GetString().Should().Be("upscale_with_model_blend");
    }

    private static GenerationParameters CreateParameters()
    {
        var parameters = new GenerationParameters();

        parameters.Assets["Model"] = "anima-preview3-base.safetensors";
        parameters.Assets["Clip"] = "qwen_3_06b_base.safetensors";
        parameters.Assets["Vae"] = "qwen_image_vae.safetensors";

        var prompts = parameters.GetOrCreateFragment("prompts");
        prompts.SetValue("positive", "an anime character portrait");
        prompts.SetValue("negative", "low quality");

        var latent = parameters.GetOrCreateFragment("latent");
        latent.SetValue("width", 1024);
        latent.SetValue("height", 1024);
        latent.SetValue("batch_size", 1);

        var sampler = parameters.GetOrCreateFragment("main_sampler");
        sampler.SetValue("sampler_name", "er_sde");
        sampler.SetValue("scheduler", "simple");
        sampler.SetValue("steps", 30);
        sampler.SetValue("cfg", 4.0);
        sampler.SetValue("denoise", 1.0);
        sampler.SetValue("seed", 42L);

        parameters.GetOrCreateFragment("seed_vr2").IsActive = false;
        parameters.GetOrCreateFragment("ultimate_sd_upscale").IsActive = false;
        parameters.GetOrCreateFragment("detailer").IsActive = false;
        parameters.GetOrCreateFragment("upscale_with_model").IsActive = false;
        parameters.GetOrCreateFragment("background_removal").IsActive = false;
        parameters.GetOrCreateFragment("easy_rembg").IsActive = false;

        return parameters;
    }
}
