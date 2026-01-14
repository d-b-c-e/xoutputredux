# Build XOutputRenew Stream Deck Plugin Package
# Creates a .streamDeckPlugin file for distribution

param(
    [string]$OutputDir = ".\dist",
    [switch]$SkipIconGeneration
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $PSScriptRoot
$pluginDir = Join-Path $scriptRoot "com.xoutputrenew.sdPlugin"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  XOutputRenew Stream Deck Plugin Build" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Get version from package.json
$packageJson = Get-Content (Join-Path $scriptRoot "package.json") | ConvertFrom-Json
$version = $packageJson.version
Write-Host "Version: $version" -ForegroundColor Yellow
Write-Host ""

# Step 1: Generate icons (unless skipped)
if (-not $SkipIconGeneration) {
    Write-Host "Step 1: Generating icons..." -ForegroundColor Cyan
    $iconScript = Join-Path $PSScriptRoot "generate-icons.ps1"
    & $iconScript
} else {
    Write-Host "Step 1: Skipping icon generation" -ForegroundColor Yellow
}

# Step 2: Install npm dependencies
Write-Host ""
Write-Host "Step 2: Installing dependencies..." -ForegroundColor Cyan
Push-Location $scriptRoot
try {
    npm install --silent
    if ($LASTEXITCODE -ne 0) {
        throw "npm install failed"
    }
} finally {
    Pop-Location
}
Write-Host "Dependencies installed" -ForegroundColor Green

# Step 3: Build TypeScript
Write-Host ""
Write-Host "Step 3: Building TypeScript..." -ForegroundColor Cyan
Push-Location $scriptRoot
try {
    npm run build
    if ($LASTEXITCODE -ne 0) {
        throw "npm build failed"
    }
} finally {
    Pop-Location
}
Write-Host "TypeScript built" -ForegroundColor Green

# Step 4: Create output directory
Write-Host ""
Write-Host "Step 4: Preparing output..." -ForegroundColor Cyan
$outputPath = Join-Path $scriptRoot $OutputDir
if (-not (Test-Path $outputPath)) {
    New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
}

# Step 5: Create staging directory
$stagingDir = Join-Path $outputPath "staging"
if (Test-Path $stagingDir) {
    Remove-Item -Path $stagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

# Copy plugin directory to staging
$stagingPluginDir = Join-Path $stagingDir "com.xoutputrenew.sdPlugin"
Copy-Item -Path $pluginDir -Destination $stagingPluginDir -Recurse

# Remove any files we don't want in the package
$filesToRemove = @(
    "*.log",
    "logs"
)
foreach ($pattern in $filesToRemove) {
    Get-ChildItem -Path $stagingPluginDir -Filter $pattern -Recurse | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
}

Write-Host "Staging prepared" -ForegroundColor Green

# Step 6: Create zip package
Write-Host ""
Write-Host "Step 5: Creating package..." -ForegroundColor Cyan

$pluginFileName = "com.xoutputrenew.streamdeck-v$version.streamDeckPlugin"
$pluginFilePath = Join-Path $outputPath $pluginFileName

# Remove existing file if present
if (Test-Path $pluginFilePath) {
    Remove-Item -Path $pluginFilePath -Force
}

# Create zip (Stream Deck plugin is just a renamed zip)
$zipPath = Join-Path $outputPath "temp.zip"
Compress-Archive -Path $stagingPluginDir -DestinationPath $zipPath -Force

# Rename to .streamDeckPlugin
Move-Item -Path $zipPath -Destination $pluginFilePath -Force

# Cleanup staging
Remove-Item -Path $stagingDir -Recurse -Force

Write-Host "Package created: $pluginFileName" -ForegroundColor Green

# Also create a copy without version for easy bundling
$latestPluginPath = Join-Path $outputPath "XOutputRenew.streamDeckPlugin"
Copy-Item -Path $pluginFilePath -Destination $latestPluginPath -Force
Write-Host "Latest copy: XOutputRenew.streamDeckPlugin" -ForegroundColor Green

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Output: $pluginFilePath" -ForegroundColor Yellow
Write-Host ""
Write-Host "To install: Double-click the .streamDeckPlugin file" -ForegroundColor Gray
Write-Host ""
