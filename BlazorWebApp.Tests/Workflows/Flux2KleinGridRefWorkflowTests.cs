using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Workflows.Templates.Flux;
using FluentAssertions;
using System.Text.Json;
using Xunit;
using WorkflowAssetType = BlazorWebApp.Workflows.Models.AssetType;

namespace BlazorWebApp.Tests.Workflows;

public class Flux2KleinGridRefWorkflowTests
{
    private readonly Flux2KleinGridRefWorkflow _workflow = new();

    [Fact]
    public void Metadata_ShouldExposeOptionalBaseAndFourRequiredGridSources()
    {
        _workflow.Metadata.Mode.Should().Be(ModeType.Img2Img);

        var sources = _workflow.Metadata.Sources.ToList();
        sources.Should().Contain(s => s.Id == "base_image" && !s.Required);
        sources.Should().Contain(s => s.Id == "grid_reference_1" && s.Required);
        sources.Should().Contain(s => s.Id == "grid_reference_2" && s.Required);
        sources.Should().Contain(s => s.Id == "grid_reference_3" && s.Required);
        sources.Should().Contain(s => s.Id == "grid_reference_4" && s.Required);
    }

    [Fact]
    public void Metadata_ShouldExposeFluxKleinAssetsAndResourceModels()
    {
        var assets = _workflow.Metadata.Assets.ToList();

        assets.Should().Contain(a => a.Parameter == "Model" && a.Type == WorkflowAssetType.DiffusionModel);
        assets.Should().Contain(a => a.Parameter == "Clip" && a.Type == WorkflowAssetType.Clip);
        assets.Should().Contain(a => a.Parameter == "Vae" && a.Type == WorkflowAssetType.Vae);
        _workflow.Metadata.CompatibleResourceBaseModels.Should().Contain("Flux.2 Klein 9B");
        _workflow.Metadata.CompatibleResourceBaseModels.Should().Contain("Flux.2 Klein 9B-base");
    }

    [Fact]
    public void GetFragments_ShouldExposeExpectedUiFragments()
    {
        var fragments = _workflow.GetFragments().ToList();

        fragments.Should().Contain(f => f.Metadata.Id == "prompts");
        fragments.Should().Contain(f => f.Metadata.Id == "grid_ref_settings" && f.Metadata.Component == "FluxKleinGridRefSettingsForm");
        fragments.Should().Contain(f => f.Metadata.Id == "latent");
        fragments.Should().Contain(f => f.Metadata.Id == "main_sampler");
        fragments.Should().Contain(f => f.Metadata.Id == "seed_vr2");
        fragments.Should().Contain(f => f.Metadata.Id == "detailer");
    }

    [Fact]
    public void Build_WithoutBaseImage_ShouldUseFauxResolutionLatentValues()
    {
        var parameters = CreateParameters(includeBase: false);

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.TryGetProperty("base_image_loader", out _).Should().BeFalse();
        var emptyLatentInputs = json.RootElement.GetProperty("empty_latent").GetProperty("inputs");
        emptyLatentInputs.GetProperty("width").GetInt32().Should().Be(832);
        emptyLatentInputs.GetProperty("height").GetInt32().Should().Be(1216);
        emptyLatentInputs.GetProperty("batch_size").GetInt32().Should().Be(2);
        json.RootElement.GetProperty("empty_latent").GetProperty("class_type").GetString().Should().Be("EmptyLatentImage");
        json.RootElement.GetProperty("grid_ref_positive").GetProperty("class_type").GetString().Should().Be("ReferenceLatent");
    }

    [Fact]
    public void Build_WithBaseImage_ShouldSizeLatentFromBaseImageAndReferenceGrid()
    {
        var parameters = CreateParameters(includeBase: true);

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("base_image_loader").GetProperty("class_type").GetString().Should().Be("LoadImage");
        json.RootElement.GetProperty("base_get_image_size").GetProperty("class_type").GetString().Should().Be("GetImageSize");

        var emptyLatentInputs = json.RootElement.GetProperty("empty_latent").GetProperty("inputs");
        emptyLatentInputs.GetProperty("width")[0].GetString().Should().Be("base_get_image_size");
        emptyLatentInputs.GetProperty("height")[0].GetString().Should().Be("base_get_image_size");
        emptyLatentInputs.GetProperty("height")[1].GetInt32().Should().Be(1);

        json.RootElement.GetProperty("base_ref_positive").GetProperty("class_type").GetString().Should().Be("ReferenceLatent");
        json.RootElement.GetProperty("base_ref_negative").GetProperty("class_type").GetString().Should().Be("ReferenceLatent");
        json.RootElement.GetProperty("grid_ref_positive").GetProperty("inputs").GetProperty("conditioning")[0].GetString()
            .Should().Be("base_ref_positive");
    }

    [Fact]
    public void Build_ShouldCreateReferenceGridWithConfiguredScaling()
    {
        var parameters = CreateParameters(includeBase: false);

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("grid_ref_row_top").GetProperty("class_type").GetString().Should().Be("ImageStitch");
        json.RootElement.GetProperty("grid_ref_grid").GetProperty("inputs").GetProperty("direction").GetString().Should().Be("down");
        json.RootElement.GetProperty("grid_ref_scale_1").GetProperty("inputs").GetProperty("megapixels").GetDouble().Should().Be(1.5);
        json.RootElement.GetProperty("grid_ref_grid_scale").GetProperty("inputs").GetProperty("megapixels").GetDouble().Should().Be(3.5);
    }

    [Fact]
    public void Build_ShouldUseBasicSchedulerSamplerPipeline()
    {
        var parameters = CreateParameters(includeBase: false);

        var workflow = _workflow.Build(parameters);
        using var json = JsonDocument.Parse(workflow.Json);

        json.RootElement.GetProperty("sampler_main_scheduler").GetProperty("class_type").GetString().Should().Be("BasicScheduler");
        json.RootElement.GetProperty("sampler_main_select").GetProperty("inputs").GetProperty("sampler_name").GetString().Should().Be("er_sde");
        json.RootElement.GetProperty("sampler_main_scheduler").GetProperty("inputs").GetProperty("scheduler").GetString().Should().Be("simple");
        json.RootElement.GetProperty("sampler_main_scheduler").GetProperty("inputs").GetProperty("steps").GetInt32().Should().Be(4);
        json.RootElement.GetProperty("sampler_main_guider").GetProperty("inputs").GetProperty("cfg").GetDouble().Should().Be(1.0);
        json.RootElement.GetProperty("sampler_main_scheduler").GetProperty("inputs").GetProperty("denoise").GetDouble().Should().Be(1.0);
    }

    private static GenerationParameters CreateParameters(bool includeBase)
    {
        var parameters = new GenerationParameters();

        parameters.Assets["Model"] = "flux-2-klein-9b-fp8.safetensors";
        parameters.Assets["Clip"] = "qwen_3_8b_fp8mixed.safetensors";
        parameters.Assets["Vae"] = "flux2-vae.safetensors";

        if (includeBase)
        {
            parameters.Sources["base_image"] = new SourceAsset { Filename = "base.png" };
        }

        parameters.Sources["grid_reference_1"] = new SourceAsset { Filename = "reference-1.png" };
        parameters.Sources["grid_reference_2"] = new SourceAsset { Filename = "reference-2.png" };
        parameters.Sources["grid_reference_3"] = new SourceAsset { Filename = "reference-3.png" };
        parameters.Sources["grid_reference_4"] = new SourceAsset { Filename = "reference-4.png" };

        var prompts = parameters.GetOrCreateFragment("prompts");
        prompts.SetValue("positive", "portrait at the beach");
        prompts.SetValue("negative", "bad quality");

        var latent = parameters.GetOrCreateFragment("latent");
        latent.SetValue("width", 832);
        latent.SetValue("height", 1216);
        latent.SetValue("batch_size", 2);

        var settings = parameters.GetOrCreateFragment("grid_ref_settings");
        settings.SetValue("grid_tile_megapixels", 1.5);
        settings.SetValue("grid_megapixels", 3.5);
        settings.SetValue("base_megapixels", 1.25);

        var sampler = parameters.GetOrCreateFragment("main_sampler");
        sampler.SetValue("sampler_name", "er_sde");
        sampler.SetValue("scheduler", "simple");
        sampler.SetValue("steps", 4);
        sampler.SetValue("cfg", 1.0);
        sampler.SetValue("denoise", 1.0);
        sampler.SetValue("seed", 12345L);

        return parameters;
    }
}