# Rebuild original Goodwin Labs vector mark into multi-resolution Windows icon and PNGs.
# Windows-only rendering; no external assets, fonts, downloads or drawing dependencies.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repoRoot = Split-Path $PSScriptRoot -Parent
$brandRoot = Join-Path $repoRoot 'branding'
$assetRoot = Join-Path $repoRoot 'src/LogLens.App/Assets'
New-Item -ItemType Directory -Force $brandRoot,$assetRoot | Out-Null
$svg = @'
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" role="img" aria-labelledby="title">
  <title id="title">LogLens — a lens revealing diagnostic log entries</title>
  <rect x="8" y="8" width="240" height="240" rx="52" fill="#101d30"/>
  <path d="M158 155L211 208" fill="none" stroke="#83e6cc" stroke-width="24" stroke-linecap="round"/>
  <circle cx="111" cy="108" r="65" fill="#101d30" stroke="#f0f5fc" stroke-width="12"/>
  <path d="M78 86H132M78 108H144M78 130H118" fill="none" stroke="#83e6cc" stroke-width="9" stroke-linecap="round"/>
</svg>
'@
Set-Content -LiteralPath (Join-Path $brandRoot 'LogLens.svg') -Value $svg -Encoding utf8
function New-LogoPng([int]$Size) {
    $bitmap = [Drawing.Bitmap]::new($Size,$Size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.ScaleTransform($Size / 256.0, $Size / 256.0)
        $navy = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#101d30'))
        $teal = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#83e6cc'),24)
        $white = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#f0f5fc'),12)
        $lines = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#83e6cc'),9)
        $shape = [Drawing.Drawing2D.GraphicsPath]::new()
        try {
            $shape.AddArc(8,8,104,104,180,90); $shape.AddArc(144,8,104,104,270,90)
            $shape.AddArc(144,144,104,104,0,90); $shape.AddArc(8,144,104,104,90,90); $shape.CloseFigure()
            $graphics.FillPath($navy,$shape)
            $teal.StartCap = $teal.EndCap = [Drawing.Drawing2D.LineCap]::Round
            $lines.StartCap = $lines.EndCap = [Drawing.Drawing2D.LineCap]::Round
            $graphics.DrawLine($teal,158,155,211,208)
            $graphics.FillEllipse($navy,46,43,130,130); $graphics.DrawEllipse($white,46,43,130,130)
            $graphics.DrawLine($lines,78,86,132,86); $graphics.DrawLine($lines,78,108,144,108); $graphics.DrawLine($lines,78,130,118,130)
            $stream = [IO.MemoryStream]::new()
            try { $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png); return ,$stream.ToArray() } finally { $stream.Dispose() }
        } finally { $shape.Dispose(); $lines.Dispose(); $white.Dispose(); $teal.Dispose(); $navy.Dispose() }
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}
[IO.File]::WriteAllBytes((Join-Path $brandRoot 'LogLens-1024.png'), (New-LogoPng 1024))
[IO.File]::WriteAllBytes((Join-Path $assetRoot 'LogLens.png'), (New-LogoPng 256))
$sizes = @(16,20,24,32,40,48,64,128,256)
$frames = @($sizes | ForEach-Object { ,(New-LogoPng $_) })
$ico = [IO.MemoryStream]::new(); $writer = [IO.BinaryWriter]::new($ico)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for($i=0; $i -lt $sizes.Count; $i++) {
        $dimension = if($sizes[$i] -eq 256){0}else{$sizes[$i]}
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach($frame in $frames){$writer.Write([byte[]]$frame)}
    $writer.Flush(); [IO.File]::WriteAllBytes((Join-Path $assetRoot 'LogLens.ico'), $ico.ToArray())
} finally { $writer.Dispose(); $ico.Dispose() }
Copy-Item -LiteralPath (Join-Path $assetRoot 'LogLens.ico') -Destination $brandRoot -Force
