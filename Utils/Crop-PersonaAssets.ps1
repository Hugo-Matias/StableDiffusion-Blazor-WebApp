<#
.SYNOPSIS
    Masks persona PNG assets so character art is transparent outside the card width,
    but only from the card-top boundary downward. Everything above bleeds freely.

.DESCRIPTION
    Image dimensions are preserved exactly (no resize, no canvas change).
    A side-transparency mask is applied only to the portion of the image that falls
    within the card frame area. Above the card top the full image width is kept, so
    arms/props/hair that extend sideways above the card still show.

    Mask boundary math (all CSS px, then scaled to image pixels):
        scale        = imgW / ImageDisplayWidth
        maskStartPx  = max(0, round(ImageBottomOffset*scale + imgH - CardHeight*scale))

    Below maskStartPx rows, pixels outside the centered card-width band are made
    fully transparent. Above maskStartPx rows nothing is touched.

    Requires ImageMagick (magick) on PATH.

.PARAMETER InputFolder
    Folder containing the persona PNG files.
    Defaults to BlazorWebApp/wwwroot/odditarium/personas relative to the script.

.PARAMETER OutputFolder
    Destination for processed images. Defaults to InputFolder (overwrites in place).
    Created automatically if it does not exist.

.PARAMETER CardWidth
    Rendered card width in CSS px. Default: 200.

.PARAMETER ImageDisplayWidth
    Width the image is rendered at in CSS px. Default: 240.

.PARAMETER CardHeight
    Rendered card height in CSS px. Default: 320.

.PARAMETER ImageBottomOffset
    CSS `bottom` value applied to the image inside the card (px). Default: 60.

.PARAMETER Inset
    Extra inset (in CSS px) shrunk from each side of the visible band before masking.
    Use this to tighten the clip when character art still bleeds past the card edge.
    Default: 0.

.PARAMETER Pattern
    Glob filter for files to process. Default: *.png

.EXAMPLE
    .\Crop-PersonaAssets.ps1
    # Masks all PNGs in the default folder in place.

.EXAMPLE
    .\Crop-PersonaAssets.ps1 -WhatIf
    # Preview what would be processed without writing files.

.EXAMPLE
    .\Crop-PersonaAssets.ps1 -OutputFolder ".\masked"
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$InputFolder,
    [string]$OutputFolder,
    [int]$CardWidth = 200,
    [int]$ImageDisplayWidth = 240,
    [int]$CardHeight = 320,
    [int]$ImageBottomOffset = 60,
    # Extra inset (in CSS px) shrunk from EACH side of the visible band before masking.
    # Use this to tighten the clip when character art still bleeds past the card edge.
    [int]$Inset = 0,
    [string]$Pattern = "*.png"
)

$ErrorActionPreference = "Stop"

# --- Resolve folders ---
$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Get-Location }

if (-not $InputFolder) {
    $InputFolder = Join-Path $scriptDir "..\BlazorWebApp\wwwroot\odditarium\personas"
}
$InputFolder = (Resolve-Path $InputFolder).Path

if (-not $OutputFolder) {
    $OutputFolder = $InputFolder
}
elseif (-not (Test-Path $OutputFolder)) {
    New-Item -ItemType Directory -Path $OutputFolder | Out-Null
    Write-Host "Created output folder: $OutputFolder"
}
$OutputFolder = (Resolve-Path $OutputFolder).Path

# --- Validate ImageMagick ---
if (-not (Get-Command magick -ErrorAction SilentlyContinue)) {
    Write-Error "ImageMagick 'magick' not found on PATH. Install from https://imagemagick.org"
    exit 1
}

$effectiveBandCss = [math]::Max(1, $CardWidth - 2 * $Inset)
$visibleRatio = $effectiveBandCss / $ImageDisplayWidth
Write-Host "Card ${CardWidth}x${CardHeight} px  |  image display width ${ImageDisplayWidth} px  |  inset ${Inset} px  |  effective band ${effectiveBandCss} px  |  visible ratio $([math]::Round($visibleRatio, 4))"
Write-Host "Image bottom offset: ${ImageBottomOffset} px"
Write-Host "Input:  $InputFolder"
Write-Host "Output: $OutputFolder"
Write-Host ""

# --- Process files ---
$files = Get-ChildItem -Path $InputFolder -Filter $Pattern
if ($files.Count -eq 0) {
    Write-Warning "No files matching '$Pattern' found in $InputFolder"
    exit 0
}

$processed = 0
$skipped = 0

foreach ($file in $files) {
    # Active-state images are intentionally left unclipped so they bleed freely when selected.
    if ($file.BaseName -like '*-active') {
        Write-Host "  $($file.Name) - SKIP (active state, bleed preserved)"
        $skipped++
        continue
    }

    $identify = magick identify -format "%w %h" $file.FullName 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "  SKIP $($file.Name) - could not read dimensions: $identify"
        $skipped++
        continue
    }

    $parts = $identify -split ' '
    $imgW = [int]$parts[0]
    $imgH = [int]$parts[1]

    # Pixel scale: how many image pixels per CSS px
    $scale = $imgW / $ImageDisplayWidth

    # Width of the visible center band in image pixels
    $bandW = [int][math]::Round($imgW * $visibleRatio)
    $sideMargin = [int][math]::Round(($imgW - $bandW) / 2)

    # Row (from image top) where side-masking begins.
    # Above this row the full image width is untouched (palette, arms, etc. bleed freely).
    $maskStartPx = [int][math]::Max(0, [math]::Round($ImageBottomOffset * $scale + $imgH - $CardHeight * $scale))
    $maskZoneH = $imgH - $maskStartPx

    $outPath = Join-Path $OutputFolder $file.Name

    Write-Host "  $($file.Name) (${imgW}x${imgH})"
    Write-Host "    visible band: ${bandW}px, side margins: ${sideMargin}px each"
    Write-Host "    mask starts at row $maskStartPx (${maskZoneH}px tall zone)"

    if ($sideMargin -le 0) {
        Write-Host "    SKIP - image already within card width, no masking needed"
        $skipped++
        continue
    }

    if ($PSCmdlet.ShouldProcess($file.FullName, "Apply side mask from row $maskStartPx")) {
        # Two-crop composite onto a transparent canvas of the original size:
        #   1) Top region (full width, rows 0..maskStartPx-1) is kept intact - sideways bleed
        #      above the card top is preserved.
        #   2) Band region (rows maskStartPx..imgH-1, x=sideMargin..sideMargin+bandW-1) is
        #      kept; pixels outside the band become transparent because they are never
        #      composited back onto the empty canvas.
        # Earlier approaches with -region/-alpha transparent or -compose CopyOpacity were
        # unreliable across ImageMagick releases (right region silently ignored or alpha
        # overwritten). Crop + composite is dimension-preserving and behaves consistently.
        $canvasSize = "${imgW}x${imgH}"
        $topCrop = "${imgW}x${maskStartPx}+0+0"
        $bandCrop = "${bandW}x${maskZoneH}+${sideMargin}+${maskStartPx}"
        $bandGeom = "+${sideMargin}+${maskStartPx}"

        $magickArgs = @(
            '-size', $canvasSize, 'xc:none',
            '(', $file.FullName, '-crop', $topCrop, '+repage', ')', '-geometry', '+0+0', '-composite',
            '(', $file.FullName, '-crop', $bandCrop, '+repage', ')', '-geometry', $bandGeom, '-composite',
            $outPath
        )
        & magick @magickArgs

        if ($LASTEXITCODE -ne 0) {
            Write-Warning "    FAILED"
            $skipped++
        }
        else {
            $processed++
            Write-Host "    OK -> $outPath"
        }
    }
}

Write-Host ""
Write-Host "Done. Processed: $processed  Skipped/Failed: $skipped"
