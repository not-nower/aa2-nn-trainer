# NN's AA2 Trainer

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

- `src/`
  - `Program.cs` — application entry point
  - `MainForm.cs` — main window, tab shell, attach/refresh, fail-safe pointer gate
  - `MainForm.Roster.cs` — class seats, hover portrait, play-as character
  - `MainForm.CharacterEditor.cs` — Character Editor tab and sub-tabs
  - `MainForm.Gameplay.cs` — Gameplay tab wiring
  - `MainForm.Options.cs` — Option tab wiring
  - `MainForm.SaveViewer.cs` — Save Viewer tab wiring
  - `MainForm.Debug.cs` — Debug tab, grids and filters
  - `MainForm.Live.cs` — background live poller and adaptive refresh
  - `MainForm.TopPanel.cs` — top summary, schedule header, clock and freeze
  - `MainForm.Text.cs` — text and formatting helpers
  - `UI/` — WinForms controls
    - `ClassRosterControl.cs` — roster cards and relationship context menu
    - `CharacterDataControl.cs` — Character Data, including read-only AAU modules
    - `TraitsControl.cs` — Traits and H Preferences
    - `BodyControl.cs`, `FaceControl.cs`, `HairControl.cs`, `OutfitsControl.cs` — appearance editors
    - `GameplayControl.cs` — Gameplay sub-tab host
    - `ActionPlannerControl.cs` — Action/Planner
    - `RelationshipControl.cs` — Relationship
    - `BehaviorHistoryControl.cs` — Behavior History
    - `LoverControl.cs` — Lover
    - `PromisesControl.cs` — Promises
    - `OptionsControl.cs` — Option editor
    - `SaveViewerControl.cs` — Save Viewer
    - `FieldGridControl.cs` — reusable debug field grid
    - `PortraitLoader.cs` — portrait image loading
    - `DashboardData.cs`, `DisplayText.cs` — definition lookup and display text
  - `Core/` — game access and data
    - `GameMemory.cs`, `MemoryWriter.cs` — process attach, reads, verified writes
    - `GameProfile.cs`, `PointerHealth.cs` — build detection and pointer health
    - `GameContext.cs`, `LiveReader.cs` — class roster context and live snapshots
    - `Definitions.cs`, `Evaluator.cs`, `NameMap.cs` — definition tables and name lookup
    - `CharacterEditor.cs`, `BodyEditor.cs`, `FaceEditor.cs`, `HairEditor.cs`, `OutfitEditor.cs` — character and appearance data
    - `Traits.cs`, `OptionEditor.cs`, `ScheduleEditor.cs`, `RiskSchedule.cs` — traits, options and schedule
    - `RelationshipData.cs`, `RelationshipEditor.cs`, `LoverEditor.cs`, `PromiseReader.cs` — relationship data
    - `ActionParams.cs`, `ActionStateReader.cs`, `ActionPlannerEngine.cs`, `PlannerHook.cs` — Action/Planner and planner hook
    - `AauModuleReader.cs` — read-only AAU module list parser
    - `SaveData.cs` — class save parser
    - `PlayerSwitch.cs` — play-as helper
  - `Data/`
    - `NameData.tsv` — action, location, personality and name tables
    - `TraitEffects.tsv` — trait effect tooltip text
- `tools/ExportNameData.py` — regenerates `NameData.tsv` from ReiEdit XML
- `build.ps1` — stops a running trainer, then builds x86 `bin/AA2Trainer.exe`
- `AA2Trainer.csproj` — SDK-style .NET Framework 4.8 project

## Credits

- **Ahrie** — original AA2Trainer; inspiration and reference for the game's memory structures.
- **AAUnlimited Contributors** — AAUnlimited; module/trigger format, planner entry, and modding framework.
- **geneishouko** — AA2QtEdit; used as a reference for save and card data handling.
