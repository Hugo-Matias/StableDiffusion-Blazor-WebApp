namespace BlazorWebApp.Services;

public interface IInfoService
{
    InfoContent? CurrentInfo { get; }
    string? HighlightedSectionId { get; }
    event Action? OnInfoChanged;
    void SetInfo(InfoContent content);
    void ClearInfo();

    /// <summary>
    /// Requests the drawer to auto-expand and scroll to the section whose <see cref="InfoSection.Id"/>
    /// matches <paramref name="sectionId"/>. Pass <c>null</c> to clear the current highlight.
    /// </summary>
    void HighlightSection(string? sectionId);
}

public record InfoContent(
    string Title,
    string Overview,
    List<ShortcutInfo> Shortcuts,
    List<string> Tips,
    List<InfoSection>? Sections = null
);

public record ShortcutInfo(string Keys, string Description);

/// <summary>
/// Structured content block rendered below Overview / Shortcuts / Tips in <c>InfoDrawer</c>.
/// Plain-text items only (no markdown). Multi-line <see cref="InfoItem.Text"/> is preserved via
/// <c>white-space: pre-wrap</c> in the renderer.
/// </summary>
public record InfoSection(
    string Id,
    string Title,
    string? Icon,
    List<InfoItem> Items
);

/// <summary>
/// Single entry inside an <see cref="InfoSection"/>. <see cref="Label"/> renders bold when present;
/// <see cref="Text"/> is always rendered. When <see cref="IsCode"/> is true the text renders in a
/// monospace code block (whitespace preserved) - use this for JSON snippets, shell commands, etc.
/// </summary>
public record InfoItem(string? Label, string Text, bool IsCode = false);

