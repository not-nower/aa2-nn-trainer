# NN's AA2 Trainer

Inspired by Ahrie's AA2Trainer. Fully vibe coded.

> **Save safety:** this tool writes to game memory and can rewrite class save files.
> Back up your saves first. Import/Export and **Options → Trainer → Save backups**
> create timestamped `.bak` files beside the save; memory edits are lost unless you
> save in-game. Tested against AA2Play 2.0.1. Use at your own risk.

## Features

- **Class Roster** — live 5×5 classroom cards with portrait, name, personality,
  location, action, target, target portrait, PC/teacher badges, filters, play-as,
  and relationship menu.
- **Gameplay** — Planner (multi-step plans, modes, triggers, drag reorder, Force
  Actions, copy/paste, save/load), Limits, Relationship, Behavior, Lover, Promises.
- **Character Editor** — Data, Traits, Body, Face, Hair, Outfits, and Import/Export
  (offline save/card replacer with card-face previews and card export).
- **Options** — live refresh, save backups, name tables, and game options
  (System/Sound/Additional).
- **Debug** — read-only game-memory browser with filters and pointer details.
- **Top bar** — game time, schedule editor, time freeze, and live academy/save.
- **Live refresh** — adaptive background polling with Live events / Planner log.

## Requirements

- Windows with .NET Framework 4.8.
- x86 AA2Play 2.0.1, with a class/save loaded.
- Run the trainer as administrator for reliable read/write access.
- A writable folder for `AA2Trainer.exe`; settings are stored beside it.
- AAUnlimited is optional (AAU modules, planner-entry hook mode).
- Action/Planner actions require free roam, not the class-load screen.

## Quick start

1. Download the latest `AA2Trainer-v*-win-x86.zip` from the
   [Releases page](https://github.com/not-nower/aa2-nn-trainer/releases/latest)
   and extract it.
2. Start AA2Play and load a class/save.
3. Run `AA2Trainer.exe` as administrator.
4. The trainer attaches automatically; use ↻ to re-attach or refresh.
5. Select a character in Class Seats or the roster.
6. Use the tabs to view or edit the selected character.
7. For plans, open **Gameplay → Planner**, install the hook, build steps, and
   press **Start plan**.
8. Before large edits, use **Options → Trainer → Save backups**.

## Planner

Build steps with **Add Action** (Target / About / Action) or **Add Move** (Room).
Drag rows to reorder, remove steps, or uncheck **On** to skip one. **Force Action**
and **Force Move** run the current selection once without changing the plan.

| Mode | Behavior |
|---|---|
| Normal | Waits for the character to start toward the step's target; the plan then substitutes the step. |
| Forced Interaction | Arms when the character starts an interaction; does not force from idle. |
| Forced Perform | Arms immediately, even while busy; may use a direct activity write fallback. |
| Ignore Interaction | Cancels the current action, then arms the step. |

- **Trigger** (Actor / Target / Either) applies to Normal mode.
- **Repeat / Shuffle** loop the plan or play every step once in random order.
- **Copy plan / Paste plan** work across characters; paste replaces the current plan.
- **Save plans / Load plans** use `AA2TrainerPlans.xml` beside the exe.
- **Start plan / Stop plan** — steps show pending, running, done, failed, or skipped.
- **Planner hook** — use **Install hook**, and **Repair hook** after a force-closed
  trainer. Normal close restores the original game bytes.
- **Live events / Planner log** keep the newest 5,000 lines and auto-scroll only
  while you're at the bottom. Clear and Save act on the active tab.

## Backups and name tables

- **Save backups** copy the current `.sav` and, when present, the AAU `.json`
  sidecar to timestamped `.bak` files. **Restore...** verifies the backup and
  creates a safety backup first. Reload the class after restoring.
- **Import/Export** creates the same backup format for the save it has open.
- **Name tables** are embedded from the 2026-09-06 XMLs. **Update from XMLs...**
  merges newer `clothes.xml` / `HEXAOC.xml` / `personalities.xml` into
  `NameData.local.tsv` (adds/renames only). **Reset overlay** deletes it.

## Settings

Preferences live in `AA2Trainer.settings` beside the exe: theme, window bounds,
rail/log state, polling, and observe-only. Action limits are per character in
`AA2Trainer.ini`. Copying or moving the exe folder carries the settings; the
trainer does not write to `%APPDATA%`.

## Build

Requires the .NET SDK 8 or newer; end users only need .NET Framework 4.8.

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

Output is `bin\WPF\AA2Trainer.exe` (WPF, x86) plus `AA2Trainer.Core.dll` and
`.config`. The WinForms fallback is available with `build.ps1 -Legacy`.

## Troubleshooting

- **Not attached** — start AA2Play and run the trainer as administrator.
- **Class data missing** — load a save/class in AA2Play first.
- **Planner does nothing** — check free roam, the selected target, and the mode.
  Normal waits for the character; Forced Interaction waits for an interaction.
- **A step is skipped** — its **On** checkbox is off.
- **Names look outdated** — update them from the newer XML folder.
- **Restored save not visible** — reload the class or restart AA2Play.
- **Crash** — `crash.log` is written beside `AA2Trainer.exe`.

## Credits

- **Ahrie** — original AA2Trainer; inspiration and memory-structure reference.
- **AAUnlimited Contributors** — module/trigger format and planner entry.
- **geneishouko** — AA2QtEdit; save and card data reference.
- **usagirei** — ReiEdit; original name tables.

## License

GNU General Public License v3.0 — see [LICENSE](LICENSE).
