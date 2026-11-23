# Generates assets\KeepAwake.ico (multi-size, PNG-compressed entries).
# The logo is the same design the tray icon draws at runtime:
# a computer-mouse silhouette with motion chevrons on a teal gradient badge.
#
# Usage: powershell -ExecutionPolicy Bypass -File tools\GenerateIcon.ps1

param(
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\assets\KeepAwake.ico')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, $r, $r, 180.0, 90.0)
    $p.AddArc($x + $w - $r, $y, $r, $r, 270.0, 90.0)
    $p.AddArc($x + $w - $r, $y + $h - $r, $r, $r, 0.0, 90.0)
    $p.AddArc($x, $y + $h - $r, $r, $r, 90.0, 90.0)
    $p.CloseFigure()
    return $p
}

function New-MouseBodyPath([float]$x, [float]$y, [float]$w, [float]$h) {
    $domeH = $w * 0.62
    $r = 3.0
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, $w, $domeH, 180.0, 180.0)
    $p.AddLine($x + $w, $y + $domeH / 2.0, $x + $w, $y + $h - $r)
    $p.AddArc($x + $w - $r, $y + $h - $r, $r, $r, 0.0, 90.0)
    $p.AddLine($x + $w - $r, $y + $h, $x + $r, $y + $h)
    $p.AddArc($x, $y + $h - $r, $r, $r, 90.0, 90.0)
    $p.CloseFigure()
    return $p
}

function New-LogoBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $scale = $size / 32.0
    $g.ScaleTransform($scale, $scale)

    # Badge: rounded square with diagonal gradient (dark blue -> teal)
    $bg = New-RoundedRectPath 1 1 30 30 8
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Rectangle(0, 0, 32, 32)),
        [System.Drawing.Color]::FromArgb(30, 58, 95),
        [System.Drawing.Color]::FromArgb(38, 166, 154),
        45.0)
    $g.FillPath($brush, $bg)
    $brush.Dispose(); $bg.Dispose()

    # Mouse silhouette
    $mx = 5.0; $my = 6.0; $mw = 13.0; $mh = 19.5
    $body = New-MouseBodyPath $mx $my $mw $mh
    $bodyBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(250, 250, 255))
    $g.FillPath($bodyBrush, $body)
    $bodyPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(20, 35, 52), [float]1.1)
    $g.DrawPath($bodyPen, $body)
    $body.Dispose(); $bodyBrush.Dispose(); $bodyPen.Dispose()

    # Split line + orange scroll wheel
    $domeH = $mw * 0.62
    $linePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(20, 35, 52), [float]1.0)
    $g.DrawLine($linePen, $mx + $mw / 2.0, $my + 0.5, $mx + $mw / 2.0, $my + $domeH / 2.0 + 0.5)
    $linePen.Dispose()
    $wheel = New-RoundedRectPath ($mx + $mw / 2.0 - 1.4) ($my + 1.3) 2.8 4.0 1.4
    $wheelBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 140, 66))
    $g.FillPath($wheelBrush, $wheel)
    $wheel.Dispose(); $wheelBrush.Dispose()

    # Motion chevrons
    $chevPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 209, 102), [float]2.4)
    $chevPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $chevPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $chevPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    foreach ($cx in @(21.0, 26.2)) {
        $chev = New-Object System.Drawing.Drawing2D.GraphicsPath
        $chev.AddLine($cx, 12.2, $cx + 3.4, 16.0)
        $chev.AddLine($cx + 3.4, 16.0, $cx, 19.8)
        $g.DrawPath($chevPen, $chev)
        $chev.Dispose()
    }
    $chevPen.Dispose()

    $g.Dispose()
    return $bmp
}

# Pack PNG frames into a .ico (PNG-compressed entries, supported since Vista)
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = New-Object System.Collections.ArrayList
foreach ($s in $sizes) {
    $bmp = New-LogoBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    [void]$frames.Add(@($s, $ms.ToArray()))
    $bmp.Dispose(); $ms.Dispose()
}

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$frames.Count)

$offset = 6 + 16 * $frames.Count
for ($i = 0; $i -lt $frames.Count; $i++) {
    $s = $frames[$i][0]
    $data = $frames[$i][1]
    $dim = 0; if ($s -lt 256) { $dim = $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim)   # width, height (0 = 256)
    $bw.Write([byte]0); $bw.Write([byte]0)          # color count, reserved
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)     # planes, bit count
    $bw.Write([UInt32]$data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
for ($i = 0; $i -lt $frames.Count; $i++) {
    $bw.Write($frames[$i][1])
}
$bw.Flush()

$fullPath = [System.IO.Path]::GetFullPath($OutputPath)
$dir = [System.IO.Path]::GetDirectoryName($fullPath)
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
[System.IO.File]::WriteAllBytes($fullPath, $out.ToArray())
$bw.Dispose(); $out.Dispose()

Write-Host "Wrote $fullPath ($($frames.Count) sizes: $($sizes -join ', '))"
