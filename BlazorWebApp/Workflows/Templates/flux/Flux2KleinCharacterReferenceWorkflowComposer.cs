using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Flux;
using BlazorWebApp.Workflows.Models;

namespace BlazorWebApp.Workflows.Templates.Flux;

public class Flux2KleinCharacterReferenceWorkflowComposer : ICharacterReferenceWorkflowComposer
{
    private readonly LoadDiffusionFragment _loadDiffusionFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly Flux2KleinCharacterShotFragment _shotFragment = new();

    public CharacterReferenceEngine Engine => CharacterReferenceEngine.Flux2Klein;

    public CharacterReferenceWorkflowBuildResult Build(AppStateCharacter characterState)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();
        var outputs = new List<CharacterReferenceOutputNode>();

        BuildSourceImage(builder, registry, characterState);
        BuildReusableDependencyImages(builder, registry, characterState);
        BuildLoader(builder, registry, characterState);
        _loraLoaderFragment.BuildAll(builder, registry, characterState.Loras);

        foreach (var slot in characterState.Slots.Where(slot => slot.IsEnabled))
        {
            var runSlot = ApplyRunSettings(slot, characterState);

            var sourceImageKey = ResolveSourceImageRefKey(runSlot, registry);
            var sourceImageRef = registry.GetRef(sourceImageKey);
            var nodePrefix = CharacterReferenceWorkflowIds.SlotNodePrefix(runSlot.Id);
            var filenamePrefix = CharacterReferenceWorkflowIds.SlotFilenamePrefix(runSlot);

            outputs.Add(_shotFragment.Build(
                builder,
                registry,
                runSlot,
                sourceImageRef,
                filenamePrefix,
                nodePrefix,
                characterState.GlobalPositivePromptExtension,
                characterState.GlobalNegativePrompt));
        }

        return new CharacterReferenceWorkflowBuildResult(builder.ToComfyWorkflow(registry), outputs);
    }

    private static void BuildSourceImage(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        AppStateCharacter characterState)
    {
        var sourceImage = characterState.SourceImage.ImagePath;
        if (string.IsNullOrWhiteSpace(sourceImage))
        {
            throw new InvalidOperationException("A source image must be uploaded before building the character reference workflow.");
        }

        builder.AddNode(CharacterReferenceWorkflowIds.SourceImageNodeId, node => node
            .Type("LoadImage")
            .Title("Character Source Image")
            .Input("image", sourceImage));

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
            builder.AddNode(nodeId, node => node
                .Type("LoadImage")
                .Title($"Reusable {slotId} Image")
                .Input("image", imagePath));

            registry.Register(CharacterReferenceWorkflowIds.SlotImageOutputKey(slotId), nodeId, 0);
        }
    }

    private void BuildLoader(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        AppStateCharacter characterState)
    {
        _loadDiffusionFragment.Build(builder, registry, new LoadDiffusionFragment.Parameters
        {
            UnetName = characterState.Assets.Flux.DiffusionModel,
            ClipName = characterState.Assets.Flux.Clip,
            ClipType = "flux2",
            VaeName = characterState.Assets.Flux.Vae
        });
    }

    private static string ResolveSourceImageRefKey(CharacterReferenceSlotState slot, NodeRegistry registry)
    {
        return slot.DependencyPolicy switch
        {
            CharacterReferenceDependencyPolicy.FrontViewOutput when registry.HasOutput(CharacterReferenceWorkflowIds.SlotImageOutputKey(CharacterReferenceWorkflowIds.FrontViewSlotId))
                => CharacterReferenceWorkflowIds.SlotImageOutputKey(CharacterReferenceWorkflowIds.FrontViewSlotId),
            CharacterReferenceDependencyPolicy.NeutralOutput when registry.HasOutput(CharacterReferenceWorkflowIds.SlotImageOutputKey(CharacterReferenceWorkflowIds.NeutralSlotId))
                => CharacterReferenceWorkflowIds.SlotImageOutputKey(CharacterReferenceWorkflowIds.NeutralSlotId),
            CharacterReferenceDependencyPolicy.PreviousSlot when !string.IsNullOrWhiteSpace(slot.DependencySlotId)
                && registry.HasOutput(CharacterReferenceWorkflowIds.SlotImageOutputKey(slot.DependencySlotId))
                => CharacterReferenceWorkflowIds.SlotImageOutputKey(slot.DependencySlotId),
            _ => CharacterReferenceWorkflowIds.SourceImageOutputKey
        };
    }

    private static CharacterReferenceSlotState ApplyRunSettings(CharacterReferenceSlotState slot, AppStateCharacter characterState)
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