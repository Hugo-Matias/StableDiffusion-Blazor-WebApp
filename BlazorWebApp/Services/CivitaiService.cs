﻿using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Data.Entities;
using System.Net.Http.Handlers;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
    public class CivitaiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ImageService _img;
        private readonly IOService _io;
        private readonly AppState _app;
        private readonly DatabaseService _db;

        public CivitaiService(HttpClient httpClient, IConfiguration configuration, ImageService img, IOService io, AppState app, DatabaseService db)
        {
            _configuration = configuration;
            _img = img;
            _io = io;
            _app = app;
            _db = db;
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
            var favorites = req.Favorites != null ? $"favorites={req.Favorites.ToString().ToLower()}&" : string.Empty;
            var hidden = req.Hidden != null ? $"hidden={req.Hidden.ToString().ToLower()}&" : string.Empty;
            var primaryFileOnly = req.IsPrimaryFileOnly != null ? $"primaryFileOnly={req.IsPrimaryFileOnly.ToString().ToLower()}" : string.Empty;

            var url = "v1/models?" + query + limit + page + username + tag + type + sort + period + rating + favorites + hidden + primaryFileOnly;
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<CivitaiModelsDto>();
            else return null;
        }

        public async Task<CivitaiModelDto> GetModel(int id)
        {
            var response = await _httpClient.GetAsync($"v1/models/{id}");
            var content = await response.Content.ReadAsStringAsync();
            var model = await response.Content.ReadFromJsonAsync<CivitaiModelDto>();
            var json = JsonNode.Parse(content);
            for (var v = 0; v < json["modelVersions"].AsArray().Count; v++)
            {
                var version = json["modelVersions"][v];
                for (var i = 0; i < version["images"].AsArray().Count; i++)
                {
                    var meta = version["images"][i]["meta"];
                    if (meta != null)
                    {
                        model.ModelVersions[v].Images[i].MetaObject = meta;
                        var isAddNetEnabled = meta["AddNet Enabled"];
                        if (isAddNetEnabled != null && isAddNetEnabled.ToString() == "True" && model.ModelVersions[v].Images[i].Meta.Resources != null)
                        {
                            var modelResources = model.ModelVersions[v].Images[i].Meta.Resources;
                            for (int r = 1; r <= modelResources.Count; r++)
                            {
                                var name = meta[$"AddNet Model {r}"];
                                var weight = meta[$"AddNet Weight A {r}"];
                                var moduleType = meta[$"AddNet Module {r}"];
                                if (name != null && weight != null)
                                    ParseAddNetResource(ref modelResources, name.ToString(), weight.ToString(), moduleType.ToString());
                            }
                        }
                    }
                }
            }
            return model;
        }

        private void ParseAddNetResource(ref List<CivitaiImageMetaResource> resources, string addnetModel, string addnetWeight, string addnetModule)
        {
            var weight = float.Parse(addnetWeight);
            var re = Regex.Matches(addnetModel, @"(.+)\((.+)\)");
            if (re.Count > 0)
            {
                var groups = re[0].Groups;
                var name = groups[1].Value;
                var hash = groups[2].Value;
                foreach (var resource in resources)
                {
                    if (resource.Hash != null && resource.Hash.Equals(hash, StringComparison.InvariantCultureIgnoreCase))
                    {
                        resource.Name = name;
                        resource.Weight = weight;
                    }
                }
            }
            else
            {
                foreach (var resource in resources)
                {
                    if (resource.Type != null && resource.Type.Equals(addnetModule, StringComparison.InvariantCultureIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(resource.Name) || resource.Name.Equals(addnetModel, StringComparison.InvariantCultureIgnoreCase))
                        {
                            resource.Name = addnetModel;
                            resource.Weight = weight;
                        }
                    }
                }
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
            else return 0;
        }

        public async Task<CivitaiDownloadStatus> DownloadResource(CivitaiModelDto model, CivitaiModelVersionDto version, CivitaiModelVersionFileDto file, string? subtype = null)
        {
            CivitaiDownloadStatus status;
            try
            {
                var url = $"download/models/{version.Id}?type={file.Type}&format={file.Format}";

                #region Initialize HTTPClient
                var progressHandler = new ProgressMessageHandler(new HttpClientHandler());
                progressHandler.HttpReceiveProgress += (sender, e) => _app.CurrentDownloadProgress = e.ProgressPercentage;
                var client = new HttpClient(progressHandler);
                client.BaseAddress = _httpClient.BaseAddress;
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                #endregion

                #region Get/Create Directory
                var path = Path.Combine(_configuration["ResourcesPath"], model.Type);
                if (subtype != null && !subtype.Equals("none", StringComparison.InvariantCultureIgnoreCase)) path = Path.Combine(path, subtype);
                Directory.CreateDirectory(path);
                #endregion

                #region Create TriggerWords Text File
                if (version.TrainedWords != null && version.TrainedWords.Count > 0)
                {
                    var wordsPath = Path.Combine(path, Path.GetFileNameWithoutExtension(file.Name) + ".txt");
                    _io.SaveText(wordsPath, string.Join("\n", version.TrainedWords));
                }
                #endregion

                #region Download Preview Image
                var previewPath = Path.Combine(_configuration["ResourcePreviewsPath"], model.Type, Path.GetFileNameWithoutExtension(file.Name) + ".png");
                await _img.DownloadImageAsPng(version.Images[0].Url, previewPath);
                #endregion

                #region Download Resource
                path = Path.Combine(path, file.Name);
                if (File.Exists(path)) status = CivitaiDownloadStatus.Database;
                else
                {
                    using var fs = new FileStream(path, FileMode.Create);
                    await response.Content.CopyToAsync(fs);
                    _app.CurrentDownloadProgress = 0;
                    status = CivitaiDownloadStatus.Success;
                }
                #endregion

                #region Add to DB
                var entity = new Resource()
                {
                    Title = model.Name,
                    Filename = file.Name,
                    Author = model.Creator.Username,
                    CivitaiModelId = model.Id,
                    CivitaiModelVersionId = version.Id,
                    Type = new() { Name = model.Type },
                    Tags = model.Tags,
                };
                if (!string.IsNullOrWhiteSpace(subtype)) entity.SubType = new() { Name = subtype };
                if (version.TrainedWords != null) entity.TriggerWords = version.TrainedWords;
                var isAdded = await _db.CreateResource(entity);
                if (!isAdded) status = CivitaiDownloadStatus.Exists;
                #endregion
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                status = CivitaiDownloadStatus.Error;
            }
            return status;
        }
    }

    public enum CivitaiDownloadStatus { Success, Error, Database, Exists }
}
