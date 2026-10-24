# AA2 NN's Trainer

Inspired by Ahrie's AA2Trainer.
Fully vibe coded.

## Build

Requires the .NET SDK 8 or newer (`dotnet --version`).

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

Output: `bin\AA2Trainer.exe` — x86, targeting .NET Framework 4.8.

The project is `AA2Trainer.csproj`; you can also build it directly with
`dotnet build -c Release`.

## Layout

- `src/` — application source
  - `MainForm.cs` — form shell, fail-safe gate, attach and refresh
  - `MainForm.Roster.cs` — class roster, seat list, hover portrait, play-as
  - `MainForm.CharacterEditor.cs` — character data and traits tabs
  - `MainForm.Debug.cs` — debug tabs, grids and filters
  - `MainForm.SaveViewer.cs` — save viewer tab
  - `MainForm.Text.cs` — formatting helpers
  - `UI/` — user controls and presentation helpers
  - `Core/` — memory access, game data, editors and parsers
  - `Data/` — embedded TSV resources
- `tools/` — development scripts
- `build.ps1` — build script (csc, x86)
