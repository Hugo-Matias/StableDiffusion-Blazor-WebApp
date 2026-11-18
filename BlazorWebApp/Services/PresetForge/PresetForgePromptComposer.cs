using System.Text;
using BlazorWebApp.Data.Dtos.Ollama;
using BlazorWebApp.Services.PresetForge.Models;

namespace BlazorWebApp.Services.PresetForge;

public sealed class PresetForgePromptComposer
{
    private const int MaxReferenceCount = 8;
    private const int MaxFieldLength = 1200;

    public List<OllamaChatMessage> Compose(PresetForgeRequest request)
    {
        var system = new StringBuilder();
        system.AppendLine("You author prompt presets for AI image generation. Return only a valid JSON array, without Markdown or commentary.");
        system.AppendLine("Every array item must use this exact schema: {\"name\":string,\"prompt\":string,\"negative_prompt\":string|null,\"category\":string,\"tags\":string[]}.");
        system.AppendLine("Names must be concise and distinct. Tags must be lowercase kebab-case. Do not duplicate reference presets.");
        system.AppendLine(KindInstructions(request));

        if (!request.IncludeNegativePrompts)
            system.AppendLine("Set negative_prompt to null for every preset.");

        var user = new StringBuilder();
        user.AppendLine($"Operation: {request.Operation}");
        user.AppendLine($"Create exactly {Math.Clamp(request.Count, 1, 24)} presets.");
        AppendField(user, "Pack theme", request.Theme);
        AppendField(user, "Category", request.Category);
        AppendField(user, "Facet", request.Facet);
        AppendField(user, "Naming prefix", request.NamingPrefix);
        AppendField(user, "Required concepts", request.RequiredConcepts);
        AppendField(user, "Excluded concepts", request.ExcludedConcepts);
        AppendField(user, "Keep unchanged", request.KeepUnchanged);
        AppendField(user, "Expansion direction", request.ExpansionDirection);

        user.AppendLine(request.BroadCoverage
            ? "Coverage: broad; cover obvious, uncommon, and surprising options without near-duplicates."
            : "Coverage: focused; keep every preset tightly aligned with the stated theme.");

        if (request.AdditionalTags.Count > 0)
            user.AppendLine($"Required tags: {string.Join(", ", request.AdditionalTags.Select(Sanitize))}");

        AppendExamples(user, "User examples", request.SeedExamples);
        AppendReferences(user, request);
        user.AppendLine("Return the complete JSON array now.");

        return
        [
            new OllamaChatMessage { Role = "system", Content = system.ToString().Trim() },
            new OllamaChatMessage { Role = "user", Content = user.ToString().Trim() }
        ];
    }

    private static string KindInstructions(PresetForgeRequest request) => request.Kind switch
    {
        PresetForgeKind.FullPrompt =>
            "Each preset is a complete T2I prompt. Describe the final visible image, including the relevant subject, environment, action, composition, lighting, and aesthetic. Do not refer to a source or reference image.",
        PresetForgeKind.BuildingBlock =>
            "Each preset is a composable T2I building block. Describe only the selected facet and do not invent a full subject, scene, wardrobe, action, or lighting setup outside that facet. Do not refer to a source or reference image.",
        _ => $"Each preset is an I2I targeted-edit instruction. Begin with this preservation contract exactly, then state one concrete edit and finish with natural lighting and focus requirements: {PreservationHeader(request.PreservationPolicy)}"
    };

    internal static string PreservationHeader(PresetPreservationPolicy policy) => policy switch
    {
        PresetPreservationPolicy.Full =>
            "Keep pose, angle, facial features, eyes, viewing direction, body proportions, body shape and subject position identical to the reference image. Preserve all anatomical details exactly as they appear in the original.",
        PresetPreservationPolicy.Body =>
            "Keep pose, angle, facial features, body shape, proportions and subject position identical to the reference image. Preserve all anatomical details exactly as they appear, including skin tone, muscle definition, and body contours beneath or around the modification.",
        PresetPreservationPolicy.PartialBody =>
            "Keep pose, angle, facial features and subject position identical to the reference image. Preserve overall identity and body frame while modifying only the specified target region. Maintain consistent skin tone and texture across modified and unmodified areas.",
        PresetPreservationPolicy.IdentityAndComposition =>
            "Keep facial features, identity, body proportions and visual style identical to the reference image. Preserve skin tone, anatomical details, and appearance characteristics while modifying pose and composition as instructed.",
        _ =>
            "Preserve every part of the reference image that is not explicitly named as a target of the edit."
    };

    private static void AppendReferences(StringBuilder builder, PresetForgeRequest request)
    {
        if (request.ReferencePresets.Count == 0)
            return;

        builder.AppendLine("Reference presets. Match their scope and quality, but do not copy them:");
        foreach (var preset in request.ReferencePresets.Take(MaxReferenceCount))
        {
            builder.AppendLine($"<preset name=\"{Sanitize(preset.Title)}\" category=\"{Sanitize(preset.Category)}\">{Sanitize(preset.Positive)}</preset>");
        }
    }

    private static void AppendExamples(StringBuilder builder, string label, IEnumerable<string> examples)
    {
        var values = examples.Where(value => !string.IsNullOrWhiteSpace(value)).Take(5).ToList();
        if (values.Count == 0)
            return;

        builder.AppendLine($"{label}:");
        foreach (var value in values)
            builder.AppendLine($"- {Sanitize(value)}");
    }

    private static void AppendField(StringBuilder builder, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            builder.AppendLine($"{label}: {Sanitize(value)}");
    }

    private static string Sanitize(string? value)
    {
        var sanitized = (value ?? string.Empty)
            .Replace("<preset", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("</preset>", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        return sanitized.Length <= MaxFieldLength ? sanitized : sanitized[..MaxFieldLength];
    }
}