$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    throw "C# compiler not found at $csc"
}

$sources = @(
    "Program.cs",
    "MainForm.cs",
    "DashboardData.cs",
    "FieldGridControl.cs",
    "DisplayText.cs",
    "ClassRosterControl.cs",
    "CharacterDataControl.cs",
    "TraitsControl.cs",
    "SaveViewerControl.cs",
    "PortraitLoader.cs",
    "NameMap.cs",
    "GameProfile.cs",
    "PointerHealth.cs",
    "Core\GameMemory.cs",
    "Core\GameContext.cs",
    "Core\CharacterEditor.cs",
    "Core\Traits.cs",
    "Core\RiskSchedule.cs",
    "Core\PlayerSwitch.cs",
    "Core\SaveData.cs",
    "Core\RelationshipData.cs",
    "Core\Evaluator.cs",
    "Core\Definitions.cs"
) | ForEach-Object { Join-Path $root $_ }

Write-Host "Building AA2Trainer.exe ..."
& $csc /nologo /target:winexe /platform:x86 `
    /out:"$root\AA2Trainer.exe" `
    "/resource:$root\NameData.tsv,AA2Trainer.NameData.tsv" `
    "/resource:$root\TraitEffects.tsv,AA2Trainer.TraitEffects.tsv" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.Xml.dll `
    $sources

if ($LASTEXITCODE -ne 0) {
    throw "csc failed with exit code $LASTEXITCODE"
}

Write-Host "Built: $root\AA2Trainer.exe"
