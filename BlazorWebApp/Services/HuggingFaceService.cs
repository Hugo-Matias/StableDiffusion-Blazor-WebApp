using BlazorWebApp.Data.Dtos.HuggingFace;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Models;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
    public class HuggingFaceService : IHuggingFaceService
    {
        private const string BaseUrl = "https://huggingface.co";
        private const string ApiBase = "https://huggingface.co/api";
        private const int MaxTreePages = 50;

        private readonly HttpClient _httpClient;
        private readonly ISettingsService _settings;
        private readonly IConfiguration _configuration;
        private readonly IDatabaseService _db;
        private readonly IEventService _events;
        private readonly IProgressService _progress;
        private readonly IImageService _img;
        private readonly ILogger<HuggingFaceService> _logger;

        public HuggingFaceService(
            HttpClient httpClient,
            ISettingsService settings,
            IConfiguration configuration,
            IDatabaseService db,
            IEventService events,
            IProgressService progress,
            IImageService img,
            ILogger<HuggingFaceService> logger)
        {
            _httpClient = httpClient;
            _settings = settings;
            _configuration = configuration;
            _db = db;
            _events = events;
            _progress = progress;
            _img = img;
            _logger = logger;

            _httpClient.BaseAddress = new Uri(BaseUrl + "/");
            ApplyTokenHeader();
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        public async Task<HuggingFaceQuickSearchDto?> QuickSearchAsync(string query, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query)) return new HuggingFaceQuickSearchDto();

            var url = QueryHelpers.AddQueryString($"{ApiBase}/quicksearch", new Dictionary<string, string?>
            {
                ["q"] = query.Trim(),
                ["type"] = "model"
            });

            return await GetJsonAsync<HuggingFaceQuickSearchDto>(url, "quicksearch", ct);
        }

        public async Task<(List<HuggingFaceModelSummaryDto> Models, string? NextCursor)> GetModelsAsync(
            HuggingFaceModelsRequest request, CancellationToken ct = default)
        {
            var param = BuildModelListParams(request);
            var url = QueryHelpers.AddQueryString($"{ApiBase}/models", param);

            using var response = await SendAsync(url, "models list", ct);
            if (response == null) throw new HttpRequestException("HuggingFace models request failed. Check the application log for details.");

            string? nextCursor = null;
            if (response.Headers.TryGetValues("X-Linked-Next", out var linkedNext))
            {
                var linked = linkedNext.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(linked))
                    nextCursor = ExtractCursorFromLinkedHeader(linked);
            }

            var models = await DeserializeAsync<List<HuggingFaceModelSummaryDto>>(response, "models list") ?? new();
            return (models, nextCursor);
        }

        public async Task<HuggingFaceModelDetailDto?> GetModelAsync(string author, string repo, CancellationToken ct = default)
        {
            if (!ValidateRepoParams(author, repo, nameof(GetModelAsync))) return null;
            var url = $"{ApiBase}/models/{Uri.EscapeDataString(author)}/{Uri.EscapeDataString(repo)}";
            return await GetJsonAsync<HuggingFaceModelDetailDto>(url, $"model {author}/{repo}", ct);
        }

        public async Task<List<HuggingFaceTreeEntryDto>> GetTreeAsync(
            string author, string repo,
            string revision = "main", string path = "",
            bool recursive = false,
            CancellationToken ct = default)
        {
            if (!ValidateRepoParams(author, repo, nameof(GetTreeAsync)))
                return new List<HuggingFaceTreeEntryDto>();

            var results = new List<HuggingFaceTreeEntryDto>();
            string? cursor = null;
            var visitedCursors = new HashSet<string>(StringComparer.Ordinal);

            for (var page = 0; page < MaxTreePages; page++)
            {
                var baseEndpoint = BuildTreeEndpoint(author, repo, revision, path);
                var param = new Dictionary<string, string?>();
                if (recursive) param["recursive"] = "true";
                if (!string.IsNullOrWhiteSpace(cursor)) param["cursor"] = cursor;

                var url = param.Count > 0
                    ? QueryHelpers.AddQueryString(baseEndpoint, param)
                    : baseEndpoint;

                using var response = await SendAsync(url, $"tree {author}/{repo}/{path}", ct);
                if (response == null) break;

                string? nextCursor = null;
                if (response.Headers.TryGetValues("X-Linked-Next", out var linkedNext))
                {
                    var linked = linkedNext.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(linked))
                        nextCursor = ExtractCursorFromLinkedHeader(linked);
                }

                var entries = await DeserializeAsync<List<HuggingFaceTreeEntryDto>>(response, $"tree {author}/{repo}/{path}");
                if (entries != null) results.AddRange(entries);

                if (recursive || string.IsNullOrWhiteSpace(nextCursor) || !visitedCursors.Add(nextCursor))
                    break;

                cursor = nextCursor;
            }

            return results;
        }

        public async Task<List<HuggingFacePathInfoDto>> GetPathsInfoAsync(
            string author, string repo, IEnumerable<string> paths,
            string revision = "main",
            CancellationToken ct = default)
        {
            if (!ValidateRepoParams(author, repo, nameof(GetPathsInfoAsync)))
                return new List<HuggingFacePathInfoDto>();

            var pathList = paths?.ToList() ?? new List<string>();
            if (pathList.Count == 0) return new List<HuggingFacePathInfoDto>();

            var url = $"{ApiBase}/models/{Uri.EscapeDataString(author)}/{Uri.EscapeDataString(repo)}/paths-info/{Uri.EscapeDataString(revision)}";
            var body = new HuggingFacePathsInfoRequest { Paths = pathList };

            try
            {
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(body)
                };
                ApplyTokenToRequest(requestMessage);

                using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("HuggingFace paths-info request failed with {StatusCode} for {Author}/{Repo}",
                        response.StatusCode, author, repo);
                    return new List<HuggingFacePathInfoDto>();
                }

                var result = await response.Content.ReadFromJsonAsync<List<HuggingFacePathInfoDto>>(cancellationToken: ct);
                return result ?? new List<HuggingFacePathInfoDto>();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogWarning(ex, "HuggingFace paths-info request failed for {Author}/{Repo}", author, repo);
                return new List<HuggingFacePathInfoDto>();
            }
        }

        public async Task<string?> GetReadmeAsync(string author, string repo, string revision = "main", CancellationToken ct = default)
        {
            if (!ValidateRepoParams(author, repo, nameof(GetReadmeAsync))) return null;
            var url = $"{BaseUrl}/{Uri.EscapeDataString(author)}/{Uri.EscapeDataString(repo)}/resolve/{Uri.EscapeDataString(revision)}/README.md";

            try
            {
                using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
                ApplyTokenToRequest(requestMessage);
                using var response = await _httpClient.SendAsync(requestMessage, ct);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("HuggingFace README request failed with {StatusCode} for {Author}/{Repo}",
                        response.StatusCode, author, repo);
                    return null;
                }

                return await response.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "HuggingFace README request failed for {Author}/{Repo}", author, repo);
                return null;
            }
        }

        public string GetResolveUrl(string author, string repo, string filePath, string revision = "main")
        {
            return $"{BaseUrl}/{Uri.EscapeDataString(author)}/{Uri.EscapeDataString(repo)}/resolve/{Uri.EscapeDataString(revision)}/{filePath.TrimStart('/')}";
        }

        // -------------------------------------------------------------------------
        // Download engine
        // -------------------------------------------------------------------------

        public async Task<HuggingFaceDownloadResult> DownloadAsync(
            HuggingFaceDownloadRequest request, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(request.ModelId))
                return HuggingFaceDownloadResult.Fail("ModelId is required.");
            if (string.IsNullOrWhiteSpace(request.TypeName))
                return HuggingFaceDownloadResult.Fail("TypeName must be confirmed before downloading.");
            if (request.Files == null || request.Files.Count == 0)
                return HuggingFaceDownloadResult.Fail("No files selected for download.");

            // Split "author/repo" from ModelId
            var parts = request.ModelId.Split('/', 2);
            if (parts.Length != 2)
                return HuggingFaceDownloadResult.Fail($"Invalid ModelId format '{request.ModelId}'. Expected 'author/repo'.");
            var author = parts[0];
            var repo = parts[1];

            // Pre-flight: flat collision check (use effective output filename when set)
            if (!request.KeepFolderStructure)
            {
                var effectivePaths = request.Files.Select(f =>
                    !string.IsNullOrWhiteSpace(f.OutputFilename) ? f.OutputFilename : f.RepoFilePath);
                var conflictResult = HuggingFacePathResolver.CheckFlatCollisions(effectivePaths);
                if (conflictResult.HasCollisions)
                    return HuggingFaceDownloadResult.Conflict(conflictResult.CollidingFilenames);
            }

            // Build destination root
            var resourcesPath = GetRequiredConfigurationPath("ResourcesPath");
            var destinationRoot = Path.Combine(resourcesPath, request.TypeName);
            if (!string.IsNullOrWhiteSpace(request.SubTypeName)
                && !request.SubTypeName.Equals("none", StringComparison.OrdinalIgnoreCase))
                destinationRoot = Path.Combine(destinationRoot, request.SubTypeName);

            var result = new HuggingFaceDownloadResult();

            foreach (var file in request.Files)
            {
                if (ct.IsCancellationRequested)
                {
                    result.Status = HuggingFaceDownloadStatus.Cancelled;
                    return result;
                }

                string localPath;
                string dbFilename;
                try
                {
                    localPath = HuggingFacePathResolver.Resolve(destinationRoot, file.RepoFilePath, request.KeepFolderStructure, file.OutputFilename);
                    dbFilename = HuggingFacePathResolver.ResolveDbFilename(file.RepoFilePath, request.KeepFolderStructure, file.OutputFilename);
                }
                catch (ArgumentException ex)
                {
                    _logger.LogWarning(ex, "Unsafe path rejected for {RepoFilePath}", file.RepoFilePath);
                    result.FailedFiles.Add(file.RepoFilePath);
                    continue;
                }

                // Skip if already on disk or in DB
                if (File.Exists(localPath) || await _db.CheckResourceExistsByFilename(dbFilename))
                {
                    result.SkippedFiles.Add(file.RepoFilePath);
                    continue;
                }

                var resolveUrl = GetResolveUrl(author, repo, file.RepoFilePath, request.Revision);
                var downloaded = await DownloadFileWithProgressAsync(resolveUrl, localPath, file.RepoFilePath, ct);

                if (!downloaded)
                {
                    result.FailedFiles.Add(file.RepoFilePath);
                    continue;
                }

                result.DownloadedFiles.Add(file.RepoFilePath);

                // Create Resource DB row for resource files
                if (file.IsResourceFile)
                {
                    var resource = new Resource
                    {
                        Title = repo,
                        Filename = dbFilename,
                        Author = author,
                        HuggingFaceModelId = request.ModelId,
                        HuggingFaceRevision = request.Revision,
                        HuggingFaceFilePath = file.RepoFilePath,
                        HuggingFaceSha = file.Sha,
                        BaseModel = request.BaseModel,
                        Tags = null,
                        SizeKb = file.SizeKb,
                        IsEnabled = true,
                        CreatedDate = DateTime.Now,
                        Type = new ResourceType { Name = request.TypeName },
                        SubType = !string.IsNullOrWhiteSpace(request.SubTypeName)
                            && !request.SubTypeName.Equals("none", StringComparison.OrdinalIgnoreCase)
                            ? new ResourceSubType { Name = request.SubTypeName }
                            : null,
                    };

                    var added = await _db.CreateResource(resource);
                    if (added)
                        _events.Publish(new ResourcesChangedEventArgs($"Downloaded {dbFilename} from HuggingFace"));
                }
            }

            // Download preview image from README (best-effort, non-blocking)
            if (request.DownloadPreview && result.DownloadedFiles.Count > 0)
            {
                try
                {
                    await TryDownloadReadmePreviewAsync(author, repo, request.Revision, request.TypeName,
                        Path.GetFileNameWithoutExtension(
                            HuggingFacePathResolver.ResolveDbFilename(
                                request.Files.First(f => f.IsResourceFile).RepoFilePath,
                                request.KeepFolderStructure)),
                        ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Preview download failed for {ModelId}", request.ModelId);
                }
            }

            // Determine overall status
            result.Status = result.DownloadedFiles.Count > 0 && result.FailedFiles.Count == 0
                ? (result.SkippedFiles.Count > 0 ? HuggingFaceDownloadStatus.PartialSuccess : HuggingFaceDownloadStatus.Success)
                : result.DownloadedFiles.Count > 0
                    ? HuggingFaceDownloadStatus.PartialSuccess
                    : result.SkippedFiles.Count > 0 && result.FailedFiles.Count == 0
                        ? HuggingFaceDownloadStatus.AlreadyExists
                        : HuggingFaceDownloadStatus.Error;

            if (result.Status is HuggingFaceDownloadStatus.Success or HuggingFaceDownloadStatus.PartialSuccess)
                _events.Publish(new DownloadCompletedEventArgs(
                    $"{result.DownloadedFiles.Count} file(s) from {request.ModelId}", request.TypeName));

            return result;
        }

        // -------------------------------------------------------------------------
        // Private helpers — download
        // -------------------------------------------------------------------------

        private async Task<bool> DownloadFileWithProgressAsync(
            string url, string localPath, string displayName, CancellationToken ct)
        {
            var progressBar = new Models.BaseProgress { Label = displayName, Value = 0 };
            try
            {
                using var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
                ApplyTokenToRequest(requestMessage);

                using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("HuggingFace file download failed for {DisplayName}: {StatusCode}", displayName, response.StatusCode);
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

                _progress.Add(progressBar);
                try
                {
                    await using var fs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                    await CopyWithProgressAsync(response.Content, fs, progressBar.Id, ct);
                }
                finally
                {
                    _progress.Remove(progressBar.Id);
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                TryDeletePartial(localPath);
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                _logger.LogWarning(ex, "HuggingFace download error for {DisplayName}", displayName);
                TryDeletePartial(localPath);
                return false;
            }
        }

        private async Task CopyWithProgressAsync(HttpContent content, Stream destination, Guid progressId, CancellationToken ct)
        {
            var totalBytes = content.Headers.ContentLength;
            var buffer = new byte[81920];
            long totalRead = 0;

            await using var source = await content.ReadAsStreamAsync(ct);
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await destination.WriteAsync(buffer, 0, bytesRead, ct);
                totalRead += bytesRead;
                if (totalBytes is > 0)
                    _progress.Update(progressId, (float)(totalRead * 100d / totalBytes.Value));
            }
        }

        private static void TryDeletePartial(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* ignore cleanup failures */ }
        }

        private async Task TryDownloadReadmePreviewAsync(
            string author, string repo, string revision,
            string typeName, string resourceStem, CancellationToken ct)
        {
            var previewsPath = _configuration["ResourcePreviewsPath"];
            if (string.IsNullOrWhiteSpace(previewsPath)) return;

            var previewStem = Path.Combine(previewsPath, typeName, resourceStem);
            if (File.Exists(previewStem + ".png") || File.Exists(previewStem + ".jpg") || File.Exists(previewStem + ".jpeg"))
                return;

            var readme = await GetReadmeAsync(author, repo, revision, ct);
            if (string.IsNullOrWhiteSpace(readme)) return;

            var imageUrl = ExtractFirstMarkdownImageUrl(readme, author, repo, revision);
            if (string.IsNullOrWhiteSpace(imageUrl)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(previewStem)!);
            await _img.DownloadImageAsPng(imageUrl, previewStem + ".png");
        }

        /// <summary>
        /// Extracts the first image URL from a markdown README.
        /// Handles standard markdown syntax <c>![alt](url)</c> and bare <c>&lt;img src="url"&gt;</c>.
        /// Relative HF paths are resolved to absolute resolve URLs.
        /// </summary>
        internal static string? ExtractFirstMarkdownImageUrl(string markdown, string author, string repo, string revision)
        {
            // Skip YAML front matter
            var content = StripFrontMatter(markdown);

            // Try markdown image syntax: ![...](url)
            var mdMatch = Regex.Match(content, @"!\[[^\]]*\]\(([^)\s""']+)", RegexOptions.Multiline);
            if (mdMatch.Success)
            {
                var url = mdMatch.Groups[1].Value.Trim();
                return ResolveReadmeImageUrl(url, author, repo, revision);
            }

            // Try HTML img tag: <img src="..." />
            var htmlMatch = Regex.Match(content, @"<img\s[^>]*\bsrc\s*=\s*[""']([^""']+)[""']",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);
            if (htmlMatch.Success)
            {
                var url = htmlMatch.Groups[1].Value.Trim();
                return ResolveReadmeImageUrl(url, author, repo, revision);
            }

            return null;
        }

        private static string? ResolveReadmeImageUrl(string url, string author, string repo, string revision)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (url.StartsWith("http://") || url.StartsWith("https://")) return url;

            // Relative path — resolve via HF resolve endpoint
            var cleanPath = url.TrimStart('.', '/');
            return $"https://huggingface.co/{Uri.EscapeDataString(author)}/{Uri.EscapeDataString(repo)}/resolve/{Uri.EscapeDataString(revision)}/{cleanPath}";
        }

        private static string StripFrontMatter(string markdown)
        {
            if (!markdown.StartsWith("---")) return markdown;
            var end = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
            if (end < 0) return markdown;
            return markdown[(end + 4)..];
        }

        private string GetRequiredConfigurationPath(string key)
        {
            var value = _configuration[key];
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Missing required configuration value: {key}");
            return value;
        }

        private void ApplyTokenHeader()
        {
            var token = _settings.Settings.Resources.HuggingFace.ApiToken;
            if (!string.IsNullOrWhiteSpace(token))
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        /// <summary>
        /// Applies the bearer token to a single outbound request when the shared
        /// HttpClient default headers do not already carry it (e.g. after a settings change).
        /// </summary>
        private void ApplyTokenToRequest(HttpRequestMessage request)
        {
            if (request.Headers.Authorization != null) return;
            var token = _settings.Settings.Resources.HuggingFace.ApiToken;
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        private async Task<HttpResponseMessage?> SendAsync(string url, string context, CancellationToken ct)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                ApplyTokenToRequest(request);
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("HuggingFace {Context} request failed with {StatusCode} at {Url}",
                        context, (int)response.StatusCode, url);
                    response.Dispose();
                    return null;
                }

                return response;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "HuggingFace {Context} request failed at {Url}", context, url);
                return null;
            }
        }

        private async Task<T?> GetJsonAsync<T>(string url, string context, CancellationToken ct)
        {
            using var response = await SendAsync(url, context, ct);
            if (response == null) return default;
            return await DeserializeAsync<T>(response, context, ct);
        }

        private async Task<T?> DeserializeAsync<T>(HttpResponseMessage response, string context, CancellationToken ct = default)
        {
            try
            {
                return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "HuggingFace {Context} JSON parse failed", context);
                return default;
            }
        }

        private static Dictionary<string, string?> BuildModelListParams(HuggingFaceModelsRequest request)
        {
            var param = new Dictionary<string, string?>();
            if (!string.IsNullOrWhiteSpace(request.Query)) param["search"] = request.Query;
            if (!string.IsNullOrWhiteSpace(request.Author)) param["author"] = request.Author;
            if (!string.IsNullOrWhiteSpace(request.Filter)) param["filter"] = request.Filter;
            if (!string.IsNullOrWhiteSpace(request.PipelineTag)) param["pipeline_tag"] = request.PipelineTag;
            if (!string.IsNullOrWhiteSpace(request.Library)) param["library"] = request.Library;
            param["sort"] = request.Sort switch
            {
                HuggingFaceSort.Downloads => "downloads",
                HuggingFaceSort.Likes => "likes",
                HuggingFaceSort.LastModified => "lastModified",
                HuggingFaceSort.Trending => "trendingScore",
                _ => "downloads",
            };
            if (!string.IsNullOrWhiteSpace(request.Direction)) param["direction"] = request.Direction;
            if (request.Limit > 0) param["limit"] = request.Limit.ToString();
            if (!string.IsNullOrWhiteSpace(request.Cursor)) param["cursor"] = request.Cursor;
            if (request.Full) param["full"] = "true";
            if (request.CardData) param["cardData"] = "true";
            return param;
        }

        private static string BuildTreeEndpoint(string author, string repo, string revision, string path)
        {
            var endpoint = $"{ApiBase}/models/{Uri.EscapeDataString(author)}/{Uri.EscapeDataString(repo)}/tree/{Uri.EscapeDataString(revision)}";
            if (!string.IsNullOrWhiteSpace(path))
                endpoint += "/" + path.TrimStart('/');
            return endpoint;
        }

        private static string? ExtractCursorFromLinkedHeader(string linkedHeader)
        {
            // X-Linked-Next header is a URL — extract cursor query param
            try
            {
                var uri = new Uri(linkedHeader.Trim('<', '>', ' '));
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                return query["cursor"];
            }
            catch
            {
                return null;
            }
        }

        private bool ValidateRepoParams(string author, string repo, string callerName)
        {
            if (string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(repo))
            {
                _logger.LogWarning("HuggingFace {Caller} called with empty author or repo", callerName);
                return false;
            }
            return true;
        }
    }
}
