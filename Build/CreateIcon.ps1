Add-Type -AssemblyName System.Drawing

# Create multiple sizes for the icon
$sizes = @(16, 32, 48, 256)
$bitmaps = @()

foreach ($size in $sizes) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # Background - rounded blue square
    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(59, 130, 246))  # Blue #3b82f6
    $margin = [int]($size * 0.1)
    $rectSize = $size - ($margin * 2)
    $radius = [int]($size * 0.15)

    # Draw rounded rectangle background
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $rect = New-Object System.Drawing.Rectangle($margin, $margin, $rectSize, $rectSize)
    $path.AddArc($rect.X, $rect.Y, $radius * 2, $radius * 2, 180, 90)
    $path.AddArc($rect.Right - $radius * 2, $rect.Y, $radius * 2, $radius * 2, 270, 90)
    $path.AddArc($rect.Right - $radius * 2, $rect.Bottom - $radius * 2, $radius * 2, $radius * 2, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $radius * 2, $radius * 2, $radius * 2, 90, 90)
    $path.CloseFigure()
    $graphics.FillPath($bgBrush, $path)

    # Draw "i" letter in white
    $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $fontSize = [int]($size * 0.55)
    $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold)

    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center

    $textRect = New-Object System.Drawing.RectangleF(0, ($size * 0.05), $size, $size)
    $graphics.DrawString("i", $font, $textBrush, $textRect, $sf)

    $graphics.Dispose()
    $bitmaps += $bitmap
}

# Save as ICO file
$iconPath = Join-Path $PSScriptRoot "..\InformationBox\Assets\app.ico"

# Create ICO file manually
$ms = New-Object System.IO.MemoryStream

# ICO header
$writer = New-Object System.IO.BinaryWriter($ms)
$writer.Write([UInt16]0)      # Reserved
$writer.Write([UInt16]1)      # Type (1 = ICO)
$writer.Write([UInt16]$sizes.Count)  # Number of images

# Calculate offsets
$headerSize = 6 + (16 * $sizes.Count)
$currentOffset = $headerSize

# Write directory entries and collect PNG data
$pngDataList = @()
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $pngStream = New-Object System.IO.MemoryStream
    $bitmaps[$i].Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngData = $pngStream.ToArray()
    $pngDataList += ,$pngData

    $width = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
    $height = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }

    $writer.Write([byte]$width)        # Width
    $writer.Write([byte]$height)       # Height
    $writer.Write([byte]0)             # Color palette
    $writer.Write([byte]0)             # Reserved
    $writer.Write([UInt16]1)           # Color planes
    $writer.Write([UInt16]32)          # Bits per pixel
    $writer.Write([UInt32]$pngData.Length)  # Size
    $writer.Write([UInt32]$currentOffset)   # Offset

    $currentOffset += $pngData.Length
}

# Write PNG data
foreach ($pngData in $pngDataList) {
    $writer.Write($pngData)
}

# Save to file
[System.IO.File]::WriteAllBytes($iconPath, $ms.ToArray())

$writer.Dispose()
$ms.Dispose()
foreach ($b in $bitmaps) { $b.Dispose() }

Write-Host "Created generic Information Box icon at $iconPath"
