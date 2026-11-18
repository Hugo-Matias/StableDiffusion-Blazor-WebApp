using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Wan;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows.Fragments;

/// <summary>
/// Tests for Img2Vid loader fragments (Phase 6 Step 2).
/// </summary>
public class WanLoaderFragmentTests
{
    #region LoadModelSageFragment

    [Fact]
    public void LoadModelSage_Build_ShouldCreateThreeNodeChain()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadModelSageFragment().Build(builder, registry, new LoadModelSageFragment.Parameters
        {
            ModelName = "high_model.safetensors"
        }, scope: "high_");

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("high_unet_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("high_sage", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("high_torch", out _).Should().BeTrue();
    }

    [Fact]
    public void LoadModelSage_Build_ShouldChainNodesCorrectly()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadModelSageFragment().Build(builder, registry, new LoadModelSageFragment.Parameters
        {
            ModelName = "model.safetensors"
        }, scope: "high_");

        var json = JsonDocument.Parse(builder.ToJson());

        // sage -> unet_loader
        var sageInputs = json.RootElement.GetProperty("high_sage").GetProperty("inputs");
        sageInputs.GetProperty("model")[0].GetString().Should().Be("high_unet_loader");

        // torch -> sage
        var torchInputs = json.RootElement.GetProperty("high_torch").GetProperty("inputs");
        torchInputs.GetProperty("model")[0].GetString().Should().Be("high_sage");
    }

    [Fact]
    public void LoadModelSage_Build_ShouldRegisterScopedModelOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadModelSageFragment().Build(builder, registry, new LoadModelSageFragment.Parameters
        {
            ModelName = "model.safetensors"
        }, scope: "high_");

        registry.HasOutput("high_model_output").Should().BeTrue();
        var (nodeId, index) = registry.GetRef("high_model_output");
        nodeId.Should().Be("high_torch");
        index.Should().Be(0);
    }

    [Fact]
    public void LoadModelSage_Build_ShouldSetCorrectClassTypes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadModelSageFragment().Build(builder, registry, new LoadModelSageFragment.Parameters
        {
            ModelName = "model.safetensors"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("unet_loader").GetProperty("class_type").GetString().Should().Be("UNETLoader");
        json.RootElement.GetProperty("sage").GetProperty("class_type").GetString().Should().Be("PathchSageAttentionKJ");
        json.RootElement.GetProperty("torch").GetProperty("class_type").GetString().Should().Be("ModelPatchTorchSettings");
    }

    [Fact]
    public void LoadModelSage_Build_DualModels_ShouldCreateIndependentChains()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadModelSageFragment().Build(builder, registry, new LoadModelSageFragment.Parameters
        {
            ModelName = "high_model.safetensors"
        }, scope: "high_");

        new LoadModelSageFragment().Build(builder, registry, new LoadModelSageFragment.Parameters
        {
            ModelName = "low_model.safetensors"
        }, scope: "low_");

        builder.NodeCount.Should().Be(6);
        registry.HasOutput("high_model_output").Should().BeTrue();
        registry.HasOutput("low_model_output").Should().BeTrue();

        var (highNode, _) = registry.GetRef("high_model_output");
        var (lowNode, _) = registry.GetRef("low_model_output");
        highNode.Should().Be("high_torch");
        lowNode.Should().Be("low_torch");
    }

    #endregion

    #region LoadClipVaeFragment

    [Fact]
    public void LoadClipVae_Build_ShouldCreateTwoNodes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadClipVaeFragment().Build(builder, registry, new LoadClipVaeFragment.Parameters
        {
            ClipName = "clip.safetensors",
            VaeName = "vae.safetensors"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("clip_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("vae_loader", out _).Should().BeTrue();
    }

    [Fact]
    public void LoadClipVae_Build_ShouldRegisterBothOutputs()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadClipVaeFragment().Build(builder, registry, new LoadClipVaeFragment.Parameters
        {
            ClipName = "clip.safetensors",
            VaeName = "vae.safetensors"
        });

        registry.HasOutput("clip_output").Should().BeTrue();
        registry.HasOutput("vae_output").Should().BeTrue();
    }

    [Fact]
    public void LoadClipVae_Build_ShouldSetWanClipType()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadClipVaeFragment().Build(builder, registry, new LoadClipVaeFragment.Parameters
        {
            ClipName = "umt5.safetensors",
            VaeName = "vae.safetensors"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var clipInputs = json.RootElement.GetProperty("clip_loader").GetProperty("inputs");
        clipInputs.GetProperty("type").GetString().Should().Be("wan");
        clipInputs.GetProperty("device").GetString().Should().Be("cpu");
    }

    [Fact]
    public void LoadClipVae_Build_ShouldSetCorrectClassTypes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        new LoadClipVaeFragment().Build(builder, registry, new LoadClipVaeFragment.Parameters
        {
            ClipName = "clip.safetensors",
            VaeName = "vae.safetensors"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("clip_loader").GetProperty("class_type").GetString().Should().Be("CLIPLoader");
        json.RootElement.GetProperty("vae_loader").GetProperty("class_type").GetString().Should().Be("VAELoader");
    }

    #endregion

    #region LoraLoaderModelOnlyFragment

    [Fact]
    public void LoraModelOnly_Build_ShouldCreateLoraLoaderNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("high_model_output", "high_torch", 0);

        new LoraLoaderModelOnlyFragment().Build(builder, registry, new LoraLoaderModelOnlyFragment.Parameters
        {
            LoraLoaderId = "high_lora_0",
            LoraPath = "loras/my_lora.safetensors",
            LoraStrength = 0.8f,
            ModelInputName = "high_model_output",
            ModelOutputName = "high_lora_model_output"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("high_lora_0", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("LoraLoader");
    }

    [Fact]
    public void LoraModelOnly_Build_ShouldReferenceModelInput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("high_model_output", "high_torch", 0);

        new LoraLoaderModelOnlyFragment().Build(builder, registry, new LoraLoaderModelOnlyFragment.Parameters
        {
            LoraLoaderId = "high_lora_0",
            LoraPath = "lora.safetensors",
            LoraStrength = 1.0f,
            ModelInputName = "high_model_output",
            ModelOutputName = "high_lora_model_output"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("high_lora_0").GetProperty("inputs");
        inputs.GetProperty("model")[0].GetString().Should().Be("high_torch");
        inputs.GetProperty("model")[1].GetInt32().Should().Be(0);
    }

    [Fact]
    public void LoraModelOnly_Build_ShouldRegisterOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("model_output", "torch_node", 0);

        new LoraLoaderModelOnlyFragment().Build(builder, registry, new LoraLoaderModelOnlyFragment.Parameters
        {
            LoraLoaderId = "lora_0",
            LoraPath = "lora.safetensors",
            LoraStrength = 1.0f,
            ModelInputName = "model_output",
            ModelOutputName = "lora_model_output"
        });

        registry.HasOutput("lora_model_output").Should().BeTrue();
    }

    [Fact]
    public void LoraModelOnly_Build_ShouldSetStrengthValues()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("model_output", "torch_node", 0);

        new LoraLoaderModelOnlyFragment().Build(builder, registry, new LoraLoaderModelOnlyFragment.Parameters
        {
            LoraLoaderId = "lora_0",
            LoraPath = "lora.safetensors",
            LoraStrength = 0.75f,
            LoraClipStrength = 0.5f,
            ModelInputName = "model_output",
            ModelOutputName = "lora_model_output"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("lora_0").GetProperty("inputs");
        inputs.GetProperty("strength_model").GetDouble().Should().BeApproximately(0.75, 0.01);
        inputs.GetProperty("strength_clip").GetDouble().Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public void LoraModelOnly_Build_ChainedLoras_ShouldOverwriteOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("high_model_output", "high_torch", 0);

        // First LoRA
        new LoraLoaderModelOnlyFragment().Build(builder, registry, new LoraLoaderModelOnlyFragment.Parameters
        {
            LoraLoaderId = "high_lora_0",
            LoraPath = "lora1.safetensors",
            LoraStrength = 1.0f,
            ModelInputName = "high_model_output",
            ModelOutputName = "high_lora_model_output"
        });

        // Second LoRA chains from first
        new LoraLoaderModelOnlyFragment().Build(builder, registry, new LoraLoaderModelOnlyFragment.Parameters
        {
            LoraLoaderId = "high_lora_1",
            LoraPath = "lora2.safetensors",
            LoraStrength = 0.5f,
            ModelInputName = "high_lora_model_output",
            ModelOutputName = "high_lora_model_output"
        });

        // Final output should point to second lora
        var (nodeId, _) = registry.GetRef("high_lora_model_output");
        nodeId.Should().Be("high_lora_1");

        // Second lora should reference first lora
        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("high_lora_1").GetProperty("inputs");
        inputs.GetProperty("model")[0].GetString().Should().Be("high_lora_0");
    }

    #endregion

    #region ModelSamplingSD3Fragment

    [Fact]
    public void ModelSamplingSD3_Build_ShouldCreateNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("high_model_output", "high_torch", 0);

        new ModelSamplingSD3Fragment().Build(builder, registry, new ModelSamplingSD3Fragment.Parameters
        {
            SamplerId = "high_model_sampling",
            Shift = 8,
            ModelInputName = "high_model_output",
            ModelOutputName = "high_sampled_model_output"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("high_model_sampling", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("ModelSamplingSD3");
    }

    [Fact]
    public void ModelSamplingSD3_Build_ShouldSetShift()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("model_output", "torch", 0);

        new ModelSamplingSD3Fragment().Build(builder, registry, new ModelSamplingSD3Fragment.Parameters
        {
            SamplerId = "sampling",
            Shift = 12,
            ModelInputName = "model_output",
            ModelOutputName = "sampled_output"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("sampling").GetProperty("inputs");
        inputs.GetProperty("shift").GetInt32().Should().Be(12);
    }

    [Fact]
    public void ModelSamplingSD3_Build_ShouldReferenceModelInput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("high_lora_model_output", "high_lora_0", 0);

        new ModelSamplingSD3Fragment().Build(builder, registry, new ModelSamplingSD3Fragment.Parameters
        {
            SamplerId = "high_sampling",
            ModelInputName = "high_lora_model_output",
            ModelOutputName = "high_sampled_model_output"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("high_sampling").GetProperty("inputs");
        inputs.GetProperty("model")[0].GetString().Should().Be("high_lora_0");
    }

    [Fact]
    public void ModelSamplingSD3_Build_ShouldRegisterOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("model_output", "torch", 0);

        new ModelSamplingSD3Fragment().Build(builder, registry, new ModelSamplingSD3Fragment.Parameters
        {
            SamplerId = "high_sampling",
            ModelInputName = "model_output",
            ModelOutputName = "high_sampled_model_output"
        });

        registry.HasOutput("high_sampled_model_output").Should().BeTrue();
    }

    #endregion

    #region Integration: Full Dual-Model Loader Chain

    [Fact]
    public void FullDualModelChain_ShouldProduceCorrectRegistry()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        // Load high model
        new LoadModelSageFragment().Build(builder, registry, new LoadModelSageFragment.Parameters
        {
            ModelName = "high_model.safetensors"
        }, scope: "high_");

        // Load low model
        new LoadModelSageFragment().Build(builder, registry, new LoadModelSageFragment.Parameters
        {
            ModelName = "low_model.safetensors"
        }, scope: "low_");

        // Load CLIP + VAE
        new LoadClipVaeFragment().Build(builder, registry, new LoadClipVaeFragment.Parameters
        {
            ClipName = "clip.safetensors",
            VaeName = "vae.safetensors"
        });

        // Apply ModelSamplingSD3 to both
        new ModelSamplingSD3Fragment().Build(builder, registry, new ModelSamplingSD3Fragment.Parameters
        {
            SamplerId = "high_model_sampling",
            Shift = 8,
            ModelInputName = "high_model_output",
            ModelOutputName = "high_sampled_model_output"
        });

        new ModelSamplingSD3Fragment().Build(builder, registry, new ModelSamplingSD3Fragment.Parameters
        {
            SamplerId = "low_model_sampling",
            Shift = 8,
            ModelInputName = "low_model_output",
            ModelOutputName = "low_sampled_model_output"
        });

        // Verify all expected outputs
        registry.HasOutput("high_model_output").Should().BeTrue();
        registry.HasOutput("low_model_output").Should().BeTrue();
        registry.HasOutput("clip_output").Should().BeTrue();
        registry.HasOutput("vae_output").Should().BeTrue();
        registry.HasOutput("high_sampled_model_output").Should().BeTrue();
        registry.HasOutput("low_sampled_model_output").Should().BeTrue();

        // Verify node count: 3 high + 3 low + 2 clip/vae + 2 sampling = 10
        builder.NodeCount.Should().Be(10);

        // Verify JSON is valid
        var action = () => JsonDocument.Parse(builder.ToJson());
        action.Should().NotThrow();
    }

    [Fact]
    public void FullDualModelChain_WithLoras_ShouldChainCorrectly()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        // Load both models
        new LoadModelSageFragment().Build(builder, registry, new LoadModelSageFragment.Parameters
        {
            ModelName = "high_model.safetensors"
        }, scope: "high_");

        new LoadModelSageFragment().Build(builder, registry, new LoadModelSageFragment.Parameters
        {
            ModelName = "low_model.safetensors"
        }, scope: "low_");

        // Apply LoRA to high model only
        new LoraLoaderModelOnlyFragment().Build(builder, registry, new LoraLoaderModelOnlyFragment.Parameters
        {
            LoraLoaderId = "high_lora_0",
            LoraPath = "my_lora_high.safetensors",
            LoraStrength = 0.8f,
            ModelInputName = "high_model_output",
            ModelOutputName = "high_lora_model_output"
        });

        // ModelSamplingSD3 should use lora output for high, direct for low
        new ModelSamplingSD3Fragment().Build(builder, registry, new ModelSamplingSD3Fragment.Parameters
        {
            SamplerId = "high_model_sampling",
            Shift = 8,
            ModelInputName = "high_lora_model_output",
            ModelOutputName = "high_sampled_model_output"
        });

        new ModelSamplingSD3Fragment().Build(builder, registry, new ModelSamplingSD3Fragment.Parameters
        {
            SamplerId = "low_model_sampling",
            Shift = 8,
            ModelInputName = "low_model_output",
            ModelOutputName = "low_sampled_model_output"
        });

        // Verify high sampling chains through lora
        var json = JsonDocument.Parse(builder.ToJson());
        var highSamplingInputs = json.RootElement.GetProperty("high_model_sampling").GetProperty("inputs");
        highSamplingInputs.GetProperty("model")[0].GetString().Should().Be("high_lora_0");

        // Verify low sampling chains directly to torch
        var lowSamplingInputs = json.RootElement.GetProperty("low_model_sampling").GetProperty("inputs");
        lowSamplingInputs.GetProperty("model")[0].GetString().Should().Be("low_torch");
    }

    #endregion
}
