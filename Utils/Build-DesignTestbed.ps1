$ErrorActionPreference = 'Stop'
$proj = Join-Path $PSScriptRoot 'BlazorWebApp\BlazorWebApp.csproj'
dotnet build $proj /property:GenerateFullPaths=true /consoleloggerparameters:NoSummary
