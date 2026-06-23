param(
    [Parameter(Mandatory = $true)]
    [string]$ComfyUIPath
)

$ErrorActionPreference = "Stop"

$source = Split-Path -Parent $MyInvocation.MyCommand.Path
$customNodes = Join-Path $ComfyUIPath "custom_nodes"
$target = Join-Path $customNodes "ComfyUI-BlazorCleanup"

if (-not (Test-Path $customNodes)) {
    throw "ComfyUI custom_nodes folder was not found: $customNodes"
}

if (Test-Path $target) {
    Remove-Item -Recurse -Force $target
}

Copy-Item -Recurse -Force $source $target
Write-Host "Installed Blazor cleanup nodes to $target"
Write-Host "Restart ComfyUI, then probe /object_info/BlazorCleanupImageEmbedding and /object_info/BlazorCleanupImageScore."