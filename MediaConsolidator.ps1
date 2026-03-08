# SONA Media Consolidation Script
# This script pulls sounds, videos, and GUI elements from various sources and puts them into the SONA Data/Media folder.

$sonaPath = "C:\Users\LionGhost\SONA\SONA"
$mediaTarget = Join-Path $sonaPath "Data\Media"
$vidsSource = "C:\Users\LionGhost\Downloads\Vids"
$iconsSource = "C:\Users\LionGhost\Downloads\Icons"
$guiSource = "C:\Users\LionGhost\Downloads\Gui"
$soundsSource = "C:\Users\LionGhost\Downloads\SND01_sine"

# Create directories
$null = New-Item -ItemType Directory -Force -Path $mediaTarget
$null = New-Item -ItemType Directory -Force -Path (Join-Path $mediaTarget "Videos")
$null = New-Item -ItemType Directory -Force -Path (Join-Path $mediaTarget "Icons")
$null = New-Item -ItemType Directory -Force -Path (Join-Path $mediaTarget "GUI")
$null = New-Item -ItemType Directory -Force -Path (Join-Path $mediaTarget "Sounds")

Write-Host "Consolidating media assets to $mediaTarget..." -ForegroundColor Cyan

# Copy Videos
if (Test-Path $vidsSource) {
    Write-Host "Copying videos from $vidsSource..."
    Copy-Item -Path "$vidsSource\*" -Destination (Join-Path $mediaTarget "Videos") -Recurse -Force -ErrorAction SilentlyContinue
}

# Copy Icons
if (Test-Path $iconsSource) {
    Write-Host "Copying icons from $iconsSource..."
    Copy-Item -Path "$iconsSource\*" -Destination (Join-Path $mediaTarget "Icons") -Recurse -Force -ErrorAction SilentlyContinue
}

# Copy GUI Elements
if (Test-Path $guiSource) {
    Write-Host "Copying GUI elements from $guiSource..."
    Copy-Item -Path "$guiSource\*" -Destination (Join-Path $mediaTarget "GUI") -Recurse -Force -ErrorAction SilentlyContinue
}

# Copy Sounds
if (Test-Path $soundsSource) {
    Write-Host "Copying sounds from $soundsSource..."
    Copy-Item -Path "$soundsSource\*" -Destination (Join-Path $mediaTarget "Sounds") -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Consolidation complete!" -ForegroundColor Green
