using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Templates.ZImage;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows;

public class ZImageImg2ImgUpscaleWorkflowTests
{
    private readonly ZImageImg2ImgUpscaleWorkflow _workflow = new();

    [Fact]
    public void Metadata_ShouldExposeSourceImageAndUpscaleAssets()
    {
        _workflow.Metadata.Sources.Should().Contain(s => s.Id == "source_image" && s.Required);

        var assets = _workflow.Metadata.Assets.ToList();
        assets.Should().Contain(a => a.Parameter == "ModelPatch" && a.Type == BlazorWebApp.Workflows.Models.AssetType.ModelPatch);
        assets.Should().Contain(a => a.Parameter == "Model" && a.Label == "Phase 2 - ZIT Model");
        assets.Should().Contain(a => a.Parameter == "UpscaleModel" && a.Label == "Phase 2 - ZIT Upscale Model");
    }

    [Fact]
    public void GetFragments_ShouldExposeUpscaleAsCoreSettings()
    {
        var fragments = _workflow.GetFragments().ToList();

        var upscale = fragments.Should().Contain(f => f.Metadata.Id == "zimage_upscale").Subject;
        upscale.Metadata.Title.Should().Be("Phase 2 - ZIT Upscale");
        upscale.Metadata.Collapsible.Should().BeFalse();
        upscale.Metadata.Type.Should().Be(BlazorWebApp.Workflows.Models.FragmentType.Settings);
        upscale.Metadata.DefaultActive.Should().BeTrue();
    }

    [Fact]
    public void Build_ShouldGenerateRedRegionUpscaleGraphFromSourceImage()
    {
        var parameters = CreateParameters();
        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("load_image").GetProperty("class_type").GetString().Should().Be("LoadImage");
        json.RootElement.GetProperty("tile_preprocessor").GetProperty("inputs").GetProperty("image")[0].GetString().Should().Be("load_image");
        json.RootElement.GetProperty("qwen_controlnet").GetProperty("class_type").GetString().Should().Be("QwenImageDiffsynthControlnet");
        json.RootElement.GetProperty("ultimate_sd_upscale").GetProperty("inputs").GetProperty("image")[0].GetString().Should().Be("load_image");
        json.RootElement.GetProperty("ultimate_sd_upscale").GetProperty("inputs").GetProperty("upscale_model")[0].GetString().Should().Be("upscale_model_loader");
        json.RootElement.TryGetProperty("sampler_base", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("sampler_zit", out _).Should().BeFalse();
    }

    [Fact]
    public void Build_WithModelPatchAssetInSubfolder_ShouldPassSubfolderPathToModelPatchLoader()
    {
        var parameters = CreateParameters();
        parameters.Assets["ModelPatch"] = "Z-Image-Turbo-Fun\\Z-Image-Turbo-Fun-Controlnet-Tile-2.1-2601-8steps.safetensors";

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("model_patch_loader")
            .GetProperty("inputs")
            .GetProperty("name")
            .GetString()
                .Should().Be("Z-Image-Turbo-Fun/Z-Image-Turbo-Fun-Controlnet-Tile-2.1-2601-8steps.safetensors");
    }

    private static GenerationParameters CreateParameters()
    {
        var parameters = new GenerationParameters();

        parameters.Assets["Model"] = "z_image_turbo_bf16.safetensors";
        parameters.Assets["Clip"] = "qwen_3_4b.safetensors";
        parameters.Assets["Vae"] = "ae.safetensors";
        parameters.Assets["ModelPatch"] = "Z-Image-Turbo-Fun-Controlnet-Tile-2.1-8steps.safetensors";
        parameters.Assets["UpscaleModel"] = "x1_ITF_SkinDiffDetail_Lite_v1.pth";

        parameters.Sources["source_image"] = new SourceAsset
        {
            Filename = "input.png",
            Width = 1024,
            Height = 1024
        };

        var prompts = parameters.GetOrCreateFragment("prompts");
        prompts.SetValue("positive", "a beautiful landscape");
        prompts.SetValue("negative", "");

        var upscale = parameters.GetOrCreateFragment("zimage_upscale");
        upscale.IsActive = true;
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