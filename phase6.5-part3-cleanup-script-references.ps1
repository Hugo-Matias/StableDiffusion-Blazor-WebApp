# Phase 6.5: Part 3 - Remove script references and using statements
# This script removes all WebUI using statements and script-related code

Write-Host "=== Phase 6.5: Part 3 - Cleanup Script References ===" -ForegroundColor Cyan
Write-Host ""

$files = @(
    "BlazorWebApp/Services/IParameterFactory.cs",
    "BlazorWebApp/Components/Prompts/WildcardsPanel.razor",
    "BlazorWebApp/Pages/Resources.razor",
    "BlazorWebApp/Extensions/Parser.cs",
    "BlazorWebApp/Services/StateService.cs",
    "BlazorWebApp/Components/Img2Img/UltimateUpscaleForm.razor",
    "BlazorWebApp/Models/Txt2ImgParameters.cs",
    "BlazorWebApp/Models/GeneratedImages.cs",
    "BlazorWebApp/Extensions/ParameterMapper.cs",
    "BlazorWebApp/Models/Img2ImgParameters.cs",
    "BlazorWebApp/Models/AppSettings.cs",
    "BlazorWebApp/Services/ImageService.cs"
)

$removedUsings = 0
$updatedFiles = 0

Write-Host "Removing 'using BlazorWebApp.Data.Dtos.WebUI;' statements..." -ForegroundColor Yellow
Write-Host ""

foreach ($file in $files) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw
        $originalContent = $content
        
        # Remove WebUI using statement
        $content = $content -replace "using BlazorWebApp\.Data\.Dtos\.WebUI;`r?`n", ""
        
        if ($content -ne $originalContent) {
            Set-Content $file -Value $content -NoNewline
            Write-Host "  ? Updated: $file" -ForegroundColor Green
            $updatedFiles++
            $removedUsings++
        }
    } else {
        Write-Host "  ? Not found: $file" -ForegroundColor DarkYellow
    }
}

Write-Host ""
Write-Host "=== Part 3 Summary ===" -ForegroundColor Cyan
Write-Host "Files updated: $updatedFiles" -ForegroundColor Green
Write-Host "Using statements removed: $removedUsings" -ForegroundColor Green
Write-Host ""
Write-Host "Next: Manually fix remaining files with script-specific code" -ForegroundColor Cyan
Write-Host ""
