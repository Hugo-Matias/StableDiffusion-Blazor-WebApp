using System.Security.Cryptography;

namespace BlazorWebApp.Models.CharacterCreator;

public class CharacterReferenceSheetBody
{
    public string Id { get; set; } = CharacterIdFactory.CreateId("sheet");
    public string Label { get; set; } = "Reference Sheet";
    public CharacterReferenceSourceImage SourceImage { get; set; } = new();
    public CharacterReferenceEngine Engine { get; set; } = CharacterReferenceEngine.Qwen;
    public CharacterLoaderMode LoaderMode { get; set; } = CharacterLoaderMode.AioCheckpoint;
    public CharacterAssetState Assets { get; set; } = new();
    public List<Lora> Loras { get; set; } = new();
    public string GlobalPositivePromptExtension { get; set; } = string.Empty;
    public string GlobalNegativePrompt { get; set; } = CharacterReferenceDefaults.GlobalNegativePrompt;
    public long GlobalSeed { get; set; } = -1;
    public CharacterSamplerOverrides SamplerOverrides { get; set; } = new();
    public List<CharacterReferenceSlotState> Slots { get; set; } = CharacterReferenceSlotCatalog.CreateDefaultSlots();
    public bool UseRtxUpscale { get; set; } = true;
    public bool UseCleanGpu { get; set; }
    public CharacterFaceReplacementSettings FaceReplacement { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static CharacterReferenceSheetBody Create(CharacterReferenceSourceImage sourceImage, string? label = null)
    {
        var now = DateTime.UtcNow;
        return new CharacterReferenceSheetBody
        {
            Id = CharacterIdFactory.CreateId("sheet"),
            Label = string.IsNullOrWhiteSpace(label) ? ResolveLabel(sourceImage) : label.Trim(),
            SourceImage = sourceImage,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public static CharacterReferenceSheetBody FromAppState(AppStateCharacter state, CharacterReferenceSourceImage sourceImage, string? label = null)
    {
        var sheet = Create(sourceImage, label);
        sheet.Engine = state.Engine;
        sheet.LoaderMode = state.LoaderMode;
        sheet.Assets = state.Assets;
        sheet.Loras = state.Loras;
        sheet.GlobalPositivePromptExtension = state.GlobalPositivePromptExtension;
        sheet.GlobalNegativePrompt = state.GlobalNegativePrompt;
        sheet.GlobalSeed = state.GlobalSeed;
        sheet.SamplerOverrides = state.SamplerOverrides;
        sheet.Slots = state.Slots;
        sheet.UseRtxUpscale = state.UseRtxUpscale;
        sheet.UseCleanGpu = state.UseCleanGpu;
        sheet.FaceReplacement = state.FaceReplacement;
        return sheet;
    }

    public static CharacterReferenceSheetBody FromAppState(AppStateCharacter state, CharacterReferenceSheetBody existing)
    {
        var sheet = FromAppState(state, existing.SourceImage, existing.Label);
        sheet.Id = existing.Id;
        sheet.CreatedAt = existing.CreatedAt;
        sheet.UpdatedAt = DateTime.UtcNow;
        return sheet;
    }

    public void ApplyToAppState(AppStateCharacter state, string? sourceImageDataUri = null)
    {
        state.SelectedReferenceSheetId = Id;
        state.Engine = Engine;
        state.LoaderMode = LoaderMode;
        state.Assets = Assets;
        state.Loras = Loras;
        state.SourceImage = new CharacterSourceImageState
        {
            ImagePath = SourceImage.ImagePath,
            ImageDataUri = sourceImageDataUri,
            SourceLabel = SourceImage.SourceLabel ?? Label
        };
        state.GlobalPositivePromptExtension = GlobalPositivePromptExtension;
        state.GlobalNegativePrompt = GlobalNegativePrompt;
        state.GlobalSeed = GlobalSeed;
        state.SamplerOverrides = SamplerOverrides;
        state.Slots = Slots;
        state.UseRtxUpscale = UseRtxUpscale;
        state.UseCleanGpu = UseCleanGpu;
        state.FaceReplacement = FaceReplacement;
    }

    private static string ResolveLabel(CharacterReferenceSourceImage sourceImage)
    {
        if (!string.IsNullOrWhiteSpace(sourceImage.SourceLabel))
        {
            return sourceImage.SourceLabel;
        }

        if (!string.IsNullOrWhiteSpace(sourceImage.OriginalFilename))
        {
            return Path.GetFileNameWithoutExtension(sourceImage.OriginalFilename);
        }

        return "Reference Sheet";
    }
}

public class CharacterReferenceSourceImage
{
    public int? ImageId { get; set; }
    public string? ImagePath { get; set; }
    public string? SourceLabel { get; set; }
    public string SourceFingerprint { get; set; } = string.Empty;
    public string? OriginalFilename { get; set; }

    public static CharacterReferenceSourceImage FromImageId(int imageId, string? imagePath = null, string? label = null, string? originalFilename = null)
    {
        return new CharacterReferenceSourceImage
        {
            ImageId = imageId,
            ImagePath = imagePath,
            SourceLabel = label,
            OriginalFilename = originalFilename ?? ResolveFilename(imagePath),
            SourceFingerprint = CharacterSourceFingerprint.FromImageId(imageId)
        };
    }

    public static CharacterReferenceSourceImage FromBytes(byte[] bytes, string? label = null, string? originalFilename = null)
    {
        return new CharacterReferenceSourceImage
        {
            SourceLabel = label,
            OriginalFilename = originalFilename,
            SourceFingerprint = CharacterSourceFingerprint.FromBytes(bytes)
        };
    }

    public static CharacterReferenceSourceImage FromPath(string imagePath, string? label = null)
    {
        return new CharacterReferenceSourceImage
        {
            ImagePath = imagePath,
            SourceLabel = label,
            OriginalFilename = ResolveFilename(imagePath),
            SourceFingerprint = CharacterSourceFingerprint.FromPath(imagePath)
        };
    }

    private static string? ResolveFilename(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : Path.GetFileName(path);
    }
}

public static class CharacterSourceFingerprint
{
    public static string FromImageId(int imageId)
    {
        if (imageId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageId), "Image id must be greater than zero.");
        }

        return $"image:{imageId}";
    }

    public static string FromBytes(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            throw new ArgumentException("Image bytes are required to create a content fingerprint.", nameof(bytes));
        }

        var hash = SHA256.HashData(bytes);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string FromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A path is required to create a path fingerprint.", nameof(path));
        }

        string normalized;
        try
        {
            normalized = Path.GetFullPath(path.Trim());
        }
        catch
        {
            normalized = path.Trim();
        }

        normalized = normalized.Replace('\\', '/').ToLowerInvariant();
        return $"path:{normalized}";
    }
}