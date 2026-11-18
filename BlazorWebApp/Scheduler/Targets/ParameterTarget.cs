using System.Text.Json.Serialization;

namespace BlazorWebApp.Scheduler.Targets
{
    /// <summary>
    /// Abstract base addressing a parameter location within a <see cref="BlazorWebApp.Models.GenerationParameters"/> instance.
    /// Unifies fragment values, LoRAs, assets, prompt text, and output routing under a single targeting model.
    /// Concrete types are combined with Directives and Variations to drive scheduled generation runs.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(FragmentTarget), "fragment")]
    [JsonDerivedType(typeof(LoraTarget), "lora")]
    [JsonDerivedType(typeof(AssetTarget), "asset")]
    [JsonDerivedType(typeof(PromptTarget), "prompt")]
    [JsonDerivedType(typeof(OutputTarget), "output")]
    public abstract class ParameterTarget
    {
        /// <summary>
        /// Returns a short human-readable label describing what this target points to.
        /// Used in the Scheduler UI to present directive/variation summaries.
        /// Implemented as a method (not a property) so it is not serialized by <see cref="System.Text.Json.JsonSerializer"/>.
        /// </summary>
        public abstract string GetDisplayName();
    }
}
