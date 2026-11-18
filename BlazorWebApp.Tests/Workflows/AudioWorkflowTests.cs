using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Fragments.Audio;
using BlazorWebApp.Workflows.Models;
using BlazorWebApp.Workflows.Templates.AceStep;
using BlazorWebApp.Workflows.Templates.DramaBox;
using FluentAssertions;
using System.Text.Json;
using Xunit;
using WfAssetType = BlazorWebApp.Workflows.Models.AssetType;
using WfSourceType = BlazorWebApp.Workflows.Models.SourceType;

namespace BlazorWebApp.Tests.Workflows;

public class AudioWorkflowTests
{
    [Fact]
    public void AceStep_Metadata_ShouldExposeTxt2MusModeAndCheckpointAsset()
    {
        var workflow = new AceStepTxt2MusWorkflow();

        workflow.Metadata.Title.Should().Be("Ace Step Music");
        workflow.Metadata.Base.Should().Be(Data.Enums.ModelBase.AceStep);
        workflow.Metadata.Mode.Should().Be(ModeType.Txt2Mus);
        workflow.Metadata.CompatibleResourceBaseModels.Should().ContainSingle("AceStep");
        workflow.Metadata.Assets.Should().ContainSingle(asset =>
            asset.Parameter == AceStepCheckpointLoaderFragment.AssetKey &&
            asset.Type == WfAssetType.CheckpointModel &&
            asset.DefaultValue == AceStepCheckpointLoaderFragment.DefaultCheckpoint);
    }

    [Fact]
    public void AceStep_Build_ShouldCreateMusicAudioPipeline()
    {
        var workflow = new AceStepTxt2MusWorkflow();
        var parameters = CreateAceStepParameters();

        var result = workflow.Build(parameters);
        using var json = JsonDocument.Parse(result.Json);
        var root = json.RootElement;

        root.GetProperty("ace_step_checkpoint_loader").GetProperty("class_type").GetString().Should().Be("CheckpointLoaderSimple");
        root.GetProperty("model_sampler_auraflow").GetProperty("class_type").GetString().Should().Be("ModelSamplingAuraFlow");
        root.GetProperty("ace_step_text_encode").GetProperty("class_type").GetString().Should().Be("TextEncodeAceStepAudio1.5");
        root.GetProperty("ace_step_empty_audio_latent").GetProperty("class_type").GetString().Should().Be("EmptyAceStep1.5LatentAudio");
        root.GetProperty("vae_decode_audio").GetProperty("class_type").GetString().Should().Be("VAEDecodeAudio");
        root.GetProperty("save_audio_mp3").GetProperty("class_type").GetString().Should().Be("SaveAudioMP3");

        var musicInputs = root.GetProperty("ace_step_text_encode").GetProperty("inputs");
        musicInputs.GetProperty("tags").GetString().Should().Be("ambient synth, soft pulse");
        musicInputs.GetProperty("duration").GetInt32().Should().Be(12);
        musicInputs.GetProperty("generate_audio_codes").GetBoolean().Should().BeTrue();

        var saveInputs = root.GetProperty("save_audio_mp3").GetProperty("inputs");
        saveInputs.GetProperty("filename_prefix").GetString().Should().Be(SaveAudioMp3Fragment.DefaultFilenamePrefix);
        saveInputs.GetProperty("quality").GetString().Should().Be("V1");
        saveInputs.GetProperty("audio")[0].GetString().Should().Be("vae_decode_audio");
    }

    [Fact]
    public void DramaBox_Metadata_ShouldExposeVoi2VoiModeAndAudioSource()
    {
        var workflow = new DramaBoxVoi2VoiWorkflow();

        workflow.Metadata.Title.Should().Be("DramaBox Voice Clone");
        workflow.Metadata.Base.Should().Be(Data.Enums.ModelBase.DramaBox);
        workflow.Metadata.Mode.Should().Be(ModeType.Voi2Voi);
        workflow.Metadata.CompatibleResourceBaseModels.Should().ContainSingle("DramaBox");
        workflow.Metadata.Assets.Should().BeEmpty();
        workflow.Metadata.Sources.Should().ContainSingle(source =>
            source.Id == DramaBoxAudioReferenceFragment.SourceKey &&
            source.Type == WfSourceType.Audio &&
            source.Required);
    }

    [Fact]
    public void DramaBox_Build_ShouldCreateVoiceCloneAudioPipeline()
    {
        var workflow = new DramaBoxVoi2VoiWorkflow();
        var parameters = CreateDramaBoxParameters();

        var result = workflow.Build(parameters);
        using var json = JsonDocument.Parse(result.Json);
        var root = json.RootElement;

        root.GetProperty("dramabox_load_audio").GetProperty("class_type").GetString().Should().Be("LoadAudio");
        root.GetProperty("dramabox_trim_audio").GetProperty("class_type").GetString().Should().Be("TrimAudioDuration");
        root.GetProperty("melband_model_loader").GetProperty("class_type").GetString().Should().Be("MelBandRoFormerModelLoader");
        root.GetProperty("melband_sampler").GetProperty("class_type").GetString().Should().Be("MelBandRoFormerSampler");
        root.GetProperty("dramabox_prompt").GetProperty("class_type").GetString().Should().Be("Text Multiline");
        root.GetProperty("dramabox_tts").GetProperty("class_type").GetString().Should().Be("DramaBoxTTS");
        root.GetProperty("save_audio_mp3").GetProperty("class_type").GetString().Should().Be("SaveAudioMP3");

        var loadInputs = root.GetProperty("dramabox_load_audio").GetProperty("inputs");
        loadInputs.GetProperty("audio").GetString().Should().Be("reference_voice.wav");

        var trimInputs = root.GetProperty("dramabox_trim_audio").GetProperty("inputs");
        trimInputs.GetProperty("start_index").GetDouble().Should().BeApproximately(2.5, 0.001);
        trimInputs.GetProperty("duration").GetDouble().Should().BeApproximately(18.0, 0.001);

        var ttsInputs = root.GetProperty("dramabox_tts").GetProperty("inputs");
        ttsInputs.GetProperty("voice_sample")[0].GetString().Should().Be("melband_sampler");
        ttsInputs.GetProperty("text")[0].GetString().Should().Be("dramabox_prompt");
        ttsInputs.GetProperty("cfg_scale").GetDouble().Should().BeApproximately(4.0, 0.001);

        root.GetProperty("save_audio_mp3").GetProperty("inputs").GetProperty("audio")[0].GetString().Should().Be("dramabox_tts");
    }

    private static GenerationParameters CreateAceStepParameters()
    {
        var parameters = new GenerationParameters
        {
            Assets = new Dictionary<string, string>
            {
                [AceStepCheckpointLoaderFragment.AssetKey] = AceStepCheckpointLoaderFragment.DefaultCheckpoint
            }
        };

        var music = parameters.GetOrCreateFragment(AceStepMusicConditioningFragment.Id);
        music.SetValue("tags", "ambient synth, soft pulse");
        music.SetValue("duration", 12);
        music.SetValue("batch_size", 1);
        music.SetValue("generate_audio_codes", true);

        var save = parameters.GetOrCreateFragment("save_audio_mp3");
        save.SetValue("quality", "V1");

        return parameters;
    }

    private static GenerationParameters CreateDramaBoxParameters()
    {
        var parameters = new GenerationParameters
        {
            Sources = new Dictionary<string, SourceAsset>
            {
                [DramaBoxAudioReferenceFragment.SourceKey] = new()
                {
                    Filename = "reference_voice.wav",
                    FilePath = "reference_voice.wav"
                }
            }
        };

        var reference = parameters.GetOrCreateFragment(DramaBoxAudioReferenceFragment.Id);
        reference.SetValue("start_index", 2.5);
        reference.SetValue("duration", 18.0);

        var speech = parameters.GetOrCreateFragment(DramaBoxSpeechFragment.Id);
        speech.SetValue("text", "The station clock struck midnight.");
        speech.SetValue("cfg_scale", 4.0);

        return parameters;
    }
}