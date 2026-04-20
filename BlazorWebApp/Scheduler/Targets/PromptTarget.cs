namespace BlazorWebApp.Scheduler.Targets
{
    /// <summary>
    /// Targets the positive or negative prompt text stored on the prompts fragment.
    /// </summary>
    public sealed class PromptTarget : ParameterTarget
    {
        /// <summary>
        /// When <c>true</c> the target refers to the negative prompt; otherwise the positive prompt.
        /// </summary>
        public bool IsNegative { get; set; }

        /// <inheritdoc />
        public override string GetDisplayName() => IsNegative ? "Prompt (negative)" : "Prompt (positive)";
    }
}
