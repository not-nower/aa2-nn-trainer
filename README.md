# AA2 Trainer - Dashboard

Read-only dashboard for **Artificial Academy 2** (`AA2Play.exe`), built with
**.NET Framework 4.8 + WinForms (x86)**.

It is the first stage of the trainer rewrite: it uses the verified pointer map
from `PointerTester` but focuses on a normal, readable dashboard instead of a
diagnostic grid.

## Run

```text
AA2Trainer\AA2Trainer.exe
```

The game must be running with a class loaded. If the game is running elevated,
run the dashboard as administrator too.

On attach the dashboard verifies the game build and tests all 882 pointers. If
the build is unknown or any pointer is broken/unreadable/suspicious, the
dashboard still works for reading but the write gate stays locked. See
**Fail-safe** below.

Rebuild:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

## What it shows

- **Class seats** on the left, including the game seat number, name,
  personality and sex.
- **Overview**: portrait, character identity, live status/action/location,
  class/time and session pointers.
- **Personal**: identity, traits, body, face, hair, outfits, study/club and
  media fields.
- **Relations**: relationship values, moods, behavior levels, feeling levels
  and promise data. Use **Target index** for the array/seat slot.
- **Activity**: status, action, target and NPC/activity pointers.
- **Class & Time**: class data, day/week, play time, club names.
- **Config**: game configuration values, including float fields such as
  movement speeds and colours.
- **All Pointers**: the full 882-entry diagnostic map with chain trace and
  `Only problems` filter.

## Mod names from XML

The dashboard reads names from the game's XML metadata:

- `ReiEdit\XML\DataBlocks\chardata.xml` — built-in enumerations
- root-level `*.xml` such as `personalities.xml`, `clothes.xml`,
  `HEXAOC.xml` — custom/modded names
- `ReiEdit\XML\Overrides\*.xml` — enabled ReiEdit overrides

Precedence is: built-in → game-root XMLs → ReiEdit overrides → raw number.
Names are shown for personality, gender, sexuality, fight style, height,
figure, breast values, face/iris/brow/eyelid/glasses/lipstick, socks, shoes
and the main outfit types. Unknown or unmapped fields stay numeric.

The status bar shows how many XML files/names were loaded (for the current
install: `Names: 7 files / 1324 names`). **Reload names** reloads them without
restarting the dashboard, so edits to the XML files are picked up immediately.
Hair IDs are not named in these files, so hair remains numeric.

## Fail-safe

The dashboard treats writes as opt-in and fails closed:

```text
AA2Play.exe 2.0.1 / module 0xD2C000 / SHA-256 DEE8451A...
  878 OK / 4 empty / 0 broken / 0 unreadable
  -> write gate open
```

On every attach, and every 15 seconds afterwards, it:

1. Reads the running module path, size and file version.
2. Hashes the executable once per launch (cached by path, size and timestamp).
3. Looks for the known profile: `AA2Play.exe` version `2.0.1`, module size
   `0xD2C000`, SHA-256
   `DEE8451A1C7DAB60703C0BE9B0E01A82053F5E98DBCAF09F704CD440923D4517`.
4. Evaluates all 882 pointer definitions and counts
   `OK / empty / suspicious / broken / unreadable`.
5. Opens the write gate only when the profile hash matches **and** there are no
   suspicious, broken or unreadable pointers.

If anything fails, the status bar turns red/orange, shows `writes locked`, and
the **Session / fail-safe** panel shows the reason. The gate is a single flag
(`writesAllowed`) that every future write/freeze path must check. Writing is not
implemented yet; when it is, each write must also re-resolve its chain and
validate the current value immediately before writing.

**Recheck** forces a fresh verification. **Details** shows the detected profile,
timings, empty fields and the failing pointer names. The health label also has a
tooltip with the same summary.

For a new game build, update the constants in `GameProfile.cs` only after the
full pointer map has been re-verified with `PointerTester`, then update the hash
and expected module size. Until that is done the new build stays read-only.

## Controls

- **Refresh** re-attaches and re-reads everything.
- **Recheck** re-runs the profile and all-pointer health check.
- **Details** opens the fail-safe report.
- **Auto 1s** refreshes the current view once per second.
- **Character list** selects whose data is shown.
- **Personal → Group** narrows the field list.
- **Relations → Target index** selects the target seat/array element
  (`0..24`); this is what the `^58`, `^4`, `^10` and `^C` strides use.
- **Filter** searches name, raw expression, category and context.
- **All Pointers → Only problems** hides resolved `OK` rows.
- **Alt+1 … Alt+7** switches tabs.

## Pointer model

The roots are the same ones verified by `PointerTester`:

```text
S           = read32(module + 0x376164)
classData   = read32(S + 0x28)
classArray  = read32(S + 0x6C)
player      = read32(S + 0x88)
charData    = read32(char + 0x28)
status      = read32(char + 0xF6C)
activity    = read32(status + 0x28)
relations   = read32(read32(char + 0xF64) + 0x16A18)
```

The game build is `AA2Play.exe 2.0.1.0` (TAA2 build 2.0.0.30). Other patches may
move the static roots.

## Notes

- This stage is **read-only**. No `WriteProcessMemory` calls.
- The portrait uses the character's face thumbnail
  (`ThumbAddr` / `ThumbSize`) and falls back to the full card image
  (`PotraitAddr` / `PotraitSize`). It is cached until the selection changes.
- Seat numbers come from `char + 0x3C`; the class-member array order is not
  the seat order.
- `NPCTarget` and `NPCTargetOth` are shown as a pointer when the value is a
  plausible address, otherwise as a number.
- Empty strings are legitimate. Depending on the character/save, fields such as
  `Profile`, `FirstSex`, `FirstAnal`, `LastSex`, `ExtIrisTextureName` and
  `ExtHTextureName` may be empty; this is not a broken pointer. The health check
  counts them separately from failures.
- Health counts can differ by a couple of entries from `PointerTester` because
  the selected character and the game state have changed. Only `suspicious`,
  `broken` and `unreadable` lock the gate.
- The dashboard intentionally focuses on read access. Editing, freeze, action
  plans, live events and character switching are the next phases.

## Files

- `Program.cs` — entry point and crash log
- `MainForm.cs` — dashboard UI
- `DashboardData.cs` — field grouping/evaluation helpers
- `FieldGridControl.cs` — reusable field grid
- `NameMap.cs` — XML name/enum loader
- `GameProfile.cs` — build fingerprint and expected offsets profile
- `PointerHealth.cs` — all-pointer self-test and failure list
- `Core/GameMemory.cs` — process/module lookup and reads
- `Core/GameContext.cs` — class/character roots
- `Core/Evaluator.cs` — pointer-chain resolver
- `Core/Definitions.cs` — 882 definitions extracted from TAA2.exe
- `build.ps1` — framework compiler build
