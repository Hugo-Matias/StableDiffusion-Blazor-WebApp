[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string[]]$Nodes,

    [string]$BaseUrl = "http://localhost:8188",

    [int]$MaxComboValues = 25,

    [switch]$AllComboValues,

    [switch]$AsJson,

    [switch]$Raw
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function ConvertTo-InputSummary {
    param(
        [hashtable]$Inputs,
        [string]$Kind
    )

    $items = @()
    if ($null -eq $Inputs) {
        return $items
    }

    foreach ($name in $Inputs.Keys) {
        $spec = $Inputs[$name]
        $type = $null
        $options = @()
        $constraints = [ordered]@{}

        if ($spec -is [System.Collections.IList] -and $spec.Count -gt 0) {
            $first = $spec[0]
            if ($first -is [System.Collections.IEnumerable] -and -not ($first -is [string])) {
                $type = "COMBO"
                $options = @($first)
            }
            else {
                $type = [string]$first
            }

            if ($spec.Count -gt 1 -and $spec[1] -is [hashtable]) {
                foreach ($key in @("default", "min", "max", "step", "round", "forceInput")) {
                    if ($spec[1].ContainsKey($key)) {
                        $constraints[$key] = $spec[1][$key]
                    }
                }
            }
        }

        $shownOptions = if ($AllComboValues) { $options } else { $options | Select-Object -First $MaxComboValues }

        $items += [pscustomobject]@{
            Name             = $name
            Kind             = $Kind
            Type             = $type
            OptionCount      = $options.Count
            Options          = @($shownOptions)
            OptionsTruncated = (-not $AllComboValues -and $options.Count -gt $MaxComboValues)
            Constraints      = $constraints
        }
    }

    return $items
}

function Get-ObjectInfo {
    param([string]$Node)

    $escaped = [uri]::EscapeDataString($Node)
    $uri = "$($BaseUrl.TrimEnd('/'))/object_info/$escaped"
    $response = Invoke-WebRequest -Uri $uri -TimeoutSec 30

    if ($Raw) {
        return [pscustomobject]@{
            Node   = $Node
            Status = "RAW"
            Uri    = $uri
            Raw    = $response.Content
        }
    }

    $payload = $response.Content | ConvertFrom-Json -AsHashTable
    if (-not $payload.ContainsKey($Node)) {
        return [pscustomobject]@{
            Node           = $Node
            Status         = "MISSING"
            Uri            = $uri
            Category       = $null
            Outputs        = @()
            OutputCount    = 0
            RequiredInputs = @()
            OptionalInputs = @()
        }
    }

    $entry = $payload[$Node]
    $outputs = @()
    if ($entry.ContainsKey("output") -and $null -ne $entry.output) {
        for ($i = 0; $i -lt $entry.output.Count; $i++) {
            $outputs += [pscustomobject]@{
                Index = $i
                Type  = [string]$entry.output[$i]
                Name  = if ($entry.ContainsKey("output_name") -and $entry.output_name.Count -gt $i) { [string]$entry.output_name[$i] } else { $null }
            }
        }
    }

    $required = @()
    $optional = @()
    if ($entry.ContainsKey("input") -and $null -ne $entry.input) {
        if ($entry.input.ContainsKey("required")) {
            $required = ConvertTo-InputSummary -Inputs $entry.input.required -Kind "required"
        }
        if ($entry.input.ContainsKey("optional")) {
            $optional = ConvertTo-InputSummary -Inputs $entry.input.optional -Kind "optional"
        }
    }

    [pscustomobject]@{
        Node           = $Node
        Status         = "OK"
        Uri            = $uri
        Category       = if ($entry.ContainsKey("category")) { [string]$entry.category } else { $null }
        Outputs        = $outputs
        OutputCount    = $outputs.Count
        RequiredInputs = $required
        OptionalInputs = $optional
    }
}

$results = foreach ($node in $Nodes) {
    try {
        Get-ObjectInfo -Node $node
    }
    catch {
        [pscustomobject]@{
            Node   = $node
            Status = "ERROR"
            Uri    = "$($BaseUrl.TrimEnd('/'))/object_info/$([uri]::EscapeDataString($node))"
            Error  = $_.Exception.Message
        }
    }
}

if ($AsJson -or $Raw) {
    $results | ConvertTo-Json -Depth 20
    exit
}

foreach ($result in $results) {
    Write-Host "NODE: $($result.Node)"
    Write-Host "STATUS: $($result.Status)"
    Write-Host "URI: $($result.Uri)"

    if ($result.Status -eq "OK") {
        Write-Host "CATEGORY: $($result.Category)"
        Write-Host "OUTPUTS:"
        if ($result.Outputs.Count -eq 0) {
            Write-Host "  (none)"
        }
        else {
            foreach ($output in $result.Outputs) {
                $nameSuffix = if ([string]::IsNullOrWhiteSpace($output.Name)) { "" } else { " name=$($output.Name)" }
                Write-Host "  [$($output.Index)] $($output.Type)$nameSuffix"
            }
        }

        foreach ($group in @(@{Title = "REQUIRED INPUTS"; Items = $result.RequiredInputs }, @{Title = "OPTIONAL INPUTS"; Items = $result.OptionalInputs })) {
            Write-Host "$($group.Title):"
            if ($group.Items.Count -eq 0) {
                Write-Host "  (none)"
                continue
            }

            foreach ($input in $group.Items) {
                $constraintParts = @()
                foreach ($key in $input.Constraints.Keys) {
                    $constraintParts += "$key=$($input.Constraints[$key])"
                }
                $constraints = if ($constraintParts.Count -gt 0) { " {" + ($constraintParts -join ", ") + "}" } else { "" }
                Write-Host "  $($input.Name): $($input.Type)$constraints"
                if ($input.OptionCount -gt 0) {
                    $suffix = if ($input.OptionsTruncated) { " ... ($($input.OptionCount) total)" } else { "" }
                    Write-Host "    options: $(($input.Options) -join ' | ')$suffix"
                }
            }
        }
    }
    elseif ($result.Status -eq "ERROR") {
        Write-Host "ERROR: $($result.Error)"
    }

    Write-Host ""
}
