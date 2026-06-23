using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BlazorWebApp.Services
{
    public class CivitaiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IImageService _img;
        private readonly IIOService _io;
        private readonly IEventService _events;
        private readonly IDatabaseService _db;
        private readonly IProgressService _progress;
        private readonly ILogger<CivitaiService> _logger;
        private readonly List<string> _ignoreFileType = new() { "config" };
        private readonly List<CivitaiModelType> _ignoreModelTypes = new() { CivitaiModelType.Controlnet, CivitaiModelType.Poses, CivitaiModelType.Wildcards, CivitaiModelType.Other };
        private const string BaseModelsJsonPath = "Data/CivitAI/basemodels.json";
        private const int MaxImageLookupPages = 20;
        private static readonly byte[] VideoTypeBytes = new byte[] { 0 };
        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp4", ".webm", ".mov", ".m4v" };
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".avif" };
        private static CivitaiBaseModelsData? _cachedBaseModelsData;
        private static readonly object _baseModelsLock = new();

        public CivitaiBaseModelsData BaseModelsData { get; private set; } = new();

        public CivitaiService(HttpClient httpClient, IConfiguration configuration, IImageService img, IIOService io, IEventService events, IDatabaseService db, IProgressService progress, ILogger<CivitaiService> logger)
        {
            _configuration = configuration;
            _img = img;
            _io = io;
            _events = events;
            _db = db;
            _logger = logger;
            _progress = progress;
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://civitai.com/api/");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _configuration["CivitaiApiToken"]);
            LoadBaseModelsData();
        }

        private void LoadBaseModelsData()
        {
            lock (_baseModelsLock)
            {
                if (_cachedBaseModelsData != null)
                {
                    BaseModelsData = _cachedBaseModelsData;
                    return;
                }
            }

            try
            {
                var json = _io.LoadText(BaseModelsJsonPath);
                if (json != null)
                {
                    var data = JsonSerializer.Deserialize<CivitaiBaseModelsData>(json);
                    if (data != null)
                    {
                        lock (_baseModelsLock)
                        {
                            _cachedBaseModelsData = data;
                        }
                        BaseModelsData = data;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load base models data from {Path}", BaseModelsJsonPath);
            }
        }

        public async Task<CivitaiCreatorsDto> GetCreators(CivitaiBaseRequest req)
        {
            var query = !string.IsNullOrWhiteSpace(req.Query) ? $"query={req.Query}&" : string.Empty;
            var limit = req.Limit > 0 ? $"limit={req.Limit}&" : string.Empty;
            var page = req.Page > 0 ? $"page={req.Page}" : string.Empty;

            var url = "v1/creators?" + query + limit + page;
            return await _httpClient.GetFromJsonAsync<CivitaiCreatorsDto>(url);
        }

        public async Task<CivitaiImagesDto> GetImages(CivitaiImagesRequest req)
        {
            if (req.Limit <= 0) req.Limit = 100;
            if (req.Page <= 0) req.Page = 1;

            var endpoint = "v1/images";
            var param = new Dictionary<string, string?>(){
                {"limit", req.Limit.ToString() },
                {"page", req.Page.ToString() }
                };

            if (req.PostId is > 0) param.Add("postId", req.PostId.Value.ToString());
            if (req.ModelId is > 0) param.Add("modelId", req.ModelId.Value.ToString());
            if (req.ModelVersionId is > 0) param.Add("modelVersionId", req.ModelVersionId.Value.ToString());
            if (!string.IsNullOrWhiteSpace(req.Username)) param.Add("username", req.Username);
            if (req.Nsfw is not null and not CivitaiNsfw.All) param.Add("nsfw", req.Nsfw.Value.ToString());
            if (req.Sort is not null) param.Add("sort", req.Sort.Value.ToString().Replace("_", " "));
            if (req.Period is not null) param.Add("period", req.Period.Value.ToString());

            var baseAddress = _httpClient.BaseAddress ?? new Uri("https://civitai.com/api/");
            var url = QueryHelpers.AddQueryString(new Uri(baseAddress, endpoint).ToString(), param);
            var images = NormalizeImagesResponse(await GetCivitaiJsonAsync<CivitaiImagesDto>(url, "images"), url);
            foreach (var image in images.Images)
            {
                await PrepareImageAsync(image, "images");
            }
            return images;
        }

        public async Task<CivitaiImageDto?> GetImageById(int id)
        {
            if (id <= 0) return null;
            var response = NormalizeImagesResponse(await GetCivitaiJsonAsync<CivitaiImagesDto>($"v1/images?nsfw=X&imageId={id}", $"image {id}"), $"image {id}");
            if (response.Images.Count == 0) return null;
            var image = response.Images[0];
            await PrepareImageAsync(image, $"image {id}");
            return image;
        }

        public async Task<CivitaiImageDto?> GetImageByModelVersionId(int modelVersionId, CivitaiImageDto imageDto)
        {
            if (imageDto == null) return null;
            if (modelVersionId <= 0) return imageDto;
            string cursor = "0";
            var visitedCursors = new HashSet<string>(StringComparer.Ordinal);
            CivitaiImageDto? image = null;

            for (var page = 0; image == null && !string.IsNullOrWhiteSpace(cursor) && visitedCursors.Add(cursor) && page < MaxImageLookupPages; page++)
            {
                var endpoint = $"v1/images?browsingLevel={imageDto.BrowsingLevel}&modelVersionId={modelVersionId}&cursor={Uri.EscapeDataString(cursor)}";
                var response = NormalizeImagesResponse(await GetCivitaiJsonAsync<CivitaiImagesDto>(endpoint, $"model version {modelVersionId} images"), endpoint);
                image = response.Images.FirstOrDefault(i => i.Id > 0 && i.Id == imageDto.Id);
                cursor = response.Metadata.NextCursor;
            }

            if (image == null)
            {
                _logger.LogInformation("CivitAI image enrichment unavailable for image {ImageId} in model version {ModelVersionId}; using stub data", imageDto.Id, modelVersionId);
                await PrepareImageAsync(imageDto, $"model version {modelVersionId} image stub");
                return imageDto;
            }

            await PrepareImageAsync(image, $"model version {modelVersionId} image {image.Id}");
            return image;
        }

        private async Task<T?> GetCivitaiJsonAsync<T>(string endpoint, string context)
        {
            try
            {
                using var response = await _httpClient.GetAsync(endpoint);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("CivitAI {Context} request failed with status {StatusCode} at {Endpoint}", context, response.StatusCode, endpoint);
                    return default;
                }

                return await response.Content.ReadFromJsonAsync<T>();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "CivitAI {Context} JSON parse failed at {Endpoint}", context, endpoint);
                return default;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "CivitAI {Context} request failed at {Endpoint}", context, endpoint);
                return default;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "CivitAI {Context} request timed out at {Endpoint}", context, endpoint);
                return default;
            }
        }

        private CivitaiImagesDto NormalizeImagesResponse(CivitaiImagesDto? response, string context)
        {
            if (response == null)
            {
                _logger.LogWarning("CivitAI image response was empty for {Context}", context);
                return new CivitaiImagesDto();
            }

            response.Images ??= new List<CivitaiImageDto>();
            response.Metadata ??= new CivitaiImagesMetadataDto();
            return response;
        }

        private async Task PrepareImageAsync(CivitaiImageDto image, string context)
        {
            if (image == null) return;

            try
            {
                if (image.MetaObject.ValueKind != JsonValueKind.Undefined && image.MetaObject.ValueKind != JsonValueKind.Null)
                    image.Meta = new(image.MetaObject);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse CivitAI metadata for {Context} image {ImageId}", context, image.Id);
            }

            image.ImageType = await ResolveImageTypeAsync(image, context);
        }

        private async Task<byte[]?> ResolveImageTypeAsync(CivitaiImageDto image, string context)
        {
            if (IsKnownVideo(image.Type, image.Url))
                return VideoTypeBytes;

            if (IsKnownImage(image.Type, image.Url))
                return null;

            if (string.IsNullOrWhiteSpace(image.Url))
                return null;

            return await ProbeImageTypeAsync(image.Url, context, image.Id);
        }

        private static byte[]? ResolveKnownImageType(string? type, string? url)
        {
            return IsKnownVideo(type, url) ? VideoTypeBytes : null;
        }

        private static bool IsKnownVideo(string? type, string? url)
        {
            if (string.Equals(type, "video", StringComparison.OrdinalIgnoreCase))
                return true;

            var extension = GetUrlExtension(url);
            return extension != null && VideoExtensions.Contains(extension);
        }

        private static bool IsKnownImage(string? type, string? url)
        {
            if (string.Equals(type, "image", StringComparison.OrdinalIgnoreCase))
                return true;

            var extension = GetUrlExtension(url);
            return extension != null && ImageExtensions.Contains(extension);
        }

        private static string? GetUrlExtension(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;

            try
            {
                var uri = new Uri(url, UriKind.Absolute);
                return Path.GetExtension(uri.AbsolutePath);
            }
            catch (UriFormatException)
            {
                var path = url.Split('?', '#')[0];
                return Path.GetExtension(path);
            }
        }

        private async Task<byte[]?> ProbeImageTypeAsync(string url, string context, int imageId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new RangeHeaderValue(0, 2);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }

                _logger.LogWarning("Failed to probe CivitAI media type for {Context} image {ImageId}, status code: {StatusCode}", context, imageId, response.StatusCode);
                return null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to probe CivitAI media type for {Context} image {ImageId}", context, imageId);
                return null;
            }
        }

        public async Task<CivitaiModelsDto?> GetModels(CivitaiModelsRequest req)
        {
            if (!string.IsNullOrWhiteSpace(req.Hash))
            {
                var id = await GetModelIdByHash(req.Hash);
                if (id != 0)
                {
                    var model = await GetModel(id);
                    if (model == null)
                        return null;

                    return new CivitaiModelsDto() { Models = new List<CivitaiModelsModelDto> { new CivitaiModelsModelDto(model) }, Metadata = new() { CurrentPage = 1, TotalPages = 1 } };
                }
                else
                {
                    _logger.LogWarning("No model found for hash: {Hash}", req.Hash);
                    return null;
                }
            }

            var query = !string.IsNullOrWhiteSpace(req.Query) ? $"query={req.Query}&" : string.Empty;
            var limit = req.Limit > 0 ? $"limit={req.Limit}&" : string.Empty;
            var username = !string.IsNullOrWhiteSpace(req.Username) ? $"username={req.Username}&" : string.Empty;
            var tag = !string.IsNullOrWhiteSpace(req.Tag) ? $"tag={req.Tag}&" : string.Empty;
            var type = req.Type != null && req.Type != CivitaiModelType.All ? $"types={req.Type}&" : string.Empty;
            var sort = req.Sort != null ? $"sort={req.Sort.ToString().Replace("_", " ")}&" : string.Empty;
            var period = req.Period != null ? $"period={req.Period}&" : string.Empty;
            var rating = req.Rating > -1 ? $"rating={req.Rating}&" : string.Empty;
            var baseModels = req.BaseModels != null && req.BaseModels.Any() ? string.Join("&", req.BaseModels.Select(b => $"baseModels={Uri.EscapeDataString(b)}")) + "&" : string.Empty;
            //var page = req.Page > 0 ? $"page={req.Page}&" : string.Empty;
            //var favorites = req.Favorites != null ? $"favorites={req.Favorites.ToString().ToLower()}&" : string.Empty;
            //var hidden = req.Hidden != null ? $"hidden={req.Hidden.ToString().ToLower()}&" : string.Empty;
            //var primaryFileOnly = req.IsPrimaryFileOnly != null ? $"primaryFileOnly={req.IsPrimaryFileOnly.ToString().ToLower()}" : string.Empty;

            //var url = "v1/models?" + query + limit + page + username + tag + type + sort + period + rating + favorites + hidden + primaryFileOnly;
            var url = "v1/models?nsfw=true&" + query + limit + username + tag + type + sort + period + rating + baseModels;
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<CivitaiModelsDto>();
            else
            {
                _logger.LogWarning("Failed to get models, status code: {StatusCode} | {Content}", response.StatusCode, await response.Content.ReadAsStringAsync());
                return null;
            }
        }

        public async Task<CivitaiModelsDto?> GetModelsFromUrl(string url)
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode) return await response.Content.ReadFromJsonAsync<CivitaiModelsDto>();
            else
            {
                _logger.LogWarning("Failed to get models from URL, status code: {StatusCode} | {Content}", response.StatusCode, await response.Content.ReadAsStringAsync());
                return null;
            }
        }

        public async Task<CivitaiModelDto?> GetModel(int id)
        {
            var response = await _httpClient.GetAsync($"v1/models/{id}");
            if (response.IsSuccessStatusCode)
            {
                var jsonString = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("CivitAI Model {ModelId} Response (first 2000 chars): {Json}",
                    id, jsonString.Length > 2000 ? jsonString.Substring(0, 2000) + "..." : jsonString);

                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                    };

                    var content = JsonSerializer.Deserialize<CivitaiModelDto>(jsonString, options);
                    if (content == null)
                    {
                        _logger.LogWarning("CivitAI model {ModelId} response deserialized to null", id);
                        return null;
                    }

                    foreach (var version in content.ModelVersions ?? new List<CivitaiModelVersionDto>())
                    {
                        version.Images = new();
                        if (version.ImagesData != null)
                        {
                            foreach (var data in version.ImagesData)
                            {
                                if (data.Id == 0)
                                {
                                    _ = int.TryParse(Path.GetFileNameWithoutExtension(data.Url), out int imageId);
                                    data.Id = imageId;
                                }
                                version.Images.Add(new CivitaiImageDto()
                                {
                                    Id = data.Id,
                                    Url = data.Url,
                                    Type = data.Type,
                                    BrowsingLevel = data.NsfwLevel,
                                    Width = data.Width,
                                    Height = data.Height,
                                    Hash = data.Hash,
                                    ImageType = ResolveKnownImageType(data.Type, data.Url)
                                });
                            }
                        }
                    }
                    return content;
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogError(jsonEx, "JSON Deserialization failed for model {ModelId}. Error at path: {Path}",
                        id, jsonEx.Path);

                    var debugPath = Path.Combine(Path.GetTempPath(), $"civitai_model_{id}_error_{DateTime.Now:yyyyMMddHHmmss}.json");
                    try
                    {
                        await File.WriteAllTextAsync(debugPath, jsonString);
                        _logger.LogDebug("Full API response saved to: {Path}", debugPath);
                    }
                    catch (Exception fileEx)
                    {
                        _logger.LogWarning(fileEx, "Failed to save debug file");
                    }

                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing model {ModelId}", id);
                    return null;
                }
            }
            else
            {
                _logger.LogWarning("Failed to get model {ModelId}, status code: {StatusCode} | {Content}",
                    id, response.StatusCode, await response.Content.ReadAsStringAsync());
                return null;
            }
        }

        public async Task<int> GetModelIdByHash(string hash)
        {
            var response = await _httpClient.GetAsync($"v1/model-versions/by-hash/{hash}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var json = JsonNode.Parse(content);
                return (int)json["modelId"];
            }
            else
            {
                _logger.LogWarning("Failed to get model ID by hash: {Hash}", hash);
                return 0;
            }
        }

        public async Task<CivitaiModelVersionDto> GetModelVersion(int id) => await _httpClient.GetFromJsonAsync<CivitaiModelVersionDto>($"v1/model-versions/{id}");

        public async Task<CivitaiDownloadStatus> DownloadResource(CivitaiModelDto model, CivitaiModelVersionDto version, CivitaiModelVersionFileDto file, string? subtype = null, string? typeOverride = null, bool downloadResourceImages = true)
        {
            CivitaiDownloadStatus status;
            var resourceType = typeOverride ?? model.Type;
            try
            {
                var url = $"download/models/{version.Id}?type={file.Type}&format={file.Metadata.Format}";

                #region Initialize HTTPClient
                var progressBar = new BaseProgress() { BarColor = Parser.ParseCivitaiResourceColorAsColor((CivitaiModelType)Enum.Parse(typeof(CivitaiModelType), model.Type)), Value = 0 };
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to download CivitAI resource {ModelId}/{VersionId}/{FileName}, status code: {StatusCode}", model.Id, version.Id, file.Name, response.StatusCode);
                    return CivitaiDownloadStatus.Error;
                }
                #endregion

                #region Get/Create Directory
                var path = Path.Combine(GetRequiredConfigurationPath("ResourcesPath"), resourceType);
                if (subtype != null && !subtype.Equals("none", StringComparison.InvariantCultureIgnoreCase)) path = Path.Combine(path, subtype);
                Directory.CreateDirectory(path);
                #endregion

                #region Create TriggerWords Text File
                var wordsPath = Path.Combine(path, Path.GetFileNameWithoutExtension(file.Name) + ".txt");
                if (!File.Exists(wordsPath) && version.TrainedWords != null && version.TrainedWords.Count > 0)
                {
                    _io.SaveText(wordsPath, string.Join("\n", version.TrainedWords));
                }
                #endregion

                #region Download Preview Image
                if (downloadResourceImages)
                    await TryDownloadPreviewImageAsync(model, version, file, resourceType);
                #endregion

                #region Download Resource
                path = Path.Combine(path, file.Name);
                if (File.Exists(path) || await _db.CheckResourceExistsByFilename(file.Name)) status = CivitaiDownloadStatus.Database;
                else
                {
                    var progressAdded = false;
                    _progress.Add(progressBar);
                    progressAdded = true;
                    try
                    {
                        using var fs = new FileStream(path, FileMode.Create);
                        await CopyContentWithProgressAsync(response.Content, fs, progressBar.Id);
                    }
                    finally
                    {
                        if (progressAdded)
                            _progress.Remove(progressBar.Id);
                    }
                    status = CivitaiDownloadStatus.Success;
                }
                #endregion

                #region Add to DB
                if (!_ignoreModelTypes.Contains((CivitaiModelType)Enum.Parse(typeof(CivitaiModelType), model.Type)) && !_ignoreFileType.Contains(file.Type.ToLower()))
                {
                    var entity = new Resource(model, version, file);
                    entity.IsEnabled = true;
                    if (typeOverride != null) entity.Type = new() { Name = resourceType };
                    if (!string.IsNullOrWhiteSpace(subtype)) entity.SubType = new() { Name = subtype };
                    var isAdded = await _db.CreateResource(entity);
                    if (isAdded)
                        _events.Publish(new ResourcesChangedEventArgs($"Downloaded {file.Name}"));
                    if (!isAdded) status = CivitaiDownloadStatus.Exists;
                }
                #endregion

                if (status == CivitaiDownloadStatus.Success)
                    _events.Publish(new DownloadCompletedEventArgs(file.Name));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while downloading resource");
                status = CivitaiDownloadStatus.Error;
            }
            return status;
        }

        private async Task CopyContentWithProgressAsync(HttpContent content, Stream destination, Guid progressId)
        {
            var totalBytes = content.Headers.ContentLength;
            var buffer = new byte[81920];
            long totalRead = 0;

            await using var source = await content.ReadAsStreamAsync();
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                if (totalBytes is > 0)
                {
                    var progress = (float)(totalRead * 100d / totalBytes.Value);
                    _progress.Update(progressId, progress);
                }
            }
        }

        private async Task TryDownloadPreviewImageAsync(CivitaiModelDto model, CivitaiModelVersionDto version, CivitaiModelVersionFileDto file, string resourceType)
        {
            if (version.Images == null || version.Images.Count == 0)
                return;

            var filename = string.IsNullOrWhiteSpace(file.Name) ? version.Id.ToString() : file.Name;
            var previewStem = Path.Combine(GetRequiredConfigurationPath("ResourcePreviewsPath"), resourceType, Path.GetFileNameWithoutExtension(filename));
            if (PreviewFileExists(previewStem))
                return;

            var previewDirectory = Path.GetDirectoryName(previewStem);
            if (!string.IsNullOrWhiteSpace(previewDirectory))
                Directory.CreateDirectory(previewDirectory);

            foreach (var image in version.Images)
            {
                var url = image.Url;
                if (string.IsNullOrWhiteSpace(url)) continue;

                try
                {
                    var isVideo = IsVideoPreview(image);
                    var previewPath = isVideo
                        ? previewStem + ResolveVideoPreviewExtension(url)
                        : previewStem + ".png";

                    var downloaded = isVideo
                        ? await DownloadPreviewMediaAsync(url, previewPath)
                        : await _img.DownloadImageAsPng(url, previewPath);

                    if (downloaded)
                        return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download CivitAI preview media {ImageId} for model {ModelId}, version {VersionId}, file {FileName}", image.Id, model.Id, version.Id, file.Name);
                }
            }

            _logger.LogWarning("No CivitAI preview media could be downloaded for model {ModelId}, version {VersionId}, file {FileName}", model.Id, version.Id, file.Name);
        }

        private static bool PreviewFileExists(string previewStem)
            => ImageExtensions.Concat(VideoExtensions).Any(extension => File.Exists(previewStem + extension));

        private static bool IsVideoPreview(CivitaiImageDto image)
            => image.ImageType is { Length: > 0 } && image.ImageType[0] == 0
                || IsKnownVideo(image.Type, image.Url);

        private static string ResolveVideoPreviewExtension(string? url)
        {
            var extension = GetUrlExtension(url);
            return extension != null && VideoExtensions.Contains(extension) ? extension : ".mp4";
        }

        private async Task<bool> DownloadPreviewMediaAsync(string? url, string path)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to download CivitAI preview media {Url}, status code: {StatusCode}", url, response.StatusCode);
                return false;
            }

            await using var source = await response.Content.ReadAsStreamAsync();
            await using var destination = File.Create(path);
            await source.CopyToAsync(destination);
            await destination.FlushAsync();

            return new FileInfo(path).Length > 0;
        }

        private string GetRequiredConfigurationPath(string key)
        {
            var value = _configuration[key];
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Missing required configuration value: {key}");

            return value;
        }

        #region For Development
        public async Task UpdateResourceDescriptions(Action<int> onProgress = null)
        {
            var resources = await _db.GetResources();
            var index = 0;
            foreach (var entity in resources.Where(r => r.CivitaiModelVersionId != null))
            {
                index++;
                var progress = (index * 100) / resources.Count;
                onProgress?.Invoke(progress);
                _events.Publish(new ProgressChangedEventArgs(progress));

                var resource = await GetModelVersion((int)entity.CivitaiModelVersionId);
                if (resource != null)
                {
                    entity.Description = resource.Description;
                    await _db.UpdateResource(entity);
                }
            }
            _events.Publish(new ProgressChangedEventArgs(0));
        }

        public async Task UpdateResourceState()
        {
            var resources = await _db.GetResources();
            foreach (var item in resources)
            {
                var basePath = Path.Combine(_configuration["ResourcesPath"]);
                var subPath = item.Type.Name;
                if (item.SubType != null) subPath = Path.Combine(subPath, item.SubType.Name);
                var enabledPath = Path.Combine(basePath, subPath, item.Filename);
                var disabledPath = Path.Combine(basePath, "_storage", subPath, item.Filename);
                if (File.Exists(enabledPath)) item.IsEnabled = true;
                else if (File.Exists(disabledPath)) item.IsEnabled = false;
                else
                {
                    await Console.Out.WriteLineAsync($"ITEM NOT FOUND: {item.Type.Name}{(item.SubType != null ? $" - {item.SubType.Name}" : "")} => {item.Filename} | {item.Id}");
                    item.IsEnabled = false;
                }
                await _db.UpdateResource(item);
            }
        }

        public async Task UpdateResourceBaseModels(Action<int> onProgress = null)
        {
            var resources = await _db.GetResources();
            var index = 0;

            foreach (var entity in resources.Where(r => r.CivitaiModelVersionId != null && string.IsNullOrEmpty(r.BaseModel)))
            {
                index++;
                var progress = (index * 100) / resources.Count;
                onProgress?.Invoke(progress);
                _events.Publish(new ProgressChangedEventArgs(progress));

                try
                {
                    var version = await GetModelVersion((int)entity.CivitaiModelVersionId);
                    if (version != null && !string.IsNullOrEmpty(version.BaseModel))
                    {
                        entity.BaseModel = version.BaseModel;
                        await _db.UpdateResource(entity);
                        _logger.LogInformation($"Updated BaseModel: {entity.Type.Name} | {entity.SubType.Name} > {entity.Id}: {entity.Title} > {entity.BaseModel}");
                    }
                }
                catch (Exception)
                {
                    _logger.LogError($"Failed to update BaseModel: {entity.Type.Name} | {entity.SubType.Name} >  {entity.Id}: {entity.Title}");
                    entity.BaseModel = "Missing";
                }
            }

            _events.Publish(new ProgressChangedEventArgs(0));
        }
        #endregion
    }

    public enum CivitaiDownloadStatus { Success, Error, Database, Exists }
}
