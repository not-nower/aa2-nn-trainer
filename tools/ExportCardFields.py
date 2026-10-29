#!/usr/bin/env python3
"""Export AA2QtEdit chardata.xml datablocks to the trainer's CardFields.tsv.

CardFields.tsv describes the 3011-byte card edit block used by
Import/Replace: section, field key, offset, type, size, enum name and the
shadow copy offset. It is embedded in the trainer and regenerated only when
the field table changes.

The enum width matters: most enums are one byte, but CLOTHES_F/CLOTHES_M are
declared type="int32" in chardata.xml and must be copied as four bytes.
"""

import argparse
import os
import sys
import xml.etree.ElementTree as ET

SECTIONS = [
    ("PROFILE", "Profile"),
    ("BODY", "Appearance"),
    ("TRAIT", "Traits"),
    ("PREFERENCE", "H Preferences"),
    ("PREGNANCY", "Risk/Pregnancy"),
    ("UNIFORM", "Outfit - Uniform"),
    ("SPORT", "Outfit - Sports"),
    ("SWIMSUIT", "Outfit - Swimsuit"),
    ("CLUB", "Outfit - Club"),
]

EDIT_DATA_SIZE = 3011


def type_size(data_type):
    if data_type in ("enum", "bool", "byte"):
        return 1
    if data_type == "int16":
        return 2
    if data_type in ("int32", "color"):
        return 4
    return 0


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument(
        "--input",
        default=r"path\to\AA2QtEdit\XML\chardata.xml",
        help="AA2QtEdit chardata.xml",
    )
    parser.add_argument(
        "--output",
        default=os.path.join(os.path.dirname(os.path.abspath(__file__)),
                             "..", "src", "Data", "CardFields.tsv"),
        help="output TSV path",
    )
    args = parser.parse_args()

    input_path = os.path.abspath(args.input)
    output_path = os.path.abspath(args.output)
    if not os.path.isfile(input_path):
        print("Input file not found: " + input_path, file=sys.stderr)
        return 1

    root = ET.parse(input_path).getroot()
    enum_types = {}
    for enumerable in root.iter("enumerable"):
        enum_types[enumerable.get("key")] = enumerable.get("type") or "byte"

    section_order = {prefix: index for index, (prefix, _name) in enumerate(SECTIONS)}
    section_names = {prefix: name for prefix, name in SECTIONS}

    rows = []
    for block in root.iter("datablock"):
        key = block.get("key", "")
        prefix = key.split("_")[0]
        if prefix not in section_names:
            continue

        address = int(block.get("address"), 16)
        data_type = block.get("type")
        if data_type == "enum":
            size = type_size(enum_types.get(block.get("metakey"), "byte")) or 1
        elif data_type in ("encodedstring", "string", "dummy"):
            size = int(block.get("datasize") or 0)
        else:
            size = type_size(data_type)

        if size <= 0:
            continue
        if address + size > EDIT_DATA_SIZE:
            print("Skipping field past the edit block: " + key, file=sys.stderr)
            continue

        copy_address = block.get("copyaddress")
        copy_offset = int(copy_address, 16) if copy_address else -1

        rows.append((
            section_names[prefix],
            section_order[prefix],
            address,
            key,
            data_type,
            size,
            block.get("metakey") or "",
            copy_offset,
        ))

    rows.sort(key=lambda row: (row[1], row[2], row[3]))

    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    with open(output_path, "w", encoding="utf-8", newline="\n") as stream:
        for section, _order, address, key, data_type, size, enum_name, copy in rows:
            stream.write("%s\t%s\t%d\t%s\t%d\t%s\t%d\n" % (
                section, key, address, data_type, size, enum_name, copy))

    print("Wrote " + output_path)
    print(str(len(rows)) + " fields")
    return 0


if __name__ == "__main__":
    sys.exit(main())
