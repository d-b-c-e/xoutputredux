# Build and package XOutputRenew Stream Deck plugin
param(
    [switch]$Install,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $PSScriptRoot
$projectRoot = Split-Path -Parent (Split-Path -Parent $scriptRoot)
$pluginName = "com.xoutputrenew.sdPlugin"
$outputDir = Join-Path $projectRoot "release\streamdeck"
$pluginDir = Join-Path $outputDir $pluginName
$buildDir = Join-Path $scriptRoot "bin\Release\net8.0-windows\win-x64"

Write-Host ""
Write-Host "XOutputRenew Stream Deck Plugin Builder" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Build if not skipped
if (-not $SkipBuild) {
    Write-Host "Building plugin..." -ForegroundColor Yellow
    Push-Location $scriptRoot
    try {
        dotnet build -c Release
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed"
        }
    }
    finally {
        Pop-Location
    }
    Write-Host "Build complete." -ForegroundColor Green
    Write-Host ""
}

# Create output directory
if (Test-Path $pluginDir) {
    Remove-Item $pluginDir -Recurse -Force
}
New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null

Write-Host "Copying files..." -ForegroundColor Yellow

# Copy binaries (from build output)
$binSource = Join-Path $scriptRoot "bin\Release\net8.0-windows\win-x64"
Get-ChildItem $binSource -File | ForEach-Object {
    Copy-Item $_.FullName $pluginDir
    Write-Host "  $_" -ForegroundColor Gray
}

# Copy manifest.json
Copy-Item (Join-Path $scriptRoot "manifest.json") $pluginDir
Write-Host "  manifest.json" -ForegroundColor Gray

# Copy Images folder
$imagesSource = Join-Path $scriptRoot "Images"
$imagesDest = Join-Path $pluginDir "Images"
if (Test-Path $imagesSource) {
    Copy-Item $imagesSource $imagesDest -Recurse
    Write-Host "  Images/ ($(Get-ChildItem $imagesSource -File | Measure-Object | Select-Object -ExpandProperty Count) files)" -ForegroundColor Gray
}

# Copy PropertyInspector folder
$piSource = Join-Path $scriptRoot "PropertyInspector"
$piDest = Join-Path $pluginDir "PropertyInspector"
if (Test-Path $piSource) {
    Copy-Item $piSource $piDest -Recurse
    Write-Host "  PropertyInspector/ ($(Get-ChildItem $piSource -File | Measure-Object | Select-Object -ExpandProperty Count) files)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Creating plugin package..." -ForegroundColor Yellow

# Create .streamDeckPlugin (it's just a renamed zip)
$pluginFile = Join-Path $outputDir "com.xoutputrenew.streamDeckPlugin"
if (Test-Path $pluginFile) {
    Remove-Item $pluginFile
}

# Create zip
$zipFile = Join-Path $outputDir "com.xoutputrenew.zip"
if (Test-Path $zipFile) {
    Remove-Item $zipFile
}

# Use .NET compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($pluginDir, $zipFile)

# Rename to .streamDeckPlugin
Move-Item $zipFile $pluginFile

Write-Host "Created: $pluginFile" -ForegroundColor Green
Write-Host ""

# Install if requested
if ($Install) {
    Write-Host "Installing plugin..." -ForegroundColor Yellow

    # Kill Stream Deck if running
    $streamDeck = Get-Process -Name "StreamDeck" -ErrorAction SilentlyContinue
    if ($streamDeck) {
        Write-Host "  Stopping Stream Deck..." -ForegroundColor Gray
        Stop-Process -Name "StreamDeck" -Force
        Start-Sleep -Seconds 2
    }

    # Install to Stream Deck plugins folder
    $sdPluginPath = Join-Path $env:APPDATA "Elgato\StreamDeck\Plugins\$pluginName"

    if (Test-Path $sdPluginPath) {
        Write-Host "  Removing old plugin..." -ForegroundColor Gray
        Remove-Item $sdPluginPath -Recurse -Force
    }

    Write-Host "  Copying to $sdPluginPath..." -ForegroundColor Gray
    Copy-Item $pluginDir $sdPluginPath -Recurse

    # Restart Stream Deck
    Write-Host "  Restarting Stream Deck..." -ForegroundColor Gray
    $sdExe = "C:\Program Files\Elgato\StreamDeck\StreamDeck.exe"
    if (Test-Path $sdExe) {
        Start-Process $sdExe
    }
    else {
        Write-Host "  Stream Deck not found at default location. Please start manually." -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "Plugin installed!" -ForegroundColor Green
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Green
Write-Host ""
