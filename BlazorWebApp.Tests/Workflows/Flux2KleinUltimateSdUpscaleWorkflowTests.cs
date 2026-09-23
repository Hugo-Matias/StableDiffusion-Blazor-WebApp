using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Fragments.Flux;
using BlazorWebApp.Workflows.Templates.Flux;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows;

public class Flux2KleinUltimateSdUpscaleWorkflowTests
{
    [Fact]
    public void Txt2Img_ShouldExposeAndBuildUltimateSdUpscale()
    {
        var workflow = new Flux2KleinTxt2ImgWorkflow();
        workflow.GetFragments().Should().Contain(f => f.Metadata.Id == "ultimate_sd_upscale");

        var parameters = CreateTxt2ImgParameters();
        ActivateUltimateSdUpscale(parameters);

        using var json = JsonDocument.Parse(workflow.Build(parameters).Json);

        AssertUltimateSdUpscaleUsesCurrentKleinPipeline(json, expectedImageInput: "vae_decoder", expectedModelInput: "unet_loader");
    }

    [Fact]
    public void Img2ImgEdit_ShouldExposeAndBuildUltimateSdUpscale()
    {
        var workflow = new Flux2KleinImg2ImgEditWorkflow();
        workflow.GetFragments().Should().Contain(f => f.Metadata.Id == "ultimate_sd_upscale");

        var parameters = CreateImg2ImgEditParameters();
        ActivateUltimateSdUpscale(parameters);

        using var json = JsonDocument.Parse(workflow.Build(parameters).Json);

        AssertUltimateSdUpscaleUsesCurrentKleinPipeline(
            json,
            expectedImageInput: "vae_decoder",
            expectedModelInput: "unet_loader",
            expectedTileWidth: 864,
            expectedTileHeight: 1280);
    }

    [Fact]
    public void Img2ImgEdit_InpaintModeWithoutMask_ShouldUseRegularEditLatent()
    {
        var workflow = new Flux2KleinImg2ImgEditWorkflow();
        var parameters = CreateImg2ImgEditParameters(ReferenceLatentSettingsFragment.EditModeInpaint);

        using var json = JsonDocument.Parse(workflow.Build(parameters).Json);

        json.RootElement.TryGetProperty("inpaint_conditioning", out _).Should().BeFalse();
        json.RootElement.GetProperty("empty_latent").GetProperty("class_type").GetString().Should().Be("EmptyFlux2LatentImage");
        json.RootElement.GetProperty("sampler_main").GetProperty("inputs").GetProperty("latent_image")[0].GetString().Should().Be("empty_latent");
    }

    [Fact]
    public void Img2ImgEdit_InpaintModeWithMask_ShouldBuildInpaintConditioning()
    {
        var workflow = new Flux2KleinImg2ImgEditWorkflow();
        var parameters = CreateImg2ImgEditParameters(ReferenceLatentSettingsFragment.EditModeInpaint, includeMask: true);

        using var json = JsonDocument.Parse(workflow.Build(parameters).Json);

        json.RootElement.TryGetProperty("empty_latent", out _).Should().BeFalse();

        var maskLoader = json.RootElement.GetProperty("inpaint_mask_loader");
        maskLoader.GetProperty("class_type").GetString().Should().Be("LoadImage");
        maskLoader.GetProperty("inputs").GetProperty("image").GetString().Should().Be("mask.png");

        var maskScaleInputs = json.RootElement.GetProperty("inpaint_mask_scale").GetProperty("inputs");
        json.RootElement.GetProperty("inpaint_mask_scale").GetProperty("class_type").GetString().Should().Be("ImageScaleToTotalPixels");
        maskScaleInputs.GetProperty("image")[0].GetString().Should().Be("inpaint_mask_loader");
        maskScaleInputs.GetProperty("upscale_method").GetString().Should().Be("nearest-exact");
        maskScaleInputs.GetProperty("megapixels").GetDouble().Should().Be(1.0);
        maskScaleInputs.GetProperty("resolution_steps").GetInt32().Should().Be(1);

        var imageToMaskInputs = json.RootElement.GetProperty("inpaint_image_to_mask").GetProperty("inputs");
        json.RootElement.GetProperty("inpaint_image_to_mask").GetProperty("class_type").GetString().Should().Be("ImageToMask");
        imageToMaskInputs.GetProperty("image")[0].GetString().Should().Be("inpaint_mask_scale");
        imageToMaskInputs.GetProperty("channel").GetString().Should().Be("red");

        var conditioningInputs = json.RootElement.GetProperty("inpaint_conditioning").GetProperty("inputs");
        json.RootElement.GetProperty("inpaint_conditioning").GetProperty("class_type").GetString().Should().Be("InpaintModelConditioning");
        conditioningInputs.GetProperty("positive")[0].GetString().Should().Be("ref_latent_positive_0");
        conditioningInputs.GetProperty("negative")[0].GetString().Should().Be("ref_latent_negative_0");
        conditioningInputs.GetProperty("vae")[0].GetString().Should().Be("vae_loader");
        conditioningInputs.GetProperty("pixels")[0].GetString().Should().Be("ref_image_scale_0");
        conditioningInputs.GetProperty("mask")[0].GetString().Should().Be("inpaint_image_to_mask");
        conditioningInputs.GetProperty("noise_mask").GetBoolean().Should().BeTrue();

        var guiderInputs = json.RootElement.GetProperty("sampler_main_guider").GetProperty("inputs");
        guiderInputs.GetProperty("positive")[0].GetString().Should().Be("inpaint_conditioning");
        guiderInputs.GetProperty("negative")[0].GetString().Should().Be("inpaint_conditioning");
        json.RootElement.GetProperty("sampler_main").GetProperty("inputs").GetProperty("latent_image")[0].GetString().Should().Be("inpaint_conditioning");
    }

    [Fact]
    public void Img2ImgEdit_PresetConditioningBlankAnchorWithReducedStrength_ShouldBuildBlend()
    {
        var workflow = new Flux2KleinImg2ImgEditWorkflow();
        var parameters = CreateImg2ImgEditParameters();
        var preset = parameters.GetOrCreateFragment("preset_conditioning");
        preset.IsActive = true;
        preset.SetValue("preset_text", "");
        preset.SetValue("preset_strength", 0.73);

        using var json = JsonDocument.Parse(workflow.Build(parameters).Json);

        var instructionsInputs = json.RootElement.GetProperty("instructions_encode").GetProperty("inputs");
        instructionsInputs.GetProperty("text").GetString().Should().Be("");

        var blendInputs = json.RootElement.GetProperty("preset_blend").GetProperty("inputs");
        blendInputs.GetProperty("conditioning_to")[0].GetString().Should().Be("positive_encode");
        blendInputs.GetProperty("conditioning_from")[0].GetString().Should().Be("instructions_encode");
        blendInputs.GetProperty("conditioning_to_strength").GetDouble().Should().Be(0.73);

        var referencePositiveInputs = json.RootElement.GetProperty("ref_latent_positive_0").GetProperty("inputs");
        referencePositiveInputs.GetProperty("conditioning")[0].GetString().Should().Be("preset_blend");
    }

    [Fact]
    public void Img2ImgEdit_PresetConditioningStaleExtendedStrength_ShouldClampToNeutralNoOp()
    {
        var workflow = new Flux2KleinImg2ImgEditWorkflow();
        var parameters = CreateImg2ImgEditParameters();
        var preset = parameters.GetOrCreateFragment("preset_conditioning");
        preset.IsActive = true;
        preset.SetValue("preset_text", "");
        preset.SetValue("preset_strength", 1.5);

        using var json = JsonDocument.Parse(workflow.Build(parameters).Json);

        json.RootElement.TryGetProperty("instructions_encode", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("preset_blend", out _).Should().BeFalse();
    }

    [Fact]
    public void Img2ImgEdit_PresetConditioningTimedMode_ShouldApplyBlendOnlyInsideWindow()
    {
        var workflow = new Flux2KleinImg2ImgEditWorkflow();
        var parameters = CreateImg2ImgEditParameters();
        var preset = parameters.GetOrCreateFragment("preset_conditioning");
        preset.IsActive = true;
        preset.SetValue("preset_text", "anchor");
        preset.SetValue("preset_strength", 0.55);
        preset.SetValue("timing_mode", PresetConditioningFragment.TimingCustom);
        preset.SetValue("timing_start", 0.2);
        preset.SetValue("timing_end", 0.8);

        using var json = JsonDocument.Parse(workflow.Build(parameters).Json);

        var blendRangeInputs = json.RootElement.GetProperty("preset_blend_range").GetProperty("inputs");
        json.RootElement.GetProperty("preset_blend_range").GetProperty("class_type").GetString().Should().Be("ConditioningSetTimestepRange");
        blendRangeInputs.GetProperty("conditioning")[0].GetString().Should().Be("preset_blend");
        blendRangeInputs.GetProperty("start").GetDouble().Should().Be(0.2);
        blendRangeInputs.GetProperty("end").GetDouble().Should().Be(0.8);

        var beforeInputs = json.RootElement.GetProperty("preset_main_before_range").GetProperty("inputs");
        beforeInputs.GetProperty("conditioning")[0].GetString().Should().Be("positive_encode");
        beforeInputs.GetProperty("start").GetDouble().Should().Be(0.0);
        beforeInputs.GetProperty("end").GetDouble().Should().Be(0.2);

        var afterInputs = json.RootElement.GetProperty("preset_main_after_range").GetProperty("inputs");
        afterInputs.GetProperty("conditioning")[0].GetString().Should().Be("positive_encode");
        afterInputs.GetProperty("start").GetDouble().Should().Be(0.8);
        afterInputs.GetProperty("end").GetDouble().Should().Be(1.0);

        var referencePositiveInputs = json.RootElement.GetProperty("ref_latent_positive_0").GetProperty("inputs");
        referencePositiveInputs.GetProperty("conditioning")[0].GetString().Should().Be("preset_timed_combine_after");
    }

    [Fact]
    public void Img2ImgEdit_InactivePresetConditioning_ShouldNotBuildBlend()
    {
        var workflow = new Flux2KleinImg2ImgEditWorkflow();
        var parameters = CreateImg2ImgEditParameters();
        var preset = parameters.GetOrCreateFragment("preset_conditioning");
        preset.IsActive = false;
        preset.SetValue("preset_text", "");
        preset.SetValue("preset_strength", 0.73);

        using var json = JsonDocument.Parse(workflow.Build(parameters).Json);

        json.RootElement.TryGetProperty("instructions_encode", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("preset_blend", out _).Should().BeFalse();

        var referencePositiveInputs = json.RootElement.GetProperty("ref_latent_positive_0").GetProperty("inputs");
        referencePositiveInputs.GetProperty("conditioning")[0].GetString().Should().Be("positive_encode");
    }

    [Fact]
    public void GridRef_ShouldExposeAndBuildUltimateSdUpscale()
    {
        var workflow = new Flux2KleinGridRefWorkflow();
        workflow.GetFragments().Should().Contain(f => f.Metadata.Id == "ultimate_sd_upscale");

        var parameters = CreateGridRefParameters();
        ActivateUltimateSdUpscale(parameters);

        using var json = JsonDocument.Parse(workflow.Build(parameters).Json);

        AssertUltimateSdUpscaleUsesCurrentKleinPipeline(json, expectedImageInput: "vae_decoder", expectedModelInput: "unet_loader");
    }

    [Fact]
    public void AdonisUpscale_ShouldExposeAndBuildUltimateSdUpscaleUsingRefineModelBranch()
    {
        var workflow = new Flux2KleinAdonisUpscaleWorkflow();
        workflow.GetFragments().Should().Contain(f => f.Metadata.Id == "ultimate_sd_upscale");

        var parameters = CreateAdonisParameters();
        ActivateUltimateSdUpscale(parameters);

        using var json = JsonDocument.Parse(workflow.Build(parameters).Json);

        AssertUltimateSdUpscaleUsesCurrentKleinPipeline(json, expectedImageInput: "vae_decoder", expectedModelInput: "adonis_refine_for_refine");
    }

    private static void AssertUltimateSdUpscaleUsesCurrentKleinPipeline(
        JsonDocument json,
        string expectedImageInput,
        string expectedModelInput,
        int expectedTileWidth = 1056,
        int expectedTileHeight = 1056)
    {
        json.RootElement.GetProperty("ultimate_sd_upscale_model_loader")
            .GetProperty("class_type")
            .GetString()
            .Should().Be("UpscaleModelLoader");

        var inputs = json.RootElement.GetProperty("ultimate_sd_upscale").GetProperty("inputs");
        json.RootElement.GetProperty("ultimate_sd_upscale").GetProperty("class_type").GetString().Should().Be("UltimateSDUpscale");
        inputs.GetProperty("image")[0].GetString().Should().Be(expectedImageInput);
        inputs.GetProperty("model")[0].GetString().Should().Be(expectedModelInput);
        inputs.GetProperty("positive")[0].GetString().Should().NotBeNullOrWhiteSpace();
        inputs.GetProperty("negative")[0].GetString().Should().NotBeNullOrWhiteSpace();
        inputs.GetProperty("vae")[0].GetString().Should().Be("vae_loader");
        inputs.GetProperty("upscale_model")[0].GetString().Should().Be("ultimate_sd_upscale_model_loader");
        inputs.GetProperty("upscale_by").GetDouble().Should().Be(2.0);
        inputs.GetProperty("seed").GetInt64().Should().Be(123L);
        inputs.GetProperty("tile_width").GetInt32().Should().Be(expectedTileWidth);
        inputs.GetProperty("tile_height").GetInt32().Should().Be(expectedTileHeight);
        json.RootElement.GetProperty("save").GetProperty("inputs").GetProperty("images")[0].GetString().Should().Be("ultimate_sd_upscale");
    }

    private static void ActivateUltimateSdUpscale(GenerationParameters parameters)
    {
        var fragment = parameters.GetOrCreateFragment("ultimate_sd_upscale");
        fragment.IsActive = true;
        fragment.SetValue("upscale_model", "4x-UltraSharpV2.safetensors");
        fragment.SetValue("upscale_by", 2.0);
        fragment.SetValue("upscale_sampler_name", "dpmpp_2m");
        fragment.SetValue("upscale_scheduler", "beta");
        fragment.SetValue("upscale_steps", 20);
        fragment.SetValue("upscale_cfg", 8.0);
        fragment.SetValue("upscale_denoise", 0.2);
        fragment.SetValue("upscale_seed", 123L);
        fragment.SetValue("auto_tile_size", true);
        fragment.SetValue("mode_type", "Linear");
        fragment.SetValue("mask_blur", 8);
        fragment.SetValue("tile_padding", 32);
        fragment.SetValue("seam_fix_mode", "None");
        fragment.SetValue("seam_fix_denoise", 1.0);
        fragment.SetValue("seam_fix_width", 64);
        fragment.SetValue("seam_fix_mask_blur", 8);
        fragment.SetValue("seam_fix_padding", 16);
        fragment.SetValue("force_uniform_tiles", true);
        fragment.SetValue("tiled_decode", false);
        fragment.SetValue("batch_size", 1);
    }

    private static GenerationParameters CreateTxt2ImgParameters()
    {
        var parameters = CreateKleinAssetParameters("flux-2-klein-base-9b-fp8.safetensors", "full_encoder_small_decoder.safetensors");
        ConfigurePrompts(parameters);
        ConfigureLatent(parameters);
        ConfigureSampler(parameters);
        parameters.GetOrCreateFragment("seed_vr2").IsActive = false;
        parameters.GetOrCreateFragment("detailer").IsActive = false;
        return parameters;
    }

    private static GenerationParameters CreateImg2ImgEditParameters(
        string editMode = ReferenceLatentSettingsFragment.EditModeRegular,
        bool includeMask = false)
    {
        var parameters = CreateKleinAssetParameters("flux-2-klein-base-9b-fp8.safetensors", "full_encoder_small_decoder.safetensors");
        parameters.Sources["reference_image"] = new SourceAsset
        {
            FilePath = "reference.png",
            Width = 832,
            Height = 1248,
            MaskFilename = includeMask ? "mask.png" : null
        };
        ConfigurePrompts(parameters);
        ConfigureSampler(parameters);
        var settings = parameters.GetOrCreateFragment("reference_latent_settings");
        settings.SetValue("edit_mode", editMode);
        settings.SetValue("megapixels", 1.0);
        settings.SetValue("resolution_steps", 1);
        parameters.GetOrCreateFragment("seed_vr2").IsActive = false;
        parameters.GetOrCreateFragment("detailer").IsActive = false;
        return parameters;
    }

    private static GenerationParameters CreateGridRefParameters()
    {
        var parameters = CreateKleinAssetParameters("flux-2-klein-9b-fp8.safetensors", "flux2-vae.safetensors");
        parameters.Sources["grid_reference_1"] = new SourceAsset { FilePath = "reference-1.png" };
        parameters.Sources["grid_reference_2"] = new SourceAsset { FilePath = "reference-2.png" };
        parameters.Sources["grid_reference_3"] = new SourceAsset { FilePath = "reference-3.png" };
        parameters.Sources["grid_reference_4"] = new SourceAsset { FilePath = "reference-4.png" };
        ConfigurePrompts(parameters);
        ConfigureLatent(parameters);
        ConfigureSampler(parameters, steps: 4, cfg: 1.0);
        var settings = parameters.GetOrCreateFragment("grid_ref_settings");
        settings.SetValue("grid_tile_megapixels", 1.5);
        settings.SetValue("grid_megapixels", 3.5);
        settings.SetValue("base_megapixels", 1.25);
        parameters.GetOrCreateFragment("seed_vr2").IsActive = false;
        parameters.GetOrCreateFragment("detailer").IsActive = false;
        return parameters;
    }

    private static GenerationParameters CreateAdonisParameters()
    {
        var parameters = CreateKleinAssetParameters("flux-2-klein-9b.safetensors", "flux2-vae.safetensors");
        parameters.Sources["input_image"] = new SourceAsset { FilePath = "input.png" };
        ConfigurePrompts(parameters);
        ConfigureSampler(parameters, samplerName: "exponential/res_2s", steps: 9, cfg: 1.0);
        var settings = parameters.GetOrCreateFragment("adonis_settings");
        settings.SetValue("megapixels", 1.7);
        settings.SetValue("multiple_of", 16);
        settings.SetValue("adonis_base_strength", 1.0);
        settings.SetValue("adonis_base_target", "base");
        settings.SetValue("adonis_refine_strength", 1.0);
        settings.SetValue("adonis_refine_target", "refine");
        return parameters;
    }

    private static GenerationParameters CreateKleinAssetParameters(string model, string vae)
    {
        var parameters = new GenerationParameters();
        parameters.Assets["Model"] = model;
        parameters.Assets["Clip"] = "qwen_3_8b_fp8mixed.safetensors";
        parameters.Assets["Vae"] = vae;
        return parameters;
    }

    private static void ConfigurePrompts(GenerationParameters parameters)
    {
        var prompts = parameters.GetOrCreateFragment("prompts");
        prompts.SetValue("positive", "portrait at the beach");
        prompts.SetValue("negative", "bad quality");
    }

    private static void ConfigureLatent(GenerationParameters parameters)
    {
        var latent = parameters.GetOrCreateFragment("latent");
        latent.SetValue("width", 1024);
        latent.SetValue("height", 1024);
        latent.SetValue("batch_size", 1);
    }

    private static void ConfigureSampler(
        GenerationParameters parameters,
        string samplerName = "euler",
        int steps = 20,
        double cfg = 5.0)
    {
        var sampler = parameters.GetOrCreateFragment("main_sampler");
        sampler.SetValue("sampler_name", samplerName);
        sampler.SetValue("scheduler", "simple");
        sampler.SetValue("steps", steps);
        sampler.SetValue("cfg", cfg);
        sampler.SetValue("denoise", 1.0);
        sampler.SetValue("eta", 1.0);
        sampler.SetValue("seed", 42L);
    }
}
