# Generate icons for XOutputRenew Stream Deck plugin
# Creates 72x72 and 144x144 PNG icons with text labels

param(
    [switch]$Force
)

Add-Type -AssemblyName System.Drawing

$scriptRoot = Split-Path -Parent $PSScriptRoot
$imagesPath = Join-Path $scriptRoot "com.xoutputrenew.sdPlugin\imgs"

# Ensure directory exists
if (-not (Test-Path $imagesPath)) {
    New-Item -ItemType Directory -Path $imagesPath -Force | Out-Null
}

function New-Icon {
    param(
        [string]$Name,
        [string]$Label,
        [string]$BackgroundColor = "#2D2D2D",
        [string]$TextColor = "#FFFFFF",
        [int]$Size = 72,
        [string]$Indicator = $null
    )

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    # Set quality
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # Fill background
    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($BackgroundColor))
    $graphics.FillRectangle($bgBrush, 0, 0, $Size, $Size)

    # Draw label text
    $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($TextColor))
    $fontSize = [int]($Size * 0.12)  # Scale font with icon size
    $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold)

    $stringFormat = New-Object System.Drawing.StringFormat
    $stringFormat.Alignment = [System.Drawing.StringAlignment]::Center
    $stringFormat.LineAlignment = [System.Drawing.StringAlignment]::Center

    $rect = New-Object System.Drawing.RectangleF(0, 0, $Size, $Size)

    # Adjust position if there's an indicator
    if ($Indicator) {
        $rect = New-Object System.Drawing.RectangleF(0, -($Size * 0.12), $Size, $Size)
    }

    $graphics.DrawString($Label, $font, $textBrush, $rect, $stringFormat)

    # Draw indicator if specified
    if ($Indicator) {
        $indicatorFont = New-Object System.Drawing.Font("Segoe UI", [int]($Size * 0.22), [System.Drawing.FontStyle]::Bold)
        $indicatorRect = New-Object System.Drawing.RectangleF(0, ($Size * 0.2), $Size, $Size)
        $graphics.DrawString($Indicator, $indicatorFont, $textBrush, $indicatorRect, $stringFormat)
        $indicatorFont.Dispose()
    }

    # Save both sizes
    $filePath = Join-Path $imagesPath "$Name.png"
    $bitmap.Save($filePath, [System.Drawing.Imaging.ImageFormat]::Png)

    # Cleanup
    $font.Dispose()
    $textBrush.Dispose()
    $bgBrush.Dispose()
    $stringFormat.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()

    Write-Host "Created: $Name.png ($Size x $Size)" -ForegroundColor Green
}

function New-IconSet {
    param(
        [string]$Name,
        [string]$Label,
        [string]$BackgroundColor = "#2D2D2D",
        [string]$TextColor = "#FFFFFF"
    )

    # Create 72x72 (standard) and 144x144 (@2x retina)
    New-Icon -Name $Name -Label $Label -BackgroundColor $BackgroundColor -TextColor $TextColor -Size 72
    New-Icon -Name "${Name}@2x" -Label $Label -BackgroundColor $BackgroundColor -TextColor $TextColor -Size 144
}

Write-Host ""
Write-Host "Generating XOutputRenew Stream Deck icons..." -ForegroundColor Cyan
Write-Host ""

# Plugin and category icons - Xbox green theme
$xboxGreen = "#107C10"
$xboxDarkGreen = "#0E6B0E"

New-IconSet -Name "plugin-icon" -Label "XOutput`nRenew" -BackgroundColor $xboxGreen
New-IconSet -Name "category-icon" -Label "XO" -BackgroundColor $xboxGreen

# Action icons

# Start Profile - Green (go)
New-IconSet -Name "action-start" -Label "Start`nProfile" -BackgroundColor "#28A745"

# Stop Profile - Red (stop)
New-IconSet -Name "action-stop" -Label "Stop`nProfile" -BackgroundColor "#DC3545"

# Toggle Profile - Off state (gray)
New-IconSet -Name "action-toggle-off" -Label "Profile`nOFF" -BackgroundColor "#6C757D"

# Toggle Profile - On state (green)
New-IconSet -Name "action-toggle-on" -Label "Profile`nON" -BackgroundColor "#28A745"

# Monitoring Start - Blue
New-IconSet -Name "action-monitor-start" -Label "Start`nMonitor" -BackgroundColor "#0D6EFD"

# Monitoring Stop - Orange
New-IconSet -Name "action-monitor-stop" -Label "Stop`nMonitor" -BackgroundColor "#FD7E14"

# Toggle Monitoring - Off state
New-IconSet -Name "action-monitor-off" -Label "Monitor`nOFF" -BackgroundColor "#6C757D"

# Toggle Monitoring - On state
New-IconSet -Name "action-monitor-on" -Label "Monitor`nON" -BackgroundColor "#0D6EFD"

# Launch App - Purple
New-IconSet -Name "action-launch" -Label "Launch`nApp" -BackgroundColor "#6F42C1"

# Profile Dial/Encoder - Teal
New-IconSet -Name "action-dial" -Label "Profile`nDial" -BackgroundColor "#20C997"

# Dial rotate indicators
New-Icon -Name "action-dial-left" -Label "Profile" -BackgroundColor "#20C997" -Size 72 -Indicator "<"
New-Icon -Name "action-dial-left@2x" -Label "Profile" -BackgroundColor "#20C997" -Size 144 -Indicator "<"
New-Icon -Name "action-dial-right" -Label "Profile" -BackgroundColor "#20C997" -Size 72 -Indicator ">"
New-Icon -Name "action-dial-right@2x" -Label "Profile" -BackgroundColor "#20C997" -Size 144 -Indicator ">"

Write-Host ""
Write-Host "Done! Icons created in: $imagesPath" -ForegroundColor Green
Write-Host ""
