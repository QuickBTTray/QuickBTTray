$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $repoRoot
try {

& (Join-Path $PSScriptRoot "stop-running-app.ps1")

$base = "1.0"
$height = (git rev-list --count HEAD)
if (-not $height) {
    throw "Unable to read git commit count."
}

$ver = "$base.$height"
$out = "artifacts/QuickBTTray-$ver"

if (Test-Path $out) {
    Remove-Item -Recurse -Force $out
}

dotnet publish QuickBTTrayApp/QuickBTTrayApp.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Version=$ver `
    -p:AssemblyVersion=$ver `
    -p:FileVersion=$ver `
    -o $out

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Published: $out/QuickBTTray.exe"
}
finally {
    Pop-Location
}
