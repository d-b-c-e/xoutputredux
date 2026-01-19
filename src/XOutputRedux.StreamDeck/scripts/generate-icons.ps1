# Generate icons for XOutputRedux Stream Deck plugin
# Creates 72x72 and 144x144 PNG icons with gamepad emoji at top
# All text below is dynamic (set by the plugin)

param(
    [switch]$Force
)

Add-Type -AssemblyName System.Drawing

$scriptRoot = Split-Path -Parent $PSScriptRoot
$imagesPath = Join-Path $scriptRoot "Images"

# Ensure directory exists
if (-not (Test-Path $imagesPath)) {
    New-Item -ItemType Directory -Path $imagesPath -Force | Out-Null
}

function New-Icon {
    param(
        [string]$Name,
        [string]$BackgroundColor = "#2D2D2D",
        [string]$TextColor = "#FFFFFF",
        [int]$Size = 72,
        [string]$StaticText = $null  # Optional static text for plugin/category icons
    )

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    # Set quality
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # Fill background
    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($BackgroundColor))
    $graphics.FillRectangle($bgBrush, 0, 0, $Size, $Size)

    $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($TextColor))

    $stringFormat = New-Object System.Drawing.StringFormat
    $stringFormat.Alignment = [System.Drawing.StringAlignment]::Center
    $stringFormat.LineAlignment = [System.Drawing.StringAlignment]::Center

    # Draw gamepad emoji at top (positioned higher to leave room for dynamic text below)
    $emojiSize = [int]($Size * 0.24)
    $emojiFont = New-Object System.Drawing.Font("Segoe UI Emoji", $emojiSize, [System.Drawing.FontStyle]::Regular)

    # Position gamepad in upper portion of icon (moved up)
    $emojiY = [int]($Size * 0.02)
    $emojiHeight = [int]($Size * 0.35)
    $emojiRect = New-Object System.Drawing.RectangleF(0, $emojiY, $Size, $emojiHeight)

    # Draw gamepad emoji 🎮
    $graphics.DrawString([char]::ConvertFromUtf32(0x1F3AE), $emojiFont, $textBrush, $emojiRect, $stringFormat)
    $emojiFont.Dispose()

    # If static text is provided (for plugin/category icons), draw it below the gamepad
    if ($StaticText) {
        $fontSize = [int]($Size * 0.11)
        $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold)

        $textY = [int]($Size * 0.48)
        $textHeight = [int]($Size * 0.50)
        $rect = New-Object System.Drawing.RectangleF(0, $textY, $Size, $textHeight)

        $graphics.DrawString($StaticText, $font, $textBrush, $rect, $stringFormat)
        $font.Dispose()
    }

    # Save
    $filePath = Join-Path $imagesPath "$Name.png"
    $bitmap.Save($filePath, [System.Drawing.Imaging.ImageFormat]::Png)

    # Cleanup
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
        [string]$BackgroundColor = "#2D2D2D",
        [string]$TextColor = "#FFFFFF",
        [string]$StaticText = $null
    )

    # Create 72x72 (standard) and 144x144 (@2x retina)
    New-Icon -Name $Name -BackgroundColor $BackgroundColor -TextColor $TextColor -Size 72 -StaticText $StaticText
    New-Icon -Name "${Name}@2x" -BackgroundColor $BackgroundColor -TextColor $TextColor -Size 144 -StaticText $StaticText
}

Write-Host ""
Write-Host "Generating XOutputRedux Stream Deck icons..." -ForegroundColor Cyan
Write-Host ""

# Plugin and category icons - Xbox green theme (these have static text)
$xboxGreen = "#107C10"

New-IconSet -Name "pluginIcon" -BackgroundColor $xboxGreen -StaticText "XOutput`nRedux"
New-IconSet -Name "categoryIcon" -BackgroundColor $xboxGreen -StaticText "XO"

# Action icons - gamepad only, all text is dynamic
New-IconSet -Name "profileAction" -BackgroundColor "#4A4A4A"
New-IconSet -Name "monitorAction" -BackgroundColor "#4A4A4A"
New-IconSet -Name "launchAppAction" -BackgroundColor "#6F42C1"

Write-Host ""
Write-Host "Done! Icons created in: $imagesPath" -ForegroundColor Green
Write-Host ""
