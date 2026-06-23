namespace BlazorWebApp.Scheduler.Targets
{
    /// <summary>
    /// Targets a LoRA entry in <see cref="BlazorWebApp.Models.GenerationParameters.Loras"/> identified by name.
    /// </summary>
    public sealed class LoraTarget : ParameterTarget
    {
        /// <summary>
        /// The LoRA's name as stored on <see cref="BlazorWebApp.Models.Lora.Name"/>.
        /// </summary>
        public string LoraName { get; set; } = string.Empty;

        /// <inheritdoc />
        public override string GetDisplayName() => $"LoRA:{LoraName}";
    }
}
