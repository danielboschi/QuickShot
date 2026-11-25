# Gera Assets\QuickShot.ico a partir do desenho vetorial abaixo. Execute em PowerShell STA.
param([string]$OutFile = (Join-Path $PSScriptRoot 'QuickShot.ico'), [string]$PreviewDir)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase

function New-IconBitmap([int]$size) {
    # Nos tamanhos da bandeja, as medidas sao em pixels inteiros para o traco ficar nitido.
    $hints = @{
        16 = @{ Margin = 3; Arm = 3; Stroke = 2; Dot = 2 }
        20 = @{ Margin = 4; Arm = 4; Stroke = 2; Dot = 2 }
        24 = @{ Margin = 5; Arm = 4; Stroke = 2; Dot = 3 }
        32 = @{ Margin = 7.5; Arm = 5; Stroke = 3; Dot = 3.5 }
        40 = @{ Margin = 9.5; Arm = 6; Stroke = 3; Dot = 4 }
    }
    $hint = $hints[$size]
    $unit = 64 / $size
    $margin = if ($hint) { $hint.Margin * $unit } else { 15.5 }
    $arm = if ($hint) { $hint.Arm * $unit } else { 9.5 }
    $stroke = if ($hint) { $hint.Stroke * $unit } else { 4.6 }
    $dot = if ($hint) { $hint.Dot * $unit } else { 6.0 }
    $inset = if ($size -le 40) { 0.0 } else { 2.0 }

    $culture = [Globalization.CultureInfo]::InvariantCulture
    $a = $margin; $b = 64 - $margin; $a2 = $a + $arm; $b2 = $b - $arm
    $path = [string]::Format($culture, 'M{0},{2} V{0} H{2} M{3},{0} H{1} V{2} M{1},{3} V{1} H{3} M{2},{1} H{0} V{3}', $a, $b, $a2, $b2)

    $gradient = [Windows.Media.LinearGradientBrush]::new([Windows.Media.Color]::FromRgb(0x38, 0x9B, 0xFF), [Windows.Media.Color]::FromRgb(0x6D, 0x3C, 0xF0), 50)
    $pen = [Windows.Media.Pen]::new([Windows.Media.Brushes]::White, $stroke)
    $cap = if ($hint) { 'Flat' } else { 'Round' }
    $pen.StartLineCap = $cap; $pen.EndLineCap = $cap; $pen.LineJoin = 'Round'

    $visual = [Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    $context.PushTransform([Windows.Media.ScaleTransform]::new($size / 64, $size / 64))
    $context.DrawRoundedRectangle($gradient, $null, [Windows.Rect]::new($inset, $inset, 64 - 2 * $inset, 64 - 2 * $inset), 15, 15)
    $context.DrawGeometry($null, $pen, [Windows.Media.Geometry]::Parse($path))
    $context.DrawEllipse([Windows.Media.Brushes]::White, $null, [Windows.Point]::new(32, 32), $dot, $dot)
    $context.Close()
    $target = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $target.Render($visual)
    [Windows.Media.Imaging.FormatConvertedBitmap]::new($target, [Windows.Media.PixelFormats]::Bgra32, $null, 0)
}

function ConvertTo-Png($bitmap) {
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.MemoryStream]::new(); $encoder.Save($stream)
    , $stream.ToArray()
}

function ConvertTo-IconDib($bitmap) {
    $size = $bitmap.PixelWidth; $stride = $size * 4
    $pixels = [byte[]]::new($stride * $size); $bitmap.CopyPixels($pixels, $stride, 0)
    $maskStride = [int]([math]::Ceiling($size / 32) * 4)
    $stream = [IO.MemoryStream]::new(); $writer = [IO.BinaryWriter]::new($stream)
    $writer.Write([int]40); $writer.Write([int]$size); $writer.Write([int]($size * 2))
    $writer.Write([int16]1); $writer.Write([int16]32); $writer.Write([int]0)
    $writer.Write([int]($stride * $size + $maskStride * $size))
    1..4 | ForEach-Object { $writer.Write([int]0) }
    for ($y = $size - 1; $y -ge 0; $y--) { $writer.Write($pixels, $y * $stride, $stride) }
    $writer.Write([byte[]]::new($maskStride * $size)); $writer.Flush()
    , $stream.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = @()
foreach ($size in $sizes) {
    $bitmap = New-IconBitmap $size
    $data = if ($size -eq 256) { ConvertTo-Png $bitmap } else { ConvertTo-IconDib $bitmap }
    $images += [pscustomobject]@{ Size = $size; Data = $data }
    if ($PreviewDir) { [IO.File]::WriteAllBytes((Join-Path $PreviewDir "icon-$size.png"), (ConvertTo-Png $bitmap)) }
}

$output = [IO.MemoryStream]::new(); $writer = [IO.BinaryWriter]::new($output)
$writer.Write([int16]0); $writer.Write([int16]1); $writer.Write([int16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $dimension = [byte]($image.Size % 256)
    $writer.Write($dimension); $writer.Write($dimension); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([int16]1); $writer.Write([int16]32); $writer.Write([int]$image.Data.Length); $writer.Write([int]$offset)
    $offset += $image.Data.Length
}
foreach ($image in $images) { $writer.Write([byte[]]$image.Data) }
$writer.Flush()
[IO.File]::WriteAllBytes($OutFile, $output.ToArray())
Write-Host "Icone gerado: $OutFile ($($output.Length) bytes)"
