using System.Text.Json.Serialization;

namespace BlazorWebApp.Models;

public class AppStateCharacter
{
    public int ActiveTabIndex { get; set; }
    public bool SidebarCollapsed { get; set; }
    public CharacterReferenceEngine Engine { get; set; } = CharacterReferenceEngine.Qwen;
    public CharacterLoaderMode LoaderMode { get; set; } = CharacterLoaderMode.AioCheckpoint;
    public CharacterAssetState Assets { get; set; } = new();
    public List<Lora> Loras { get; set; } = new();
    public CharacterSourceImageState SourceImage { get; set; } = new();
    public string GlobalPositivePromptExtension { get; set; } = string.Empty;
    public string GlobalNegativePrompt { get; set; } = CharacterReferenceDefaults.GlobalNegativePrompt;
    public long GlobalSeed { get; set; } = -1;
    public CharacterSamplerOverrides SamplerOverrides { get; set; } = new();
    public List<CharacterReferenceSlotState> Slots { get; set; } = CharacterReferenceSlotCatalog.CreateDefaultSlots();
    public bool ShowEngineSettings { get; set; }
    public bool ShowAdvancedSettings { get; set; }
    public bool UseRtxUpscale { get; set; } = true;
    public bool UseCleanGpu { get; set; }
    public CharacterFaceReplacementSettings FaceReplacement { get; set; } = new();

    [JsonIgnore]
    public Dictionary<string, string> ReusableDependencyImagePaths { get; set; } = new(StringComparer.Ordinal);

    [JsonIgnore]
    public IReadOnlyDictionary<string, string> ActiveAssets => Assets.GetActiveAssets(Engine, LoaderMode);

    public CharacterReferenceSlotState AddSlot(CharacterReferenceSlotPresetKey presetKey, string? label = null)
    {
        var slot = CharacterReferenceSlotCatalog.CreateCustomSlot(presetKey, label);
        Slots.Add(slot);
        return slot;
    }
}

public class CharacterSourceImageState
{
    public string? ImagePath { get; set; }
    public string? ImageDataUri { get; set; }
    public string? SourceLabel { get; set; }
}

public class CharacterSamplerOverrides
{
    public bool Enabled { get; set; }
    public int Steps { get; set; } = CharacterReferenceDefaults.Steps;
    public double Cfg { get; set; } = CharacterReferenceDefaults.Cfg;
    public string SamplerName { get; set; } = CharacterReferenceDefaults.SamplerName;
    public string Scheduler { get; set; } = CharacterReferenceDefaults.Scheduler;
    public double Denoise { get; set; } = CharacterReferenceDefaults.Denoise;
}

public class CharacterFaceReplacementSettings
{
    public bool Enabled { get; set; }
    public bool UseCloseNeutralReference { get; set; }
    public int CropResolution { get; set; } = 768;
    public double DetectionThreshold { get; set; } = 0.25;
    public double SourceDetectionThreshold { get; set; } = 0.2;
    public double TargetCropSizeMultiplier { get; set; } = 1.35;
    public double SourceCropSizeMultiplier { get; set; } = 1.6;
    public int Steps { get; set; } = CharacterReferenceDefaults.Steps;
    public double Cfg { get; set; } = CharacterReferenceDefaults.Cfg;
    public double Denoise { get; set; } = 0.55;
    public int MaskExpand { get; set; } = 18;
    public double MaskBlurRadius { get; set; } = 12;
    public double BorderBlending { get; set; } = 0.5;
}

public class CharacterAssetState
{
    public CharacterAioAssetState Aio { get; set; } = new();
    public CharacterSplitAssetState Split { get; set; } = new();
    public CharacterFluxAssetState Flux { get; set; } = new();

    public IReadOnlyDictionary<string, string> GetActiveAssets(
        CharacterReferenceEngine engine,
        CharacterLoaderMode loaderMode)
    {
        if (engine == CharacterReferenceEngine.Flux2Klein)
        {
            return new Dictionary<string, string>
            {
                [CharacterAssetKeys.DiffusionModel] = Flux.DiffusionModel,
                [CharacterAssetKeys.Clip] = Flux.Clip,
                [CharacterAssetKeys.Vae] = Flux.Vae
            };
        }

        return loaderMode switch
        {
            CharacterLoaderMode.SplitStack => new Dictionary<string, string>
            {
                [CharacterAssetKeys.DiffusionModel] = Split.DiffusionModel,
                [CharacterAssetKeys.Clip] = Split.Clip,
                [CharacterAssetKeys.Vae] = Split.Vae
            },
            _ => new Dictionary<string, string>
            {
                [CharacterAssetKeys.Checkpoint] = Aio.Checkpoint
            }
        };
    }
}

public class CharacterAioAssetState
{
    public string Checkpoint { get; set; } = CharacterReferenceDefaults.AioCheckpoint;
}

public class CharacterSplitAssetState
{
    public string DiffusionModel { get; set; } = string.Empty;
    public string Clip { get; set; } = CharacterReferenceDefaults.SplitClip;
    public string Vae { get; set; } = CharacterReferenceDefaults.SplitVae;
}

public class CharacterFluxAssetState
{
    public string DiffusionModel { get; set; } = CharacterReferenceDefaults.Flux2KleinDiffusionModel;
    public string Clip { get; set; } = CharacterReferenceDefaults.Flux2KleinClip;
    public string Vae { get; set; } = CharacterReferenceDefaults.Flux2KleinVae;
}

public class CharacterReferenceSlotState
{
    public string Id { get; set; } = CharacterReferenceSlotCatalog.CreateCustomSlotId(CharacterReferenceSlotPresetKey.Blank);
    public string Label { get; set; } = string.Empty;
    public CharacterReferenceSlotKind Kind { get; set; } = CharacterReferenceSlotKind.Custom;
    public CharacterReferenceSlotPresetKey? PresetKey { get; set; }
    public bool IsBuiltIn { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int BatchSize { get; set; } = 1;
    public long Seed { get; set; } = -1;
    public int Steps { get; set; } = CharacterReferenceDefaults.Steps;
    public double Cfg { get; set; } = CharacterReferenceDefaults.Cfg;
    public string SamplerName { get; set; } = CharacterReferenceDefaults.SamplerName;
    public string Scheduler { get; set; } = CharacterReferenceDefaults.Scheduler;
    public double Denoise { get; set; } = CharacterReferenceDefaults.Denoise;
    public string PromptTemplate { get; set; } = string.Empty;
    public string PromptExtension { get; set; } = string.Empty;
    public string PromptOverride { get; set; } = string.Empty;
    public string NegativePromptOverride { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public CharacterReferenceDependencyPolicy DependencyPolicy { get; set; } = CharacterReferenceDependencyPolicy.SourceImage;
    public string? DependencySlotId { get; set; }
    public int? LastOutputImageId { get; set; }
    public string? LastOutputPath { get; set; }
    public CharacterReferenceSlotStatus Status { get; set; } = CharacterReferenceSlotStatus.Idle;
    public bool PromptExpanded { get; set; }
}

public class CharacterReferenceSlotTemplate
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required CharacterReferenceSlotKind Kind { get; init; }
    public required CharacterReferenceSlotPresetKey PresetKey { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required double Cfg { get; init; }
    public required CharacterReferenceDependencyPolicy DependencyPolicy { get; init; }
    public string PromptTemplate { get; init; } = string.Empty;
}

public static class CharacterReferenceSlotCatalog
{
    public static IReadOnlyList<CharacterReferenceSlotKind> SlotKindOrder { get; } =
    [
        CharacterReferenceSlotKind.BodyAngle,
        CharacterReferenceSlotKind.Expression,
        CharacterReferenceSlotKind.Pose,
        CharacterReferenceSlotKind.Body,
        CharacterReferenceSlotKind.Outfit,
        CharacterReferenceSlotKind.Landscape,
        CharacterReferenceSlotKind.Custom
    ];

    private static readonly IReadOnlyList<CharacterReferenceSlotTemplate> DefaultTemplates =
    [
        Body(CharacterReferenceSlotPresetKey.FrontView, "front-view", "Front view", 1.6, CharacterReferenceDependencyPolicy.SourceImage,
            "Same exact person and body proportions, same exact detailed artstyle, (same exact hairstyle:1.3), change pose to standing straight, both feet on the ground, natural relaxed pose, arms to the side, direct front view, eye level, adjust zoom level to get a full body portrait from feet to head. (completely white solid background)"),
        Body(CharacterReferenceSlotPresetKey.LeftProfile, "left-profile", "Left Profile", 1.4, CharacterReferenceDependencyPolicy.FrontViewOutput,
            "Same exact person and body proportions, same exact detailed artstyle, change pose to standing, natural pose, profile view (((she is facing the left side of the frame))), full body portrait from feet to head. zoom in to maintain full body portrait. (completely white solid background)"),
        Body(CharacterReferenceSlotPresetKey.RightProfile, "right-profile", "Right Profile", 1.8, CharacterReferenceDependencyPolicy.FrontViewOutput,
            "Same exact person and body proportions, same exact detailed artstyle, side view (((she is facing the right side of the frame))), full body portrait from feet to head. zoom in to maintain full body portrait. (completely white solid background)"),
        Body(CharacterReferenceSlotPresetKey.BackView, "back-view", "Back View", 1.6, CharacterReferenceDependencyPolicy.FrontViewOutput,
            "Same exact person and body proportions, same exact detailed artstyle, change pose to standing, natural pose, back view (((no face visible, recreate extremely accurate hairstyle and clothes from behind))), full body portrait from feet to head. zoom in to maintain full body portrait. (completely white solid background)"),
        Body(CharacterReferenceSlotPresetKey.FrontThreeQuarter, "front-three-quarter", "Front Three-Quarter", 1.4, CharacterReferenceDependencyPolicy.FrontViewOutput,
            "Same exact person and body proportions, same exact detailed artstyle, change pose to standing, natural pose, three-quarter view (((she is facing the left side of the frame))), full body portrait from feet to head. zoom in to maintain full body portrait. (completely white solid background)"),
        Body(CharacterReferenceSlotPresetKey.BackThreeQuarter, "back-three-quarter", "Back Three-Quarter", 1.4, CharacterReferenceDependencyPolicy.FrontViewOutput,
            "Same exact person and body proportions, same exact detailed artstyle, change pose to standing, natural pose, three-quarter view from behind, full body portrait from feet to head. zoom in to maintain full body portrait. (completely white solid background)"),
        Expression(CharacterReferenceSlotPresetKey.NeutralExpression, "neutral", "Close Neutral", CharacterReferenceDependencyPolicy.SourceImage,
            "Same exact person and facial features, same exact detailed artstyle, closeup of her head. natural neutral expression."),
        Expression(CharacterReferenceSlotPresetKey.HappyExpression, "happy", "Close Happy", CharacterReferenceDependencyPolicy.NeutralOutput,
            "Same exact person and facial features, same exact detailed artstyle, closeup of her head. natural happy smiling expression. half lidded eyes."),
        Expression(CharacterReferenceSlotPresetKey.ScaredExpression, "scared", "Close Scared", CharacterReferenceDependencyPolicy.NeutralOutput,
            "Same exact person and facial features, same exact detailed artstyle, closeup of her head. Scared expression, mouth open, eyes open very wide. Reduce irises and pupils size to very small showing a lot of sclera."),
        Expression(CharacterReferenceSlotPresetKey.AngryExpression, "angry", "Close Angry", CharacterReferenceDependencyPolicy.NeutralOutput,
            "Same exact person and facial features, same exact detailed artstyle, closeup of her head. Angry expression, teeth slightly visible, furrowed brows."),
        Expression(CharacterReferenceSlotPresetKey.SadExpression, "sad", "Close Sad", CharacterReferenceDependencyPolicy.NeutralOutput,
            "Same exact person and facial features, same exact detailed artstyle, closeup of her head. Sad expression, frowning or pouting."),
        Expression(CharacterReferenceSlotPresetKey.CryingExpression, "crying", "Close Crying", CharacterReferenceDependencyPolicy.NeutralOutput,
            "Same exact person and facial features, same exact detailed artstyle, closeup of her head. Crying expression, open mouth, tears flowing down her cheeks."),
        Expression(CharacterReferenceSlotPresetKey.SmugExpression, "smug", "Close Smug", CharacterReferenceDependencyPolicy.NeutralOutput,
            "Same exact person and facial features, same exact detailed artstyle, closeup of her head. Smug or cocky expression, half-smile without any teeth showing."),
        Pose(CharacterReferenceSlotPresetKey.ModelPose, "model-pose", "Model Pose", 1.4,
            "Same exact person and body proportions, same exact detailed artstyle, change pose to a confident modeling pose, soft smile, half lidded eyes. full body portrait from feet to head. zoom in to maintain full body portrait. (completely white solid background)"),
        Pose(CharacterReferenceSlotPresetKey.ActionPose, "action-pose", "Action Pose", 1.4,
            "Same exact person and body proportions, same exact detailed artstyle, change pose to a dynamic action pose, natural movement, focused and determined expression. full body portrait from feet to head. zoom in to maintain full body portrait. (completely white solid background)")
    ];

    private static readonly IReadOnlyList<CharacterReferenceSlotTemplate> CustomPresetTemplates =
    [
        new()
        {
            Id = "custom-expression",
            Label = "Expression",
            Kind = CharacterReferenceSlotKind.Expression,
            PresetKey = CharacterReferenceSlotPresetKey.CustomExpression,
            Width = 1088,
            Height = 1088,
            Cfg = 1.6,
            DependencyPolicy = CharacterReferenceDependencyPolicy.NeutralOutput,
            PromptTemplate = "Same exact person and facial features, same exact detailed artstyle."
        },
        new()
        {
            Id = "custom-pose",
            Label = "Pose",
            Kind = CharacterReferenceSlotKind.Pose,
            PresetKey = CharacterReferenceSlotPresetKey.CustomPose,
            Width = 1088,
            Height = 1920,
            Cfg = 1.6,
            DependencyPolicy = CharacterReferenceDependencyPolicy.FrontViewOutput,
            PromptTemplate = "Same exact person and body proportions, same exact detailed artstyle, same exact hairstyle, and outfit."
        },
        new()
        {
            Id = "custom-camera",
            Label = "Camera",
            Kind = CharacterReferenceSlotKind.BodyAngle,
            PresetKey = CharacterReferenceSlotPresetKey.CustomCamera,
            Width = 1088,
            Height = 1920,
            Cfg = 1.6,
            DependencyPolicy = CharacterReferenceDependencyPolicy.FrontViewOutput,
            PromptTemplate = "Same exact person and body proportions, same exact detailed artstyle, same exact hairstyle, and outfit. Same exact facial expression and pose."
        },
        new()
        {
            Id = "custom-body",
            Label = "Body",
            Kind = CharacterReferenceSlotKind.Body,
            PresetKey = CharacterReferenceSlotPresetKey.CustomBody,
            Width = 1088,
            Height = 1920,
            Cfg = 1.6,
            DependencyPolicy = CharacterReferenceDependencyPolicy.FrontViewOutput,
            PromptTemplate = "Same exact person and detailed anime artstyle, same exact facial features, hairstyle, and outfit. Preserve the character identity and body proportions."
        },
        new()
        {
            Id = "custom-outfit",
            Label = "Outfit",
            Kind = CharacterReferenceSlotKind.Outfit,
            PresetKey = CharacterReferenceSlotPresetKey.CustomOutfit,
            Width = 1088,
            Height = 1920,
            Cfg = 1.6,
            DependencyPolicy = CharacterReferenceDependencyPolicy.FrontViewOutput,
            PromptTemplate = "Same exact person and body proportions, same exact detailed artstyle, same exact facial features and hairstyle."
        },
        new()
        {
            Id = "custom-landscape",
            Label = "Landscape",
            Kind = CharacterReferenceSlotKind.Landscape,
            PresetKey = CharacterReferenceSlotPresetKey.CustomLandscape,
            Width = 1920,
            Height = 1088,
            Cfg = 1.6,
            DependencyPolicy = CharacterReferenceDependencyPolicy.FrontViewOutput,
            PromptTemplate = "Same exact person and body proportions, same exact detailed artstyle."
        },
        new()
        {
            Id = "blank",
            Label = "Blank",
            Kind = CharacterReferenceSlotKind.Custom,
            PresetKey = CharacterReferenceSlotPresetKey.Blank,
            Width = 1088,
            Height = 1088,
            Cfg = 1.6,
            DependencyPolicy = CharacterReferenceDependencyPolicy.SourceImage
        }
    ];

    public static List<CharacterReferenceSlotState> CreateDefaultSlots()
    {
        return DefaultTemplates.Select(template => CreateSlot(template, isBuiltIn: true, id: template.Id)).ToList();
    }

    public static IReadOnlyList<CharacterReferenceSlotTemplate> GetAddablePresets()
    {
        return CustomPresetTemplates;
    }

    public static IReadOnlyList<CharacterReferenceSlotState> OrderSlotsByKind(IEnumerable<CharacterReferenceSlotState> slots)
    {
        var slotList = slots.ToList();
        var knownKinds = SlotKindOrder.ToHashSet();

        return SlotKindOrder
            .SelectMany(kind => slotList.Where(slot => slot.Kind == kind))
            .Concat(slotList.Where(slot => !knownKinds.Contains(slot.Kind)))
            .ToList();
    }

    public static CharacterReferenceSlotState CreateCustomSlot(CharacterReferenceSlotPresetKey presetKey, string? label = null)
    {
        var template = CustomPresetTemplates.FirstOrDefault(template => template.PresetKey == presetKey)
            ?? CustomPresetTemplates.First(template => template.PresetKey == CharacterReferenceSlotPresetKey.Blank);

        return CreateSlot(template, isBuiltIn: false, id: CreateCustomSlotId(presetKey), labelOverride: label);
    }

    public static string CreateCustomSlotId(CharacterReferenceSlotPresetKey presetKey)
    {
        return $"custom-{presetKey.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}";
    }

    public static string NormalizeLabel(string label)
    {
        var normalized = string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim();
        return normalized.StartsWith("Close ", StringComparison.OrdinalIgnoreCase)
            ? normalized[6..].Trim()
            : normalized;
    }

    public static string ComposePrompt(CharacterReferenceSlotState slot, string? globalPromptExtension = null)
    {
        var promptTemplate = ResolvePromptTemplate(slot).Trim();
        var promptExtension = ResolvePromptExtension(slot).Trim();
        var globalExtension = (globalPromptExtension ?? string.Empty).Trim();
        var extensions = new[] { promptExtension, globalExtension }
            .Where(extension => !string.IsNullOrWhiteSpace(extension));
        var combinedExtension = string.Join(' ', extensions);

        if (string.IsNullOrWhiteSpace(promptTemplate))
        {
            return combinedExtension;
        }

        return string.IsNullOrWhiteSpace(combinedExtension)
            ? promptTemplate
            : $"{promptTemplate.TrimEnd()} {combinedExtension}";
    }

    public static string ResolvePromptTemplate(CharacterReferenceSlotState slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.PromptTemplate))
        {
            return slot.PromptTemplate;
        }

        return slot.PresetKey is { } presetKey
            ? DefaultTemplates.Concat(CustomPresetTemplates)
                .FirstOrDefault(template => template.PresetKey == presetKey)?.PromptTemplate ?? string.Empty
            : string.Empty;
    }

    public static string ResolvePromptExtension(CharacterReferenceSlotState slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.PromptExtension))
        {
            return slot.PromptExtension;
        }

        var legacyPrompt = slot.PromptOverride.Trim();
        if (string.IsNullOrWhiteSpace(legacyPrompt))
        {
            return string.Empty;
        }

        return string.Equals(legacyPrompt, ResolvePromptTemplate(slot).Trim(), StringComparison.Ordinal)
            ? string.Empty
            : legacyPrompt;
    }

    private static CharacterReferenceSlotState CreateSlot(
        CharacterReferenceSlotTemplate template,
        bool isBuiltIn,
        string id,
        string? labelOverride = null)
    {
        return new CharacterReferenceSlotState
        {
            Id = id,
            Label = NormalizeLabel(labelOverride ?? template.Label),
            Kind = template.Kind,
            PresetKey = template.PresetKey,
            IsBuiltIn = isBuiltIn,
            Width = template.Width,
            Height = template.Height,
            Cfg = template.Cfg,
            DependencyPolicy = template.DependencyPolicy,
            PromptTemplate = template.PromptTemplate
        };
    }

    private static CharacterReferenceSlotTemplate Body(
        CharacterReferenceSlotPresetKey presetKey,
        string id,
        string label,
        double cfg,
        CharacterReferenceDependencyPolicy dependencyPolicy,
        string promptTemplate)
    {
        return new CharacterReferenceSlotTemplate
        {
            Id = id,
            Label = label,
            Kind = CharacterReferenceSlotKind.BodyAngle,
            PresetKey = presetKey,
            Width = 1088,
            Height = 1920,
            Cfg = cfg,
            DependencyPolicy = dependencyPolicy,
            PromptTemplate = promptTemplate
        };
    }

    private static CharacterReferenceSlotTemplate Expression(
        CharacterReferenceSlotPresetKey presetKey,
        string id,
        string label,
        CharacterReferenceDependencyPolicy dependencyPolicy,
        string promptTemplate)
    {
        return new CharacterReferenceSlotTemplate
        {
            Id = id,
            Label = label,
            Kind = CharacterReferenceSlotKind.Expression,
            PresetKey = presetKey,
            Width = 1088,
            Height = 1088,
            Cfg = 1.6,
            DependencyPolicy = dependencyPolicy,
            PromptTemplate = promptTemplate
        };
    }

    private static CharacterReferenceSlotTemplate Pose(
        CharacterReferenceSlotPresetKey presetKey,
        string id,
        string label,
        double cfg,
        string promptTemplate)
    {
        return new CharacterReferenceSlotTemplate
        {
            Id = id,
            Label = label,
            Kind = CharacterReferenceSlotKind.Pose,
            PresetKey = presetKey,
            Width = 1088,
            Height = 1920,
            Cfg = cfg,
            DependencyPolicy = CharacterReferenceDependencyPolicy.FrontViewOutput,
            PromptTemplate = promptTemplate
        };
    }
}

public static class CharacterReferenceDefaults
{
    public const string AioCheckpoint = "Base/Qwen-Rapid-AIO-NSFW-v19.safetensors";
    public const string SplitClip = "qwen_2.5_vl_7b_fp8_scaled.safetensors";
    public const string SplitVae = "qwen_image_vae.safetensors";
    public const string Flux2KleinDiffusionModel = "flux-2-klein-base-9b-fp8.safetensors";
    public const string Flux2KleinClip = "qwen_3_8b_fp8mixed.safetensors";
    public const string Flux2KleinVae = "full_encoder_small_decoder.safetensors";
    public const string GlobalNegativePrompt = "(bra straps, shirt straps:1.5), (((straight black lines,)))";
    public const string QwenEditInstruction = "Describe the key features of the input image (clothes, color, shape, size, texture, objects, background), then explain how the user's text instruction should alter or modify the image. The image should contain a single character ONLY. Generate a new image that meets the user's requirements while maintaining consistency with the original input where appropriate.";
    public const int Steps = 4;
    public const double Cfg = 1.6;
    public const string SamplerName = "euler";
    public const string Scheduler = "simple";
    public const double Denoise = 1.0;
}

public static class CharacterAssetKeys
{
    public const string Checkpoint = "Checkpoint";
    public const string DiffusionModel = "DiffusionModel";
    public const string Clip = "Clip";
    public const string Vae = "Vae";
}

public enum CharacterLoaderMode
{
    AioCheckpoint,
    SplitStack
}

public enum CharacterReferenceEngine
{
    Qwen,
    Flux2Klein
}

public enum CharacterReferenceSlotKind
{
    BodyAngle,
    Expression,
    Pose,
    Body,
    Outfit,
    Landscape,
    Custom
}

public enum CharacterReferenceSlotPresetKey
{
    FrontView,
    LeftProfile,
    RightProfile,
    BackView,
    NeutralExpression,
    HappyExpression,
    FrontThreeQuarter,
    BackThreeQuarter,
    ScaredExpression,
    AngryExpression,
    SadExpression,
    CryingExpression,
    SmugExpression,
    ModelPose,
    ActionPose,
    CustomExpression,
    CustomPose,
    CustomLandscape,
    Blank,
    CustomCamera,
    CustomBody,
    CustomOutfit
}

public enum CharacterReferenceDependencyPolicy
{
    SourceImage,
    FrontViewOutput,
    NeutralOutput,
    PreviousSlot
}

public enum CharacterReferenceSlotStatus
{
    Idle,
    Queued,
    Running,
    Succeeded,
    Failed
}
