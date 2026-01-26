# XOutputRedux Release Script
# Creates installer, portable ZIP, and Stream Deck plugin for distribution
# Usage: .\scripts\release.ps1 [-SkipBuild] [-SkipStreamDeck] [-InnoSetupPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"]

param(
    [switch]$SkipBuild,
    [switch]$SkipStreamDeck,
    [string]$InnoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"
$ScriptsDir = $PSScriptRoot
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$PublishDir = Join-Path $ProjectRoot "publish"
$DistDir = Join-Path $ProjectRoot "dist"
$InstallerScript = Join-Path $ProjectRoot "installer\XOutputRedux.iss"
$AppProject = Join-Path $ProjectRoot "src\XOutputRedux.App\XOutputRedux.App.csproj"

# Extract version from csproj
function Get-Version {
    $csproj = [xml](Get-Content $AppProject)
    $version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ }
    if ($version -is [array]) { $version = $version[0] }
    return $version
}

$Version = Get-Version
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "XOutputRedux Release v$Version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Create dist directory
if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir | Out-Null
}

# Determine total steps
$totalSteps = 3
if (-not $SkipStreamDeck) { $totalSteps++ }
$currentStep = 0

# Build if not skipped
$currentStep++
if (-not $SkipBuild) {
    Write-Host "`n[$currentStep/$totalSteps] Building application..." -ForegroundColor Yellow
    & "$ScriptsDir\build.ps1" -Configuration Release -Clean
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
} else {
    Write-Host "`n[$currentStep/$totalSteps] Skipping build (using existing publish folder)" -ForegroundColor Gray
}

# Build Stream Deck plugin
if (-not $SkipStreamDeck) {
    $currentStep++
    Write-Host "`n[$currentStep/$totalSteps] Building Stream Deck plugin..." -ForegroundColor Yellow
    $StreamDeckProject = Join-Path $ProjectRoot "src\XOutputRedux.StreamDeck"
    $StreamDeckBuildScript = Join-Path $StreamDeckProject "scripts\build-plugin.ps1"

    if (Test-Path $StreamDeckBuildScript) {
        Push-Location $StreamDeckProject
        try {
            & $StreamDeckBuildScript
            if ($LASTEXITCODE -ne 0) {
                Write-Host "Stream Deck plugin build failed (non-fatal)" -ForegroundColor Yellow
            } else {
                # Plugin is output to release\streamdeck\com.xoutputredux.streamDeckPlugin
                $PluginOutputDir = Join-Path $ProjectRoot "release\streamdeck"
                $PluginFile = Join-Path $PluginOutputDir "com.xoutputredux.streamDeckPlugin"
                if (Test-Path $PluginFile) {
                    # Copy to publish directory so it's included in the installer/portable ZIP
                    Copy-Item $PluginFile -Destination $PublishDir
                    # Also copy to dist for standalone download
                    Copy-Item $PluginFile -Destination $DistDir
                    Write-Host "Stream Deck plugin bundled with application" -ForegroundColor Green
                } else {
                    Write-Host "Stream Deck plugin file not found at expected location" -ForegroundColor Yellow
                }
            }
        } finally {
            Pop-Location
        }
    } else {
        Write-Host "Stream Deck build script not found at: $StreamDeckBuildScript" -ForegroundColor Yellow
    }
}

# Create portable ZIP
$currentStep++
Write-Host "`n[$currentStep/$totalSteps] Creating portable ZIP..." -ForegroundColor Yellow
$ZipName = "XOutputRedux-$Version-Portable.zip"
$ZipPath = Join-Path $DistDir $ZipName

if (Test-Path $ZipPath) {
    Remove-Item $ZipPath
}

Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal
Write-Host "Created: $ZipName" -ForegroundColor Green

# Build installer
$currentStep++
Write-Host "`n[$currentStep/$totalSteps] Building installer..." -ForegroundColor Yellow

if (-not (Test-Path $InnoSetupPath)) {
    Write-Host "Inno Setup not found at: $InnoSetupPath" -ForegroundColor Red
    Write-Host "Skipping installer creation. Install Inno Setup 6 or specify path with -InnoSetupPath" -ForegroundColor Yellow
} else {
    $InnoArgs = @(
        "/DMyAppVersion=$Version"
        $InstallerScript
    )

    & $InnoSetupPath @InnoArgs
    if ($LASTEXITCODE -ne 0) { throw "Installer build failed" }

    $InstallerName = "XOutputRedux-$Version-Setup.exe"
    Write-Host "Created: $InstallerName" -ForegroundColor Green
}

# Summary
Write-Host "`n========================================" -ForegroundColor Green
Write-Host "Release Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "Version: $Version"
Write-Host "Output:  $DistDir"
Write-Host ""
Write-Host "Files created:"

Get-ChildItem $DistDir -Filter "XOutputRedux-$Version*" | ForEach-Object {
    $sizeMB = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  - $($_.Name) ($sizeMB MB)"
}

Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Test the installer and portable ZIP"
Write-Host "  2. Create a git tag: git tag v$Version"
Write-Host "  3. Push the tag: git push origin v$Version"
Write-Host "  4. Create GitHub release and upload files from dist/"
