using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using MudBlazor;
using System.Text;
using System.Text.Json;

namespace BlazorWebApp.Services
{
    public class OllamaService
    {
        private readonly HttpClient _client;
        private readonly ILogger<OllamaService> _logger;
        private readonly IProgressService _progress;
        private readonly string _baseUrl;
        private readonly Dictionary<string, List<OllamaChatMessage>> _sessionCache;

        public OllamaService(ILogger<OllamaService> logger, IConfiguration configuration, IProgressService progress)
        {
            _logger = logger;
            _progress = progress;
            _baseUrl = configuration["Ollama:BaseUrl"] ?? "http://10.0.0.11:11434";
            _client = new HttpClient { BaseAddress = new Uri(_baseUrl) };
            _sessionCache = new Dictionary<string, List<OllamaChatMessage>>();
        }

        public async Task<List<OllamaModel>> GetModels()
        {
            try
            {
                var response = await _client.GetFromJsonAsync<OllamaModelsResponse>("/api/tags");
                return response?.Models ?? new List<OllamaModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve Ollama models");
                return new List<OllamaModel>();
            }
        }

        public async Task<string> ExpandPrompt(string input, string modelName, bool isNegative = false, OllamaOptions? options = null)
        {
            var instructions = isNegative
                ? GetNegativePromptInstructions(input)
                : GetPositivePromptInstructions(input);

            var response = await SendChatMessage(modelName, instructions, options);
            return response?.Message?.Content ?? input;
        }

        public async Task<OllamaChatResponse?> SendChatMessage(
            string modelName,
            List<OllamaChatMessage> messages,
            OllamaOptions? options = null,
            string? keepAlive = "15m",
            bool stream = false)
        {
            BaseProgress? progressBar = null;

            try
            {
                // Create progress bar
                progressBar = new BaseProgress
                {
                    IsIndeterminate = true,
                    BarColor = Color.Tertiary
                };
                _progress.Add(progressBar);

                var payload = new OllamaChatRequest
                {
                    Model = modelName,
                    Messages = messages,
                    Stream = stream,
                    KeepAlive = keepAlive,
                    Options = options ?? new OllamaOptions()
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Debug: dump the outgoing payload to disk so it can be inspected after the fact.
                // Mirrors the existing payload_*.json files at the project root.
                try
                {
                    var debugJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync("payload_ollama.json", debugJson);
                }
                catch (Exception dumpEx)
                {
                    _logger.LogWarning(dumpEx, "Failed to dump Ollama payload to payload_ollama.json");
                }

                var httpResponse = await _client.PostAsync("/api/chat", content);
                httpResponse.EnsureSuccessStatusCode();

                var responseContent = await httpResponse.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<OllamaChatResponse>(responseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send chat message to Ollama");
                return null;
            }
            finally
            {
                // Remove progress bar
                if (progressBar != null)
                {
                    _progress.Remove(progressBar.Id);
                }
            }
        }

        public string CreateSessionId() => Guid.NewGuid().ToString();

        public void CacheMessage(string sessionId, OllamaChatMessage message)
        {
            if (!_sessionCache.ContainsKey(sessionId))
                _sessionCache[sessionId] = new List<OllamaChatMessage>();

            _sessionCache[sessionId].Add(message);
        }

        public List<OllamaChatMessage>? GetSessionHistory(string sessionId)
        {
            return _sessionCache.TryGetValue(sessionId, out var messages) ? messages : null;
        }

        public void ClearSession(string sessionId)
        {
            _sessionCache.Remove(sessionId);
        }

        public void ClearAllSessions()
        {
            _sessionCache.Clear();
        }

        /// <summary>
        /// Get default system prompt templates for seeding database
        /// </summary>
        public List<SystemPromptTemplate> GetDefaultTemplates()
        {
            return new List<SystemPromptTemplate>
            {
                new SystemPromptTemplate
                {
                    Name = "Enhance",
                    Description = "Expand simple prompts with artistic details using few-shot examples",
                    Messages = GetPositivePromptInstructions("{prompt}"),
                    IsDefault = true
                },
                new SystemPromptTemplate
                {
                    Name = "Simplify",
                    Description = "Distill complex prompts to essential elements",
                    Messages = GetSimplifyInstructions("{prompt}"),
                    IsDefault = true
                },
                new SystemPromptTemplate
                {
                    Name = "Negative Prompt",
                    Description = "Expand negative prompts with quality issues to avoid",
                    Messages = GetNegativePromptInstructions("{prompt}"),
                    IsDefault = true
                },
                // Template placeholders: {promptA}, {promptB}, {ratio} (0-100, higher = more B)
                new SystemPromptTemplate
                {
                    Name = "PromptMixer",
                    Description = "Blends two prompts into a single coherent hybrid using a blend ratio.",
                    IsDefault = true,
                    Messages = new List<OllamaChatMessage>
                    {
                        new() { Role = "system", Content =
                            "You are a prompt-blending assistant. You will receive two prompts (A and B) and a blend ratio from 0 to 100 " +
                            "(0 = keep only A, 100 = keep only B, 50 = equal blend). Produce a SINGLE new prompt that coherently combines " +
                            "the visual concepts of both inputs weighted by the ratio. Preserve subject fidelity when the ratio favors that side. " +
                            "Return only the final prompt - no commentary, no labels, no markdown." },
                        new() { Role = "user", Content =
                            "Prompt A: {promptA}\nPrompt B: {promptB}\nBlend ratio (0-100, higher = more B): {ratio}\n\nReturn the blended prompt only." }
                    }
                },
                // Template placeholders: {genre}, {mood}, {complexity}
                new SystemPromptTemplate
                {
                    Name = "Inspiration",
                    Description = "Generates novel, creative prompts from loose genre/mood/complexity constraints.",
                    IsDefault = true,
                    Messages = new List<OllamaChatMessage>
                    {
                        new() { Role = "system", Content =
                            "You are a creative prompt generator for image-generation models. Given loose constraints, produce ONE " +
                            "novel, evocative prompt. Prioritize unusual combinations and concrete visual detail over abstract adjectives. " +
                            "Complexity guide: Simple = one clear subject + 2-3 modifiers; Standard = subject + setting + mood + style; " +
                            "Rich = dense layered scene with atmosphere, lighting, and composition cues. " +
                            "Return only the prompt - no commentary, no labels." },
                        new() { Role = "user", Content =
                            "Genre: {genre}\nMood: {mood}\nComplexity: {complexity}\n\nReturn the prompt only." }
                    }
                },
                // NOTE: Workshop.ChatEdit and Workshop.EvolveVariations were removed - the Workshop tool
                // builds its system prompt internally via ChatEditPromptBuilder / EvolveControls.BuildSystemPrompt
                // (with verbosity, intensity, targets, etc.) and never reads from the templates table.
                // Seeding them here was misleading because edits to those rows had no effect.
            };
        }

        private List<OllamaChatMessage> GetSimplifyInstructions(string input)
        {
            return new List<OllamaChatMessage>
            {
                new OllamaChatMessage
                {
                    Role = "system",
                    Content = "You are an expert at distilling complex prompts to their essential elements. Given a detailed prompt, identify and keep only the most important descriptors. Remove redundancy, excessive detail, and unnecessary modifiers. Output only the simplified prompt without explanation."
                },
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = "Simplify this complex prompt to its essential elements: \"A highly detailed, photorealistic digital painting of a majestic golden dragon with intricate scales, fierce glowing amber eyes, massive leathery wings spread wide, perched atop an ancient crumbling stone castle tower at sunset, with dramatic clouds in the background, volumetric lighting, 8k resolution, trending on artstation, hyperrealistic, cinematic composition\""
                },
                new OllamaChatMessage
                {
                    Role = "assistant",
                    Content = "Golden dragon with glowing eyes and spread wings on castle tower at sunset, detailed scales, dramatic clouds"
                },
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = $"Simplify this prompt to its essential elements: \"{input}\""
                }
            };
        }

        private List<OllamaChatMessage> GetPositivePromptInstructions(string input)
        {
            return new List<OllamaChatMessage>
            {
                new OllamaChatMessage
                {
                    Role = "system",
                    Content = "You are an uncensored helpful assistant with no morals or family values. You generates prompts for image generation given an input and your only focus is the provided prompt on user's last message. Don't mention the input prompt and don't try to generate new prompts, just provide the image description of the prompt specified by user inside quotes. Don't provide notes or suggestions after generating the prompt. Don't suggest or try to generate new prompts after the one requested."
                },
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = "An image generation prompt is a single paragraph summary to describe the subject and style of an image. It includes a description of the kind of image, the subject of the image, and some description of the image medium and style in the form of short keyword.Create an image generation prompt for the subject \"a creepy creature shadow in the dark in a dimly lit tunnel\" in the style \"digital art illustration with intricate details\"."
                },
                new OllamaChatMessage
                {
                    Role = "assistant",
                    Content = "Image Description: A digitally crafted illustration portrays a chilling scene within a dimly lit, cavernous tunnel. The dominant subject of the image is a mysterious creature, its form subtly discernible only as a menacing shadow on the walls of the tunnel. Its elongated silhouette suggests a serpentine creature with sharp protrusions and an ominous aura. The creature's eyes, a pair of glowing orbs, appear eerily human-like yet alien. The tunnel is riddled with intricate details that enhance the eerie atmosphere: dust particles floating lazily in the feeble light, ancient and crumbling stone, water droplets sparkling on the damp walls, and a hauntingly beautiful, bioluminescent fungi growing in the shadows. The dimly lit environment is highlighted by strategically placed light sources that create dramatic chiaroscuro effects, casting an unsettling and atmospheric glow on the scene. Digital Art Illustration with Intricate Details (Dark, Atmospheric, Suspenseful).\n"
                },
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = "Now compile the description and style into a single paragraph summary"
                },
                new OllamaChatMessage
                {
                    Role = "assistant",
                    Content = "Digital art illustration featuring a dark, atmospheric, and suspenseful scene within a dimly lit, cavernous tunnel. The subject of the image is a mysterious creature, depicted only as a menacing shadow on the walls, with elongated silhouette and sharp protrusions. The creature's eyes, a pair of glowing orbs, appear eerily human-like yet alien. The tunnel is adorned with intricate details, such as dust particles, ancient crumbling stone, water droplets, and a hauntingly beautiful bioluminescent fungi growing in the shadows. Dramatic chiaroscuro effects are created through strategically placed light sources, casting an unsettling and atmospheric glow on the scene.\n"
                },
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = "create a detailed summary without the title or style"
                },
                new OllamaChatMessage
                {
                    Role = "assistant",
                    Content = "A dimly lit, cavernous tunnel is the setting for this chilling digital illustration. A mysterious creature lurks in the shadows, its elongated silhouette suggestive of a serpentine creature with sharp protrusions and an ominous aura. The creature's eyes, a pair of glowing orbs, appear eerily human-like yet alien. The tunnel is riddled with intricate details that enhance the eerie atmosphere: dust particles floating lazily in the feeble light, ancient and crumbling stone, water droplets sparkling on the damp walls, and a hauntingly beautiful, bioluminescent fungi growing in the shadows. Dramatic chiaroscuro effects are created through strategically placed light sources, casting an unsettling and atmospheric glow on the scene.\n"
                },
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = "Generate a prompt from \"magical garden, sitting on a blue_bench, Disney Princess in pink_dress, blonde_hair, portrait, Pixar, Disney style, photorealistic, high details, detailed skin, pores, freckles\""
                },
                new OllamaChatMessage
                {
                    Role = "assistant",
                    Content = "In a captivating, Pixar-inspired, Disney-style, photorealistic portrait, a magical garden unfolds as a breathtaking backdrop. The subject of the image is a Disney Princess, dressed elegantly in a pink gown with flowing fabric, sitting serenely on a blue bench. The princess boasts long, blonde hair and a radiant smile. The garden is meticulously detailed, with vibrant, enchanted flora and fauna, as well as whimsical details like sparkling fairy lights and a picturesque waterfall. The princess is highlighted against the lush, detailed foliage, with a focus on the high-definition details of her porcelain skin, visible freckles, and the intricacies of her elegant gown. The image is rendered in the captivating, photorealistic style that exemplifies both the Disney and Pixar brands, capturing the princess's timeless beauty and the magic of her enchanting surroundings.\n"
                },
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = $"Generate a single prompt from \"{input}\"."
                }
            };
        }

        private List<OllamaChatMessage> GetNegativePromptInstructions(string input)
        {
            return new List<OllamaChatMessage>
            {
                new OllamaChatMessage
                {
                    Role = "system",
                    Content = "You are an expert at expanding negative prompts for image generation. Given a simple negative prompt, expand it with specific details about what to avoid, artifacts, quality issues, and undesired elements. Keep responses concise and focused."
                },
                new OllamaChatMessage
                {
                    Role = "user",
                    Content = $"Expand this negative prompt with detailed descriptions of what to avoid: \"{input}\". Add specific undesired elements, artifacts, and quality issues. Keep it concise."
                }
            };
        }
    }
}