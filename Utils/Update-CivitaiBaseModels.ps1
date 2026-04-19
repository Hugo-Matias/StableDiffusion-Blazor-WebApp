<#
.SYNOPSIS
    Fetches CivitAI base model constants from GitHub and generates a structured basemodels.json file.

.DESCRIPTION
    Downloads the base-model.constants.ts file from the CivitAI GitHub repository,
    parses the three main data structures (baseModelFamilyConfig, baseModelConfig,
    baseModelGroupConfig), then outputs a structured JSON file.

    The TypeScript source file is NOT kept in the repository - only the parsed JSON output.

.EXAMPLE
    .\Update-CivitaiBaseModels.ps1
    .\Update-CivitaiBaseModels.ps1 -OutputPath ".\custom-output.json"
#>

[CmdletBinding()]
param(
    [string]$OutputPath,
    [string]$SourceUrl = "https://raw.githubusercontent.com/civitai/civitai/refs/heads/main/src/shared/constants/base-model.constants.ts"
)

$ErrorActionPreference = "Stop"

# Resolve output path - default to BlazorWebApp/Data/CivitAI/basemodels.json relative to script location
if (-not $OutputPath) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Get-Location }
    $OutputPath = Join-Path $scriptDir "..\BlazorWebApp\Data\CivitAI\basemodels.json"
}

Write-Host "Fetching base-model.constants.ts from CivitAI GitHub..." -ForegroundColor Cyan
$tsContent = Invoke-WebRequest -Uri $SourceUrl -UseBasicParsing | Select-Object -ExpandProperty Content
Write-Host "Downloaded $($tsContent.Length) characters." -ForegroundColor Green

# =============================================================================
# Parse baseModelFamilyConfig (families)
# =============================================================================
Write-Host "Parsing baseModelFamilyConfig..." -ForegroundColor Cyan

$families = [ordered]@{}
$familyBlock = [regex]::Match($tsContent, 'export const baseModelFamilyConfig[^=]*=\s*\{([\s\S]*?)\};\s*\n')
if (-not $familyBlock.Success) { throw "Failed to find baseModelFamilyConfig block" }

# Match each family entry: key: { name: '...', description: '...' }
$familyEntries = [regex]::Matches($familyBlock.Groups[1].Value, "(\w+)\s*:\s*\{([\s\S]*?)\}")
foreach ($entry in $familyEntries) {
    $key = $entry.Groups[1].Value
    $body = $entry.Groups[2].Value

    $name = [regex]::Match($body, "name\s*:\s*'([^']*)'").Groups[1].Value
    $desc = [regex]::Match($body, "description\s*:\s*'([^']*)'").Groups[1].Value
    # Handle escaped apostrophes in descriptions
    if (-not $desc) {
        $descMatch = [regex]::Match($body, 'description\s*:\s*"([^"]*)"')
        if ($descMatch.Success) { $desc = $descMatch.Groups[1].Value }
    }

    $family = [ordered]@{
        key         = $key
        name        = $name
        description = $desc
    }

    $disabledMatch = [regex]::Match($body, 'disabled\s*:\s*true')
    if ($disabledMatch.Success) { $family["disabled"] = $true }

    $families[$key] = [PSCustomObject]$family
}
Write-Host "  Found $($families.Count) families." -ForegroundColor Gray

# =============================================================================
# Parse baseModelGroupConfig (groups with family references)
# =============================================================================
Write-Host "Parsing baseModelGroupConfig..." -ForegroundColor Cyan

$groups = [ordered]@{}
$groupBlock = [regex]::Match($tsContent, 'export const baseModelGroupConfig[^=]*=\s*\{([\s\S]*?)\};\s*\n')
if (-not $groupBlock.Success) { throw "Failed to find baseModelGroupConfig block" }

# Match entries - handle both simple keys and quoted keys like 'WanVideo-22-TI2V-5B'
$groupEntries = [regex]::Matches($groupBlock.Groups[1].Value, "(?:'([^']+)'|(\w+))\s*:\s*\{([\s\S]*?)\}")
foreach ($entry in $groupEntries) {
    $key = if ($entry.Groups[1].Value) { $entry.Groups[1].Value } else { $entry.Groups[2].Value }
    $body = $entry.Groups[3].Value

    $name = [regex]::Match($body, "name\s*:\s*'([^']*)'").Groups[1].Value
    $descMatch = [regex]::Match($body, "description\s*:\s*'([^']*)'")
    $desc = if ($descMatch.Success) { $descMatch.Groups[1].Value } else { $null }
    # Handle multi-line or complex descriptions
    if (-not $desc) {
        $descAltMatch = [regex]::Match($body, "description\s*:\s*\n?\s*'([^']*)'")
        if ($descAltMatch.Success) { $desc = $descAltMatch.Groups[1].Value }
    }

    $familyMatch = [regex]::Match($body, "family\s*:\s*'(\w+)'")
    $family = if ($familyMatch.Success) { $familyMatch.Groups[1].Value } else { $null }

    $selectorMatch = [regex]::Match($body, "selector\s*:\s*'([^']*)'")
    $selector = if ($selectorMatch.Success) { $selectorMatch.Groups[1].Value } else { $null }

    $group = [ordered]@{
        key         = $key
        name        = $name
    }
    if ($desc) { $group["description"] = $desc }
    if ($family) { $group["family"] = $family }
    if ($selector) { $group["selector"] = $selector }

    $groups[$key] = [PSCustomObject]$group
}
Write-Host "  Found $($groups.Count) groups." -ForegroundColor Gray

# =============================================================================
# Parse baseModelConfig (base models)
# =============================================================================
Write-Host "Parsing baseModelConfig..." -ForegroundColor Cyan

$baseModels = @()
$bmBlock = [regex]::Match($tsContent, 'const baseModelConfig\s*=\s*\[([\s\S]*?)\]\s*as\s+const')
if (-not $bmBlock.Success) { throw "Failed to find baseModelConfig block" }

$bmObjects = [regex]::Matches($bmBlock.Groups[1].Value, '\{([\s\S]*?)\}')
foreach ($obj in $bmObjects) {
    $body = $obj.Groups[1].Value

    $name = [regex]::Match($body, "name\s*:\s*'([^']*)'").Groups[1].Value
    $type = [regex]::Match($body, "type\s*:\s*'([^']*)'").Groups[1].Value
    $groupKey = [regex]::Match($body, "group\s*:\s*'([^']*)'").Groups[1].Value

    $hidden = $body -match 'hidden\s*:\s*true'

    $ecosystemMatch = [regex]::Match($body, "ecosystem\s*:\s*'([^']*)'")
    $ecosystem = if ($ecosystemMatch.Success) { $ecosystemMatch.Groups[1].Value } else { $null }

    $engineMatch = [regex]::Match($body, "engine\s*:\s*'([^']*)'")
    $engine = if ($engineMatch.Success) { $engineMatch.Groups[1].Value } else { $null }

    $familyMatch = [regex]::Match($body, "family\s*:\s*'([^']*)'")
    $familyDirect = if ($familyMatch.Success) { $familyMatch.Groups[1].Value } else { $null }

    # Resolve the family: direct family on model > family from group config > null
    $resolvedFamily = $familyDirect
    if (-not $resolvedFamily -and $groups.Contains($groupKey)) {
        $groupObj = $groups[$groupKey]
        if ($groupObj.family) { $resolvedFamily = $groupObj.family }
    }

    # Get display name from group config
    $groupDisplayName = $null
    if ($groups.Contains($groupKey)) {
        $groupDisplayName = $groups[$groupKey].name
    }

    # Get family display name
    $familyDisplayName = $null
    if ($resolvedFamily -and $families.Contains($resolvedFamily)) {
        $familyDisplayName = $families[$resolvedFamily].name
    }

    $model = [ordered]@{
        name             = $name
        type             = $type
        group            = $groupKey
        groupDisplayName = $groupDisplayName
    }
    if ($resolvedFamily) { $model["family"] = $resolvedFamily }
    if ($familyDisplayName) { $model["familyDisplayName"] = $familyDisplayName }
    if ($hidden) { $model["hidden"] = $true }
    if ($ecosystem) { $model["ecosystem"] = $ecosystem }
    if ($engine) { $model["engine"] = $engine }

    $baseModels += [PSCustomObject]$model
}
Write-Host "  Found $($baseModels.Count) base models." -ForegroundColor Gray

# =============================================================================
# Build and write JSON output
# =============================================================================
Write-Host "Building JSON output..." -ForegroundColor Cyan

$output = [ordered]@{
    families = $families.Values | ForEach-Object { $_ }
    groups   = $groups.Values | ForEach-Object { $_ }
    models   = $baseModels
}

$outputDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$json = $output | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText($OutputPath, $json, [System.Text.Encoding]::UTF8)

Write-Host ""
Write-Host "Successfully generated basemodels.json" -ForegroundColor Green
Write-Host "  Output: $OutputPath" -ForegroundColor Gray
Write-Host "  Families: $($families.Count)" -ForegroundColor Gray
Write-Host "  Groups: $($groups.Count)" -ForegroundColor Gray
Write-Host "  Base Models: $($baseModels.Count)" -ForegroundColor Gray
