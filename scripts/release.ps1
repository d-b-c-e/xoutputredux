# XOutputRedux Release Script
# Creates installer, portable ZIP, Stream Deck plugin, and Moza plugin for distribution
# Usage: .\scripts\release.ps1 [-SkipBuild] [-SkipStreamDeck] [-SkipMozaPlugin] [-InnoSetupPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"]

param(
    [switch]$SkipBuild,
    [switch]$SkipStreamDeck,
    [switch]$SkipMozaPlugin,
    [string]$InnoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
)

$ErrorActionPreference = "Stop"
$ScriptsDir = $PSScriptRoot
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$PublishDir = Join-Path $ProjectRoot "publish"
$DistDir = Join-Path $ProjectRoot "dist"
$InstallerScript = Join-Path $ProjectRoot "installer\XOutputRedux.iss"
$AppProject = Join-Path $ProjectRoot "src\XOutputRedux.App\XOutputRedux.App.csproj"

# Extract version using MSBuild (evaluates computed properties)
function Get-Version {
    $version = & dotnet msbuild $AppProject -getProperty:Version -nologo 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($version)) {
        # Fallback
        $version = "unknown"
    }
    return $version.Trim()
}

$Version = Get-Version
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "XOutputRedux Release v$Version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Create dist directory
if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir | Out-Null
}

# Determine total steps (build + portable ZIP + installer, plus optional Stream Deck + Moza)
$totalSteps = 3
if (-not $SkipStreamDeck) { $totalSteps++ }
if (-not $SkipMozaPlugin) { $totalSteps++ }
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

# Build Moza plugin
if (-not $SkipMozaPlugin) {
    $currentStep++
    Write-Host "`n[$currentStep/$totalSteps] Building Moza plugin..." -ForegroundColor Yellow
    $MozaPluginProject = Join-Path $ProjectRoot "src\XOutputRedux.Moza.Plugin\XOutputRedux.Moza.Plugin.csproj"

    if (Test-Path $MozaPluginProject) {
        $MozaPublishDir = Join-Path $ProjectRoot "publish-moza-plugin"
        if (Test-Path $MozaPublishDir) {
            Remove-Item -Recurse -Force $MozaPublishDir
        }

        & dotnet publish $MozaPluginProject -c Release -o $MozaPublishDir --nologo --self-contained false
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Moza plugin build failed (non-fatal)" -ForegroundColor Yellow
        } else {
            # Build MozaHelper.exe (out-of-process SDK helper)
            $MozaHelperProject = Join-Path $ProjectRoot "src\XOutputRedux.Moza.Helper\XOutputRedux.Moza.Helper.csproj"
            $MozaHelperPublishDir = Join-Path $ProjectRoot "publish-moza-helper"
            if (Test-Path $MozaHelperPublishDir) {
                Remove-Item -Recurse -Force $MozaHelperPublishDir
            }

            & dotnet publish $MozaHelperProject -c Release -o $MozaHelperPublishDir --nologo --self-contained false
            if ($LASTEXITCODE -ne 0) {
                Write-Host "Moza helper build failed (non-fatal)" -ForegroundColor Yellow
            } else {
                # Copy helper files into plugin publish dir
                Copy-Item (Join-Path $MozaHelperPublishDir "MozaHelper.exe") $MozaPublishDir
                Copy-Item (Join-Path $MozaHelperPublishDir "MozaHelper.dll") $MozaPublishDir
                Copy-Item (Join-Path $MozaHelperPublishDir "MozaHelper.runtimeconfig.json") $MozaPublishDir
            }

            # Clean up helper publish dir
            if (Test-Path $MozaHelperPublishDir) {
                Remove-Item -Recurse -Force $MozaHelperPublishDir
            }

            # Remove XOutputRedux.Core.dll from plugin output (already in main app)
            $coreDll = Join-Path $MozaPublishDir "XOutputRedux.Core.dll"
            if (Test-Path $coreDll) {
                Remove-Item $coreDll
            }

            # Remove deps.json that references Core (not needed for plugin loading)
            Get-ChildItem $MozaPublishDir -Filter "*.deps.json" | Remove-Item -Force

            # Remove .pdb files (not needed in release)
            Get-ChildItem $MozaPublishDir -Filter "*.pdb" | Remove-Item -Force

            # Create plugin package (.xoutputreduxplugin is a renamed ZIP)
            $MozaPluginName = "XOutputRedux-$Version-MozaPlugin.xoutputreduxplugin"
            $MozaPluginPath = Join-Path $DistDir $MozaPluginName
            # Build as .zip first, then rename (Compress-Archive requires .zip extension)
            $MozaTempZip = Join-Path $DistDir "MozaPlugin-temp.zip"

            if (Test-Path $MozaPluginPath) {
                Remove-Item $MozaPluginPath
            }
            if (Test-Path $MozaTempZip) {
                Remove-Item $MozaTempZip
            }

            Compress-Archive -Path "$MozaPublishDir\*" -DestinationPath $MozaTempZip -CompressionLevel Optimal
            Move-Item $MozaTempZip $MozaPluginPath
            Write-Host "Created: $MozaPluginName" -ForegroundColor Green

            # Clean up temp publish dir
            Remove-Item -Recurse -Force $MozaPublishDir
        }
    } else {
        Write-Host "Moza plugin project not found at: $MozaPluginProject" -ForegroundColor Yellow
    }
}

# Create portable ZIP
$currentStep++
Write-Host "`n[$currentStep/$totalSteps] Creating portable ZIP..." -ForegroundColor Yellow

$PortableStagingDir = Join-Path $ProjectRoot "portable-staging"
$PortableZipName = "XOutputRedux-$Version-Portable.zip"
$PortableZipPath = Join-Path $DistDir $PortableZipName

try {
    # Clean staging directory
    if (Test-Path $PortableStagingDir) {
        Remove-Item -Recurse -Force $PortableStagingDir
    }

    # Copy publish output to staging
    Copy-Item -Path $PublishDir -Destination $PortableStagingDir -Recurse

    # Create portable.txt marker (activates portable mode on launch)
    Set-Content -Path (Join-Path $PortableStagingDir "portable.txt") -Value "This file enables portable mode. Settings are stored in the data\ subfolder."

    # Create empty data directory (so users see where configs go)
    New-Item -ItemType Directory -Path (Join-Path $PortableStagingDir "data") | Out-Null

    # Remove existing ZIP if present
    if (Test-Path $PortableZipPath) {
        Remove-Item $PortableZipPath
    }

    Compress-Archive -Path "$PortableStagingDir\*" -DestinationPath $PortableZipPath -CompressionLevel Optimal
    Write-Host "Created: $PortableZipName" -ForegroundColor Green
} catch {
    Write-Host "Portable ZIP creation failed: $_" -ForegroundColor Red
    throw
} finally {
    # Clean up staging directory
    if (Test-Path $PortableStagingDir) {
        Remove-Item -Recurse -Force $PortableStagingDir
    }
}

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

# List Stream Deck plugin if present
$StreamDeckInDist = Join-Path $DistDir "com.xoutputredux.streamDeckPlugin"
if (Test-Path $StreamDeckInDist) {
    $sizeMB = [math]::Round((Get-Item $StreamDeckInDist).Length / 1MB, 2)
    Write-Host "  - com.xoutputredux.streamDeckPlugin ($sizeMB MB)"
}

Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Test the installer"
Write-Host "  2. Create a git tag: git tag v$Version"
Write-Host "  3. Push the tag: git push origin v$Version"
Write-Host "  4. Create GitHub release and upload files from dist/"
