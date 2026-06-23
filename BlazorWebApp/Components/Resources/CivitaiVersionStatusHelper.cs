using BlazorWebApp.Data.Dtos;
using BlazorWebApp.Services;
using MudBlazor;

namespace BlazorWebApp.Components.Resources;

/// <summary>
/// Resolves the per-version saved/downloaded status used by the model dialog UI.
/// Mirrors the legacy <c>CivitaiModelVersionInfoPanel.GetTabIconColor</c> rules so
/// the new selector / strip / hero share a single source of truth.
/// </summary>
public static class CivitaiVersionStatusHelper
{
    public enum VersionStatus
    {
        Missing,
        FileMissingImagesPartial,
        Downloaded,
        DownloadedImagesPartial,
        DownloadedImagesAllSaved,
        Unknown,
    }

    public static async Task<VersionStatus> ResolveAsync(IDatabaseService db, CivitaiModelVersionDto version)
    {
        if (db == null || version == null) return VersionStatus.Unknown;

        var fileExists = await db.CheckResourceExistsByModelVersionId(version.Id);
        var imageCount = await db.ResourceImageByModelVersionIdCount(version.Id);
        var totalImages = version.Images?.Count ?? 0;

        if (!fileExists) return VersionStatus.Missing;
        if (totalImages == 0) return VersionStatus.Downloaded;
        if (imageCount == 0) return VersionStatus.FileMissingImagesPartial;
        if (imageCount >= totalImages) return VersionStatus.DownloadedImagesAllSaved;
        return VersionStatus.DownloadedImagesPartial;
    }

    public static Color ToColor(VersionStatus status) => status switch
    {
        VersionStatus.DownloadedImagesAllSaved => Color.Success,
        VersionStatus.Downloaded => Color.Warning,
        VersionStatus.DownloadedImagesPartial => Color.Info,
        VersionStatus.FileMissingImagesPartial => Color.Error,
        VersionStatus.Missing => Color.Default,
        _ => Color.Default,
    };

    public static string ToTooltip(VersionStatus status) => status switch
    {
        VersionStatus.DownloadedImagesAllSaved => "File downloaded - all preview images saved",
        VersionStatus.Downloaded => "File downloaded",
        VersionStatus.DownloadedImagesPartial => "File downloaded - some preview images saved",
        VersionStatus.FileMissingImagesPartial => "File downloaded - no preview images saved",
        VersionStatus.Missing => "Not downloaded",
        _ => string.Empty,
    };
}
