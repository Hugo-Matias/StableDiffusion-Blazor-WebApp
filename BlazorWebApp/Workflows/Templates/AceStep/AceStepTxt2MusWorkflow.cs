using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Audio;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.AceStep;

public class AceStepTxt2MusWorkflow : IWorkflowBuilder
{
    private readonly AceStepCheckpointLoaderFragment _checkpoint = new();
    private readonly ModelSamplingAuraFlowFragment _modelSampling = new();
    private readonly AceStepMusicConditioningFragment _music = new();
    private readonly AceStepEmptyAudioLatentFragment _latent = new();
    private readonly SamplerStandardFragment _sampler = new()
    {
        Defaults = new SamplerStandardFragment.Parameters
        {
            SamplerName = "euler",
            Scheduler = "simple",
            Steps = 8,
            Cfg = 1.0,
            Denoise = 1.0,
            Seed = 31
        }
    };
    private readonly VaeDecodeAudioFragment _decode = new();
    private readonly SaveAudioMp3Fragment _save = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Ace Step Music",
        Description = "Text-to-music generation with Ace Step 1.5. Use tags for style and arrangement, lyrics for sung content, and the music controls for tempo, key, duration, and sampling behavior.",
        Base = Data.Enums.ModelBase.AceStep,
        Mode = ModeType.Txt2Mus,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = AceStepCheckpointLoaderFragment.AssetKey,
                Label = "Ace Step Checkpoint",
                Type = AssetType.CheckpointModel,
                DefaultValue = AceStepCheckpointLoaderFragment.DefaultCheckpoint,
                Order = 1,
                ColumnSize = 12
            }
        ],
        CompatibleResourceBaseModels = ["AceStep"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _music;
        yield return _sampler;
        yield return _save;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        _checkpoint.Build(builder, parameters, registry);
        _modelSampling.Build(builder, registry, new ModelSamplingAuraFlowFragment.Parameters { ModelShift = 3.0 });
        _music.Build(builder, parameters, registry);
        _latent.Build(builder, parameters, registry);
        _sampler.Build(builder, parameters, registry);
        _decode.Build(builder, parameters, registry);
        _save.Build(builder, parameters, registry);

        return builder.ToComfyWorkflow(registry);
    }
}