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

        // --- Phase 7.5: Anchor personality ---

        /// <summary>
        /// Anchor names ordered high-to-low persona affinity. Biases the anchor queue shuffle:
        /// high-affinity anchors surface earlier in the session. Anchors not listed are appended
        /// in default tier order after all listed ones.
        /// </summary>
        public string[] AnchorAffinity { get; set; } = Array.Empty<string>();

        /// <summary>Minimum deepen follow-up rounds to run per anchor for this persona (0 = possible skip).</summary>
        public int MinDeepensPerAnchor { get; set; } = 1;

        /// <summary>Maximum deepen follow-up rounds to run per anchor for this persona.</summary>
        public int MaxDeepensPerAnchor { get; set; } = 2;

        // --- Phase 7.6: Persona voice depth ---

        /// <summary>
        /// How this persona frames a question — injected as a directive in the round system prompt.
        /// Shapes sentence structure and rhetorical stance, not just vocabulary.
        /// </summary>
        public string QuestionFraming { get; set; } = string.Empty;

        /// <summary>
        /// Register and style rules for option labels and hints — injected so the LLM writes
        /// labels and hints that sound unmistakably like this persona, not a generic game.
        /// </summary>
        public string OptionVoice { get; set; } = string.Empty;

        /// <summary>
        /// Hard aesthetic exclusions — injected as a FORBIDDEN rule to prevent persona drift
        /// back toward generic or cross-persona aesthetics across long sessions.
        /// </summary>
        public string ForbiddenZones { get; set; } = string.Empty;

        /// <summary>
        /// What this persona believes makes a great image — injected as context that explains
        /// why they make the creative choices they do, giving the LLM a reasoning foundation.
        /// </summary>
        public string CreativePhilosophy { get; set; } = string.Empty;
    }
}
