using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Qwen;
using BlazorWebApp.Workflows.Models;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace BlazorWebApp.Tests.Workflows.Fragments;

/// <summary>
/// Tests for Qwen-specific fragments: ModelSamplingAuraFlowFragment, LoadQwenEditFragment, EncodeEditFragment.
/// </summary>
public class QwenFragmentTests
{
    #region ModelSamplingAuraFlowFragment Tests

    [Fact]
    public void ModelSamplingAuraFlow_Metadata_ShouldHaveCorrectId()
    {
        var fragment = new ModelSamplingAuraFlowFragment();
        fragment.Metadata.Id.Should().Be("model_sampling");
    }

    [Fact]
    public void ModelSamplingAuraFlow_Metadata_ShouldBeHidden()
    {
        var fragment = new ModelSamplingAuraFlowFragment();
        fragment.Metadata.IsHidden.Should().BeTrue();
    }

    [Fact]
    public void ModelSamplingAuraFlow_Build_ShouldAddModelSamplerNode()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("model_output", "unet_loader", 0);

        var fragment = new ModelSamplingAuraFlowFragment();
        fragment.Build(builder, registry, new ModelSamplingAuraFlowFragment.Parameters
        {
            ModelShift = 3.10
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("model_sampler_auraflow", out var node).Should().BeTrue();
        node.GetProperty("class_type").GetString().Should().Be("ModelSamplingAuraFlow");
        node.GetProperty("inputs").GetProperty("shift").GetDouble().Should().Be(3.10);
    }

    [Fact]
    public void ModelSamplingAuraFlow_Build_ShouldReferenceModelOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("model_output", "unet_loader", 0);

        var fragment = new ModelSamplingAuraFlowFragment();
        fragment.Build(builder, registry, new ModelSamplingAuraFlowFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        var inputs = json.RootElement.GetProperty("model_sampler_auraflow").GetProperty("inputs");
        inputs.GetProperty("model")[0].GetString().Should().Be("unet_loader");
    }

    [Fact]
    public void ModelSamplingAuraFlow_Build_ShouldOverwriteModelOutput()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("model_output", "unet_loader", 0);

        var fragment = new ModelSamplingAuraFlowFragment();
        fragment.Build(builder, registry, new ModelSamplingAuraFlowFragment.Parameters());

        var ref_ = registry.GetRef("model_output");
        ref_.Should().Be(("model_sampler_auraflow", 0));
    }

    [Fact]
    public void ModelSamplingAuraFlow_Build_WithScope_ShouldUseScopedIds()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("detailer_model_output", "detailer_unet", 0);

        var fragment = new ModelSamplingAuraFlowFragment();
        fragment.Build(builder, registry, new ModelSamplingAuraFlowFragment.Parameters
        {
            Scope = "detailer_",
            ScopeTitle = "Detailer "
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("detailer_model_sampler_auraflow", out _).Should().BeTrue();
        registry.GetRef("detailer_model_output").Should().Be(("detailer_model_sampler_auraflow", 0));
    }

    #endregion

    #region LoadQwenEditFragment Tests

    [Fact]
    public void LoadQwenEdit_Metadata_ShouldHaveCorrectId()
    {
        var fragment = new LoadQwenEditFragment();
        fragment.Metadata.Id.Should().Be("loader_qwen_edit");
    }

    [Fact]
    public void LoadQwenEdit_Build_ShouldAddAllNodes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var fragment = new LoadQwenEditFragment();
        fragment.Build(builder, registry, new LoadQwenEditFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("unet_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("clip_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("vae_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("lora_loader", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("model_sampling", out _).Should().BeTrue();
        json.RootElement.TryGetProperty("cfg_norm", out _).Should().BeTrue();
    }

    [Fact]
    public void LoadQwenEdit_Build_ShouldUseQwenImageClipType()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var fragment = new LoadQwenEditFragment();
        fragment.Build(builder, registry, new LoadQwenEditFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("clip_loader").GetProperty("inputs")
            .GetProperty("type").GetString().Should().Be("qwen_image");
    }

    [Fact]
    public void LoadQwenEdit_Build_ShouldChainCorrectly()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var fragment = new LoadQwenEditFragment();
        fragment.Build(builder, registry, new LoadQwenEditFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());

        // lora_loader -> unet_loader
        json.RootElement.GetProperty("lora_loader").GetProperty("inputs")
            .GetProperty("model")[0].GetString().Should().Be("unet_loader");

        // model_sampling -> lora_loader
        json.RootElement.GetProperty("model_sampling").GetProperty("inputs")
            .GetProperty("model")[0].GetString().Should().Be("lora_loader");

        // cfg_norm -> model_sampling
        json.RootElement.GetProperty("cfg_norm").GetProperty("inputs")
            .GetProperty("model")[0].GetString().Should().Be("model_sampling");
    }

    [Fact]
    public void LoadQwenEdit_Build_ShouldRegisterOutputs()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var fragment = new LoadQwenEditFragment();
        fragment.Build(builder, registry, new LoadQwenEditFragment.Parameters());

        registry.GetRef("model_output").Should().Be(("cfg_norm", 0));
        registry.GetRef("clip_output").Should().Be(("clip_loader", 0));
        registry.GetRef("vae_output").Should().Be(("vae_loader", 0));
    }

    [Fact]
    public void LoadQwenEdit_Build_ShouldUseCustomParameters()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var fragment = new LoadQwenEditFragment();
        fragment.Build(builder, registry, new LoadQwenEditFragment.Parameters
        {
            UnetName = "custom_model.safetensors",
            LoraStrength = 0.8,
            ModelShift = 5.0,
            CfgNormStrength = 0.5
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("unet_loader").GetProperty("inputs")
            .GetProperty("unet_name").GetString().Should().Be("custom_model.safetensors");
        json.RootElement.GetProperty("lora_loader").GetProperty("inputs")
            .GetProperty("strength_model").GetDouble().Should().Be(0.8);
        json.RootElement.GetProperty("model_sampling").GetProperty("inputs")
            .GetProperty("shift").GetDouble().Should().Be(5.0);
        json.RootElement.GetProperty("cfg_norm").GetProperty("inputs")
            .GetProperty("strength").GetDouble().Should().Be(0.5);
    }

    [Fact]
    public void LoadQwenEdit_Build_LoraLoader_ShouldUseCorrectClassType()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var fragment = new LoadQwenEditFragment();
        fragment.Build(builder, registry, new LoadQwenEditFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("lora_loader").GetProperty("class_type")
            .GetString().Should().Be("LoraLoaderModelOnly");
    }

    #endregion

    #region EncodeEditFragment Tests

    [Fact]
    public void EncodeEdit_Metadata_ShouldHaveCorrectId()
    {
        var fragment = new EncodeEditFragment();
        fragment.Metadata.Id.Should().Be("encode_edit");
    }

    [Fact]
    public void EncodeEdit_Build_ShouldAddBothEncodeNodes()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("clip_output", "clip_loader", 0);
        registry.Register("vae_output", "vae_loader", 0);
        registry.Register("image_input", "image_scale", 0);

        var fragment = new EncodeEditFragment();
        fragment.Build(builder, registry, new EncodeEditFragment.Parameters
        {
            Positive = "edit this image",
            Negative = ""
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.TryGetProperty("encode_positive", out var pos).Should().BeTrue();
        json.RootElement.TryGetProperty("encode_negative", out var neg).Should().BeTrue();
        pos.GetProperty("class_type").GetString().Should().Be("TextEncodeQwenImageEditPlus");
        neg.GetProperty("class_type").GetString().Should().Be("TextEncodeQwenImageEditPlus");
    }

    [Fact]
    public void EncodeEdit_Build_ShouldReferenceClipVaeAndImage()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("clip_output", "clip_loader", 0);
        registry.Register("vae_output", "vae_loader", 0);
        registry.Register("image_input", "image_scale", 0);

        var fragment = new EncodeEditFragment();
        fragment.Build(builder, registry, new EncodeEditFragment.Parameters());

        var json = JsonDocument.Parse(builder.ToJson());
        var posInputs = json.RootElement.GetProperty("encode_positive").GetProperty("inputs");
        posInputs.GetProperty("clip")[0].GetString().Should().Be("clip_loader");
        posInputs.GetProperty("vae")[0].GetString().Should().Be("vae_loader");
        posInputs.GetProperty("image1")[0].GetString().Should().Be("image_scale");
    }

    [Fact]
    public void EncodeEdit_Build_ShouldRegisterOutputs()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("clip_output", "clip_loader", 0);
        registry.Register("vae_output", "vae_loader", 0);
        registry.Register("image_input", "image_scale", 0);

        var fragment = new EncodeEditFragment();
        fragment.Build(builder, registry, new EncodeEditFragment.Parameters());

        registry.GetRef("positive_output").Should().Be(("encode_positive", 0));
        registry.GetRef("negative_output").Should().Be(("encode_negative", 0));
    }

    [Fact]
    public void EncodeEdit_Build_ShouldSetPromptValues()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("clip_output", "clip_loader", 0);
        registry.Register("vae_output", "vae_loader", 0);
        registry.Register("image_input", "image_scale", 0);

        var fragment = new EncodeEditFragment();
        fragment.Build(builder, registry, new EncodeEditFragment.Parameters
        {
            Positive = "make it blue",
            Negative = "ugly"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("encode_positive").GetProperty("inputs")
            .GetProperty("prompt").GetString().Should().Be("make it blue");
        json.RootElement.GetProperty("encode_negative").GetProperty("inputs")
            .GetProperty("prompt").GetString().Should().Be("ugly");
    }

    [Fact]
    public void EncodeEdit_Build_WithCustomImageRef_ShouldUseIt()
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        registry.Register("clip_output", "clip_loader", 0);
        registry.Register("vae_output", "vae_loader", 0);
        registry.Register("custom_image", "custom_loader", 0);

        var fragment = new EncodeEditFragment();
        fragment.Build(builder, registry, new EncodeEditFragment.Parameters
        {
            ImageRef = "custom_image"
        });

        var json = JsonDocument.Parse(builder.ToJson());
        json.RootElement.GetProperty("encode_positive").GetProperty("inputs")
            .GetProperty("image1")[0].GetString().Should().Be("custom_loader");
    }

    #endregion
}
