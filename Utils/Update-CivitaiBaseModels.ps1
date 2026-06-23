<#
.SYNOPSIS
    Fetches CivitAI base model constants from GitHub and generates a structured basemodels.json file.

.DESCRIPTION
    Downloads basemodel.constants.ts from the CivitAI GitHub repository (V2 ecosystem-based schema),
    parses ecosystemFamilies, ecosystems, and baseModelRecords, then outputs a structured JSON file
    compatible with the app's CivitaiBaseModelsData schema.

    Family keys in the output use the numeric family id (as a string) from the source.
    The "groups" array is kept empty for backwards compatibility with the existing DTO.

.EXAMPLE
    .\Update-CivitaiBaseModels.ps1
    .\Update-CivitaiBaseModels.ps1 -OutputPath ".\custom-output.json"
#>

[CmdletBinding()]
param(
    [string]$OutputPath,
    [string]$SourceUrl = "https://raw.githubusercontent.com/civitai/civitai/refs/heads/main/src/shared/constants/basemodel.constants.ts"
)

$ErrorActionPreference = "Stop"

# Resolve output path - default to BlazorWebApp/Data/CivitAI/basemodels.json relative to script location
if (-not $OutputPath) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Get-Location }
    $OutputPath = Join-Path $scriptDir "..\BlazorWebApp\Data\CivitAI\basemodels.json"
}

Write-Host "Fetching basemodel.constants.ts from CivitAI GitHub..." -ForegroundColor Cyan
$tsContent = Invoke-WebRequest -Uri $SourceUrl -UseBasicParsing | Select-Object -ExpandProperty Content
Write-Host "Downloaded $($tsContent.Length) characters." -ForegroundColor Green

# Returns the body text of each flat (no nested braces) object { ... } found in a block
function Get-FlatObjectBodies([string]$block) {
    [regex]::Matches($block, '\{([^{}]+)\}') | ForEach-Object { $_.Groups[1].Value }
}

# Returns the value of a named string field (single or double quoted) from an object body
function Get-StringField([string]$body, [string]$field) {
    $m = [regex]::Match($body, "$field\s*:\s*'([^']*)'")
    if ($m.Success) { return $m.Groups[1].Value }
    $m = [regex]::Match($body, "$field\s*:\s*`"([^`"]*)`"")
    if ($m.Success) { return $m.Groups[1].Value }
    return $null
}

# =============================================================================
# 1. Parse ECO constant block (name -> numeric id map)
# =============================================================================
Write-Host "Parsing ECO constants..." -ForegroundColor Cyan

$ecoConstBlock = [regex]::Match($tsContent, 'export const ECO\s*=\s*\{([\s\S]*?)\}\s*as const')
if (-not $ecoConstBlock.Success) { throw "Failed to find ECO constant block" }

$ecoMap = @{}
[regex]::Matches($ecoConstBlock.Groups[1].Value, '(\w+)\s*:\s*(\d+)') | ForEach-Object {
    $ecoMap[$_.Groups[1].Value] = [int]$_.Groups[2].Value
}
Write-Host "  Found $($ecoMap.Count) ECO entries." -ForegroundColor Gray

# =============================================================================
# 2. Parse ecosystemFamilies -> families output list
# =============================================================================
Write-Host "Parsing ecosystemFamilies..." -ForegroundColor Cyan

$famArrBlock = [regex]::Match($tsContent, 'export const ecosystemFamilies[^=]*=\s*\[([\s\S]*?)\];')
if (-not $famArrBlock.Success) { throw "Failed to find ecosystemFamilies block" }

$familiesById = @{}
$familiesList = @(Get-FlatObjectBodies $famArrBlock.Groups[1].Value | ForEach-Object {
        $body = $_
        $idMatch = [regex]::Match($body, 'id\s*:\s*(\d+)')
        if (-not $idMatch.Success) { return }

        $id = [int]$idMatch.Groups[1].Value
        $name = Get-StringField $body 'name'
        $desc = Get-StringField $body 'description'

        $entry = [PSCustomObject][ordered]@{
            key         = $id.ToString()
            name        = if ($name) { $name } else { '' }
            description = if ($desc) { $desc } else { '' }
        }
        $familiesById[$id] = $entry
        $entry
    })
Write-Host "  Found $($familiesList.Count) families." -ForegroundColor Gray

# =============================================================================
# 3. Parse ecosystems array -> lookup table (id -> key, displayName, familyId)
#    Groups have been removed in V2; ecosystems serve as the equivalent concept.
# =============================================================================
Write-Host "Parsing ecosystems..." -ForegroundColor Cyan

$ecoArrBlock = [regex]::Match($tsContent, 'export const ecosystems[^=]*=\s*\[([\s\S]*?)\];')
if (-not $ecoArrBlock.Success) { throw "Failed to find ecosystems block" }

$ecosystemsById = @{}
Get-FlatObjectBodies $ecoArrBlock.Groups[1].Value | ForEach-Object {
    $body = $_
    # id field references the ECO constant: id: ECO.Flux1
    $ecoRefMatch = [regex]::Match($body, '\bid\s*:\s*ECO\.(\w+)')
    if (-not $ecoRefMatch.Success) { return }

    $ecoName = $ecoRefMatch.Groups[1].Value
    if (-not $ecoMap.ContainsKey($ecoName)) { return }
    $id = $ecoMap[$ecoName]

    $displayName = Get-StringField $body 'displayName'
    $familyIdMatch = [regex]::Match($body, 'familyId\s*:\s*(\d+)')
    $familyId = if ($familyIdMatch.Success) { [int]$familyIdMatch.Groups[1].Value } else { $null }

    $ecosystemsById[$id] = [PSCustomObject]@{
        id = $id; displayName = $displayName; familyId = $familyId
    }
}
Write-Host "  Found $($ecosystemsById.Count) ecosystems." -ForegroundColor Gray

# =============================================================================
# 4. Parse baseModelRecords -> models output list
# =============================================================================
Write-Host "Parsing baseModelRecords..." -ForegroundColor Cyan

$bmArrBlock = [regex]::Match($tsContent, 'export const baseModelRecords[^=]*=\s*\[([\s\S]*?)\];')
if (-not $bmArrBlock.Success) { throw "Failed to find baseModelRecords block" }

$modelsList = @(Get-FlatObjectBodies $bmArrBlock.Groups[1].Value | ForEach-Object {
        $body = $_

        # Skip models explicitly disabled at the record level
        if ($body -match 'disabled\s*:\s*true') { return }

        $name = Get-StringField $body 'name'
        if (-not $name) { return }

        # type is a single string ('image'/'video') or an array (['image','video'])
        # For array types, fall back to 'image' - the field is informational only
        $type = Get-StringField $body 'type'
        if (-not $type) { $type = 'image' }

        $hidden = $body -match 'hidden\s*:\s*true'

        # ecosystemId references the ECO constant: ecosystemId: ECO.Flux1
        $ecoRefMatch = [regex]::Match($body, 'ecosystemId\s*:\s*ECO\.(\w+)')
        if (-not $ecoRefMatch.Success) { return }
        $ecoName = $ecoRefMatch.Groups[1].Value
        if (-not $ecoMap.ContainsKey($ecoName)) { return }
        $ecosystemId = $ecoMap[$ecoName]

        # Resolve family key and display name via ecosystem -> familyId chain
        $familyKey = $null
        $familyDisplayName = $null
        if ($ecosystemsById.ContainsKey($ecosystemId)) {
            $eco = $ecosystemsById[$ecosystemId]
            if ($null -ne $eco.familyId -and $familiesById.ContainsKey($eco.familyId)) {
                $fam = $familiesById[$eco.familyId]
                $familyKey = $fam.key
                $familyDisplayName = $fam.name
            }
        }

        $model = [ordered]@{ name = $name; type = $type }
        if ($familyKey) { $model['family'] = $familyKey }
        if ($familyDisplayName) { $model['familyDisplayName'] = $familyDisplayName }
        if ($hidden) { $model['hidden'] = $true }

        [PSCustomObject]$model
    })
Write-Host "  Found $($modelsList.Count) base models." -ForegroundColor Gray

# =============================================================================
# 5. Build and write JSON output
# =============================================================================
Write-Host "Building JSON output..." -ForegroundColor Cyan

$output = [ordered]@{
    families = $familiesList
    groups   = @()       # Removed in V2 schema; kept empty for DTO compatibility
    models   = $modelsList
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
Write-Host "  Families: $($familiesList.Count)" -ForegroundColor Gray
Write-Host "  Base Models: $($modelsList.Count)" -ForegroundColor Gray
