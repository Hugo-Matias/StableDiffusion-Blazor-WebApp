using System.Text.Json;
using System.Text.Json.Serialization;
using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Extensions;

namespace BlazorWebApp.Services;

public interface ICivitaiResourceImageService
{
    Task<CivitaiResourceImageSaveResult> SaveImageAsync(CivitaiResourceImageSaveRequest request);
    Task<Dictionary<int, CivitaiResourceImageSaveResult>> GetSavedImagesAsync(IReadOnlyList<CivitaiImageDto> images);
}

public sealed class CivitaiResourceImageSaveRequest
{
    public CivitaiImageDto Image { get; set; } = default!;
    public int ModelId { get; set; }
    public int ModelVersionId { get; set; }
    public string? ModelName { get; set; }
    public string? ResourceType { get; set; }
    public string? Filename { get; set; }
    public bool IsPreview { get; set; }
    public bool EnrichFromModelVersion { get; set; }
}

public sealed class CivitaiResourceImageSaveResult
{
    public CivitaiImageDto? Image { get; set; }
    public ResourceImage? ResourceImage { get; set; }
    public string? LocalPath { get; set; }
    public bool FileCreated { get; set; }
    public bool FileAlreadyExisted { get; set; }
    public bool ResourceImageCreated { get; set; }
    public bool ImageAvailable => Image != null && !string.IsNullOrWhiteSpace(LocalPath) && (FileCreated || FileAlreadyExisted);
}

public sealed class CivitaiResourceImageService : ICivitaiResourceImageService
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.InvariantCultureIgnoreCase)
    {
        ".mp4",
        ".webm",
        ".mov",
        ".avi",
        ".mkv",
        ".m4v"
    };

    private readonly CivitaiService _civitai;
    private readonly IImageService _imageService;
    private readonly IIOService _io;
    private readonly IDatabaseService _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CivitaiResourceImageService> _logger;

    public CivitaiResourceImageService(
        CivitaiService civitai,
        IImageService imageService,
        IIOService io,
        IDatabaseService db,
        IConfiguration configuration,
        ILogger<CivitaiResourceImageService> logger)
    {
        _civitai = civitai;
        _imageService = imageService;
        _io = io;
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CivitaiResourceImageSaveResult> SaveImageAsync(CivitaiResourceImageSaveRequest request)
    {
        if (request.Image == null)
            return new CivitaiResourceImageSaveResult();

        var image = await ResolveImageAsync(request);
        if (image == null)
            return new CivitaiResourceImageSaveResult();
        if (string.IsNullOrWhiteSpace(image.Url))
            return new CivitaiResourceImageSaveResult { Image = image };

        var modelVersionId = ResolveModelVersionId(request, image);
        var physicalStem = BuildPhysicalStem(request, image, modelVersionId);
        var mediaExtension = ResolveMediaExtension(image.Url);
        var mediaPath = physicalStem + mediaExtension;
        var localPath = ToWebImagePath(mediaPath);
        var result = new CivitaiResourceImageSaveResult
        {
            Image = image,
            LocalPath = localPath,
            FileAlreadyExisted = File.Exists(mediaPath)
        };

        if (!result.FileAlreadyExisted)
        {
            result.FileCreated = IsVideoExtension(mediaExtension)
                ? await DownloadMediaFileAsync(image.Url, mediaPath)
                : await _imageService.DownloadImageAsPng(image.Url, mediaPath, overwrite: true);
        }

        if (!result.FileCreated && !result.FileAlreadyExisted)
            return result;

        if (!request.IsPreview)
        {
            SaveMetadataJson(image, physicalStem);

            var entity = BuildResourceImage(request, image, modelVersionId, localPath);
            result.ResourceImage = entity;
            result.ResourceImageCreated = await _db.CreateResourceImage(entity);
        }

        return result;
    }

    public async Task<Dictionary<int, CivitaiResourceImageSaveResult>> GetSavedImagesAsync(IReadOnlyList<CivitaiImageDto> images)
    {
        var results = new Dictionary<int, CivitaiResourceImageSaveResult>();
        if (images == null || images.Count == 0) return results;

        var indexedHashes = images
            .Select((image, index) => new { Image = image, Index = index, Hashes = ResolveCandidateImageHashes(image) })
            .Where(item => item.Hashes.Count > 0)
            .ToList();

        if (indexedHashes.Count == 0) return results;

        var savedImages = await _db.GetResourceImagesByHashes(indexedHashes.SelectMany(item => item.Hashes));
        var savedByHash = savedImages
            .Where(image => !string.IsNullOrWhiteSpace(image.Hash))
            .GroupBy(image => image.Hash)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var item in indexedHashes)
        {
            var resourceImage = item.Hashes
                .Select(hash => savedByHash.TryGetValue(hash, out var image) ? image : null)
                .FirstOrDefault(image => image != null);

            if (resourceImage == null) continue;
            if (string.IsNullOrWhiteSpace(resourceImage.Path)) continue;
            if (!File.Exists(_io.ResolveFilePath(resourceImage.Path))) continue;

            resourceImage.Path = ToWebImagePath(_io.ResolveFilePath(resourceImage.Path));

            results[item.Index] = new CivitaiResourceImageSaveResult
            {
                Image = item.Image,
                ResourceImage = resourceImage,
                LocalPath = resourceImage.Path,
                FileAlreadyExisted = true
            };
        }

        return results;
    }

    private async Task<CivitaiImageDto?> ResolveImageAsync(CivitaiResourceImageSaveRequest request)
    {
        try
        {
            var modelVersionId = ResolveModelVersionId(request, request.Image);
            if (request.EnrichFromModelVersion && modelVersionId > 0)
            {
                var enriched = await _civitai.GetImageByModelVersionId(modelVersionId, request.Image);
                if (enriched != null)
                    return enriched;
            }

            if (request.Image.Id > 0 && request.Image.Meta == null)
            {
                var enriched = await _civitai.GetImageById(request.Image.Id);
                if (enriched != null)
                    return enriched;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enrich CivitAI image {ImageId} before saving", request.Image.Id);
        }

        return request.Image;
    }

    private static int ResolveModelVersionId(CivitaiResourceImageSaveRequest request, CivitaiImageDto image)
    {
        if (request.ModelVersionId > 0)
            return request.ModelVersionId;

        return image.ModelVersionIds != null && image.ModelVersionIds.Count > 0
            ? image.ModelVersionIds[0]
            : 0;
    }

    private string BuildPhysicalStem(CivitaiResourceImageSaveRequest request, CivitaiImageDto image, int modelVersionId)
    {
        var outputDir = _configuration["OutputDir"] ?? string.Empty;
        var resourceType = Parser.SanitizePath(string.IsNullOrWhiteSpace(request.ResourceType) ? "CivitAI" : request.ResourceType);
        var path = Path.Combine(outputDir, "Saved", resourceType);

        if (request.ModelId > 0 && !string.IsNullOrWhiteSpace(request.ModelName))
        {
            path = Path.Combine(path, $"{request.ModelId}-{Parser.SanitizePath(request.ModelName)}");
        }
        else
        {
            path = Path.Combine(path, "Images");
        }

        if (request.IsPreview)
            return Path.Combine(path, modelVersionId > 0 ? modelVersionId.ToString() : ResolveImageFilename(image, request.Filename));

        if (modelVersionId > 0)
            path = Path.Combine(path, modelVersionId.ToString());

        return Path.Combine(path, ResolveImageFilename(image, request.Filename));
    }

    private static string ResolveImageFilename(CivitaiImageDto image, string? filename)
    {
        if (!string.IsNullOrWhiteSpace(filename))
            return Parser.SanitizePath(filename);

        if (!string.IsNullOrWhiteSpace(image.Hash))
            return Parser.SanitizePath(image.Hash);

        return image.Id > 0 ? $"civitai-{image.Id}" : $"civitai-{Guid.NewGuid():N}";
    }

    private static string? ResolveImageHash(CivitaiImageDto? image)
    {
        if (image == null) return null;
        if (!string.IsNullOrWhiteSpace(image.Hash)) return image.Hash;
        return image.Id > 0 ? $"civitai-{image.Id}" : null;
    }

    private static List<string> ResolveCandidateImageHashes(CivitaiImageDto? image)
    {
        var hashes = new List<string>();
        if (image == null) return hashes;

        if (!string.IsNullOrWhiteSpace(image.Hash)) hashes.Add(image.Hash);
        if (image.Id > 0) hashes.Add($"civitai-{image.Id}");

        return hashes.Distinct().ToList();
    }

    private static string ResolveMediaExtension(string? url)
    {
        var extension = ResolveUrlExtension(url);
        return IsVideoExtension(extension) ? extension : ".png";
    }

    private static string ResolveUrlExtension(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return Path.GetExtension(uri.AbsolutePath);

        return Path.GetExtension(url);
    }

    private static bool IsVideoExtension(string? extension)
        => !string.IsNullOrWhiteSpace(extension) && VideoExtensions.Contains(extension);

    private async Task<bool> DownloadMediaFileAsync(string? url, string path)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            using var httpClient = new HttpClient();
            await using var source = await httpClient.GetStreamAsync(url);
            await using var destination = File.Create(path);
            await source.CopyToAsync(destination);
            await destination.FlushAsync();

            return new FileInfo(path).Length > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download CivitAI media file {Url}", url);
            return false;
        }
    }

    private string ToWebImagePath(string physicalPath)
    {
        var outputDir = _configuration["OutputDir"] ?? string.Empty;
        var relative = Path.GetRelativePath(outputDir, physicalPath).Replace(Path.DirectorySeparatorChar, '/');
        return EncodeWebPath($"/image/{relative.TrimStart('/')}");
    }

    private static string EncodeWebPath(string path)
    {
        return string.Join('/', path.Split('/').Select(segment => string.IsNullOrEmpty(segment) ? segment : Uri.EscapeDataString(segment)));
    }

    private void SaveMetadataJson(CivitaiImageDto image, string physicalStem)
    {
        if (image.MetaObject.ValueKind == JsonValueKind.Undefined || image.MetaObject.ValueKind == JsonValueKind.Null)
            return;

        var parameters = JsonSerializer.Serialize(image.MetaObject, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        _io.SaveText(physicalStem + ".json", parameters, overwrite: true);
    }

    private static ResourceImage BuildResourceImage(CivitaiResourceImageSaveRequest request, CivitaiImageDto image, int modelVersionId, string localPath)
    {
        var entity = new ResourceImage(image)
        {
            CivitaiModelId = request.ModelId,
            CivitaiModelVersionID = modelVersionId,
            Path = localPath,
            Hash = ResolveImageHash(image) ?? $"civitai-{Guid.NewGuid():N}"
        };

        if (image.Meta != null)
        {
            entity.Prompt = Parser.ParseCivitaiImageResources(image.Meta.Prompt, image.Meta.Resources);
        }

        return entity;
    }
}