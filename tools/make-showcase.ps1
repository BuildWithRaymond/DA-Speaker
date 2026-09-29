# Render repository showcase layouts around unmodified application captures.
# Run from any directory on Windows: powershell -File tools/make-showcase.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repoRoot = Split-Path $PSScriptRoot -Parent
$imageRoot = Join-Path $repoRoot 'docs/images'
$screenRoot = Join-Path $imageRoot 'screenshots'

function Paint-Text([System.Drawing.Graphics]$canvas, [string]$value, [float]$x, [float]$y, [float]$size, [string]$color, [string]$family = 'Segoe UI') {
    $font = [System.Drawing.Font]::new($family, $size, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($color))
    $canvas.DrawString($value, $font, $brush, $x, $y)
    $brush.Dispose()
    $font.Dispose()
}

function Paint-Screen([System.Drawing.Graphics]$canvas, [string]$filename, [float]$x, [float]$y, [float]$width) {
    $capture = [System.Drawing.Image]::FromFile((Join-Path $screenRoot $filename))
    $height = $capture.Height * $width / $capture.Width
    $shadow = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(110, 0, 0, 0))
    $canvas.FillRectangle($shadow, $x - 12, $y + 15, $width + 24, $height + 14)
    $canvas.DrawImage($capture, [System.Drawing.RectangleF]::new($x, $y, $width, $height))
    $border = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#3A3831'), 1)
    $canvas.DrawRectangle($border, $x, $y, $width, $height)
    $border.Dispose()
    $shadow.Dispose()
    $capture.Dispose()
}

function New-Canvas([int]$width, [int]$height) {
    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $canvas = [System.Drawing.Graphics]::FromImage($bitmap)
    $canvas.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $canvas.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $canvas.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $canvas.Clear([System.Drawing.ColorTranslator]::FromHtml('#101113'))
    $field = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Point]::new(0, 0), [System.Drawing.Point]::new($width, $height), [System.Drawing.ColorTranslator]::FromHtml('#191813'), [System.Drawing.ColorTranslator]::FromHtml('#0B0B0D'))
    $canvas.FillRectangle($field, 0, 0, $width, $height)
    $field.Dispose()
    $rule = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#393328'), 1)
    $canvas.DrawLine($rule, 80, 85, $width - 80, 85)
    $rule.Dispose()
    Paint-Text $canvas 'DA SPEAKER' 80 42 20 '#D8B878'
    Paint-Text $canvas 'DARK AGES / WINDOWS' ($width - 370) 45 16 '#A6A5A1'
    return @{ Bitmap = $bitmap; Canvas = $canvas }
}

$hero = New-Canvas 1600 1400
Paint-Text $hero.Canvas 'Your words. Your pace.' 76 119 80 '#EFEADF' 'Georgia'
Paint-Text $hero.Canvas 'Prepare your script. Preview every message. Speak when you are ready.' 81 223 26 '#A6A5A0'
Paint-Screen $hero.Canvas 'main.png' 160 326 1280
Paint-Text $hero.Canvas 'SCRIPT EDITOR  /  EXACT PREVIEW  /  PACED PLAYBACK' 160 1328 17 '#D8B878'
Paint-Text $hero.Canvas 'Open source. Made for your desktop.' 1130 1328 17 '#A6A5A0'
$hero.Bitmap.Save((Join-Path $imageRoot 'showcase.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$hero.Canvas.Dispose()
$hero.Bitmap.Dispose()

$detail = New-Canvas 1600 1190
Paint-Text $detail.Canvas 'A quieter way to play.' 76 119 72 '#EFEADF' 'Georgia'
Paint-Text $detail.Canvas 'Keep your script close. Keep the controls simple.' 81 217 26 '#A6A5A0'
Paint-Text $detail.Canvas 'ROOM FOR YOUR GAME' 82 302 17 '#D8B878'
Paint-Text $detail.Canvas 'Compact preview, with playback always in reach.' 82 336 21 '#A6A5A0'
Paint-Text $detail.Canvas 'SETTINGS, WHEN YOU NEED THEM' 920 302 17 '#D8B878'
Paint-Text $detail.Canvas 'Shortcuts, timing, and a little fine-tuning.' 920 336 21 '#A6A5A0'
Paint-Screen $detail.Canvas 'compact.png' 86 406 710
Paint-Screen $detail.Canvas 'settings.png' 920 406 580
Paint-Text $detail.Canvas 'Actual application screens with a sample script.' 82 1134 18 '#8F939D'
Paint-Text $detail.Canvas 'DA Speaker 1.1.0' 1350 1134 18 '#D8B878'
$detail.Bitmap.Save((Join-Path $imageRoot 'controls.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$detail.Canvas.Dispose()
$detail.Bitmap.Dispose()
Write-Output 'Created docs/images/showcase.png and docs/images/controls.png'
