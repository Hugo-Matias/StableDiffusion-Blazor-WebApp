namespace BlazorWebApp.Scheduler.Targets
{
    /// <summary>
    /// How a prompt target should combine its produced value with the existing prompt text.
    /// Variations (Wildcard, List, LLM, ...) targeting <see cref="PromptTarget"/> respect this mode;
    /// the default is <see cref="Append"/> so picking a prompt target adds to the base prompt
    /// instead of destructively replacing it.
    /// </summary>
    public enum PromptWriteMode
    {
        /// <summary>Concatenate the produced value after the existing prompt using <see cref="PromptTarget.Separator"/>.</summary>
        Append = 0,

        /// <summary>Concatenate the produced value before the existing prompt using <see cref="PromptTarget.Separator"/>.</summary>
        Prepend = 1,

        /// <summary>Overwrite the existing prompt with the produced value.</summary>
        Replace = 2,
    }

    /// <summary>
    /// Targets the positive or negative prompt text stored on the prompts fragment.
    /// </summary>
    public sealed class PromptTarget : ParameterTarget
    {
        /// <summary>
        /// When <c>true</c> the target refers to the negative prompt; otherwise the positive prompt.
        /// </summary>
        public bool IsNegative { get; set; }

        /// <summary>
        /// Controls how each iteration's value combines with the existing prompt text.
        /// Defaults to <see cref="PromptWriteMode.Append"/>.
        /// </summary>
        public PromptWriteMode Mode { get; set; } = PromptWriteMode.Append;

        /// <summary>
        /// Separator inserted between existing prompt text and the produced value when
        /// <see cref="Mode"/> is <see cref="PromptWriteMode.Append"/> or <see cref="PromptWriteMode.Prepend"/>.
        /// Ignored when <see cref="Mode"/> is <see cref="PromptWriteMode.Replace"/>.
        /// </summary>
        public string Separator { get; set; } = ", ";

        /// <inheritdoc />
        public override string GetDisplayName()
        {
            var side = IsNegative ? "negative" : "positive";
            return Mode switch
            {
                PromptWriteMode.Append => $"Prompt ({side}, append)",
                PromptWriteMode.Prepend => $"Prompt ({side}, prepend)",
                PromptWriteMode.Replace => $"Prompt ({side}, replace)",
                _ => $"Prompt ({side})",
            };
        }
    }
}
