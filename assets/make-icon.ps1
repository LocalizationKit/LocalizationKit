# Draws the package icon with a real font rasterizer: a bold A under a teal tilde. The accent is the
# point of the mark — a glyph that a localizer has been through — so the brackets are gone.
Add-Type -AssemblyName System.Drawing

$size = 128
$plate = [System.Drawing.Color]::FromArgb(255, 14, 42, 41)   # deep teal
$ink = [System.Drawing.Color]::FromArgb(255, 240, 247, 246)  # near-white letter
$accent = [System.Drawing.Color]::FromArgb(255, 79, 209, 199) # the editor's accent, for the tilde

$bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.Clear([System.Drawing.Color]::Transparent)

# The plate: a rounded square, so the icon has a shape of its own on both a light and a dark listing.
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

$plated = New-RoundedRect 0 0 $size $size 26
$brush = New-Object System.Drawing.SolidBrush $plate
$g.FillPath($brush, $plated)

# The letter, placed by its own outline rather than by a text baseline, so it sits optically centred.
$letter = New-Object System.Drawing.Drawing2D.GraphicsPath
$family = New-Object System.Drawing.FontFamily "Segoe UI"
$format = [System.Drawing.StringFormat]::GenericTypographic
$letter.AddString("A", $family, [int][System.Drawing.FontStyle]::Bold, 80, (New-Object System.Drawing.PointF 0, 0), $format)
$b = $letter.GetBounds()
$shift = New-Object System.Drawing.Drawing2D.Matrix
$shift.Translate((64 - ($b.X + $b.Width / 2)), (84 - ($b.Y + $b.Height / 2)))
$letter.Transform($shift)
$inkBrush = New-Object System.Drawing.SolidBrush $ink
$g.FillPath($inkBrush, $letter)

# The tilde: two mirrored curves drawn as one stroke, thick enough to survive a 32px listing.
$tilde = New-Object System.Drawing.Drawing2D.GraphicsPath
$tilde.AddBezier(36, 30, 46, 15, 56, 15, 64, 26)
$tilde.AddBezier(64, 26, 72, 37, 82, 37, 92, 22)
$pen = New-Object System.Drawing.Pen $accent, 11
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$g.DrawPath($pen, $tilde)

$g.Dispose()
$target = "C:\Users\FrancescoMazzella\Code\repos\LocalizationKit\assets\icon.png"
$bitmap.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)

# A 90px copy for the VSIX manifest, scaled from the same drawing so the two stay in step.
$small = New-Object System.Drawing.Bitmap 90, 90
$sg = [System.Drawing.Graphics]::FromImage($small)
$sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$sg.DrawImage($bitmap, 0, 0, 90, 90)
$sg.Dispose()
$small.Save("C:\Users\FrancescoMazzella\Code\repos\LocalizationKit\tools\LocalizationKit.VsExtension\icon.png",
  [System.Drawing.Imaging.ImageFormat]::Png)

$bitmap.Dispose()
Write-Host "wrote $target ((Get-Item $target).Length bytes)"
Get-Item $target | Select-Object FullName, Length
