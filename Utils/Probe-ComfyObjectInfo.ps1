# Probes ComfyUI /object_info for a list of node class types and prints each
# required input as raw type or COMBO sample. Used to verify which folder /
# asset enum a workflow node loads from before wiring it as a WorkflowAsset.
param(
    [string]$BaseUrl = 'http://localhost:8188',
    [string[]]$Nodes = @(
        'UNETLoader',
        'DualCLIPLoader',
        'VAELoader',
        'LTXVAudioVAELoader',
        'LatentUpscaleModelLoader',
        'UpscaleModelLoader',
        'LTXVChunkFeedForward',
        'LTX2SamplingPreviewOverride'
    )
)

foreach ($n in $Nodes) {
    Write-Host ("=== {0} ===" -f $n) -ForegroundColor Cyan
    try {
        $r = Invoke-RestMethod -Uri "$BaseUrl/object_info/$n" -TimeoutSec 5
    }
    catch {
        Write-Host ("  MISSING NODE or HTTP error: {0}" -f $_.Exception.Message) -ForegroundColor Yellow
        continue
    }
    $info = $r.$n
    if (-not $info) {
        # ComfyUI returns 200 + {} for unknown class types. Treat as missing.
        Write-Host "  MISSING NODE (not registered on this ComfyUI install)" -ForegroundColor Yellow
        continue
    }
    Write-Host ("  category : {0}" -f $info.category)
    Write-Host ("  output   : {0}" -f ($info.output -join ', '))
    $req = $info.input.required
    if (-not $req) {
        Write-Host "  (no required inputs)"
        continue
    }
    foreach ($p in $req.PSObject.Properties) {
        $v = $p.Value
        $first = $v[0]
        if ($first -is [System.Collections.IList] -and $first.Count -gt 0) {
            $sample = ($first | Select-Object -First 3) -join ' | '
            Write-Host ("  required.{0}: COMBO[{1}] sample=[{2}]" -f $p.Name, $first.Count, $sample)
        }
        elseif ($first -is [string] -and $first -eq 'COMBO' -and $v.Count -ge 2) {
            $opts = $v[1].options
            $sample = ($opts | Select-Object -First 3) -join ' | '
            Write-Host ("  required.{0}: COMBO-typed[{1}] sample=[{2}]" -f $p.Name, $opts.Count, $sample)
        }
        else {
            Write-Host ("  required.{0}: {1}" -f $p.Name, $first)
        }
    }
}
