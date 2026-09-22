param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $repoRoot "artifacts"

& (Join-Path $PSScriptRoot "build.ps1") -GameDir $GameDir -Configuration Release

$packages = @(
    @{ Name = "WolfSniperHit"; Version = "0.1.1" },
    @{ Name = "AlbatrossWolfHit"; Version = "0.1.0" }
)

foreach ($package in $packages) {
    $name = $package.Name
    $version = $package.Version
    $stage = Join-Path $artifacts "$name-$version"
    $pluginDir = Join-Path $stage "BepInEx\plugins\$name"
    $assetDir = Join-Path $pluginDir "assets"

    New-Item -ItemType Directory -Force -Path $assetDir | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot "src\$name\bin\Release\netstandard2.1\$name.dll") `
        -Destination (Join-Path $pluginDir "$name.dll") -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot "src\$name\manifest.json") `
        -Destination (Join-Path $stage "manifest.json") -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot "README.zh-CN.md") `
        -Destination (Join-Path $stage "README.zh-CN.md") -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot "THIRD_PARTY_NOTICES.md") `
        -Destination (Join-Path $stage "THIRD_PARTY_NOTICES.md") -Force
    New-Item -ItemType Directory -Force -Path (Join-Path $stage "docs") | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot "docs\ASSETS.zh-CN.md") `
        -Destination (Join-Path $stage "docs\ASSETS.zh-CN.md") -Force
    Set-Content -LiteralPath (Join-Path $assetDir "PUT_ASSETS_HERE.txt") `
        -Value "Place howl_flash.png and howl_bgm.ogg in this directory. See README.zh-CN.md."

    Compress-Archive -Path (Join-Path $stage "*") `
        -DestinationPath (Join-Path $artifacts "$name-$version.zip") `
        -CompressionLevel Optimal -Force
}

Write-Host "Packages written to $artifacts"
