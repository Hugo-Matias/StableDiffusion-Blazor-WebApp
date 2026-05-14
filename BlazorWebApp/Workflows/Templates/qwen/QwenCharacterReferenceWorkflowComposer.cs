using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Qwen;
using BlazorWebApp.Workflows.Models;

namespace BlazorWebApp.Workflows.Templates.Qwen;

public class QwenCharacterReferenceWorkflowComposer : ICharacterReferenceWorkflowComposer
{
    private readonly QwenCharacterLoaderFragment _loaderFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly QwenCharacterShotFragment _shotFragment = new();

    public CharacterReferenceEngine Engine => CharacterReferenceEngine.Qwen;

    public CharacterReferenceWorkflowBuildResult Build(AppStateCharacter characterState)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        var outputs = new List<CharacterReferenceOutputNode>();

        BuildSourceImage(builder, registry, characterState);
        BuildReusableDependencyImages(builder, registry, characterState);
        BuildLoader(builder, registry, characterState);
        _loraLoaderFragment.BuildAll(builder, registry, characterState.Loras);

        foreach (var slot in OrderSlotsForBuild(characterState))
        {
            var sourceImageRefKey = ResolveSourceImageRefKey(registry, slot);
            var nodePrefix = CharacterReferenceWorkflowIds.SlotNodePrefix(slot.Id);
            var filenamePrefix = CharacterReferenceWorkflowIds.SlotFilenamePrefix(slot);

            outputs.Add(_shotFragment.Build(builder, registry, new QwenCharacterShotFragment.Parameters
            {
                Slot = ApplyRunSettings(slot, characterState),
                NodePrefix = nodePrefix,
                SourceImageRefKey = sourceImageRefKey,
                GlobalPositivePromptExtension = characterState.GlobalPositivePromptExtension,
                GlobalNegativePrompt = characterState.GlobalNegativePrompt,
                UseRtxUpscale = characterState.UseRtxUpscale,
                UseCleanGpu = characterState.UseCleanGpu,
                FaceReplacement = characterState.FaceReplacement,
                OriginalSourceImageRefKey = CharacterReferenceWorkflowIds.SourceImageOutputKey,
                CloseNeutralFaceRefKey = ResolveCloseNeutralFaceRefKey(registry, characterState),
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

    private static void BuildReusableDependencyImages(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        AppStateCharacter characterState)
    {
        foreach (var (slotId, imagePath) in characterState.ReusableDependencyImagePaths)
        {
            if (string.IsNullOrWhiteSpace(slotId) || string.IsNullOrWhiteSpace(imagePath))
            {
                continue;
            }

            var nodeId = CharacterReferenceWorkflowIds.DependencyImageNodeId(slotId);
            var label = characterState.Slots.FirstOrDefault(slot => slot.Id == slotId)?.Label ?? slotId;

            builder.AddNode(nodeId, node => node
                .Type("LoadImage")
                .Title($"{label} Reused Dependency")
                .Input("image", imagePath));

            registry.Register(CharacterReferenceWorkflowIds.SlotImageOutputKey(slotId), nodeId, 0);
        }
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

    private static IReadOnlyList<CharacterReferenceSlotState> OrderSlotsForBuild(AppStateCharacter characterState)
    {
        var enabledSlots = characterState.Slots.Where(slot => slot.IsEnabled).ToList();
        if (!characterState.FaceReplacement.Enabled || !characterState.FaceReplacement.UseCloseNeutralReference)
        {
            return enabledSlots;
        }

        var neutralSlot = enabledSlots.FirstOrDefault(slot => slot.Id == CharacterReferenceWorkflowIds.NeutralSlotId);
        if (neutralSlot is null)
        {
            return enabledSlots;
        }

        return enabledSlots
            .Where(slot => slot.Id != CharacterReferenceWorkflowIds.NeutralSlotId)
            .Prepend(neutralSlot)
            .ToList();
    }

    private static string? ResolveCloseNeutralFaceRefKey(
        NodeRegistry registry,
        AppStateCharacter characterState)
    {
        if (!characterState.FaceReplacement.Enabled || !characterState.FaceReplacement.UseCloseNeutralReference)
        {
            return null;
        }

        var neutralOutputKey = CharacterReferenceWorkflowIds.SlotImageOutputKey(CharacterReferenceWorkflowIds.NeutralSlotId);
        return registry.HasOutput(neutralOutputKey) ? neutralOutputKey : null;
    }

    private static CharacterReferenceSlotState ApplyRunSettings(
        CharacterReferenceSlotState slot,
        AppStateCharacter characterState)
    {
        if (characterState.GlobalSeed < 0 && !characterState.SamplerOverrides.Enabled)
        {
            return slot;
        }

        var overrides = characterState.SamplerOverrides;

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
            Seed = characterState.GlobalSeed >= 0 ? characterState.GlobalSeed : slot.Seed,
            Steps = overrides.Enabled ? overrides.Steps : slot.Steps,
            Cfg = overrides.Enabled ? overrides.Cfg : slot.Cfg,
            SamplerName = overrides.Enabled ? overrides.SamplerName : slot.SamplerName,
            Scheduler = overrides.Enabled ? overrides.Scheduler : slot.Scheduler,
            Denoise = overrides.Enabled ? overrides.Denoise : slot.Denoise,
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
