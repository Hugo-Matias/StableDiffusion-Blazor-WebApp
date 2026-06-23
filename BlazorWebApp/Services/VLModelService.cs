using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Orchestrates Image-to-Prompt flows against an Ollama vision-language model.
    /// Resolves the appropriate <see cref="SystemPromptTemplate"/> by style, encodes
    /// the input image to base64, sends the multimodal chat request, and optionally
    /// pipes the raw response through <see cref="TagPromptService"/> for Danbooru-tag
    /// normalization.
    /// </summary>
    public class VLModelService
    {
        private readonly OllamaService _ollama;
        private readonly IDatabaseService _database;
        private readonly TagPromptService? _tagPrompts;
        private readonly ILogger<VLModelService> _logger;

        public VLModelService(
            OllamaService ollama,
            IDatabaseService database,
            ILogger<VLModelService> logger,
            TagPromptService? tagPrompts = null)
        {
            _ollama = ollama;
            _database = database;
            _logger = logger;
            _tagPrompts = tagPrompts;
        }

        /// <summary>
        /// Run the configured VL model on the request image and return the produced prompt.
        /// </summary>
        public async Task<InterrogationResult> InterrogateAsync(
            InterrogationRequest request,
            CancellationToken ct = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.ModelName))
                throw new ArgumentException("ModelName is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.ImagePath) && (request.ImageBytes is null || request.ImageBytes.Length == 0))
                throw new ArgumentException("Either ImagePath or ImageBytes must be provided.", nameof(request));

            var result = new InterrogationResult
            {
                ModelName = request.ModelName,
                Style = request.Style
            };

            // 1. Resolve the system prompt template (VL.{Style}).
            var templateName = $"VL.{request.Style}";
            var template = await ResolveTemplateAsync(templateName);
            if (template is null)
            {
                _logger.LogWarning("VL template '{Template}' not found in DB; falling back to a minimal prompt.", templateName);
            }

            // 2. Encode image -> base64.
            string base64;
            if (!string.IsNullOrWhiteSpace(request.ImagePath))
            {
                base64 = await _ollama.EncodeImageBase64Async(request.ImagePath!, ct);
            }
            else
            {
                base64 = Convert.ToBase64String(request.ImageBytes!);
            }

            // 3. Compose the message list. Use the template messages verbatim, then attach the
            //    image to the LAST user-role message. If the template has no user message, append
            //    a minimal one.
            var messages = template is null
                ? new List<OllamaChatMessage>
                  {
                      new OllamaChatMessage
                      {
                          Role = "system",
                          Content = "You are a vision model. Describe the input image as a stable-diffusion prompt."
                      }
                  }
                : template.Messages.Select(m => new OllamaChatMessage
                {
                    Role = m.Role,
                    Content = m.Content,
                    Images = m.Images
                }).ToList();

            // Substitute {concept} placeholder for VideoInstruct style.
            if (request.Style == InterrogationStyle.VideoInstruct && !string.IsNullOrWhiteSpace(request.UserInstruction))
            {
                foreach (var msg in messages)
                {
                    if (!string.IsNullOrEmpty(msg.Content))
                        msg.Content = msg.Content.Replace("{concept}", request.UserInstruction);
                }
            }

            var lastUser = messages.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (lastUser is null)
            {
                lastUser = new OllamaChatMessage { Role = "user", Content = string.Empty };
                messages.Add(lastUser);
            }
            lastUser.Images = new List<string> { base64 };

            ct.ThrowIfCancellationRequested();

            // 4. Send to Ollama (non-streaming for v1 - Step 5 adds streaming).
            var response = await _ollama.SendChatMessage(
                request.ModelName,
                messages,
                options: null,
                keepAlive: "15m",
                stream: false);

            var raw = response?.Message?.Content ?? string.Empty;
            result.RawResponse = raw;
            result.Prompt = raw;

            if (string.IsNullOrWhiteSpace(raw))
            {
                _logger.LogWarning("VL model '{Model}' returned empty content.", request.ModelName);
                return result;
            }

            // 5. Optional Danbooru-tag normalization pass.
            if (request.NormalizeToDanbooruTags && _tagPrompts is not null)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var tagModel = string.IsNullOrWhiteSpace(request.TagNormalizationModelName)
                        ? request.ModelName
                        : request.TagNormalizationModelName!;

                    var tagRequest = new TagBuilderRequest(
                        UserInput: raw,
                        Verbosity: TagVerbosity.Standard,
                        Preset: TagModelPreset.Illustrious,
                        CategoryToggles: new Dictionary<TagCategory, bool>(),
                        GroundingPrompt: null,
                        Mode: TagBuilderMode.TwoPass);

                    var tagResult = await _tagPrompts.BuildAsync(tagRequest, tagModel, options: null, ct);
                    if (!string.IsNullOrWhiteSpace(tagResult.FinalPrompt))
                    {
                        result.NormalizedTagPrompt = tagResult.FinalPrompt;
                        result.Prompt = tagResult.FinalPrompt;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tag normalization pass failed; returning raw VL response.");
                }
            }

            return result;
        }

        /// <summary>
        /// Streaming variant of <see cref="InterrogateAsync"/>. Reports incremental content
        /// chunks via <paramref name="onChunk"/> as they arrive (called synchronously inline
        /// with the streaming loop, NOT via SynchronizationContext.Post - this matters for
        /// Blazor Server where Progress&lt;T&gt;'s deferred posts cause both UI lag and a
        /// post-completion duplicate-append race against the final assignment).
        /// </summary>
        public async Task<InterrogationResult> InterrogateStreamingAsync(
            InterrogationRequest request,
            Action<string>? onChunk,
            CancellationToken ct = default)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(request.ModelName))
                throw new ArgumentException("ModelName is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.ImagePath) && (request.ImageBytes is null || request.ImageBytes.Length == 0))
                throw new ArgumentException("Either ImagePath or ImageBytes must be provided.", nameof(request));

            var result = new InterrogationResult
            {
                ModelName = request.ModelName,
                Style = request.Style
            };

            var template = await ResolveTemplateAsync($"VL.{request.Style}");

            string base64 = !string.IsNullOrWhiteSpace(request.ImagePath)
                ? await _ollama.EncodeImageBase64Async(request.ImagePath!, ct)
                : Convert.ToBase64String(request.ImageBytes!);

            var messages = template is null
                ? new List<OllamaChatMessage>
                  {
                      new OllamaChatMessage
                      {
                          Role = "system",
                          Content = "You are a vision model. Describe the input image as a stable-diffusion prompt."
                      }
                  }
                : template.Messages.Select(m => new OllamaChatMessage
                {
                    Role = m.Role,
                    Content = m.Content,
                    Images = m.Images
                }).ToList();

            // Substitute {concept} placeholder for VideoInstruct style.
            if (request.Style == InterrogationStyle.VideoInstruct && !string.IsNullOrWhiteSpace(request.UserInstruction))
            {
                foreach (var msg in messages)
                {
                    if (!string.IsNullOrEmpty(msg.Content))
                        msg.Content = msg.Content.Replace("{concept}", request.UserInstruction);
                }
            }

            var lastUser = messages.LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (lastUser is null)
            {
                lastUser = new OllamaChatMessage { Role = "user", Content = string.Empty };
                messages.Add(lastUser);
            }
            lastUser.Images = new List<string> { base64 };

            ct.ThrowIfCancellationRequested();

            var sb = new System.Text.StringBuilder();
            await foreach (var chunk in _ollama.StreamChatMessageAsync(request.ModelName, messages, options: null, keepAlive: "15m", ct))
            {
                var piece = chunk.Message?.Content;
                if (!string.IsNullOrEmpty(piece))
                {
                    sb.Append(piece);
                    onChunk?.Invoke(piece);
                }
                if (chunk.Done) break;
            }

            var raw = sb.ToString();
            result.RawResponse = raw;
            result.Prompt = raw;

            if (string.IsNullOrWhiteSpace(raw))
            {
                _logger.LogWarning("VL model '{Model}' returned empty streamed content.", request.ModelName);
                return result;
            }

            if (request.NormalizeToDanbooruTags && _tagPrompts is not null)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var tagModel = string.IsNullOrWhiteSpace(request.TagNormalizationModelName)
                        ? request.ModelName
                        : request.TagNormalizationModelName!;

                    var tagRequest = new TagBuilderRequest(
                        UserInput: raw,
                        Verbosity: TagVerbosity.Standard,
                        Preset: TagModelPreset.Illustrious,
                        CategoryToggles: new Dictionary<TagCategory, bool>(),
                        GroundingPrompt: null,
                        Mode: TagBuilderMode.TwoPass);

                    var tagResult = await _tagPrompts.BuildAsync(tagRequest, tagModel, options: null, ct);
                    if (!string.IsNullOrWhiteSpace(tagResult.FinalPrompt))
                    {
                        result.NormalizedTagPrompt = tagResult.FinalPrompt;
                        result.Prompt = tagResult.FinalPrompt;
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tag normalization pass failed; returning raw VL response.");
                }
            }

            return result;
        }

        private async Task<SystemPromptTemplate?> ResolveTemplateAsync(string name)
        {
            var all = await _database.GetSystemPromptTemplates();
            // Prefer default (seeded) templates over user-edited duplicates with the same name.
            return all
                .Where(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(t => t.IsDefault)
                .FirstOrDefault();
        }
    }
}
