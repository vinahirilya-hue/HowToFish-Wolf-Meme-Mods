param(
    [Parameter(Mandatory = $true)]
    [string]$SourceAssetDir,

    [Parameter(Mandatory = $true)]
    [string]$GameDir,

    [Parameter(Mandatory = $true)]
    [ValidateSet("WolfSniperHit", "AlbatrossWolfHit")]
    [string]$Plugin
)

$ErrorActionPreference = "Stop"
$source = (Resolve-Path -LiteralPath $SourceAssetDir).Path
$game = (Resolve-Path -LiteralPath $GameDir).Path
$destination = Join-Path $game "BepInEx\plugins\$Plugin\assets"

$audio = Join-Path $source "howl_bgm.ogg"
$image = Join-Path $source "howl_flash.png"

if (-not (Test-Path -LiteralPath $audio)) {
    throw "Missing required file: $audio"
}
if (-not (Test-Path -LiteralPath $image)) {
    throw "Missing required file: $image"
}

New-Item -ItemType Directory -Force -Path $destination | Out-Null
Copy-Item -LiteralPath $audio -Destination (Join-Path $destination "howl_bgm.ogg") -Force
Copy-Item -LiteralPath $image -Destination (Join-Path $destination "howl_flash.png") -Force

Write-Host "Assets copied to $destination"

