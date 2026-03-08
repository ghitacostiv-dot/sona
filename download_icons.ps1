# download_icons.ps1 - Downloads Fluent UI System Icons for SONA
$ErrorActionPreference = 'Continue'

# Create subdirectories
$dirs = @(
    'Resources\Icons\nav',
    'Resources\Icons\player',
    'Resources\Icons\window',
    'Resources\Icons\actions',
    'Resources\Icons\categories'
)
foreach ($d in $dirs) {
    New-Item -ItemType Directory -Force -Path $d | Out-Null
}

# Helper: try to download. If it fails (icon name doesn't exist), silently catch.
function TryDownload([string]$url, [string]$dest) {
    try {
        $wc = [System.Net.WebClient]::new()
        $wc.DownloadFile($url, $dest)
        Write-Host "OK  $dest"
    } catch {
        Write-Host "SKIP $dest (not found)"
    }
}

$base = 'https://raw.githubusercontent.com/microsoft/fluentui-system-icons/main/assets/'

# Map: local path (relative) -> [asset_folder, file_name]
$icons = [ordered]@{
    # ── NAV ──────────────────────────────────────────────────────────
    'Resources\Icons\nav\home.png'        = 'Home/PNG/24/ic_fluent_home_24_regular.png'
    'Resources\Icons\nav\games.png'       = 'Games/PNG/24/ic_fluent_games_24_regular.png'
    'Resources\Icons\nav\movies.png'      = 'Movies/PNG/24/ic_fluent_movies_and_tv_24_regular.png'
    'Resources\Icons\nav\music.png'       = 'Music%20Note%201/PNG/24/ic_fluent_music_note_1_24_regular.png'
    'Resources\Icons\nav\radio.png'       = 'Radio/PNG/24/ic_fluent_radio_24_regular.png'
    'Resources\Icons\nav\tv.png'          = 'Television/PNG/24/ic_fluent_tv_24_regular.png'
    'Resources\Icons\nav\anime.png'       = 'Star/PNG/24/ic_fluent_star_24_regular.png'
    'Resources\Icons\nav\manga.png'       = 'Book/PNG/24/ic_fluent_book_24_regular.png'
    'Resources\Icons\nav\books.png'       = 'Library/PNG/24/ic_fluent_library_24_regular.png'
    'Resources\Icons\nav\podcasts.png'    = 'Headphones/PNG/24/ic_fluent_headphones_24_regular.png'
    'Resources\Icons\nav\audiobooks.png'  = 'Headphones%20Sound%20Wave/PNG/24/ic_fluent_headphones_sound_wave_24_regular.png'
    'Resources\Icons\nav\recipes.png'     = 'Food/PNG/24/ic_fluent_food_24_regular.png'
    'Resources\Icons\nav\modapks.png'     = 'Phone/PNG/24/ic_fluent_phone_24_regular.png'
    'Resources\Icons\nav\programs.png'    = 'Apps/PNG/24/ic_fluent_apps_24_regular.png'
    'Resources\Icons\nav\torrents.png'    = 'Arrow%20Download/PNG/24/ic_fluent_arrow_download_24_regular.png'
    'Resources\Icons\nav\ai.png'          = 'Bot/PNG/24/ic_fluent_bot_24_regular.png'
    'Resources\Icons\nav\hacking.png'     = 'Shield/PNG/24/ic_fluent_shield_24_regular.png'
    'Resources\Icons\nav\browser.png'     = 'Globe/PNG/24/ic_fluent_globe_24_regular.png'
    'Resources\Icons\nav\install.png'     = 'Archive/PNG/24/ic_fluent_archive_24_regular.png'
    'Resources\Icons\nav\debloat.png'     = 'Broom/PNG/24/ic_fluent_broom_24_regular.png'
    'Resources\Icons\nav\settings.png'    = 'Settings/PNG/24/ic_fluent_settings_24_regular.png'

    # ── PLAYER ───────────────────────────────────────────────────────
    'Resources\Icons\player\play.png'     = 'Play/PNG/24/ic_fluent_play_24_filled.png'
    'Resources\Icons\player\pause.png'    = 'Pause/PNG/24/ic_fluent_pause_24_filled.png'
    'Resources\Icons\player\next.png'     = 'Next/PNG/24/ic_fluent_next_24_filled.png'
    'Resources\Icons\player\prev.png'     = 'Previous/PNG/24/ic_fluent_previous_24_filled.png'
    'Resources\Icons\player\shuffle.png'  = 'Arrow%20Shuffle/PNG/24/ic_fluent_arrow_shuffle_24_regular.png'
    'Resources\Icons\player\repeat.png'   = 'Arrow%20Repeat%20All/PNG/24/ic_fluent_arrow_repeat_all_24_regular.png'
    'Resources\Icons\player\volume.png'   = 'Speaker%202/PNG/24/ic_fluent_speaker_2_24_regular.png'
    'Resources\Icons\player\volume_mute.png' = 'Speaker%20Off/PNG/24/ic_fluent_speaker_off_24_regular.png'

    # ── WINDOW CHROME ────────────────────────────────────────────────
    'Resources\Icons\window\minimize.png' = 'Subtract/PNG/24/ic_fluent_subtract_24_regular.png'
    'Resources\Icons\window\maximize.png' = 'Maximize/PNG/24/ic_fluent_maximize_24_regular.png'
    'Resources\Icons\window\close.png'    = 'Dismiss/PNG/24/ic_fluent_dismiss_24_regular.png'
    'Resources\Icons\window\restore.png'  = 'Square%20Multiple/PNG/24/ic_fluent_square_multiple_24_regular.png'

    # ── ACTIONS ──────────────────────────────────────────────────────
    'Resources\Icons\actions\search.png'    = 'Search/PNG/24/ic_fluent_search_24_regular.png'
    'Resources\Icons\actions\download.png'  = 'Arrow%20Download/PNG/24/ic_fluent_arrow_download_24_filled.png'
    'Resources\Icons\actions\install.png'   = 'Flash/PNG/24/ic_fluent_flash_24_regular.png'
    'Resources\Icons\actions\uninstall.png' = 'Delete/PNG/24/ic_fluent_delete_24_regular.png'
    'Resources\Icons\actions\add.png'       = 'Add%20Circle/PNG/24/ic_fluent_add_circle_24_regular.png'
    'Resources\Icons\actions\logs.png'      = 'Document%20Text/PNG/24/ic_fluent_document_text_24_regular.png'
    'Resources\Icons\actions\filter.png'    = 'Filter/PNG/24/ic_fluent_filter_24_regular.png'
    'Resources\Icons\actions\check.png'     = 'Checkmark%20Circle/PNG/24/ic_fluent_checkmark_circle_24_regular.png'
    'Resources\Icons\actions\scan.png'      = 'Scan/PNG/24/ic_fluent_scan_24_regular.png'
    'Resources\Icons\actions\warning.png'   = 'Warning/PNG/24/ic_fluent_warning_24_regular.png'
    'Resources\Icons\actions\gear.png'      = 'Settings/PNG/24/ic_fluent_settings_24_regular.png'
    'Resources\Icons\actions\heart.png'     = 'Heart/PNG/24/ic_fluent_heart_24_regular.png'
    'Resources\Icons\actions\queue.png'     = 'Apps%20List/PNG/24/ic_fluent_apps_list_24_regular.png'
    'Resources\Icons\actions\info.png'      = 'Info/PNG/24/ic_fluent_info_24_regular.png'
    'Resources\Icons\actions\open_link.png' = 'Open/PNG/24/ic_fluent_open_24_regular.png'
    'Resources\Icons\actions\refresh.png'   = 'Arrow%20Sync/PNG/24/ic_fluent_arrow_sync_24_regular.png'
    'Resources\Icons\actions\close.png'     = 'Dismiss%20Circle/PNG/24/ic_fluent_dismiss_circle_24_regular.png'
    'Resources\Icons\actions\edit.png'      = 'Edit/PNG/24/ic_fluent_edit_24_regular.png'
    'Resources\Icons\actions\copy.png'      = 'Copy/PNG/24/ic_fluent_copy_24_regular.png'
}

foreach ($kv in $icons.GetEnumerator()) {
    $dest = $kv.Key
    $url  = $base + $kv.Value
    TryDownload $url $dest
}

Write-Host "`nAll icon downloads attempted."
