# AA2Trainer WPF prototype (M1.1)

Standalone WPF **.NET Framework 4.8 / x86** prototype for the planned UI rebuild.
It references `AA2Trainer.Core`, attaches to AA2Play, and reads live game data on
a background poller. The shipping WinForms app remains the parity fallback.

## Run

```powershell
cd src\AA2Trainer.Wpf
dotnet run -c Release
```

Or build and launch:

```powershell
dotnet build -c Release
.\bin\Release\net48\AA2Trainer.WpfPrototype.exe
```

## Implemented in M0 and M1.1

- **Live session:** attaches to AA2Play on a background timer, refreshes the
  roster and relationships once per second, reads live action/activity at 4 Hz,
  checks pointer health every 5 seconds, and publishes one coalesced snapshot
  to the UI dispatcher. Attach/Detach and Refresh work from the top bar.- Compact contextual shell: top bar, five main tabs, status bar. The seat rail,
  character header and log drawer appear only where they help.
- **System / Light / Dark** themes with a runtime switch and dark window chrome.
- **Roster** is a fixed **5×5 classroom grid** in Japanese seat order: columns
  run right-to-left and top-to-bottom, with teacher seat **24 at the top-middle**.
  It hides the seat rail, character header and log drawer so the 25 cards get the
  full window. Cards reserve a 68×80 portrait area with a seat badge overlay and
  no reflow on resize.
- **Teacher badge:** seat 24 carries a small `Teacher` pill (muted while the seat
  is empty) in the roster card and the card-face popup.
- Empty seats are not selectable; arrow keys move by classroom position rather
  than by seat number.
- **Card previews** read the game’s card save folders plus a `Cards` folder
  beside the exe. Only the top level of each folder is scanned; there is no
  source picker, file import, or recursive scan. Names are read exactly as
  stored and matched to live seat names without translation. Card images are
  decoded only for matched seats, so a large card library does not sit in
  memory. A hover popup opens the card face only when a card matched; detaching
  clears names and card art.
- Character/Options/Debug restore the seat rail and header; Gameplay also shows
  the log drawer. Roster selection still drives the rail/header when you switch
  tabs.
- Character Data sample form (edits update the header live).
- Gameplay > Planner sample:
  - plan list with On checkbox, state colors, skipped dimming;
  - compact step editor;
  - plan-level Repeat/Shuffle/Mode/Trigger on the plan bar;
  - Live events / Planner log tabs.
- Options and Debug sample surfaces, including a virtualized field grid.
- Custom compact scrollbars, tab strip, combo box, checkbox, and buttons.
- Layout verified at **1180×760** and **1024×680** (125% DPI): the 5×5 roster
  fits without a scrollbar at the minimum size.
- Rail and log drawer collapse/expand; the log is visible on Gameplay only.

Card status is shown in the status bar, for example `Cards: 4 shown`.
Matching is by exact name; M2 will use the save/card provider for
identity-based seat mapping. Character names are displayed exactly as the game
stores them; no translation or romanization is applied.

## Not implemented (next phases)

- M2: writes, full Character editor parity, planner/hook wiring, real card
  provider.
- M3: real planner runner, logs, and Gameplay sub-tabs.
- M4: Options/Debug parity.
- M5: keyboard/accessibility/performance pass.
- M6: cutover to `AA2Trainer.exe`.

## Design notes

- Default window: **1180×760**; minimum **1024×680**.
- Roster cells are a fixed 5×5 classroom grid (right-to-left columns, teacher
  seat 24 top-middle); the roster is a focused view and does not show the
  rail/header/log. Other tabs restore the surrounding chrome.
- Density: 24–28 px rows, 4/8 px spacing, 12/13 px Segoe UI.
- Theme brushes are semantic (`Surface`, `Text`, `Accent`, `Success`, …) and
  consumed via `DynamicResource`; `Themes/Shared.xaml` holds the control styles.
- Logs use a virtualized `ListBox` with a border-separated time column, never a
  `RichTextBox`/FlowDocument.
- The right planner panel no longer scrolls at the minimum size; plan-level
  controls live on the plan bar.
