$ErrorActionPreference = "Stop"

$processName = "QuickBTTray"
$running = Get-Process -Name $processName -ErrorAction SilentlyContinue

if (-not $running) {
    Write-Host "No running process found: $processName"
    return
}

Write-Host "Stopping process: $processName"
$running | Stop-Process -Force

# Wait briefly to ensure file handles are released before build/publish starts.
$timeout = [DateTime]::UtcNow.AddSeconds(5)
while ((Get-Process -Name $processName -ErrorAction SilentlyContinue) -and [DateTime]::UtcNow -lt $timeout) {
    Start-Sleep -Milliseconds 100
}

if (Get-Process -Name $processName -ErrorAction SilentlyContinue) {
    throw "Failed to stop process: $processName"
}

Write-Host "Process stopped: $processName"