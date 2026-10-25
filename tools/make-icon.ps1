Add-Type -AssemblyName System.Drawing

$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

$path = New-RoundedRect 16 16 224 224 58
$bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.PointF(0, 16)),
    (New-Object System.Drawing.PointF(0, 240)),
    [System.Drawing.Color]::FromArgb(255, 20, 26, 46),
    [System.Drawing.Color]::FromArgb(255, 4, 5, 12))
$g.FillPath($bg, $path)

$glow = New-Object System.Drawing.Drawing2D.GraphicsPath
$glow.AddEllipse(48, 120, 160, 130)
$glowBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush($glow)
$glowBrush.CenterColor = [System.Drawing.Color]::FromArgb(120, 45, 91, 255)
$glowBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(0, 25, 184, 255))
$g.FillPath($glowBrush, $glow)

$eyeBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.PointF(0, 90)),
    (New-Object System.Drawing.PointF(0, 170)),
    [System.Drawing.Color]::White,
    [System.Drawing.Color]::FromArgb(255, 150, 220, 255))
$g.FillEllipse($eyeBrush, 74, 96, 40, 66)
$g.FillEllipse($eyeBrush, 142, 96, 40, 66)

$g.Dispose()

$png = New-Object System.IO.MemoryStream
$bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
$data = $png.ToArray()
$png.Dispose()

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $PSScriptRoot) | Out-Null
$out = Join-Path (Join-Path (Split-Path -Parent $PSScriptRoot) 'Assets') 'nusa.ico'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $out) | Out-Null

$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]1)
$bw.Write([Byte]0)
$bw.Write([Byte]0)
$bw.Write([Byte]0)
$bw.Write([Byte]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]32)
$bw.Write([UInt32]$data.Length)
$bw.Write([UInt32]22)
$bw.Write($data)
$bw.Flush()
$bw.Close()
Write-Output "icon written: $out ($($data.Length) bytes png)"
