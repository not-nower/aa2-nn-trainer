param(
    [switch]$Legacy,
    [switch]$KeepRunning
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = if ($Legacy) {
    Join-Path $root "AA2Trainer.csproj"
} else {
    Join-Path $root "src\AA2Trainer.Wpf\AA2Trainer.WpfPrototype.csproj"
}
$output = if ($Legacy) {
    Join-Path $root "bin\AA2Trainer.exe"
} else {
    Join-Path $root "bin\WPF\AA2Trainer.exe"
}
$label = if ($Legacy) { "WinForms legacy" } else { "WPF" }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK not found. Install the .NET SDK 8 or newer."
}

# Both builds use the same process name, so a running copy of either app would
# lock the output. Poll by process name rather than Process object state.
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
        throw "AA2Trainer.exe is running (PID $ids) and would lock the build output. Close it first or omit -KeepRunning."
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

Write-Host "Building $label AA2Trainer.exe ..."
dotnet build $project -c Release --nologo -v:m

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

Write-Host "Built: $output"
if (-not $Legacy) {
    Write-Host "Legacy WinForms build remains available with: build.ps1 -Legacy"
}
