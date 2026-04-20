namespace BlazorWebApp.Scheduler.Variations
{
    /// <summary>
    /// Produces one iteration per replacement value by performing a textual search-and-replace on the
    /// positive or negative prompt. The <see cref="Variation.Target"/> is ignored for this variation;
    /// prompt resolution is determined by <see cref="IsNegative"/>.
    /// </summary>
    public sealed class SearchReplaceVariation : Variation
    {
        /// <summary>Text to find within the prompt.</summary>
        public string Search { get; set; } = string.Empty;

        /// <summary>Replacement values; one iteration per entry.</summary>
        public List<string> Replacements { get; set; } = new();

        /// <summary>When <c>true</c> the replacement targets the negative prompt.</summary>
        public bool IsNegative { get; set; }

        /// <summary>When <c>true</c> the search is case-sensitive.</summary>
        public bool CaseSensitive { get; set; }
    }
}
