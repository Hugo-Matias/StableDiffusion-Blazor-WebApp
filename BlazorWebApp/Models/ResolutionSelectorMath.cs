namespace BlazorWebApp.Models;

/// <summary>
/// Pure calculation logic mirroring ComfyUI's built-in "Resolution Selector" node
/// (https://docs.comfy.org/built-in-nodes/ResolutionSelector). Given an aspect ratio,
/// a target megapixel count, and a rounding multiple, computes the width/height in
/// pixels. Used by <c>ResolutionPanel</c> to offer an aspect-ratio + megapixels mode
/// as an alternative to manual width/height entry, without requiring a dedicated
/// ComfyUI node in the graph (the app performs the calculation and writes the
/// resulting width/height into the existing fragment parameters).
/// </summary>
public static class ResolutionSelectorMath
{
    /// <summary>
    /// Aspect ratio options matching the ComfyUI ResolutionSelector node's COMBO input.
    /// </summary>
    public static readonly string[] AspectRatios =
    [
        "1:1 (Square)",
        "2:3 (Portrait Photo)",
        "3:2 (Photo)",
        "3:4 (Portrait Standard)",
        "4:3 (Standard)",
        "9:16 (Portrait Widescreen)",
        "16:9 (Widescreen)",
        "21:9 (Ultrawide)"
    ];

    /// <summary>
    /// 1.0 megapixel is treated as 1024x1024 pixels (1,048,576px), matching the node's
    /// documented behavior ("1.0 MP ≈ 1024x1024 for square") rather than the photographic
    /// 1,000,000px definition.
    /// </summary>
    private const double MegapixelUnit = 1024d * 1024d;

    /// <summary>
    /// Calculates width/height for the given aspect ratio, target megapixels, and rounding multiple.
    /// </summary>
    /// <param name="aspectRatio">One of <see cref="AspectRatios"/>. Unknown values fall back to 1:1.</param>
    /// <param name="megapixels">Target total resolution in megapixels (clamped to a sane minimum).</param>
    /// <param name="multiple">Nearest multiple to round the result to (clamped to a minimum of 1).</param>
    public static (int Width, int Height) Calculate(string aspectRatio, double megapixels, int multiple)
    {
        var (ratioWidth, ratioHeight) = ParseRatio(aspectRatio);
        var safeMultiple = Math.Max(1, multiple);
        var area = Math.Max(0.01, megapixels) * MegapixelUnit;
        var ratio = ratioWidth / ratioHeight;

        var rawWidth = Math.Sqrt(area * ratio);
        var rawHeight = Math.Sqrt(area / ratio);

        var width = Math.Max(safeMultiple, RoundToMultiple(rawWidth, safeMultiple));
        var height = Math.Max(safeMultiple, RoundToMultiple(rawHeight, safeMultiple));

        return (width, height);
    }

    private static int RoundToMultiple(double value, int multiple)
    {
        return (int)(Math.Round(value / multiple) * multiple);
    }

    private static (double Width, double Height) ParseRatio(string aspectRatio)
    {
        var token = aspectRatio.Split(' ', 2)[0];
        var parts = token.Split(':');

        if (parts.Length == 2 &&
            double.TryParse(parts[0], out var width) &&
            double.TryParse(parts[1], out var height) &&
            width > 0 && height > 0)
        {
            return (width, height);
        }

        return (1, 1);
    }
}
