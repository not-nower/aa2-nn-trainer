#!/usr/bin/env python3
"""Export ReiEdit XML enumerations to the trainer's embedded NameData.tsv.

This is a development tool. It is only needed when refreshing the baseline;
the trainer reads the generated TSV from its embedded resources at runtime.
"""

import argparse
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


def collect_files(input_dir):
    files = []
    for root, _dirs, names in os.walk(input_dir):
        for name in names:
            if name.lower().endswith(".xml"):
                files.append(os.path.join(root, name))
    files.sort(key=lambda path: os.path.relpath(path, input_dir).lower())
    return files


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--input",
        default=r"path\to\ReiEdit\XML",
        help="ReiEdit XML folder",
    )
    parser.add_argument(
        "--output",
        default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "Data", "NameData.tsv"),
        help="output TSV path",
    )
    args = parser.parse_args()

    input_dir = os.path.abspath(args.input)
    output_path = os.path.abspath(args.output)
    if not os.path.isdir(input_dir):
        print("Input folder not found: " + input_dir, file=sys.stderr)
        return 1

    files = collect_files(input_dir)
    entries = {}
    enum_keys = set()

    for path in files:
        try:
            root = ET.parse(path).getroot()
        except Exception as exc:
            print("Skipping " + path + ": " + str(exc), file=sys.stderr)
            continue

        added = 0
        for enumerable in root.findall("./enumerables/enumerable"):
            key = enumerable.get("key")
            if not key:
                continue
            enum_keys.add(key)
            for pair in enumerable.findall("./enumpair"):
                try:
                    value_id = int(pair.get("key"))
                except (TypeError, ValueError):
                    continue
                name = clean_name(pair.get("value"))
                if not name:
                    continue
                entries[(key, value_id)] = name
                added += 1

        if added:
            rel = os.path.relpath(path, input_dir)
            print(rel + ": " + str(added) + " entries")

    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    with open(output_path, "w", encoding="utf-8", newline="\n") as stream:
        stream.write("# AA2 NN's Trainer name data\n")
        stream.write("# enum<TAB>id<TAB>name\n")
        for (key, value_id), name in sorted(
            entries.items(), key=lambda item: (item[0][0].lower(), item[0][1])
        ):
            stream.write(key + "\t" + str(value_id) + "\t" + name + "\n")

    print("")
    print("Wrote " + output_path)
    print(str(len(enum_keys)) + " enumerations, " + str(len(entries)) + " unique entries")
    return 0


if __name__ == "__main__":
    sys.exit(main())
