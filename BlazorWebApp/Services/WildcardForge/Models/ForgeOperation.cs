namespace BlazorWebApp.Services.WildcardForge.Models
{
    /// <summary>
    /// Strongly-typed enumeration of the five Wildcard Forge operations.
    /// Each maps to either a template from <c>LLM_PROMPTS.json</c> or a custom code path.
    /// </summary>
    public enum ForgeOperation
    {
        Generate,
        Expand,
        Refine,
        Convert,
        Describe
    }
}
