using System.Text.Json.Serialization;

namespace BlazorWebApp.Data.Dtos.Ollama
{
    public class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OllamaChatMessage> Messages { get; set; } = new();

        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false;

        [JsonPropertyName("keep_alive")]
        public string? KeepAlive { get; set; } = "15m";

        [JsonPropertyName("options")]
        public OllamaOptions? Options { get; set; }

        /// <summary>
        /// When set to <c>"json"</c>, instructs Ollama to constrain output to a single JSON document.
        /// Omitted from serialized payloads when null so existing free-form callers are unaffected.
        /// </summary>
        [JsonPropertyName("format")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Format { get; set; }

        /// <summary>
        /// Controls thinking mode for models that support it (e.g. Qwen3, QwQ).
        /// This is a top-level Ollama API field — NOT inside <c>options</c>.
        /// Set to <c>false</c> to disable the reasoning phase (saves tokens, recommended for structured JSON).
        /// Omitted when null so non-thinking models are unaffected.
        /// </summary>
        [JsonPropertyName("think")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Think { get; set; }
    }

    public class OllamaChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Optional list of base64-encoded image payloads (no data-URI prefix) per the Ollama chat API spec.
        /// Only honored by multimodal models (LLaVA, Qwen2-VL / Qwen3-VL, MiniCPM-V, llama3.2-vision, moondream, etc.).
        /// Omitted from the serialized JSON when null so existing text-only callers produce identical payloads.
        /// </summary>
        [JsonPropertyName("images")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Images { get; set; }
    }

    public class OllamaOptions
    {
        [JsonPropertyName("seed")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Seed { get; set; }

        [JsonPropertyName("temperature")]
        public float Temperature { get; set; } = 1.0f;

        [JsonPropertyName("top_k")]
        public int TopK { get; set; } = 50;

        [JsonPropertyName("top_p")]
        public float TopP { get; set; } = 0.9f;

        [JsonPropertyName("min_p")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? MinP { get; set; }

        [JsonPropertyName("num_ctx")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? NumCtx { get; set; }

        [JsonPropertyName("num_predict")]
        /// <summary>
        /// Maximum tokens to generate. -1 = unlimited (model stops at EOS); -2 = fill context.
        /// Defaults to -1 so the model is never cut off mid-response regardless of variation count or verbosity level.
        /// Override per-call via OllamaOptions if you need a hard cap.
        /// </summary>
        public int NumPredict { get; set; } = -1;

        [JsonPropertyName("stop")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Stop { get; set; }


    }
}