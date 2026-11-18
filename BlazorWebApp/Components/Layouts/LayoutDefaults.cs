namespace BlazorWebApp.Components.Layouts;

/// <summary>
/// Shared layout constants used across tabbed-page layout components.
/// Values that cannot be expressed as CSS custom properties (for example
/// MudBlazor <c>Elevation</c> parameters, which are <see cref="int"/>)
/// live here so they can be tweaked in one place.
/// See <c>Documentation/Architecture/04-UI-DESIGN-LANGUAGE.md</c> (Layout section).
/// </summary>
public static class LayoutDefaults
{
    /// <summary>Elevation applied to the top-level <c>MudTabs</c> on tabbed pages.</summary>
    public const int TabsElevation = 4;

    /// <summary>
    /// Recommended elevation for card surfaces that child components render
    /// inside a layout slot (e.g. a browser panel wrapped in its own
    /// <c>MudPaper</c>). The layout components themselves are transparent;
    /// children decide whether to show a surface.
    /// </summary>
    public const int SurfaceElevation = 1;
}
