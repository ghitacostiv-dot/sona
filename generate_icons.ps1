# generate_icons.ps1
# Generates PNG icons using Segoe MDL2 Assets / Segoe Fluent Icons (built into Windows 10/11)
# Falls back to Segoe UI Symbol if Fluent is not available.
Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Continue'

# Ensure directories exist
@('nav', 'player', 'window', 'actions', 'categories') | ForEach-Object {
    New-Item -ItemType Directory -Force -Path "Resources\Icons\$_" | Out-Null
}

# ── Font selection ────────────────────────────────────────────────────────────
function Get-IconFont([int]$size) {
    $preferred = @('Segoe Fluent Icons', 'Segoe MDL2 Assets', 'Segoe UI Symbol', 'Arial Unicode MS')
    foreach ($name in $preferred) {
        try {
            $f = [System.Drawing.Font]::new($name, $size, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
            if ($f.Name -eq $name) { return $f }
        }
        catch {}
    }
    return [System.Drawing.Font]::new('Arial', $size, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
}

# ── Core render function ──────────────────────────────────────────────────────
function Render-Icon {
    param(
        [string]$OutPath,
        [string]$Char,        # Unicode codepoint or literal char
        [int]$Size = 24,
        [string]$FgHex = '#FFFFFF',
        [string]$BgHex = '#00000000'  # transparent
    )

    $bmp = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    # Parse colours
    $fg = [System.Drawing.ColorTranslator]::FromHtml($FgHex)
    $brush = [System.Drawing.SolidBrush]::new($fg)

    # Font size tuning: MDL2/Fluent icons look best slightly larger than bitmap
    $font = Get-IconFont ([int]($Size * 0.75))

    # Measure & centre
    $sf = [System.Drawing.StringFormat]::new()
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $rect = [System.Drawing.RectangleF]::new(0, 0, $Size, $Size)
    $g.DrawString($Char, $font, $brush, $rect, $sf)

    $dir = Split-Path $OutPath
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose(); $font.Dispose(); $brush.Dispose()
    Write-Host "  $OutPath"
}

Write-Host "`n=== Generating SONA Icons ==="
Write-Host "Font stack: Segoe Fluent Icons > Segoe MDL2 Assets > Segoe UI Symbol`n"

# ── ICON MAP ─────────────────────────────────────────────────────────────────
# Format: OutPath, Char (Unicode escape OR literal), Size
# Segoe MDL2 / Fluent icon codepoints (decimal) are used where Segoe MDL2 supports them.
# Chars list: https://docs.microsoft.com/en-us/windows/apps/design/style/segoe-ui-symbol-font
# Common MDL2 codepoints:
#   Home=0xE80F, Search=0xE721, Settings=0xE713, Play=0xE768, Pause=0xE769,
#   Next=0xE893, Prev=0xE892, Shuffle=0xE8B1, Repeat=0xE8EE, Volume=0xE767,
#   Minimize=0xE921, Maximize=0xE922, Close=0xE711
#   Download=0xE896, Globe=0xE909, Phone=0xE8EA, Book=0xE736, Library=0xE8F1
#   Games=0xE7FC, Music=0xEC4F, Movies=0xE8B2, Radio=0xE8D6, TV=0xE7F4
#   Bot=0xE99A, Shield=0xEA18, Archive=0xE7B8, Broom=0xEA99, Filter=0xE71C
#   Flash=0xE945, Delete=0xE74D, Add=0xE710, Logs=0xE9F9, Info=0xE946
#   Scan=0xEE6F, Warning=0xE7BA, Heart=0xEB51, Queue=0xE90B, Gear=0xE713
#   Podcasts=0xE93E, Food=0xEAA0, Star=0xE735, Headphones=0xE9A8

$W = '#FFFFFF'
$icons = @(
    # NAV
    @{p = 'Resources\Icons\nav\home.png'      ; c = [char]0xE80F; s = 24 }
    @{p = 'Resources\Icons\nav\games.png'     ; c = [char]0xE7FC; s = 24 }
    @{p = 'Resources\Icons\nav\movies.png'    ; c = [char]0xE8B2; s = 24 }
    @{p = 'Resources\Icons\nav\music.png'     ; c = [char]0xEC4F; s = 24 }
    @{p = 'Resources\Icons\nav\radio.png'     ; c = [char]0xE8D6; s = 24 }
    @{p = 'Resources\Icons\nav\tv.png'        ; c = [char]0xE7F4; s = 24 }
    @{p = 'Resources\Icons\nav\anime.png'     ; c = [char]0xE735; s = 24 }
    @{p = 'Resources\Icons\nav\manga.png'     ; c = [char]0xE736; s = 24 }
    @{p = 'Resources\Icons\nav\books.png'     ; c = [char]0xE8F1; s = 24 }
    @{p = 'Resources\Icons\nav\podcasts.png'  ; c = [char]0xE93E; s = 24 }
    @{p = 'Resources\Icons\nav\audiobooks.png'; c = [char]0xE9A8; s = 24 }
    @{p = 'Resources\Icons\nav\recipes.png'   ; c = [char]0xEAA0; s = 24 }
    @{p = 'Resources\Icons\nav\modapks.png'   ; c = [char]0xE8EA; s = 24 }
    @{p = 'Resources\Icons\nav\programs.png'  ; c = [char]0xE737; s = 24 }
    @{p = 'Resources\Icons\nav\torrents.png'  ; c = [char]0xE896; s = 24 }
    @{p = 'Resources\Icons\nav\ai.png'        ; c = [char]0xE99A; s = 24 }
    @{p = 'Resources\Icons\nav\hacking.png'   ; c = [char]0xEA18; s = 24 }
    @{p = 'Resources\Icons\nav\browser.png'   ; c = [char]0xE909; s = 24 }
    @{p = 'Resources\Icons\nav\install.png'   ; c = [char]0xE7B8; s = 24 }
    @{p = 'Resources\Icons\nav\debloat.png'   ; c = [char]0xEA99; s = 24 }
    @{p = 'Resources\Icons\nav\settings.png'  ; c = [char]0xE713; s = 24 }

    # PLAYER (use filled/bold codepoints for better look at small sizes)
    @{p = 'Resources\Icons\player\play.png'     ; c = [char]0xE768; s = 24 }
    @{p = 'Resources\Icons\player\pause.png'    ; c = [char]0xE769; s = 24 }
    @{p = 'Resources\Icons\player\next.png'     ; c = [char]0xE893; s = 24 }
    @{p = 'Resources\Icons\player\prev.png'     ; c = [char]0xE892; s = 24 }
    @{p = 'Resources\Icons\player\shuffle.png'  ; c = [char]0xE8B1; s = 24 }
    @{p = 'Resources\Icons\player\repeat.png'   ; c = [char]0xE8EE; s = 24 }
    @{p = 'Resources\Icons\player\volume.png'   ; c = [char]0xE767; s = 24 }
    @{p = 'Resources\Icons\player\volume_mute.png'; c = [char]0xE74F; s = 24 }

    # WINDOW CHROME (smaller)
    @{p = 'Resources\Icons\window\minimize.png'; c = [char]0xE921; s = 16 }
    @{p = 'Resources\Icons\window\maximize.png'; c = [char]0xE922; s = 16 }
    @{p = 'Resources\Icons\window\close.png'   ; c = [char]0xE711; s = 16 }
    @{p = 'Resources\Icons\window\restore.png' ; c = [char]0xE923; s = 16 }

    # ACTIONS
    @{p = 'Resources\Icons\actions\search.png'   ; c = [char]0xE721; s = 24 }
    @{p = 'Resources\Icons\actions\download.png' ; c = [char]0xE896; s = 24 }
    @{p = 'Resources\Icons\actions\install.png'  ; c = [char]0xE945; s = 24 }
    @{p = 'Resources\Icons\actions\uninstall.png'; c = [char]0xE74D; s = 24 }
    @{p = 'Resources\Icons\actions\add.png'      ; c = [char]0xE710; s = 24 }
    @{p = 'Resources\Icons\actions\logs.png'     ; c = [char]0xE9F9; s = 24 }
    @{p = 'Resources\Icons\actions\filter.png'   ; c = [char]0xE71C; s = 24 }
    @{p = 'Resources\Icons\actions\check.png'    ; c = [char]0xE73E; s = 24 }
    @{p = 'Resources\Icons\actions\scan.png'     ; c = [char]0xEE6F; s = 24 }
    @{p = 'Resources\Icons\actions\warning.png'  ; c = [char]0xE7BA; s = 24 }
    @{p = 'Resources\Icons\actions\gear.png'     ; c = [char]0xE713; s = 24 }
    @{p = 'Resources\Icons\actions\heart.png'    ; c = [char]0xEB51; s = 24 }
    @{p = 'Resources\Icons\actions\queue.png'    ; c = [char]0xE90B; s = 24 }
    @{p = 'Resources\Icons\actions\info.png'     ; c = [char]0xE946; s = 24 }
    @{p = 'Resources\Icons\actions\open_link.png'; c = [char]0xE8A7; s = 24 }
    @{p = 'Resources\Icons\actions\refresh.png'  ; c = [char]0xE72C; s = 24 }
    @{p = 'Resources\Icons\actions\close.png'    ; c = [char]0xE711; s = 24 }
    @{p = 'Resources\Icons\actions\edit.png'     ; c = [char]0xE70F; s = 24 }
    @{p = 'Resources\Icons\actions\copy.png'     ; c = [char]0xE8C8; s = 24 }
    @{p = 'Resources\Icons\actions\back.png'     ; c = [char]0xE72B; s = 24 }

    # CATEGORIES (larger 32px for homepage featured tiles)
    @{p = 'Resources\Icons\categories\games.png' ; c = [char]0xE7FC; s = 32 }
    @{p = 'Resources\Icons\categories\movies.png'; c = [char]0xE8B2; s = 32 }
    @{p = 'Resources\Icons\categories\music.png' ; c = [char]0xEC4F; s = 32 }
)

foreach ($ic in $icons) {
    Render-Icon -OutPath $ic.p -Char ([string]$ic.c) -Size $ic.s -FgHex '#FFFFFF'
}

Write-Host "`nDone! Generated $($icons.Count) icons."
