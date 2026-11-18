namespace BlazorWebApp.Scheduler.Directives
{
    /// <summary>
    /// Performs a textual search/replace on the positive or negative prompt.
    /// </summary>
    public sealed class ReplacePromptDirective : Directive
    {
        /// <summary>
        /// Text to find within the prompt.
        /// </summary>
        public string Search { get; set; } = string.Empty;

        /// <summary>
        /// Replacement text.
        /// </summary>
        public string Replace { get; set; } = string.Empty;

        /// <summary>
        /// When <c>true</c> the edit targets the negative prompt; otherwise the positive prompt.
        /// </summary>
        public bool IsNegative { get; set; }

        /// <summary>
        /// When <c>true</c> the search is case-sensitive.
        /// </summary>
        public bool CaseSensitive { get; set; }
    }
}
