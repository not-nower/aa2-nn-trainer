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
# and fail the copy step with a retry storm.
function Wait-ForExit($processes, $timeoutMs) {
    $deadline = (Get-Date).AddMilliseconds($timeoutMs)
    while ((Get-Date) -lt $deadline) {
        $alive = 0
        foreach ($p in $processes) {
            try { if (-not $p.HasExited) { $alive++ } } catch { }
        }
        if ($alive -eq 0) { return }
        Start-Sleep -Milliseconds 100
    }
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
    Wait-ForExit $running 3000

    $still = @($running | Where-Object { try { -not $_.HasExited } catch { $false } })
    if ($still.Count -gt 0) {
        foreach ($p in $still) {
            try { $p.Kill() | Out-Null } catch { }
        }
        Wait-ForExit $still 3000
    }

    $remaining = @(Get-Process -Name AA2Trainer -ErrorAction SilentlyContinue)
    if ($remaining.Count -gt 0) {
        $left = ($remaining | ForEach-Object { $_.Id }) -join ", "
        throw "Could not stop AA2Trainer.exe (PID $left). Close it and build again."
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
