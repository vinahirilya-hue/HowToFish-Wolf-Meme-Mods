param(
    [Parameter(Mandatory = $true)]
    [string]$GameDir,

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$resolvedGameDir = (Resolve-Path -LiteralPath $GameDir).Path
$gameExe = Join-Path $resolvedGameDir "How to Fish.exe"

if (-not (Test-Path -LiteralPath $gameExe)) {
    throw "How to Fish.exe was not found under: $resolvedGameDir"
}

& dotnet build (Join-Path $repoRoot "HowToFish.WolfMemeMods.sln") `
    -c $Configuration `
    "-p:GameDir=$resolvedGameDir"

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

