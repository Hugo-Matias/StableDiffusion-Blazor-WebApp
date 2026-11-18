param(
    [int]$ModelId = 958009,
    [string]$BaseUri = "https://civitai.com/api"
)

$ErrorActionPreference = "Stop"

function Get-JsonEndpoint {
    param([string]$Path)

    $uri = "$BaseUri/$Path"
    try {
        $response = Invoke-RestMethod -Uri $uri -Method Get -Headers @{ "User-Agent" = "BlazorDiffusion-CivitaiProbe/1.0" }
        return [pscustomobject]@{
            endpoint = $Path
            ok       = $true
            status   = "ok"
            data     = $response
        }
    }
    catch {
        return [pscustomobject]@{
            endpoint = $Path
            ok       = $false
            status   = $_.Exception.Message
            data     = $null
        }
    }
}

function Get-PropertyNames {
    param($Value)

    if ($null -eq $Value) { return @() }
    return @($Value.PSObject.Properties | ForEach-Object { $_.Name } | Sort-Object -Unique)
}

function Get-FirstItem {
    param($Items)

    if ($null -eq $Items) { return $null }
    if ($Items -is [array]) { return $Items | Select-Object -First 1 }
    return $Items
}

function Get-FirstImageWithNestedMeta {
    param($Items)

    if ($null -eq $Items) { return $null }
    foreach ($item in @($Items)) {
        if ($null -ne $item.meta -and $null -ne $item.meta.meta) {
            return $item
        }
    }
    return $null
}

function Get-MetaSummary {
    param($Image)

    if ($null -eq $Image) { return $null }
    $meta = $Image.meta
    if ($null -eq $meta) {
        return [pscustomobject]@{
            state       = "null"
            wrapperKeys = @()
            nestedState = "missing"
            nestedKeys  = @()
        }
    }

    $wrapperKeys = Get-PropertyNames $meta
    $nested = $meta.meta
    $nestedState = if ($null -eq $nested) { "null-or-missing" } else { "object" }
    $nestedKeys = Get-PropertyNames $nested

    return [pscustomobject]@{
        state       = "object"
        wrapperKeys = $wrapperKeys
        nestedState = $nestedState
        nestedKeys  = $nestedKeys
    }
}

$modelResult = Get-JsonEndpoint "v1/models/$ModelId"
$model = $modelResult.data
$firstVersion = Get-FirstItem $model.modelVersions
$firstModelImage = Get-FirstItem $firstVersion.images
$sampleVersionId = $firstVersion.id

$modelImagesResult = Get-JsonEndpoint "v1/images?modelId=$ModelId&limit=20"
$sampleImage = Get-FirstItem $modelImagesResult.data.items
$sampleImageId = $sampleImage.id

$results = @()
$results += $modelResult
$results += $modelImagesResult

if ($sampleImageId) {
    $results += Get-JsonEndpoint "v1/images?imageId=$sampleImageId"
}

if ($sampleVersionId) {
    $results += Get-JsonEndpoint "v1/images?modelVersionId=$sampleVersionId&limit=20"
}

$summary = foreach ($result in $results) {
    $data = $result.data
    $firstImage = $null
    if ($null -ne $data.items) {
        $firstImage = Get-FirstItem $data.items
    }
    elseif ($null -ne $data.modelVersions) {
        $version = Get-FirstItem $data.modelVersions
        $firstImage = Get-FirstItem $version.images
    }

    $nestedImage = Get-FirstImageWithNestedMeta $data.items

    [pscustomobject]@{
        endpoint                 = $result.endpoint
        ok                       = $result.ok
        status                   = $result.status
        topLevelKeys             = Get-PropertyNames $data
        metadataKeys             = Get-PropertyNames $data.metadata
        firstImageKeys           = Get-PropertyNames $firstImage
        firstImageMeta           = Get-MetaSummary $firstImage
        firstNestedMetaImageKeys = Get-PropertyNames $nestedImage
        firstNestedMeta          = Get-MetaSummary $nestedImage
    }
}

$summary | ConvertTo-Json -Depth 10