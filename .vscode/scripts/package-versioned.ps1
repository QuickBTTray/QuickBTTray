$ErrorActionPreference = "Stop"

function Get-AvailableZipPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PreferredPath
    )

    if (-not (Test-Path $PreferredPath)) {
        return $PreferredPath
    }

    try {
        Remove-Item -Force $PreferredPath
        return $PreferredPath
    }
    catch [System.IO.IOException] {
        $directory = Split-Path -Parent $PreferredPath
        $baseName = [System.IO.Path]::GetFileNameWithoutExtension($PreferredPath)
        $extension = [System.IO.Path]::GetExtension($PreferredPath)
        $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
        $fallbackPath = Join-Path $directory "$baseName-$timestamp$extension"
        Write-Warning "Package file is in use. Writing to $fallbackPath instead."
        return $fallbackPath
    }
}

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

$zip = Get-AvailableZipPath -PreferredPath "artifacts/QuickBTTray-$ver.zip"

Compress-Archive -Path "$folder/*" -DestinationPath $zip -CompressionLevel Optimal
Write-Host "Packaged: $zip"
}
finally {
    Pop-Location
}
