param(
    [switch]$KeepRunning
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "AA2Trainer.csproj"
$output = Join-Path $root "bin\AA2Trainer.exe"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK not found. Install the .NET SDK 8 or newer."
}

# The build writes bin\AA2Trainer.exe, so a running copy would lock the file
# and fail the copy step with a retry storm. Poll by process name rather than
# by Process object state, which can lag behind an exiting process.
function Test-TrainerRunning {
    return @(Get-Process -Name AA2Trainer -ErrorAction SilentlyContinue).Count -gt 0
}

function Wait-ForTrainerExit($timeoutMs) {
    $deadline = (Get-Date).AddMilliseconds($timeoutMs)
    while ((Get-Date) -lt $deadline) {
        if (-not (Test-TrainerRunning)) { return $true }
        Start-Sleep -Milliseconds 100
    }
    return -not (Test-TrainerRunning)
}

$running = @(Get-Process -Name AA2Trainer -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    $ids = ($running | ForEach-Object { $_.Id }) -join ", "
    if ($KeepRunning) {
        throw "AA2Trainer.exe is running (PID $ids) and would lock bin\AA2Trainer.exe. Close it first or omit -KeepRunning."
    }

    Write-Host "Stopping running AA2Trainer.exe (PID $ids) so the build can replace it."
    foreach ($p in $running) {
        try { $p.CloseMainWindow() | Out-Null } catch { }
    }

    if (-not (Wait-ForTrainerExit 4000)) {
        foreach ($p in @(Get-Process -Name AA2Trainer -ErrorAction SilentlyContinue)) {
            try { $p.Kill() | Out-Null } catch { }
        }
        if (-not (Wait-ForTrainerExit 5000)) {
            $left = (@(Get-Process -Name AA2Trainer -ErrorAction SilentlyContinue) |
                     ForEach-Object { $_.Id }) -join ", "
            throw "Could not stop AA2Trainer.exe (PID $left). Close it and build again."
        }
    }
}

# Catch a lock from anything else (antivirus, explorer preview, another tool)
# before MSBuild spends ten seconds retrying the copy.
if (Test-Path $output) {
    try {
        $stream = [System.IO.File]::Open($output, [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $stream.Close()
    }
    catch {
        throw "Cannot replace $output because the file is locked by another process. Close it and build again."
    }
}

Write-Host "Building AA2Trainer.exe ..."
dotnet build $project -c Release --nologo -v:m

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

Write-Host "Built: $output"
