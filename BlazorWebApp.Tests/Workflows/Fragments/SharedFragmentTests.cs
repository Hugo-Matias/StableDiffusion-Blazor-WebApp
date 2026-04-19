using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Models;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows.Fragments;

/// <summary>
/// Tests for shared fragments: LoadCheckpointFragment, SamplerStandardFragment.
/// </summary>
public class SharedFragmentTests
{
    #region LoadCheckpointFragment

    [Fact]
    public void LoadCheckpoint_Build_ShouldCreateCheckpointLoaderNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadCheckpointFragment().Build(builder, registry, new LoadCheckpointFragment.Parameters
        {
            CheckpointName = "v1-5-pruned.safetensors",
            Positive = "a cat",
            Negative = "bad quality"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("model_loader", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("CheckpointLoaderSimple");
        node.GetProperty("inputs").GetProperty("ckpt_name").GetString().Should().Be("v1-5-pruned.safetensors");
    }

    [Fact]
    public void LoadCheckpoint_Build_ShouldCreatePromptPrimitives()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadCheckpointFragment().Build(builder, registry, new LoadCheckpointFragment.Parameters
        {
            CheckpointName = "model.safetensors",
            Positive = "hello world",
            Negative = "ugly"
        });

        var json = JsonDocument.Parse(builder.ToJson());

        json.RootElement.TryGetProperty("text_prompt", out var positive).Should().BeTrue();
        positive.GetProperty("class_type").GetString().Should().Be("PrimitiveStringMultiline");
        positive.GetProperty("inputs").GetProperty("value").GetString().Should().Be("hello world");

        json.RootElement.TryGetProperty("text_negative", out var negative).Should().BeTrue();
        negative.GetProperty("inputs").GetProperty("value").GetString().Should().Be("ugly");
    }

    [Fact]
    public void LoadCheckpoint_Build_ShouldCreateLoraLoaders()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadCheckpointFragment().Build(builder, registry, new LoadCheckpointFragment.Parameters
        {
            CheckpointName = "model.safetensors",
            Positive = "test",
            Negative = ""
        });

        var json = JsonDocument.Parse(builder.ToJson());

        // Positive LoRA loader references model_loader and text_prompt
        json.RootElement.TryGetProperty("lora_positive", out var loraPos).Should().BeTrue();
        loraPos.GetProperty("class_type").GetString().Should().Be("PCLazyLoraLoader");
        var loraPosInputs = loraPos.GetProperty("inputs");
        loraPosInputs.GetProperty("model")[0].GetString().Should().Be("model_loader");
        loraPosInputs.GetProperty("clip")[0].GetString().Should().Be("model_loader");
        loraPosInputs.GetProperty("clip")[1].GetInt32().Should().Be(1);

        // Negative LoRA loader chains from positive
        json.RootElement.TryGetProperty("lora_negative", out var loraNeg).Should().BeTrue();
        var loraNegInputs = loraNeg.GetProperty("inputs");
        loraNegInputs.GetProperty("model")[0].GetString().Should().Be("lora_positive");
        loraNegInputs.GetProperty("clip")[0].GetString().Should().Be("lora_positive");
    }

    [Fact]
    public void LoadCheckpoint_Build_ShouldCreateTextEncoders()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadCheckpointFragment().Build(builder, registry, new LoadCheckpointFragment.Parameters
        {
            CheckpointName = "model.safetensors",
            Positive = "test",
            Negative = "bad"
        });

        var json = JsonDocument.Parse(builder.ToJson());

        // Positive encoder uses lora_positive clip
        json.RootElement.TryGetProperty("encode_positive", out var encPos).Should().BeTrue();
        encPos.GetProperty("class_type").GetString().Should().Be("PCLazyTextEncode");
        encPos.GetProperty("inputs").GetProperty("clip")[0].GetString().Should().Be("lora_positive");

        // Negative encoder uses lora_negative clip
        json.RootElement.TryGetProperty("encode_negative", out var encNeg).Should().BeTrue();
        encNeg.GetProperty("inputs").GetProperty("clip")[0].GetString().Should().Be("lora_negative");
    }

    [Fact]
    public void LoadCheckpoint_Build_ShouldRegisterAllOutputs()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadCheckpointFragment().Build(builder, registry, new LoadCheckpointFragment.Parameters
        {
            CheckpointName = "model.safetensors",
            Positive = "test",
            Negative = ""
        });

        registry.HasOutput("model_output").Should().BeTrue();
        registry.HasOutput("clip_output").Should().BeTrue();
        registry.HasOutput("vae_output").Should().BeTrue();
        registry.HasOutput("positive_output").Should().BeTrue();
        registry.HasOutput("negative_output").Should().BeTrue();

        // model_output and clip_output come from lora_positive
        registry.GetRef("model_output").nodeId.Should().Be("lora_positive");
        registry.GetRef("model_output").outputIndex.Should().Be(0);
        registry.GetRef("clip_output").nodeId.Should().Be("lora_positive");
        registry.GetRef("clip_output").outputIndex.Should().Be(1);

        // vae_output comes from the checkpoint loader (slot 2)
        registry.GetRef("vae_output").nodeId.Should().Be("model_loader");
        registry.GetRef("vae_output").outputIndex.Should().Be(2);
    }

    [Fact]
    public void LoadCheckpoint_Build_WithScope_ShouldPrefixAllNodes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadCheckpointFragment().Build(builder, registry, new LoadCheckpointFragment.Parameters
        {
            CheckpointName = "model.safetensors",
            Positive = "test",
            Negative = ""
        }, scope: "detailer_", scopeTitle: "Detailer ");

        var json = JsonDocument.Parse(builder.ToJson());

        // All nodes should be prefixed
        json.RootElement.TryGetProperty("detailer_model_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("detailer_text_prompt", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("detailer_text_negative", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("detailer_lora_positive", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("detailer_lora_negative", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("detailer_encode_positive", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("detailer_encode_negative", out _).Should().BeTrue();

        // Outputs should be scoped
        registry.HasOutput("detailer_model_output").Should().BeTrue();
        registry.HasOutput("detailer_vae_output").Should().BeTrue();
        registry.HasOutput("detailer_positive_output").Should().BeTrue();
        registry.HasOutput("detailer_negative_output").Should().BeTrue();
    }

    [Fact]
    public void LoadCheckpoint_Build_WithScope_ShouldSetTitles()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadCheckpointFragment().Build(builder, registry, new LoadCheckpointFragment.Parameters
        {
            CheckpointName = "model.safetensors",
            Positive = "test",
            Negative = ""
        }, scope: "detailer_", scopeTitle: "Detailer ");

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("detailer_model_loader")
            .GetProperty("_meta").GetProperty("title").GetString()
            .Should().Be("Detailer Load Checkpoint");
    }

    [Fact]
    public void LoadCheckpoint_Build_WithCustomLoaderId_ShouldUseIt()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadCheckpointFragment().Build(builder, registry, new LoadCheckpointFragment.Parameters
        {
            LoaderId = "custom_loader",
            CheckpointName = "model.safetensors",
            Positive = "test",
            Negative = ""
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("custom_loader", out _).Should().BeTrue();
        registry.GetRef("vae_output").nodeId.Should().Be("custom_loader");
    }

    [Fact]
    public void LoadCheckpoint_Metadata_ShouldBeHiddenLoaderType()
    {
        var fragment = new LoadCheckpointFragment();
        fragment.Metadata.Id.Should().Be("load_checkpoint");
        fragment.Metadata.Type.Should().Be(FragmentType.Loader);
        fragment.Metadata.IsHidden.Should().BeTrue();
    }

    #endregion

    #region SamplerStandardFragment

    [Fact]
    public void SamplerStandard_Build_ShouldCreateKSamplerNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("model_output", "lora_positive", 0);
        registry.Register("positive_output", "encode_positive", 0);
        registry.Register("negative_output", "encode_negative", 0);
        registry.Register("latent_output", "empty_latent", 0);

        new SamplerStandardFragment().Build(builder, registry, new SamplerStandardFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("sampler_main", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("KSampler");
    }

    [Fact]
    public void SamplerStandard_Build_ShouldSetAllInputs()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("model_output", "model", 0);
        registry.Register("positive_output", "pos", 0);
        registry.Register("negative_output", "neg", 0);
        registry.Register("latent_output", "latent", 0);

        new SamplerStandardFragment().Build(builder, registry, new SamplerStandardFragment.Parameters
        {
            SamplerId = "my_sampler",
            SamplerName = "dpmpp_2m",
            Scheduler = "beta",
            Steps = 30,
            Cfg = 5.5,
            Denoise = 0.75,
            Seed = 123
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("my_sampler").GetProperty("inputs");
        inputs.GetProperty("sampler_name").GetString().Should().Be("dpmpp_2m");
        inputs.GetProperty("scheduler").GetString().Should().Be("beta");
        inputs.GetProperty("steps").GetInt32().Should().Be(30);
        inputs.GetProperty("cfg").GetDouble().Should().Be(5.5);
        inputs.GetProperty("denoise").GetDouble().Should().Be(0.75);
        inputs.GetProperty("seed").GetInt64().Should().Be(123);
    }

    [Fact]
    public void SamplerStandard_Build_ShouldReferenceRegistryOutputs()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("model_output", "lora_pos", 0);
        registry.Register("positive_output", "enc_pos", 0);
        registry.Register("negative_output", "enc_neg", 0);
        registry.Register("latent_output", "vae_encoder", 0);

        new SamplerStandardFragment().Build(builder, registry, new SamplerStandardFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("sampler_main").GetProperty("inputs");
        inputs.GetProperty("model")[0].GetString().Should().Be("lora_pos");
        inputs.GetProperty("positive")[0].GetString().Should().Be("enc_pos");
        inputs.GetProperty("negative")[0].GetString().Should().Be("enc_neg");
        inputs.GetProperty("latent_image")[0].GetString().Should().Be("vae_encoder");
    }

    [Fact]
    public void SamplerStandard_Build_ShouldRegisterLatentOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("model_output", "model", 0);
        registry.Register("positive_output", "pos", 0);
        registry.Register("negative_output", "neg", 0);
        registry.Register("latent_output", "latent", 0);

        new SamplerStandardFragment().Build(builder, registry, new SamplerStandardFragment.Parameters
        {
            SamplerId = "my_sampler"
        });

        registry.GetRef("latent_output").nodeId.Should().Be("my_sampler");
        registry.GetRef("latent_output").outputIndex.Should().Be(0);
    }

    [Fact]
    public void SamplerStandard_Build_WithScope_ShouldUseScopedRefs()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("detailer_model_output", "det_model", 0);
        registry.Register("detailer_positive_output", "det_pos", 0);
        registry.Register("detailer_negative_output", "det_neg", 0);
        registry.Register("detailer_latent_output", "det_latent", 0);

        new SamplerStandardFragment().Build(builder, registry, new SamplerStandardFragment.Parameters
        {
            Scope = "detailer_"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("sampler_main").GetProperty("inputs");
        inputs.GetProperty("model")[0].GetString().Should().Be("det_model");
    }

    [Fact]
    public void SamplerStandard_Metadata_ShouldBeSamplerType()
    {
        var fragment = new SamplerStandardFragment();
        fragment.Metadata.Id.Should().Be("main_sampler");
        fragment.Metadata.Type.Should().Be(FragmentType.Sampler);
        fragment.Metadata.Component.Should().Be("SamplerForm");
    }

    #endregion

    #region Integration: LoadCheckpoint -> SamplerStandard Chain

    [Fact]
    public void LoadCheckpoint_ThenSamplerStandard_ShouldChainCorrectly()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        // Load checkpoint (registers model, clip, vae, positive, negative)
        new LoadCheckpointFragment().Build(builder, registry, new LoadCheckpointFragment.Parameters
        {
            CheckpointName = "v1-5-pruned.safetensors",
            Positive = "a beautiful landscape",
            Negative = "ugly"
        });

        // Create empty latent for the sampler
        registry.Register("latent_output", "empty_latent", 0);

        // Sample
        new SamplerStandardFragment().Build(builder, registry, new SamplerStandardFragment.Parameters
        {
            Steps = 20,
            Cfg = 7.0,
            Seed = 42
        });

        // Verify sampler references checkpoint outputs
        var json = JsonDocument.Parse(builder.ToJson());
        var samplerInputs = json.RootElement.GetProperty("sampler_main").GetProperty("inputs");
        samplerInputs.GetProperty("model")[0].GetString().Should().Be("lora_positive");
        samplerInputs.GetProperty("positive")[0].GetString().Should().Be("encode_positive");
        samplerInputs.GetProperty("negative")[0].GetString().Should().Be("encode_negative");

        // Latent output should now point to sampler
        registry.GetRef("latent_output").nodeId.Should().Be("sampler_main");
    }

    #endregion
}
