namespace BlazorWebApp.Models
{
    /// <summary>
    /// Output style for the Image-to-Prompt VL flow. Maps 1:1 to a seeded
    /// SystemPromptTemplate named <c>VL.{Style}</c>.
    /// </summary>
    public enum InterrogationStyle
    {
        Detailed,
        Simple,
        Artistic,
        Technical,
        Tags,
        Focus,
        VideoInstruct
    }

    /// <summary>
    /// Request for VLModelService.InterrogateAsync. Either ImagePath OR ImageBytes is required.
    /// </summary>
    public class InterrogationRequest
    {
        public string ModelName { get; set; } = string.Empty;
        public InterrogationStyle Style { get; set; } = InterrogationStyle.Detailed;
        public string? ImagePath { get; set; }
        public byte[]? ImageBytes { get; set; }
        public bool NormalizeToDanbooruTags { get; set; } = false;

        /// <summary>
        /// Optional text-LLM model name used for the tag-normalization pass. Falls back to
        /// <see cref="ModelName"/> when null (works but a dedicated text model usually performs
        /// better at the structured concept extraction than a VL model).
        /// </summary>
        public string? TagNormalizationModelName { get; set; }

        /// <summary>
        /// User-supplied concept instruction for <see cref="InterrogationStyle.VideoInstruct"/>.
        /// Substituted for the <c>{concept}</c> placeholder in the VL.VideoInstruct system prompt
        /// at runtime.
        /// </summary>
        public string? UserInstruction { get; set; }
    }

    /// <summary>
    /// Result from VLModelService.InterrogateAsync.
    /// <para>
    /// <see cref="Prompt"/> is what the UI should display: the normalized tag prompt when
    /// normalization is requested and succeeded, otherwise the raw VL response.
    /// </para>
    /// </summary>
    public class InterrogationResult
    {
        public string ModelName { get; set; } = string.Empty;
        public InterrogationStyle Style { get; set; }
        public string Prompt { get; set; } = string.Empty;
        public string? NormalizedTagPrompt { get; set; }
        public string RawResponse { get; set; } = string.Empty;
    }
}
