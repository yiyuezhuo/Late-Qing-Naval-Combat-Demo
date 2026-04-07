#!/usr/bin/env python3
"""
add_localization.py — Add new key(s) to the Dynamic Table localization assets.

USAGE
-----
  python Tools/add_localization.py --key "Some Key" --en "Some Key" --ja "日本語" --zh-hans "简体" --zh-hant "繁體"

  # Add multiple keys at once via JSON:
  python Tools/add_localization.py --file Tools/localization_batch.json

  # Or via a pipe-delimited text file:
  python Tools/add_localization.py --file Tools/localization_batch.txt

ARGUMENT REFERENCE
------------------
  --key       Localization key string (must be unique in Dynamic Table Shared Data)
  --en        English value
  --ja        Japanese value  (plain text — will be auto-converted to Unicode escapes)
  --zh-hans   Simplified Chinese value (auto Unicode escaped)
  --zh-hant   Traditional Chinese value (auto Unicode escaped)
  --file      Path to a JSON file or pipe-delimited text file with entries
  --dry-run   Print what would be inserted without modifying files

BATCH FILE FORMATS
------------------
  JSON:
  [
    {
      "key": "Some Key",
      "en": "Some Key",
      "ja": "日本語",
      "zh-hans": "简体中文",
      "zh-hant": "繁體中文"
    },
    ...
  ]

  Text:
    key|en|ja|zh-hans|zh-hant
    Another Key|English|日本語|简体中文|繁體中文

NOTES
-----
- Keys are added to Dynamic Table (used by C# Localize() and LocalizeEnum()).
- Standard Table is for UXML-bound static labels — use Tools/standard_localization.py for those.
- New IDs are assigned from the sequential small-negative range (-1, -2, -3, ...).
- Existing legacy large-magnitude negative IDs are ignored when picking the next ID.
- Non-ASCII characters in --ja/--zh-hans/--zh-hant are auto-converted to \\uXXXX.
- YAML special characters in values (e.g. { } : #) are automatically quoted.
- Enum keys follow the convention "{EnumTypeName}.{MemberName}" (e.g. Country.China).
"""

import argparse
import json
import os
import re
import sys

BASE = os.path.join(os.path.dirname(__file__), "..", "Assets", "DynamicStringTableCollection")
SHARED_DATA = os.path.join(BASE, "Dynamic Table Shared Data.asset")

LOCALE_FILES = {
    "en":      os.path.join(BASE, "Dynamic Table_en.asset"),
    "ja":      os.path.join(BASE, "Dynamic Table_ja.asset"),
    "zh-hans": os.path.join(BASE, "Dynamic Table_zh-Hans.asset"),
    "zh-hant": os.path.join(BASE, "Dynamic Table_zh-Hant.asset"),
}

SHARED_MARKER  = "  m_Metadata:\n    m_Items: []\n  m_KeyGenerator:"
LOCALE_MARKER  = "  references:\n    version: 2"


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def to_unicode_escapes(text: str) -> str:
    """Convert non-ASCII characters to \\uXXXX escapes (for YAML assets)."""
    result = []
    for ch in text:
        if ord(ch) > 127:
            result.append(f"\\u{ord(ch):04X}")
        else:
            result.append(ch)
    return "".join(result)


def yaml_quote(value: str, escape_unicode: bool = False) -> str:
    """
    Return a YAML-safe representation of value.
    - Non-ASCII chars are escaped when escape_unicode=True.
    - Values containing YAML special chars are single-quoted (en) or double-quoted.
    """
    if escape_unicode:
        value = to_unicode_escapes(value)
        # After escaping, wrap in double quotes so \\uXXXX sequences are preserved.
        return f'"{value}"'

    # For English: check if quoting is needed.
    needs_quoting = any(ch in value for ch in "{}:\"'#&*!|>%@`")
    if needs_quoting:
        # Use single quotes; escape any single quotes inside by doubling them.
        return "'" + value.replace("'", "''") + "'"
    return value


def get_existing_keys(content: str) -> set:
    return set(re.findall(r"m_Key: (.+)", content))


def get_lowest_manual_negative_id(content: str) -> int:
    """
    Return the lowest sequential manual ID in the file (e.g. -74).
    Large-magnitude legacy negatives (e.g. -9.1e17) are ignored.
    """
    MANUAL_THRESHOLD = -10 ** 15
    ids = [int(x) for x in re.findall(r"m_Id: (-\d+)", content)
           if int(x) > MANUAL_THRESHOLD]
    return min(ids) if ids else 0


def next_negative_id(current_max: int) -> int:
    return current_max - 1


def load_entries_from_file(path: str) -> list[dict]:
    with open(path, "r", encoding="utf-8") as f:
        raw = f.read()

    stripped = raw.lstrip()
    if stripped.startswith("["):
        return json.loads(raw)

    entries = []
    for line_number, line in enumerate(raw.splitlines(), start=1):
        stripped_line = line.strip()
        if not stripped_line or stripped_line.startswith("#"):
            continue

        parts = [part.strip() for part in line.split("|")]
        if len(parts) != 5:
            raise ValueError(
                f"Invalid batch line {line_number}: expected 5 pipe-delimited fields "
                f"(key|en|ja|zh-hans|zh-hant)"
            )

        entries.append({
            "key": parts[0],
            "en": parts[1],
            "ja": parts[2],
            "zh-hans": parts[3],
            "zh-hant": parts[4],
        })

    return entries


# ---------------------------------------------------------------------------
# Core insertion
# ---------------------------------------------------------------------------

def add_entries(entries: list[dict], dry_run: bool = False):
    """
    entries: list of dicts with keys: key, en, ja, zh-hans, zh-hant
    """
    with open(SHARED_DATA, "r", encoding="utf-8") as f:
        shared_content = f.read()

    existing_keys = get_existing_keys(shared_content)
    current_max_id = get_lowest_manual_negative_id(shared_content)

    new_shared_blocks = []
    assigned = []  # (id, entry)

    for entry in entries:
        key = entry["key"]
        if key in existing_keys:
            print(f"  SKIP  '{key}' — already exists in Shared Data")
            continue
        new_id = next_negative_id(current_max_id)
        current_max_id = new_id
        new_shared_blocks.append(
            f"  - m_Id: {new_id}\n"
            f"    m_Key: {yaml_quote(key)}\n"
            f"    m_Metadata:\n"
            f"      m_Items: []\n"
        )
        assigned.append((new_id, entry))
        print(f"  ADD   '{key}'  →  id {new_id}")

    if not assigned:
        print("Nothing to add.")
        return

    if not dry_run:
        updated_shared = shared_content.replace(
            SHARED_MARKER, "".join(new_shared_blocks) + SHARED_MARKER, 1
        )
        with open(SHARED_DATA, "w", encoding="utf-8", newline="\n") as f:
            f.write(updated_shared)
        print(f"  Wrote Shared Data")

    # Per-locale files
    locale_keys = {"en": "en", "ja": "ja", "zh-hans": "zh-hans", "zh-hant": "zh-hant"}
    for locale_key, file_path in LOCALE_FILES.items():
        with open(file_path, "r", encoding="utf-8") as f:
            content = f.read()

        locale_blocks = []
        for new_id, entry in assigned:
            raw_value = entry.get(locale_key, entry.get("en", ""))
            need_escape = locale_key != "en"
            localized = yaml_quote(raw_value, escape_unicode=need_escape)
            locale_blocks.append(
                f"  - m_Id: {new_id}\n"
                f"    m_Localized: {localized}\n"
                f"    m_Metadata:\n"
                f"      m_Items: []\n"
            )

        if not dry_run:
            updated = content.replace(
                LOCALE_MARKER, "".join(locale_blocks) + LOCALE_MARKER, 1
            )
            with open(file_path, "w", encoding="utf-8", newline="\n") as f:
                f.write(updated)
        print(f"  {'DRY ' if dry_run else ''}Wrote {os.path.basename(file_path)}")


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="Add entries to Unity Dynamic Table localization assets.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument("--key",      help="Localization key")
    parser.add_argument("--en",       help="English value")
    parser.add_argument("--ja",       help="Japanese value")
    parser.add_argument("--zh-hans",  dest="zh_hans", help="Simplified Chinese value")
    parser.add_argument("--zh-hant",  dest="zh_hant", help="Traditional Chinese value")
    parser.add_argument("--file",     help="JSON or pipe-delimited batch file with entries")
    parser.add_argument("--dry-run",  action="store_true", help="Preview without writing")
    args = parser.parse_args()

    if args.file:
        entries = load_entries_from_file(args.file)
    elif args.key and args.en:
        entries = [{
            "key":      args.key,
            "en":       args.en,
            "ja":       args.ja       or args.en,
            "zh-hans":  args.zh_hans  or args.en,
            "zh-hant":  args.zh_hant  or args.en,
        }]
    else:
        parser.print_help()
        sys.exit(1)

    add_entries(entries, dry_run=args.dry_run)


if __name__ == "__main__":
    main()
