# NN's AA2 Trainer

Inspired by Ahrie's AA2Trainer.
Fully vibe coded.

> **Save safety:** this tool writes to game memory and can rewrite class save
> files. Back up your saves first. Import/Export writes a timestamped `.bak`
> next to the save before changing it, but that is a convenience, not a
> guarantee. Memory edits are lost unless you save in-game, and editing is only
> tested against AA2Play 2.0.1. Use at your own risk.

## Features

- **Class Roster** — live cards, action/target/location, hover portraits, play-as, relationship menu.
- **Gameplay** — Action/Planner, Relationship, Behavior History, Lover, Promises.
- **Character Editor** — Character Data (including the read-only AAU module list), Traits, Body, Face, Hair, Outfits, and Import/Export (offline save/card replacer with side-by-side card-face previews and card export).
- **Option** — trainer live-refresh settings and bundled name-table updates, plus game System/Sound/Additional options.
- **Debug** — pointer/definition browser and filters.
- **Top bar** — weekday/week/period, clock, schedule edits, and time freeze.
- **Live refresh** — adaptive background polling.

## Requirements

- Windows with .NET Framework 4.8.
- x86 AA2Play, tested against version 2.0.1.
- Run the trainer as administrator for reliable read/write access. Without it, the trainer may attach read-only.
- A class/save loaded in AA2Play.
- AAUnlimited is optional. It is needed for AAU modules and the AAU planner-entry hook mode.
- Action/Planner actions require free roam/gameplay, not the class-load screen.

## Quick start

1. Download the latest `AA2Trainer-v*-win-x86.zip` from the
   [Releases page](https://github.com/not-nower/aa2-nn-trainer/releases/latest)
   and extract it. The zip contains `AA2Trainer.exe`, its `.config`, `LICENSE`,
   and a short readme.
2. Start AA2Play and load a class/save.
3. Run `AA2Trainer.exe` as administrator.
4. The trainer attaches automatically. Use the ↻ button to re-attach or refresh.
5. Select a character in Class Seats or the roster.
6. Use the tabs to view or edit the selected character.
7. For Action/Planner, enter free roam, select an actor and target, then run the action.

## Name tables

Name tables are embedded and built from the game XML set dated **2026-09-06**
(`clothes.xml`, `HEXAOC.xml`, `personalities.xml`). At runtime the trainer also
merges `*.xml` found in the game folder.

If the community releases newer XMLs, open **Option → Trainer → Name tables**
and use **Update from XMLs...**. The trainer writes `NameData.local.tsv` next
to `AA2Trainer.exe`, reloads the names, and keeps the overlay across exe
updates. Imports only add or rename entries — names are never removed — and
**Reset overlay** deletes the file. Only the folder you pick is read (no
subfolders), so ReiEdit/mod override tables are not pulled in.

## Build

Requires the .NET SDK 8 or newer (`dotnet --version`). End users only need .NET Framework 4.8.

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

Output: `bin\AA2Trainer.exe` — x86, targeting .NET Framework 4.8.

`build.ps1` stops a running trainer before building. `-KeepRunning` opts out,
but the build will fail while the trainer is running. The project is
`AA2Trainer.csproj`; `dotnet build -c Release` also works, with the same
running-app caveat.

## Dependencies

- **Runtime:** Windows x86 with .NET Framework 4.8 and AA2Play 2.0.1. Optional:
  AAUnlimited for AAU modules and the planner hook. No external DLLs — name,
  trait and card-field tables are embedded in the exe.
- **Build:** .NET SDK 8+ and the `Microsoft.NETFramework.ReferenceAssemblies`
  NuGet package (build-only).
- **Tools only:** Python 3 for the `tools/` exporters. They are not part of the
  build; the generated TSVs are committed and embedded as-is.

## Layout

- `src/`
  - `Program.cs` — application entry point
  - `MainForm.cs` — main window, tab shell, attach/refresh, fail-safe pointer gate
  - `MainForm.Roster.cs` — class seats, hover portrait, play-as character
  - `MainForm.CharacterEditor.cs` — Character Editor tab and sub-tabs
  - `MainForm.Gameplay.cs` — Gameplay tab wiring
  - `MainForm.Options.cs` — Option tab wiring
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
    - `ImportReplaceControl.cs` — offline save/card replacer and card exporter
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
    - `CardFields.cs` — card edit-data field definitions
    - `CardSnapshot.cs` — card PNG/edit-data parser
    - `ClassSaveFile.cs` — class save parser/writer
    - `NameOverlay.cs` — user name-table overlay (`NameData.local.tsv`)
    - `PlayerSwitch.cs` — play-as helper
  - `Data/`
    - `NameData.tsv` — action, location, personality and name tables
    - `TraitEffects.tsv` — trait effect tooltip text
    - `CardFields.tsv` — card edit-data field table
- `tools/ExportNameData.py` — regenerates `NameData.tsv` from the game or AA2QtEdit XML (merge-only)
- `tools/ExportCardFields.py` — regenerates `CardFields.tsv` from AA2QtEdit `chardata.xml`
- `tools/README.md` — exporter usage notes
- `build.ps1` — stops a running trainer, then builds x86 `bin/AA2Trainer.exe`
- `AA2Trainer.csproj` — SDK-style .NET Framework 4.8 project

## Troubleshooting

- **Not attached / OpenProcess failed** — start AA2Play and run the trainer as administrator.
- **Class data is not loaded** — load a save/class in AA2Play first.
- **Action/Planner does nothing** — make sure you are in free roam, the actor is idle, and a valid target is selected. Check the planner log for the hook state.
- **Build fails because the trainer is running** — `build.ps1` stops it automatically; `-KeepRunning` skips the stop, so close the trainer first.
- **Names look outdated** — Option → Trainer → Name tables → **Update from XMLs...** with the folder containing the newer `clothes.xml`, `HEXAOC.xml`, or `personalities.xml`.
- **Crash** — `crash.log` is written next to `AA2Trainer.exe`.

## Credits

- **Ahrie** — original AA2Trainer; inspiration and reference for the game's memory structures.
- **AAUnlimited Contributors** — [AAUnlimited](https://github.com/aa2g/AA2Unlimited); module/trigger format, planner entry, and modding framework.
- **geneishouko** — [AA2QtEdit](https://github.com/geneishouko/AA2QtEdit); used as a reference for save and card data handling.
- **usagirei** — [ReiEdit](https://github.com/usagirei/ReiEdit-AA2); original 2017 XML name tables (the shipped baseline has since been extended from the game's XMLs).

## License

GNU General Public License v3.0 — see [LICENSE](LICENSE).
