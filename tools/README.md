# tools

Development-only helpers. They need **Python 3** (standard library only);
neither the trainer nor `build.ps1` uses them.

## `ExportNameData.py`

Refreshes `src/Data/NameData.tsv`, the embedded name-table baseline.

```powershell
python tools\ExportNameData.py --input "E:\Game\Etc\AA2Again" `
    --base src\Data\NameData.tsv --label 2026-09-06
```

- `--input` — AA2 install folder or AA2QtEdit XML folder. Both use the same
  `<dataset><enumerables><enumerable>` schema.
- Only the folder itself is read. Add `--recursive` for trees like ReiEdit's,
  where the XMLs live under `XML\DataBlocks`.
- `--base` — merge with an existing TSV. Imports are **merge-only**: entries are
  added or renamed, never removed, and only keys present in the XML change.
- `--label` — writes `# Bundled from game XML set <label>` into the header.

Do not point this at ReiEdit's 2017 XMLs without `--base`; those tables are the
original baseline and are stale. The game folder (clothes.xml, HEXAOC.xml,
personalities.xml) or AA2QtEdit's XML folder has the newer catalogs.

## `ExportCardFields.py`

Regenerates `src/Data/CardFields.tsv`, the 3011-byte card edit-block layout
used by Import/Replace.

```powershell
python tools\ExportCardFields.py --input "path\to\AA2QtEdit\XML\chardata.xml"
```

Enum width comes from chardata.xml: most enums are one byte, but
`CLOTHES_F`/`CLOTHES_M` are `int32` and are copied as four bytes.

## After regenerating

`build.ps1` embeds both TSVs into `AA2Trainer.exe`; rebuild to ship them.
