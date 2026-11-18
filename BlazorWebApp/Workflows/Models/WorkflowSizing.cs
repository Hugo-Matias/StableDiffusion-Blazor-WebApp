namespace BlazorWebApp.Workflows.Models;

public readonly record struct WorkflowImageSize(int Width, int Height);

public static class WorkflowSizing
{
    private const int PixelsPerMegapixel = 1024 * 1024;

    public static WorkflowImageSize ScaleToTotalPixels(
        int? sourceWidth,
        int? sourceHeight,
        double megapixels,
        int resolutionSteps = 1,
        WorkflowImageSize? fallback = null)
    {
        if (sourceWidth is not > 0 || sourceHeight is not > 0)
        {
            return fallback ?? new WorkflowImageSize(1024, 1024);
        }

        var targetPixels = Math.Max(0.01, megapixels) * PixelsPerMegapixel;
        var sourcePixels = (double)sourceWidth.Value * sourceHeight.Value;
        var scale = Math.Sqrt(targetPixels / sourcePixels);

        var width = SnapToResolutionStep(
            (int)Math.Round(sourceWidth.Value * scale, MidpointRounding.AwayFromZero),
            resolutionSteps);
        var height = SnapToResolutionStep(
            (int)Math.Round(sourceHeight.Value * scale, MidpointRounding.AwayFromZero),
            resolutionSteps);

        return new WorkflowImageSize(width, height);
    }

    public static int CalculateUltimateSdTileSize(int baseSize, double upscaleBy)
        => AlignToStep((int)(upscaleBy * baseSize / 2 + 32), 8, 64, 8192);

    public static int AlignToStep(int value, int step, int min, int max)
    {
        var clamped = Math.Clamp(value, min, max);
        return Math.Max(min, (clamped / step) * step);
    }

    private static int SnapToResolutionStep(int value, int resolutionSteps)
    {
        var step = Math.Max(1, resolutionSteps);
        if (step == 1)
        {
            return Math.Max(1, value);
        }

        var snapped = (int)Math.Round(value / (double)step, MidpointRounding.AwayFromZero) * step;
        return Math.Max(step, snapped);
    }
}