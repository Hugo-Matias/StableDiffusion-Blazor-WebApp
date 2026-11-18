using System.Text.Json.Serialization;

namespace BlazorWebApp.Scheduler.Directives
{
    /// <summary>
    /// A stackable unit operation applied to a cloned <see cref="BlazorWebApp.Models.GenerationParameters"/>
    /// at the start of each iteration within a <see cref="BlazorWebApp.Scheduler.Models.JobAction"/>.
    /// Directives run in declared order before variation values are applied.
    /// Concrete types are pure configuration holders in Phase 1; apply logic lives in Phase 4.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(SetValueDirective), "set")]
    [JsonDerivedType(typeof(AppendPromptDirective), "append_prompt")]
    [JsonDerivedType(typeof(ReplacePromptDirective), "replace_prompt")]
    [JsonDerivedType(typeof(AddLoraDirective), "add_lora")]
    [JsonDerivedType(typeof(RemoveLoraDirective), "remove_lora")]
    [JsonDerivedType(typeof(ToggleLoraDirective), "toggle_lora")]
    [JsonDerivedType(typeof(AddPromptStyleDirective), "add_style")]
    [JsonDerivedType(typeof(SwapAssetDirective), "swap_asset")]
    [JsonDerivedType(typeof(SetOutputDirective), "set_output")]
    public abstract class Directive
    {
        /// <summary>
        /// Whether this directive participates in the iteration. Disabled directives are skipped.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Optional user-facing label for the Scheduler UI.
        /// </summary>
        public string? Label { get; set; }
    }
}
