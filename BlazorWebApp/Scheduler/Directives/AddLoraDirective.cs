using BlazorWebApp.Models;

namespace BlazorWebApp.Scheduler.Directives
{
    /// <summary>
    /// Adds a LoRA to the parameters' LoRA list.
    /// The LoRA is cloned on apply to avoid sharing state across iterations.
    /// </summary>
    public sealed class AddLoraDirective : Directive
    {
        /// <summary>
        /// The LoRA configuration to add.
        /// </summary>
        public Lora Lora { get; set; } = default!;
    }
}
