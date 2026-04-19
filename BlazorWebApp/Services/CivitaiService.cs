using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Handlers;
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
            if (req.Limit == null || req.Limit <= 0) req.Limit = 100;
            if (req.Page == null || req.Page <= 0) req.Page = 1;

            var endpoint = "v1/images";
            var param = new Dictionary<string, string>(){
                {"limit", req.Limit.ToString() },
                {"page", req.Page.ToString() }
                };

            if (req.PostId != null && req.PostId > 0) param.Add("postId", req.PostId.ToString());
            if (req.ModelId != null && req.ModelId > 0) param.Add("modelId", req.ModelId.ToString());
            if (req.ModelVersionId != null && req.ModelVersionId > 0) param.Add("modelVersionId", req.ModelVersionId.ToString());
            if (!string.IsNullOrWhiteSpace(req.Username)) param.Add("username", req.Username);
            if (req.Nsfw != null && req.Nsfw != CivitaiNsfw.All) param.Add("nsfw", req.Nsfw.ToString());
            if (req.Sort != null) param.Add("sort", req.Sort.ToString().Replace("_", " "));
            if (req.Period != null) param.Add("period", req.Period.ToString());

            var url = new Uri(QueryHelpers.AddQueryString(new Uri(_httpClient.BaseAddress, endpoint).ToString(), param));
            var images = await _httpClient.GetFromJsonAsync<CivitaiImagesDto>(url);
            foreach (var image in images.Images)
            {
                if (image.MetaObject.ValueKind != JsonValueKind.Null) image.Meta = new(image.MetaObject);
                if (!string.IsNullOrWhiteSpace(image.Url)) image.ImageType = await GetImageType(image.Url);
            }
            return images;
        }

        public async Task<CivitaiImageDto?> GetImageById(int id)
        {
            if (id <= 0) return null;
            var response = await _httpClient.GetFromJsonAsync<CivitaiImagesDto>($"v1/images?nsfw=X&imageId={id}");
            if (response == null || response.Images == null || response.Images.Count == 0) return null;
            var image = response.Images[0];
            if (image.MetaObject.ValueKind != JsonValueKind.Null) image.Meta = new(image.MetaObject);
            if (!string.IsNullOrWhiteSpace(image.Url)) image.ImageType = await GetImageType(image.Url);
            return image;
        }

        public async Task<CivitaiImageDto> GetImageByModelVersionId(int modelVersionId, CivitaiImageDto imageDto)
        {
            if (modelVersionId <= 0) return null;
            string cursor = "0";
            CivitaiImagesDto response = new() { Metadata = new() { NextCursor = cursor } };
            List<CivitaiImageDto> images = new();
            CivitaiImageDto image = null;

            while (image == null && !string.IsNullOrWhiteSpace(response.Metadata.NextCursor))
            {
                response = await _httpClient.GetFromJsonAsync<CivitaiImagesDto>($"v1/images?browsingLevel={imageDto.BrowsingLevel}&modelVersionId={modelVersionId}&cursor={cursor}");
                cursor = response?.Metadata.NextCursor;
                images.AddRange(response?.Images);
                image = response?.Images.FirstOrDefault(i => i.Id == imageDto.Id);
            }
            if (image == null) return null;
            if (image.MetaObject.ValueKind != JsonValueKind.Null) image.Meta = new(image.MetaObject);
            if (!string.IsNullOrWhiteSpace(image.Url)) image.ImageType = await GetImageType(image.Url);
            return image;
        }

        private async Task<byte[]?> GetImageType(string url)
        {

            HttpClient httpClientImage = new HttpClient();
            httpClientImage.DefaultRequestHeaders.Range = new RangeHeaderValue(0, 2);
            try
            {
                var response = await httpClientImage.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsByteArrayAsync();
                }
                else
                {
                    _logger.LogWarning("Failed to get image type, status code: {StatusCode} | {Content}", response.StatusCode, await response.Content.ReadAsStringAsync());
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while getting image type");
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

                    foreach (var version in content.ModelVersions)
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
                                version.Images.Add(new CivitaiImageDto() { Id = data.Id, Url = data.Url, BrowsingLevel = data.NsfwLevel });
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

                    throw new InvalidOperationException(
                        $"Failed to deserialize CivitAI model {id}. Check logs for details. Debug file: {debugPath}",
                        jsonEx);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing model {ModelId}", id);
                    throw;
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

        public async Task<CivitaiDownloadStatus> DownloadResource(CivitaiModelDto model, CivitaiModelVersionDto version, CivitaiModelVersionFileDto file, string? subtype = null, string? typeOverride = null)
        {
            CivitaiDownloadStatus status;
            var resourceType = typeOverride ?? model.Type;
            try
            {
                var url = $"download/models/{version.Id}?type={file.Type}&format={file.Metadata.Format}";

                #region Initialize HTTPClient
                var progressBar = new BaseProgress() { BarColor = Parser.ParseCivitaiResourceColorAsColor((CivitaiModelType)Enum.Parse(typeof(CivitaiModelType), model.Type)), Value = 0 };
                var progressHandler = new ProgressMessageHandler(new HttpClientHandler());
                progressHandler.HttpReceiveProgress += (sender, e) => _progress.Update(progressBar.Id, e.ProgressPercentage);
                var client = new HttpClient(progressHandler);
                client.BaseAddress = _httpClient.BaseAddress;
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _configuration["CivitaiApiToken"]);
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                #endregion

                #region Get/Create Directory
                var path = Path.Combine(_configuration["ResourcesPath"], "_storage", resourceType);
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
                var previewPath = Path.Combine(_configuration["ResourcePreviewsPath"], resourceType, Path.GetFileNameWithoutExtension(file.Name) + ".png");
                if (!File.Exists(previewPath))
                {
                    var index = 0;
                    bool isImageDownloaded;
                    do
                    {
                        isImageDownloaded = await _img.DownloadImageAsPng(version.Images[index].Url, previewPath);
                        index++;
                    }
                    while (!isImageDownloaded && index < version.Images.Count);
                }
                #endregion

                #region Download Resource
                path = Path.Combine(path, file.Name);
                if (File.Exists(path) || await _db.CheckResourceExistsByFilename(file.Name)) status = CivitaiDownloadStatus.Database;
                else
                {
                    _progress.Add(progressBar);
                    using var fs = new FileStream(path, FileMode.Create);
                    await response.Content.CopyToAsync(fs);
                    _progress.Remove(progressBar.Id);
                    status = CivitaiDownloadStatus.Success;
                }
                #endregion

                #region Add to DB
                if (!_ignoreModelTypes.Contains((CivitaiModelType)Enum.Parse(typeof(CivitaiModelType), model.Type)) && !_ignoreFileType.Contains(file.Type.ToLower()))
                {
                    var entity = new Resource(model, version, file);
                    if (typeOverride != null) entity.Type = new() { Name = resourceType };
                    if (!string.IsNullOrWhiteSpace(subtype)) entity.SubType = new() { Name = subtype };
                    var isAdded = await _db.CreateResource(entity);
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
