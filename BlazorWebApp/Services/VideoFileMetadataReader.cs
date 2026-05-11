using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using MetadataExtractor;

namespace BlazorWebApp.Services;

public sealed record VideoFileMetadata(
    int Width,
    int Height,
    int FrameCount,
    double FrameRate,
    double DurationSeconds);

public static class VideoFileMetadataReader
{
    public static VideoFileMetadata Read(string filePath, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return Empty;

        var ffprobeMetadata = ReadWithFfprobe(filePath, logger);
        if (ffprobeMetadata != Empty)
            return ffprobeMetadata;

        return ReadWithMetadataExtractor(filePath, logger);
    }

    private static VideoFileMetadata Empty { get; } = new(0, 0, 0, 0, 0);

    private static VideoFileMetadata ReadWithFfprobe(string filePath, ILogger? logger)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffprobe",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-v");
            startInfo.ArgumentList.Add("error");
            startInfo.ArgumentList.Add("-select_streams");
            startInfo.ArgumentList.Add("v:0");
            startInfo.ArgumentList.Add("-count_frames");
            startInfo.ArgumentList.Add("-show_entries");
            startInfo.ArgumentList.Add("stream=width,height,avg_frame_rate,r_frame_rate,nb_read_frames,nb_frames,duration:format=duration");
            startInfo.ArgumentList.Add("-of");
            startInfo.ArgumentList.Add("json");
            startInfo.ArgumentList.Add(filePath);

            using var process = new Process { StartInfo = startInfo };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            if (!process.WaitForExit(15_000))
            {
                process.Kill(entireProcessTree: true);
                logger?.LogWarning("ffprobe timed out while reading video metadata for {FilePath}", filePath);
                return Empty;
            }

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                logger?.LogDebug("ffprobe could not read video metadata for {FilePath}: {Error}", filePath, error);
                return Empty;
            }

            return ParseFfprobeJson(output);
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "ffprobe is unavailable or failed while reading video metadata for {FilePath}", filePath);
            return Empty;
        }
    }

    internal static VideoFileMetadata ParseFfprobeJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("streams", out var streams) || streams.ValueKind != JsonValueKind.Array)
            return Empty;

        var stream = streams.EnumerateArray().FirstOrDefault();
        if (stream.ValueKind != JsonValueKind.Object)
            return Empty;

        var width = GetInt(stream, "width");
        var height = GetInt(stream, "height");
        var frameRate = ParseRate(GetString(stream, "avg_frame_rate"))
            ?? ParseRate(GetString(stream, "r_frame_rate"))
            ?? 0;
        var duration = GetDouble(stream, "duration");

        if (duration <= 0 && root.TryGetProperty("format", out var format))
            duration = GetDouble(format, "duration");

        var frameCount = GetInt(stream, "nb_read_frames");
        if (frameCount <= 0)
            frameCount = GetInt(stream, "nb_frames");
        if (frameCount <= 0 && frameRate > 0 && duration > 0)
            frameCount = (int)Math.Round(frameRate * duration);

        return new VideoFileMetadata(width, height, frameCount, frameRate, duration);
    }

    private static VideoFileMetadata ReadWithMetadataExtractor(string filePath, ILogger? logger)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);
            var width = 0;
            var height = 0;
            var duration = 0.0;

            foreach (var directory in directories)
            {
                if (directory.Name == "QuickTime Movie Header")
                {
                    var durationTag = directory.Tags.FirstOrDefault(tag => tag.Name == "Duration");
                    duration = ParseDuration(durationTag?.Description) ?? duration;
                }

                if (directory.Name != "QuickTime Track Header")
                    continue;

                var trackWidth = ParseNumber(directory.Tags.FirstOrDefault(tag => tag.Name == "Width")?.Description);
                var trackHeight = ParseNumber(directory.Tags.FirstOrDefault(tag => tag.Name == "Height")?.Description);

                if (width <= 0 && height <= 0 && trackWidth > 0 && trackHeight > 0)
                {
                    width = (int)Math.Round(trackWidth.Value);
                    height = (int)Math.Round(trackHeight.Value);
                }
            }

            return new VideoFileMetadata(width, height, 0, 0, duration);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to read video metadata from {FilePath}", filePath);
            return Empty;
        }
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    private static double GetDouble(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            _ => null
        };
    }

    private static double? ParseRate(string? rate)
    {
        if (string.IsNullOrWhiteSpace(rate) || rate == "0/0")
            return null;

        var parts = rate.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator) &&
            denominator != 0)
        {
            return numerator / denominator;
        }

        return double.TryParse(rate, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static double? ParseDuration(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var timeSpan))
            return timeSpan.TotalSeconds;

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ? seconds : null;
    }

    private static double? ParseNumber(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : null;
    }
}