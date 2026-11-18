namespace BlazorWebApp.Scheduler.Directives
{
    /// <summary>
    /// Appends or prepends text to the positive or negative prompt with a configurable separator.
    /// </summary>
    public sealed class AppendPromptDirective : Directive
    {
        /// <summary>
        /// The text fragment to add to the prompt.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// When <c>true</c> the text is prepended; otherwise appended.
        /// </summary>
        public bool IsPrefix { get; set; }

        /// <summary>
        /// When <c>true</c> the edit targets the negative prompt; otherwise the positive prompt.
        /// </summary>
        public bool IsNegative { get; set; }

        /// <summary>
        /// Separator inserted between the existing text and the added text when both are non-empty.
        /// </summary>
        public string Separator { get; set; } = ", ";
    }
}
