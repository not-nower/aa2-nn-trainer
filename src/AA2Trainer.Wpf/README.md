# AA2 Trainer (WPF)

WPF **.NET Framework 4.8 / x86** app. It references `AA2Trainer.Core`, attaches
to AA2Play, and reads live game data on a background poller. The WinForms app
remains available as a legacy fallback via `build.ps1 -Legacy`.

## Run

```powershell
cd ..\..
powershell -ExecutionPolicy Bypass -File build.ps1
.\bin\WPF\AA2Trainer.exe
```

For development, `dotnet build src\AA2Trainer.Wpf -c Release` writes the same
`bin\WPF\AA2Trainer.exe`.

## Implemented

- **Live session:** attaches to AA2Play on a background timer, refreshes the
  roster and relationships once per second, reads live action/activity at 4 Hz,
  checks pointer health every 5 seconds, and publishes one coalesced snapshot
  to the UI dispatcher. Attach/Detach and Refresh work from the top bar.
- **Roster filters:** text search over name, room, action, status, and seat,
  plus All seats / Students / Teachers. Filtered cards collapse in place so the
  classroom layout stays fixed. Group-by-room is deferred because it would
  break that grid.
- **Live Character Data:** the selected character’s profile, attributes,
  academics, and status are read from Core once per second. The Data tab is
  editable; Apply sends a draft through the session write queue and Revert
  restores the live values.
- **Character writes:** names, profile, attributes, and academics are editable.
  Apply re-checks pointer health before writing, reports the changed fields,
  and refreshes the live snapshot after success. Club choices come from the
  current class data, matching WinForms.
- **Traits:** trait, extra, and H-preference checkboxes plus the two-week risk
  schedule. Apply writes changed traits/risk cells in one queued batch after a
  pointer-health check; Revert restores live values.
- **Body:** body, breast, and skin fields with number, choice, and color
  controls. Colors use a swatch, a hex field, and a centered picker with a
  spectrum square, hue bar, and direct H/S/L and R/G/B input. Apply writes
  changed body fields through the session queue; Revert restores live values.
- **Face:** face type, eyes, iris, brow, glasses, moles, and AAUnlimited
  external-texture names. Colors use the same picker; texture names are plain
  Shift-JIS text fields. Apply writes changed face values/texts in one batch.
- **Hair:** four style indices, lengths, flip flags, and the hair color.
- **Outfits:** School, Sport, Swimsuit, and Club sets with uniform, top,
  bottom, shoes/socks, and underwear groups, including color/opacity pairs.
- **Schedule:** click the game-time chip in the top bar to edit weekday, week,
  period, duration, and time, or to freeze/pin the current time. Academy, year,
  class, and save name stay read-only.
- **Import/Export:** offline class-save editor. Select a save, a character, and
  a card; choose sections; review the side-by-side comparison; apply with a
  timestamped backup or export the character as a standalone card.
- **Live log:** the Gameplay drawer records attach/detach requests, write
  results, player switches, relationship actions, and schedule/freeze events.
- **Planner:** install the planner hook from the toolbar, build steps from live
  targets/rooms/actions, choose mode and trigger, toggle Repeat/Shuffle, and
  start/stop on the game thread. Plans can be copied, pasted, saved to, and
  loaded from `PlanStore.DefaultPath`; step state and the planner log update
  live.
- **Limits:** a checkable action list with Enable and Apply-to-PC; checked
  actions are cancelled by the `ActionLimiter`.
- **Relationship:** incoming feelings table with Set Love/Like/Dislike/Hate and
  Freeze/Unfreeze.
- **Behavior:** feeling history per classmate, oldest first.
- **Lover:** incoming lover flags, days, and compatibility with Set Lover/Break
  Up.
- **Promises:** active promises involving the selected character (read-only).
- Compact contextual shell: top bar, five main tabs, status bar. The seat rail,
  character header and log drawer appear only where they help.
- **System / Light / Dark** themes with a runtime switch and dark window chrome;
  the choice persists beside the exe.
- **Roster** is a fixed **5×5 classroom grid** in Japanese seat order: columns
  run right-to-left and top-to-bottom, with teacher seat **24 at the top-middle**.
  The Class seats rail is available here and starts collapsed; the character
  header and log drawer stay hidden so the cards keep the height. Expand the
  rail from its top button when needed. Cards reserve a 68×80 portrait area with
  a seat badge overlay and no reflow on resize. Seat status is a vertical bar at
  the left of each row, so it stays visible when the rail is collapsed.
- **Roster menu:** right-click a card for Play this character, Open in Character,
  Set Lover/Break Up, Set Love/Like/Dislike/Hate, and Freeze Relationship. The
  header Play as button uses the same queued player-switch path.
- **Teacher badge:** seat 24 carries a small `Teacher` pill (muted while the seat
  is empty) in the roster card.
- Empty seats are not selectable; arrow keys move by classroom position rather
  than by seat number.
- **Portraits** are read from live game memory for every character-data block
  (`+0x28/0x2C` thumbnail, `+0x30/0x34` full portrait), cached by pointer and
  address/size. The roster grid uses the live thumbnail and the Class seats rail
  hover popup uses the live full portrait. Full portraits fill in one per poll
  after a class/save change. Card files remain the fallback: the game’s card
  save folders plus a `Cards` folder beside the exe, top level only, matched by
  exact stored name without translation. Card images are decoded only for
  matched seats, so a large card library does not sit in memory. Detaching
  clears names and portrait art.
- Character/Options/Debug keep the rail expanded by default and restore the
  character header; Gameplay also shows the log drawer. Roster selection still
  drives the rail/header when you switch tabs.
- Character Data now shows and edits live profile, attributes, academics, and
  status for the selected character. Apply/Revert are enabled only when the
  draft has changes.
- Gameplay > Planner sample:
  - plan list with On checkbox, state colors, skipped dimming;
  - compact step editor;
  - plan-level Repeat/Shuffle/Mode/Trigger on the plan bar;
  - Live events / Planner log tabs.
- **Options** reads all live game options through `OptionEditor` and groups
  them into System, Sound, Additional (1–3). Toggles, choices, and sliders
  update `OptionSnapshot` values; Apply writes only changed fields on the poll
  thread, and Revert restores the last live values.
- **Debug** is a read-only view of game memory only: module/global roots, class
  array bounds, selected CharacterInfo/characterData, status/activity, AI and
  movement descriptors, walk/stare targets, room/NPC pointers and targets,
  action/conversation/location/mood/response/instruction values, busy flags,
  status records, and schedule/save values. Trainer state is not shown. Text
  and type filters run over the live rows.
- Custom compact scrollbars, tab strip, combo box, checkbox, and buttons.
- Layout verified at **1180×760** and **1024×680** (125% DPI): the 5×5 roster
  fits without a scrollbar at the minimum size.
- Rail and log drawer collapse/expand; the log is visible on Gameplay only.
  Window bounds, maximized state, and rail/log collapse states persist. Restored
  bounds are checked against the connected desktop, so an unplugged monitor
  centers the window instead of stranding it off-screen.

Portrait status is shown in the status bar, for example `Portraits: 11 live`
or `Portraits: 7 live, 4 card`. Live memory is primary; card matching is by
exact stored name and only fills seats where the live portrait is unavailable.
Character names are displayed exactly as the game stores them; no translation
or romanization is applied.

## Not implemented (next phases)

- M5: trainer settings/backups/name-table wiring; focus/accessibility/
  performance pass; full keyboard navigation deferred.
- M6: cutover to `AA2Trainer.exe`.

## Design notes

- Default window: **1180×760**; minimum **1024×680**.
- Roster cells are a fixed 5×5 classroom grid (right-to-left columns, teacher
  seat 24 top-middle). The Class seats rail is shown collapsed by default on
  the roster; the character header and log stay hidden. Other tabs start with
  the rail expanded.
- Density: 24–28 px rows, 4/8 px spacing, 12/13 px Segoe UI.
- Theme brushes are semantic (`Surface`, `Text`, `Accent`, `Success`, …) and
  consumed via `DynamicResource`; `Themes/Shared.xaml` holds the control styles.
- Scrollbars are 10 px. The system scrollbar-size keys are overridden in
  `App.xaml` so `ScrollViewer` scrollbars shrink too, not just the custom
  `ScrollBar` style.
- Logs use a virtualized `ListBox` with a border-separated time column, never a
  `RichTextBox`/FlowDocument.
- The right planner panel no longer scrolls at the minimum size; plan-level
  controls live on the plan bar.
