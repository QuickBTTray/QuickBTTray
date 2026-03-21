$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $repoRoot
try {

$base = "1.0"
$height = (git rev-list --count HEAD)
if (-not $height) {
    throw "Unable to read git commit count."
}

$ver = "$base.$height"
$folder = "artifacts/QuickBTTray-$ver"
if (-not (Test-Path $folder)) {
    throw "Release folder not found: $folder"
}

$zip = "artifacts/QuickBTTray-$ver.zip"
if (Test-Path $zip) {
    Remove-Item -Force $zip
}

Compress-Archive -Path "$folder/*" -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Packaged: $zip"
}
finally {
    Pop-Location
}
