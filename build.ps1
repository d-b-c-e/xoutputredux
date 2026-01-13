# XOutputRenew Build Script
# Usage: .\build.ps1 [-Configuration Release|Debug] [-SelfContained] [-Clean]

param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [switch]$SelfContained,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$AppProject = Join-Path $ProjectRoot "src\XOutputRenew.App\XOutputRenew.App.csproj"
$PublishDir = Join-Path $ProjectRoot "publish"

# Extract version from csproj
function Get-Version {
    $csproj = [xml](Get-Content $AppProject)
    $version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ }
    if ($version -is [array]) { $version = $version[0] }
    return $version
}

$Version = Get-Version
Write-Host "Building XOutputRenew v$Version" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host "Self-Contained: $SelfContained" -ForegroundColor Gray

# Clean if requested
if ($Clean) {
    Write-Host "`nCleaning..." -ForegroundColor Yellow
    if (Test-Path $PublishDir) {
        Remove-Item -Recurse -Force $PublishDir
    }
    dotnet clean $AppProject -c $Configuration --nologo -v q
}

# Restore
Write-Host "`nRestoring packages..." -ForegroundColor Yellow
dotnet restore $AppProject --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Restore failed" }

# Build
Write-Host "`nBuilding..." -ForegroundColor Yellow
dotnet build $AppProject -c $Configuration --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

# Publish
Write-Host "`nPublishing..." -ForegroundColor Yellow
$publishArgs = @(
    "publish"
    $AppProject
    "-c", $Configuration
    "-o", $PublishDir
    "--nologo"
    "-p:PublishReadyToRun=true"
)

if ($SelfContained) {
    $publishArgs += "-r", "win-x64"
    $publishArgs += "--self-contained", "true"
    $publishArgs += "-p:PublishSingleFile=false"
} else {
    $publishArgs += "--self-contained", "false"
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "Publish failed" }

# Summary
$exePath = Join-Path $PublishDir "XOutputRenew.exe"
if (Test-Path $exePath) {
    $fileInfo = Get-Item $exePath
    $sizeMB = [math]::Round($fileInfo.Length / 1MB, 2)

    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host "Build Successful!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "Version:  $Version"
    Write-Host "Output:   $PublishDir"
    Write-Host "Exe Size: $sizeMB MB"
    Write-Host ""
} else {
    throw "Build completed but XOutputRenew.exe not found"
}

# Return version for use by other scripts
return $Version
