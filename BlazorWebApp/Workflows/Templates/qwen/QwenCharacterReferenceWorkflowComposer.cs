using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Qwen;
using BlazorWebApp.Workflows.Models;

namespace BlazorWebApp.Workflows.Templates.Qwen;

public class QwenCharacterReferenceWorkflowComposer
{
    private readonly QwenCharacterLoaderFragment _loaderFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly QwenCharacterShotFragment _shotFragment = new();

    public CharacterReferenceWorkflowBuildResult Build(AppStateCharacter characterState)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        var outputs = new List<CharacterReferenceOutputNode>();

        BuildSourceImage(builder, registry, characterState);
        BuildLoader(builder, registry, characterState);
        _loraLoaderFragment.BuildAll(builder, registry, characterState.Loras);

        foreach (var slot in characterState.Slots.Where(slot => slot.IsEnabled))
        {
            var sourceImageRefKey = ResolveSourceImageRefKey(registry, slot);
            var nodePrefix = CharacterReferenceWorkflowIds.SlotNodePrefix(slot.Id);
            var filenamePrefix = CharacterReferenceWorkflowIds.SlotFilenamePrefix(slot);

            outputs.Add(_shotFragment.Build(builder, registry, new QwenCharacterShotFragment.Parameters
            {
                Slot = ApplySamplerOverrides(slot, characterState.SamplerOverrides),
                NodePrefix = nodePrefix,
                SourceImageRefKey = sourceImageRefKey,
                GlobalNegativePrompt = characterState.GlobalNegativePrompt,
                UseRtxUpscale = characterState.UseRtxUpscale,
                UseCleanGpu = characterState.UseCleanGpu,
                FilenamePrefix = filenamePrefix
            }));
        }

        return new CharacterReferenceWorkflowBuildResult(builder.ToComfyWorkflow(registry), outputs);
    }

    private static void BuildSourceImage(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        AppStateCharacter characterState)
    {
        builder.AddNode(CharacterReferenceWorkflowIds.SourceImageNodeId, node => node
            .Type("LoadImage")
            .Title("Character Source Image")
            .Input("image", characterState.SourceImage.ImagePath ?? string.Empty));

        registry.Register(CharacterReferenceWorkflowIds.SourceImageOutputKey, CharacterReferenceWorkflowIds.SourceImageNodeId, 0);
    }

    private void BuildLoader(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        AppStateCharacter characterState)
    {
        _loaderFragment.Build(builder, registry, new QwenCharacterLoaderFragment.Parameters
        {
            LoaderMode = characterState.LoaderMode,
            CheckpointName = characterState.Assets.Aio.Checkpoint,
            UnetName = characterState.Assets.Split.DiffusionModel,
            ClipName = characterState.Assets.Split.Clip,
            VaeName = characterState.Assets.Split.Vae
        });
    }

    private static string ResolveSourceImageRefKey(NodeRegistry registry, CharacterReferenceSlotState slot)
    {
        return slot.DependencyPolicy switch
        {
            CharacterReferenceDependencyPolicy.FrontViewOutput
                when registry.HasOutput(CharacterReferenceWorkflowIds.SlotImageOutputKey(CharacterReferenceWorkflowIds.FrontViewSlotId))
                => CharacterReferenceWorkflowIds.SlotImageOutputKey(CharacterReferenceWorkflowIds.FrontViewSlotId),
            CharacterReferenceDependencyPolicy.NeutralOutput
                when registry.HasOutput(CharacterReferenceWorkflowIds.SlotImageOutputKey(CharacterReferenceWorkflowIds.NeutralSlotId))
                => CharacterReferenceWorkflowIds.SlotImageOutputKey(CharacterReferenceWorkflowIds.NeutralSlotId),
            CharacterReferenceDependencyPolicy.PreviousSlot
                when !string.IsNullOrWhiteSpace(slot.DependencySlotId)
                    && registry.HasOutput(CharacterReferenceWorkflowIds.SlotImageOutputKey(slot.DependencySlotId))
                => CharacterReferenceWorkflowIds.SlotImageOutputKey(slot.DependencySlotId),
            _ => CharacterReferenceWorkflowIds.SourceImageOutputKey
        };
    }

    private static CharacterReferenceSlotState ApplySamplerOverrides(
        CharacterReferenceSlotState slot,
        CharacterSamplerOverrides overrides)
    {
        if (!overrides.Enabled)
        {
            return slot;
        }

        return new CharacterReferenceSlotState
        {
            Id = slot.Id,
            Label = slot.Label,
            Kind = slot.Kind,
            PresetKey = slot.PresetKey,
            IsBuiltIn = slot.IsBuiltIn,
            Width = slot.Width,
            Height = slot.Height,
            BatchSize = slot.BatchSize,
            Seed = overrides.Seed,
            Steps = overrides.Steps,
            Cfg = overrides.Cfg,
            SamplerName = overrides.SamplerName,
            Scheduler = overrides.Scheduler,
            Denoise = overrides.Denoise,
            PromptTemplate = slot.PromptTemplate,
            PromptExtension = slot.PromptExtension,
            PromptOverride = slot.PromptOverride,
            NegativePromptOverride = slot.NegativePromptOverride,
            IsEnabled = slot.IsEnabled,
            DependencyPolicy = slot.DependencyPolicy,
            DependencySlotId = slot.DependencySlotId,
            LastOutputImageId = slot.LastOutputImageId,
            LastOutputPath = slot.LastOutputPath,
            Status = slot.Status,
            PromptExpanded = slot.PromptExpanded
        };
    }
}
