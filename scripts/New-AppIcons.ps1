# Draws WinThunar's icon (an original design: a blue folder with a lightning bolt) and writes
# AppIcon.ico plus every MSIX logo asset. The artwork is defined here on a 256-unit grid, so
# edit the shapes below and re-run rather than editing the images.
#
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\New-AppIcons.ps1

param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\Assets')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedRectangle([float]$x, [float]$y, [float]$width, [float]$height, [float]$radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $radius * 2
    $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
    $path.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
    $path.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-Color([string]$hex) {
    return [System.Drawing.ColorTranslator]::FromHtml($hex)
}

# Draws the icon into a square of the given size whose top-left corner is (left, top).
function Draw-Icon([System.Drawing.Graphics]$graphics, [float]$left, [float]$top, [float]$size) {
    $state = $graphics.Save()
    $graphics.TranslateTransform($left, $top)
    $graphics.ScaleTransform($size / 256, $size / 256)

    # Back panel with the folder tab.
    $back = New-RoundedRectangle 18 50 220 166 20
    $tab = New-RoundedRectangle 18 34 100 44 16
    $backBrush = New-Object System.Drawing.SolidBrush (New-Color '#1E3A8A')
    $graphics.FillPath($backBrush, $back)
    $graphics.FillPath($backBrush, $tab)

    # Front panel, lighter at the top.
    $front = New-RoundedRectangle 18 82 220 142 20
    $frontBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF 0, 82),
        (New-Object System.Drawing.PointF 0, 224),
        (New-Color '#4F8EF7'),
        (New-Color '#1D4ED8'))
    $graphics.FillPath($frontBrush, $front)

    # Lightning bolt, outlined so it still reads at 16 px.
    $bolt = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bolt.AddPolygon([System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF 130, 94),
        (New-Object System.Drawing.PointF 174, 94),
        (New-Object System.Drawing.PointF 147, 140),
        (New-Object System.Drawing.PointF 174, 140),
        (New-Object System.Drawing.PointF 100, 218),
        (New-Object System.Drawing.PointF 118, 160),
        (New-Object System.Drawing.PointF 86, 160)))
    $boltBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF 0, 94),
        (New-Object System.Drawing.PointF 0, 216),
        (New-Color '#FFE45C'),
        (New-Color '#F5B70A'))
    $outline = New-Object System.Drawing.Pen ((New-Color '#7C4A03'), 7)
    $outline.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawPath($outline, $bolt)
    $graphics.FillPath($boltBrush, $bolt)

    foreach ($disposable in @($back, $tab, $backBrush, $front, $frontBrush, $bolt, $boltBrush, $outline)) {
        $disposable.Dispose()
    }

    $graphics.Restore($state)
}

# Renders a PNG of width x height with the icon centered at iconSize.
function New-IconPng([int]$width, [int]$height, [float]$iconSize) {
    $bitmap = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)
    Draw-Icon $graphics (($width - $iconSize) / 2) (($height - $iconSize) / 2) $iconSize
    $graphics.Dispose()

    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    return ,$stream.ToArray()
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

# MSIX logos: name, width, height, icon size inside the canvas.
$logos = @(
    @('Square44x44Logo.scale-200.png', 88, 88, 88),
    @('Square44x44Logo.targetsize-24_altform-unplated.png', 24, 24, 24),
    @('Square44x44Logo.targetsize-48_altform-lightunplated.png', 48, 48, 48),
    @('Square150x150Logo.scale-200.png', 300, 300, 200),
    @('Wide310x150Logo.scale-200.png', 620, 300, 200),
    @('SplashScreen.scale-200.png', 1240, 600, 320),
    @('LockScreenLogo.scale-200.png', 48, 48, 48),
    @('StoreLogo.png', 50, 50, 50)
)
foreach ($logo in $logos) {
    $bytes = New-IconPng $logo[1] $logo[2] $logo[3]
    [System.IO.File]::WriteAllBytes((Join-Path $OutputDirectory $logo[0]), $bytes)
}

# AppIcon.ico with PNG-compressed entries (supported by Windows Vista and later).
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$images = foreach ($size in $sizes) { ,(New-IconPng $size $size $size) }
$icon = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $icon
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($index = 0; $index -lt $sizes.Count; $index++) {
    $dimension = if ($sizes[$index] -ge 256) { 0 } else { $sizes[$index] }
    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$images[$index].Length)
    $writer.Write([uint32]$offset)
    $offset += $images[$index].Length
}
foreach ($image in $images) {
    $writer.Write([byte[]]$image)
}
$writer.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $OutputDirectory 'AppIcon.ico'), $icon.ToArray())
$writer.Dispose()

Write-Host "Wrote $($logos.Count) logos and AppIcon.ico ($($sizes -join ', ') px) to $OutputDirectory"
