#!/usr/bin/env python3
"""Merge AA2 name tables from XML into the trainer's embedded NameData.tsv.

The trainer ships a curated baseline and merges the game's XML files at
runtime, so this tool is for refreshing that baseline when newer community
XMLs appear. Imports are merge-only: entries are added or renamed, never
removed, and only the folder you pass is read (no recursion unless asked).

Supported sources use the same <dataset><enumerables><enumerable> schema:
  - an AA2 install folder (clothes.xml, HEXAOC.xml, personalities.xml)
  - an AA2QtEdit XML folder (chardata.xml, playdata.xml, ...)

ReiEdit's 2017 XMLs used the same schema and are the original baseline, but
they are stale; prefer the game folder or AA2QtEdit's XML folder.
"""

import argparse
import glob
import os
import sys
import xml.etree.ElementTree as ET


def clean_name(value):
    if value is None:
        return ""
    text = value.strip()
    if text.startswith("["):
        close = text.find("]")
        if close > 0 and close + 1 < len(text):
            text = text[close + 1:].strip()
    return text.replace("\t", " ").replace("\r", " ").replace("\n", " ")


def collect_files(input_dir, recursive):
    if recursive:
        files = []
        for root, _dirs, names in os.walk(input_dir):
            for name in names:
                if name.lower().endswith(".xml"):
                    files.append(os.path.join(root, name))
        files.sort(key=lambda path: os.path.relpath(path, input_dir).lower())
        return files

    files = glob.glob(os.path.join(input_dir, "*.xml"))
    files.sort(key=os.path.basename)
    return files


def parse_xml(path):
    """Returns {key: {id: name}} for every enumerable in one XML file."""
    result = {}
    root = ET.parse(path).getroot()
    for enumerable in root.findall(".//enumerables/enumerable"):
        key = enumerable.get("key")
        if not key:
            continue
        table = result.setdefault(key, {})
        for pair in enumerable.findall("./enumpair"):
            try:
                value_id = int(pair.get("key"))
            except (TypeError, ValueError):
                continue
            name = clean_name(pair.get("value"))
            if name:
                table[value_id] = name
    return result


def read_tsv(path):
    entries = {}
    if not os.path.exists(path):
        return entries
    with open(path, "r", encoding="utf-8") as stream:
        for line in stream:
            if not line or line.startswith("#"):
                continue
            parts = line.rstrip("\n").split("\t")
            if len(parts) < 3:
                continue
            try:
                entries[(parts[0], int(parts[1]))] = parts[2]
            except ValueError:
                continue
    return entries


def write_tsv(path, entries, label):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as stream:
        stream.write("# AA2 NN's Trainer name data\n")
        stream.write("# enum<TAB>id<TAB>name\n")
        if label:
            stream.write("# Bundled from game XML set " + label + "\n")
        for (key, value_id), name in sorted(
            entries.items(), key=lambda item: (item[0][0].lower(), item[0][1])
        ):
            stream.write(key + "\t" + str(value_id) + "\t" + name + "\n")


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument(
        "--input",
        default=r"path\to\AA2Again",
        help="AA2 install folder or AA2QtEdit XML folder",
    )
    parser.add_argument(
        "--output",
        default=os.path.join(os.path.dirname(os.path.abspath(__file__)),
                             "..", "src", "Data", "NameData.tsv"),
        help="output TSV path",
    )
    parser.add_argument(
        "--base",
        default=None,
        help="existing TSV to merge with (usually the output file)",
    )
    parser.add_argument(
        "--label",
        default=None,
        help="source label written as a header comment, e.g. 2026-09-06",
    )
    parser.add_argument(
        "--recursive",
        action="store_true",
        help="read subfolders too (needed for ReiEdit's XML tree)",
    )
    args = parser.parse_args()

    input_dir = os.path.abspath(args.input)
    output_path = os.path.abspath(args.output)
    if not os.path.isdir(input_dir):
        print("Input folder not found: " + input_dir, file=sys.stderr)
        return 1

    files = collect_files(input_dir, args.recursive)
    if not files:
        print("No XML files found in " + input_dir, file=sys.stderr)
        return 1

    entries = {}
    if args.base:
        entries = read_tsv(os.path.abspath(args.base))
        print("Base: " + str(len(entries)) + " entries from " + args.base)

    added = updated = unchanged = 0
    parsed_files = 0
    for path in files:
        try:
            parsed = parse_xml(path)
        except Exception as exc:
            print("Skipping " + os.path.basename(path) + ": " + str(exc), file=sys.stderr)
            continue
        if not parsed:
            continue
        parsed_files += 1

        file_added = file_updated = file_unchanged = 0
        for key, table in parsed.items():
            for value_id, name in table.items():
                current = entries.get((key, value_id))
                if current is None:
                    file_added += 1
                elif current != name:
                    file_updated += 1
                else:
                    file_unchanged += 1
                entries[(key, value_id)] = name

        added += file_added
        updated += file_updated
        unchanged += file_unchanged
        print("  " + os.path.basename(path) + ": +" + str(file_added) +
              " added, " + str(file_updated) + " renamed, " +
              str(file_unchanged) + " unchanged")

    if parsed_files == 0:
        print("No enumerable tables found.", file=sys.stderr)
        return 1

    write_tsv(output_path, entries, args.label)
    print("")
    print("Wrote " + output_path)
    print(str(parsed_files) + " file(s), " + str(len(entries)) + " entries: +" +
          str(added) + " added, " + str(updated) + " renamed, " +
          str(unchanged) + " unchanged")
    return 0


if __name__ == "__main__":
    sys.exit(main())
