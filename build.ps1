$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "AA2Trainer.csproj"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK not found. Install the .NET SDK 8 or newer."
}

Write-Host "Building AA2Trainer.exe ..."
dotnet build $project -c Release --nologo -v:m

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

Write-Host "Built: $(Join-Path $root 'bin\AA2Trainer.exe')"
