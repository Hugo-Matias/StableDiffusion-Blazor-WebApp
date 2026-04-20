namespace BlazorWebApp.Scheduler.Directives
{
    /// <summary>
    /// Enables or disables an existing LoRA on the parameters' LoRA list without removing it.
    /// </summary>
    public sealed class ToggleLoraDirective : Directive
    {
        /// <summary>
        /// Name of the LoRA to toggle.
        /// </summary>
        public string LoraName { get; set; } = string.Empty;

        /// <summary>
        /// Target enabled state. <c>true</c> enables, <c>false</c> disables.
        /// </summary>
        public bool Enable { get; set; } = true;
    }
}
