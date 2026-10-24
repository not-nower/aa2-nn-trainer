$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$bin = Join-Path $root "bin"
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    throw "C# compiler not found at $csc"
}

if (-not (Test-Path $bin)) {
    New-Item -ItemType Directory -Path $bin | Out-Null
}

$sources = @(
    "src\Program.cs",
    "src\MainForm.cs",
    "src\MainForm.Roster.cs",
    "src\MainForm.CharacterEditor.cs",
    "src\MainForm.Debug.cs",
    "src\MainForm.SaveViewer.cs",
    "src\MainForm.Text.cs",
    "src\UI\DashboardData.cs",
    "src\UI\FieldGridControl.cs",
    "src\UI\DisplayText.cs",
    "src\UI\ClassRosterControl.cs",
    "src\UI\CharacterDataControl.cs",
    "src\UI\TraitsControl.cs",
    "src\UI\SaveViewerControl.cs",
    "src\UI\PortraitLoader.cs",
    "src\Core\GameMemory.cs",
    "src\Core\GameContext.cs",
    "src\Core\GameProfile.cs",
    "src\Core\CharacterEditor.cs",
    "src\Core\Traits.cs",
    "src\Core\RiskSchedule.cs",
    "src\Core\PlayerSwitch.cs",
    "src\Core\SaveData.cs",
    "src\Core\RelationshipData.cs",
    "src\Core\Evaluator.cs",
    "src\Core\Definitions.cs",
    "src\Core\NameMap.cs",
    "src\Core\PointerHealth.cs"
) | ForEach-Object { Join-Path $root $_ }

Write-Host "Building AA2Trainer.exe ..."
& $csc /nologo /target:winexe /platform:x86 `
    /out:"$bin\AA2Trainer.exe" `
    "/resource:$root\src\Data\NameData.tsv,AA2Trainer.NameData.tsv" `
    "/resource:$root\src\Data\TraitEffects.tsv,AA2Trainer.TraitEffects.tsv" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.Xml.dll `
    $sources

if ($LASTEXITCODE -ne 0) {
    throw "csc failed with exit code $LASTEXITCODE"
}

Write-Host "Built: $bin\AA2Trainer.exe"
