param(
    [string]$BaseUrl = "http://localhost:8188"
)

$ErrorActionPreference = "Stop"

$nodes = @(
    "BlazorCleanupImageEmbedding",
    "BlazorCleanupImageScore"
)

foreach ($node in $nodes) {
    Write-Host "Probing $node"
    Invoke-RestMethod "$BaseUrl/object_info/$node" | ConvertTo-Json -Depth 10
}