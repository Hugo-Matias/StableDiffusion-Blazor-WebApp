namespace BlazorWebApp.Models
{
    /// <summary>
    /// A playable persona for Odditarium. Each persona fundamentally reshapes every question
    /// and option the LLM generates during a game session.
    /// </summary>
    public sealed class OdditariumPersona
    {
        /// <summary>Unique key (e.g., "muse", "iron-mother").</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Display name shown on the persona card.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Short tagline (1-2 words) displayed beneath the name.</summary>
        public string Tagline { get; set; } = string.Empty;

        /// <summary>Flavor text paragraph shown on the card back / hover tooltip.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Thematic tags displayed as chips on the card (e.g., "Beauty", "Classical").</summary>
        public string[] ThematicTags { get; set; } = Array.Empty<string>();

        /// <summary>Tone bias injected into the system prompt (e.g., "Poetic, refined, slightly distant").</summary>
        public string ToneBias { get; set; } = string.Empty;

        /// <summary>Style preferences injected into the system prompt for Commit assembly.</summary>
        public string StylePreferences { get; set; } = string.Empty;

        /// <summary>Path to transparent PNG - idle state (relative to wwwroot).</summary>
        public string ImageAssetIdle { get; set; } = string.Empty;

        /// <summary>Path to transparent PNG - hover state (glowing/animated variant).</summary>
        public string ImageAssetHover { get; set; } = string.Empty;

        /// <summary>Path to transparent PNG - active/selected state (most prominent variant).</summary>
        public string ImageAssetActive { get; set; } = string.Empty;

        /// <summary>Path to transparent PNG - thinking/loading state (pensive/contemplative variant).</summary>
        public string ImageAssetThinking { get; set; } = string.Empty;

        /// <summary>CSS color accent used for card border glow and selection highlight.</summary>
        public string AccentColor { get; set; } = string.Empty;

        /// <summary>In-character loading messages cycled on the thinking screen. 15-20 entries recommended.</summary>
        public string[] ThinkingMessages { get; set; } = Array.Empty<string>();

        /// <summary>In-character retort messages shown when user clicks the thinking image (easter egg). 15-20 entries recommended.</summary>
        public string[] ProdMessages { get; set; } = Array.Empty<string>();
    }
}
