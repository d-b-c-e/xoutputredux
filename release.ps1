# XOutputRenew Release Script
# Creates installer and portable ZIP for distribution
# Usage: .\release.ps1 [-SkipBuild] [-InnoSetupPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"]

param(
    [switch]$SkipBuild,
    [string]$InnoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$PublishDir = Join-Path $ProjectRoot "publish"
$DistDir = Join-Path $ProjectRoot "dist"
$InstallerScript = Join-Path $ProjectRoot "installer\XOutputRenew.iss"
$AppProject = Join-Path $ProjectRoot "src\XOutputRenew.App\XOutputRenew.App.csproj"

# Extract version from csproj
function Get-Version {
    $csproj = [xml](Get-Content $AppProject)
    $version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ }
    if ($version -is [array]) { $version = $version[0] }
    return $version
}

$Version = Get-Version
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "XOutputRenew Release v$Version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Create dist directory
if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir | Out-Null
}

# Build if not skipped
if (-not $SkipBuild) {
    Write-Host "`n[1/3] Building application..." -ForegroundColor Yellow
    & "$ProjectRoot\build.ps1" -Configuration Release -Clean
    if ($LASTEXITCODE -ne 0) { throw "Build failed" }
} else {
    Write-Host "`n[1/3] Skipping build (using existing publish folder)" -ForegroundColor Gray
}

# Create portable ZIP
Write-Host "`n[2/3] Creating portable ZIP..." -ForegroundColor Yellow
$ZipName = "XOutputRenew-$Version-Portable.zip"
$ZipPath = Join-Path $DistDir $ZipName

if (Test-Path $ZipPath) {
    Remove-Item $ZipPath
}

Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipPath -CompressionLevel Optimal
Write-Host "Created: $ZipName" -ForegroundColor Green

# Build installer
Write-Host "`n[3/3] Building installer..." -ForegroundColor Yellow

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

    $InstallerName = "XOutputRenew-$Version-Setup.exe"
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

Get-ChildItem $DistDir -Filter "XOutputRenew-$Version*" | ForEach-Object {
    $sizeMB = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  - $($_.Name) ($sizeMB MB)"
}

Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Test the installer and portable ZIP"
Write-Host "  2. Create a git tag: git tag v$Version"
Write-Host "  3. Push the tag: git push origin v$Version"
Write-Host "  4. Create GitHub release and upload files from dist/"
