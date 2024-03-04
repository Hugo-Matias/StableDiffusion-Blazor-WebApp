using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Handlers;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BlazorWebApp.Services
{
    public class CivitaiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ImageService _img;
        private readonly IOService _io;
        private readonly ManagerService _m;
        private readonly DatabaseService _db;
        private readonly ProgressService _progress;
        private readonly List<string> _ignoreFileType = new() { "config" };
        private readonly List<CivitaiModelType> _ignoreModelTypes = new() { CivitaiModelType.Controlnet, CivitaiModelType.Poses, CivitaiModelType.Wildcards, CivitaiModelType.Other };

        public CivitaiService(HttpClient httpClient, IConfiguration configuration, ImageService img, IOService io, ManagerService m, DatabaseService db, ProgressService progress)
        {
            _configuration = configuration;
            _img = img;
            _io = io;
            _m = m;
            _db = db;
            _progress = progress;
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://civitai.com/api/");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _configuration["CivitaiApiToken"]);
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
            var response = await _httpClient.GetFromJsonAsync<CivitaiImagesDto>($"v1/images?imageId={id}");
            if (response == null || response.Images == null || response.Images.Count == 0) return null;
            var image = response.Images[0];
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
                    await Console.Out.WriteLineAsync(response.StatusCode.ToString());
                    return null;
                }
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync(ex.Message);
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
                else return null;
            }

            var query = !string.IsNullOrWhiteSpace(req.Query) ? $"query={req.Query}&" : string.Empty;
            var limit = req.Limit > 0 ? $"limit={req.Limit}&" : string.Empty;
            var page = req.Page > 0 ? $"page={req.Page}&" : string.Empty;
            var username = !string.IsNullOrWhiteSpace(req.Username) ? $"username={req.Username}&" : string.Empty;
            var tag = !string.IsNullOrWhiteSpace(req.Tag) ? $"tag={req.Tag}&" : string.Empty;
            var type = req.Type != null && req.Type != CivitaiModelType.All ? $"types={req.Type}&" : string.Empty;
            var sort = req.Sort != null ? $"sort={req.Sort.ToString().Replace("_", " ")}&" : string.Empty;
            var period = req.Period != null ? $"period={req.Period}&" : string.Empty;
            var rating = req.Rating > -1 ? $"rating={req.Rating}&" : string.Empty;
            //var favorites = req.Favorites != null ? $"favorites={req.Favorites.ToString().ToLower()}&" : string.Empty;
            //var hidden = req.Hidden != null ? $"hidden={req.Hidden.ToString().ToLower()}&" : string.Empty;
            //var primaryFileOnly = req.IsPrimaryFileOnly != null ? $"primaryFileOnly={req.IsPrimaryFileOnly.ToString().ToLower()}" : string.Empty;

            //var url = "v1/models?" + query + limit + page + username + tag + type + sort + period + rating + favorites + hidden + primaryFileOnly;
            var url = "v1/models?" + query + limit + page + username + tag + type + sort + period + rating;
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<CivitaiModelsDto>();
            else return null;
        }

        public async Task<CivitaiModelDto?> GetModel(int id)
        {
            var response = await _httpClient.GetAsync($"v1/models/{id}");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadFromJsonAsync<CivitaiModelDto>();
                foreach (var version in content.ModelVersions)
                {
                    version.Images = new();
                    foreach (var data in version.ImagesData)
                    {
                        if (data.Id == 0)
                        {
                            _ = int.TryParse(Path.GetFileNameWithoutExtension(data.Url), out int imageId);
                            data.Id = imageId;
                        }
                        version.Images.Add(new CivitaiImageDto() { Id = data.Id, Url = data.Url });
                        //var image = await GetImageById(data.Id);
                        //if (image != null) version.Images.Add(image);
                    }
                }
                return content;
            }
            else return null;
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
            else return 0;
        }

        public async Task<CivitaiModelVersionDto> GetModelVersion(int id) => await _httpClient.GetFromJsonAsync<CivitaiModelVersionDto>($"v1/model-versions/{id}");

        public async Task<CivitaiDownloadStatus> DownloadResource(CivitaiModelDto model, CivitaiModelVersionDto version, CivitaiModelVersionFileDto file, string? subtype = null)
        {
            CivitaiDownloadStatus status;
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
                var path = Path.Combine(_configuration["ResourcesPath"], "_storage", model.Type);
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
                var previewPath = Path.Combine(_configuration["ResourcePreviewsPath"], model.Type, Path.GetFileNameWithoutExtension(file.Name) + ".png");
                if (!File.Exists(previewPath)) await _img.DownloadImageAsPng(version.Images[0].Url, previewPath);
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
                    if (!string.IsNullOrWhiteSpace(subtype)) entity.SubType = new() { Name = subtype };
                    var isAdded = await _db.CreateResource(entity);
                    if (!isAdded) status = CivitaiDownloadStatus.Exists;
                }
                #endregion
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                status = CivitaiDownloadStatus.Error;
            }
            return status;
        }

        #region For Development
        public async Task UpdateResourceDescriptions()
        {
            var resources = await _db.GetResources();
            var index = 0;
            foreach (var entity in resources.Where(r => r.CivitaiModelVersionId != null))
            {
                index++;
                _m.CurrentProgress = (index * 100) / resources.Count;
                var resource = await GetModelVersion((int)entity.CivitaiModelVersionId);
                if (resource != null)
                {
                    entity.Description = resource.Description;
                    await _db.UpdateResource(entity);
                }
            }
            _m.CurrentProgress = 0;
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
        #endregion
    }

    public enum CivitaiDownloadStatus { Success, Error, Database, Exists }
}
