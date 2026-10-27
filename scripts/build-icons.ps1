# Builds the multi-size Windows .ico files from the supplied PNGs in src/CommunitiesForever.Upload.App/Assets/Icons/png.
# Each .ico stores the PNGs unchanged (PNG-compressed entries, Windows Vista and later).
# Run after the icon set changes; the .ico files are committed.
$ErrorActionPreference = 'Stop'
$icons = Join-Path $PSScriptRoot '..\src\CommunitiesForever.Upload.App\Assets\Icons'
$png = Join-Path $icons 'png'

function Write-Ico([string]$Target, [string[]]$Sources) {
    $images = $Sources | ForEach-Object { , [IO.File]::ReadAllBytes((Join-Path $png $_)) }
    $stream = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter $stream
    $w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($bytes in $images) {
        # PNG IHDR: width and height are big-endian at bytes 16..23
        $width = ($bytes[16] -shl 24) -bor ($bytes[17] -shl 16) -bor ($bytes[18] -shl 8) -bor $bytes[19]
        $size = if ($width -ge 256) { 0 } else { $width }
        $w.Write([byte]$size); $w.Write([byte]$size); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]$bytes.Length); $w.Write([UInt32]$offset)
        $offset += $bytes.Length
    }
    foreach ($bytes in $images) { $w.Write($bytes) }
    [IO.File]::WriteAllBytes((Join-Path $icons $Target), $stream.ToArray())
    Write-Host "$Target ($($images.Count) sizes)"
}

# App and taskbar: the small variant (thicker lines) up to 32 px, the full icon from 40 px.
Write-Ico 'app.ico' @('app-icon-small-16.png', 'app-icon-small-20.png', 'app-icon-small-24.png', 'app-icon-small-32.png',
    'app-icon-40.png', 'app-icon-48.png', 'app-icon-64.png', 'app-icon-96.png', 'app-icon-128.png', 'app-icon-256.png')

# Tray states (UP-08): watching, paused, needs attention, uploading frames 1..4.
$sizes = 16, 20, 24, 32, 64
foreach ($state in 'tray', 'tray-paused', 'tray-attention', 'tray-uploading-1', 'tray-uploading-2', 'tray-uploading-3', 'tray-uploading-4') {
    Write-Ico "$state.ico" ($sizes | ForEach-Object { "$state-$_.png" })
}
