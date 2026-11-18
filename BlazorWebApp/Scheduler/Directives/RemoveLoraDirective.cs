namespace BlazorWebApp.Scheduler.Directives
{
    /// <summary>
    /// Removes a LoRA from the parameters' LoRA list by name.
    /// </summary>
    public sealed class RemoveLoraDirective : Directive
    {
        /// <summary>
        /// Name of the LoRA to remove (matches <see cref="BlazorWebApp.Models.Lora.Name"/>).
        /// </summary>
        public string LoraName { get; set; } = string.Empty;
    }
}
