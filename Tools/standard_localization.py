#!/usr/bin/env python3
"""
standard_localization.py — Query and add entries in the Standard Table localization assets.

The Standard Table is used for UXML-bound static labels (property="label", property="text", etc.).
After adding an entry with this script, manually insert the UXML binding snippet that is printed.

COMMANDS
--------
  query <key>
      Check whether a key exists. Prints its ID and all 4 locale values.
      If found, also prints the ready-to-paste UXML binding snippet.

  add --key "..." --en "..." --ja "..." --zh-hans "..." --zh-hant "..."
      Add a new key. Prints the assigned ID and ready-to-paste UXML snippet.
      Use --dry-run to preview without writing.

  list
      List all keys in Standard Table with their IDs.

WORKFLOW FOR AGENTS
-------------------
  1. Run: python Tools/standard_localization.py query "My Label"
  2a. If FOUND    → copy the printed UXML snippet and paste it into the target UXML file.
  2b. If NOT FOUND → run 'add', then paste the printed UXML snippet into the target UXML file.

UXML BINDING FORMAT
-------------------
  The printed snippet looks like:
    <UnityEngine.Localization.LocalizedString
        property="label"
        table="GUID:7dfd13ea0ff0ef0408a7f015356a0054"
        entry="Id(-42)" />

  Wrap it in a <Bindings> block inside the target element. Change property= if needed
  (e.g. "text" for a Button, "label" for a TextField or Toggle).

STANDARD TABLE GUID
-------------------
  7dfd13ea0ff0ef0408a7f015356a0054
"""

import argparse
import os
import re
import sys

BASE = os.path.join(os.path.dirname(__file__), "..", "Assets", "StandardStringTableCollection")
SHARED_DATA = os.path.join(BASE, "Standard Table Shared Data.asset")

LOCALE_FILES = {
    "en":      os.path.join(BASE, "Standard Table_en.asset"),
    "ja":      os.path.join(BASE, "Standard Table_ja.asset"),
    "zh-hans": os.path.join(BASE, "Standard Table_zh-Hans.asset"),
    "zh-hant": os.path.join(BASE, "Standard Table_zh-Hant.asset"),
}

TABLE_GUID = "7dfd13ea0ff0ef0408a7f015356a0054"
SHARED_MARKER = "  m_Metadata:\n    m_Items: []\n  m_KeyGenerator:"
LOCALE_MARKER = "  references:\n    version: 2"


# ---------------------------------------------------------------------------
# YAML helpers (same rules as Dynamic Table — identical asset format)
# ---------------------------------------------------------------------------

def to_unicode_escapes(text: str) -> str:
    return "".join(
        f"\\u{ord(ch):04X}" if ord(ch) > 127 else ch
        for ch in text
    )


def yaml_quote(value: str, escape_unicode: bool = False) -> str:
    if escape_unicode:
        return f'"{to_unicode_escapes(value)}"'
    if any(ch in value for ch in "{}:\"'#&*!|>%@`"):
        return "'" + value.replace("'", "''") + "'"
    return value


def uxml_snippet(entry_id: int, prop: str = "label") -> str:
    lines = [
        f'<UnityEngine.Localization.LocalizedString',
        f'    property="{prop}"',
        f'    table="GUID:{TABLE_GUID}"',
        f'    entry="Id({entry_id})" />',
    ]
    return "\n".join(lines)


# ---------------------------------------------------------------------------
# Parsing helpers
# ---------------------------------------------------------------------------

def parse_shared_keys(content: str) -> dict[str, int]:
    """Return {key_string: id} for all entries in Shared Data."""
    result = {}
    for m in re.finditer(r"- m_Id: (-?\d+)\s+m_Key: (.+)", content):
        key_id = int(m.group(1))
        key_str = m.group(2).strip()
        if key_str.startswith("'") and key_str.endswith("'"):
            key_str = key_str[1:-1].replace("''", "'")
        elif key_str.startswith('"') and key_str.endswith('"'):
            key_str = key_str[1:-1]
        result[key_str] = key_id
    return result


def parse_locale_values(content: str) -> dict[int, str]:
    """Return {id: display_value} for all entries in a locale file."""
    result = {}
    for m in re.finditer(r"- m_Id: (-?\d+)\s+m_Localized: (.*?)\s+m_Metadata:", content, re.DOTALL):
        key_id = int(m.group(1))
        value = m.group(2).strip()
        if value.startswith("'") and value.endswith("'"):
            value = value[1:-1].replace("''", "'")
        elif value.startswith('"') and value.endswith('"'):
            value = value[1:-1].encode("raw_unicode_escape").decode("unicode_escape")
        result[key_id] = value
    return result


def next_id(content: str) -> int:
    """Return the next available manual negative ID (most-negative existing minus 1)."""
    THRESHOLD = -(10 ** 15)
    ids = [int(x) for x in re.findall(r"m_Id: (-\d+)", content) if int(x) > THRESHOLD]
    return (min(ids) - 1) if ids else -1


# ---------------------------------------------------------------------------
# Commands
# ---------------------------------------------------------------------------

def cmd_query(key: str):
    with open(SHARED_DATA, "r", encoding="utf-8") as f:
        shared = f.read()

    keys = parse_shared_keys(shared)
    if key not in keys:
        print(f"NOT FOUND: '{key}' is not in Standard Table.")
        print("Use the 'add' command to create it.")
        sys.exit(1)

    entry_id = keys[key]
    print(f"FOUND: '{key}'  (ID: {entry_id})\n")

    for locale, path in LOCALE_FILES.items():
        with open(path, "r", encoding="utf-8") as f:
            content = f.read()
        val = parse_locale_values(content).get(entry_id, "<missing>")
        print(f"  {locale:8s}: {val}")

    print()
    print("UXML binding snippet (paste inside a <Bindings> block):")
    print(uxml_snippet(entry_id))


def cmd_list():
    with open(SHARED_DATA, "r", encoding="utf-8") as f:
        shared = f.read()
    keys = parse_shared_keys(shared)
    if not keys:
        print("Standard Table is empty.")
        return
    print(f"{'ID':>8}  Key")
    print("-" * 60)
    for key, key_id in sorted(keys.items(), key=lambda x: x[1]):
        print(f"  {key_id:6d}  {key}")


def cmd_add(key: str, en: str, ja: str, zh_hans: str, zh_hant: str, dry_run: bool):
    with open(SHARED_DATA, "r", encoding="utf-8") as f:
        shared = f.read()

    existing = parse_shared_keys(shared)
    if key in existing:
        entry_id = existing[key]
        print(f"ALREADY EXISTS: '{key}'  (ID: {entry_id})")
        print()
        print("UXML binding snippet (paste inside a <Bindings> block):")
        print(uxml_snippet(entry_id))
        return

    new_id = next_id(shared)
    prefix = "DRY RUN — " if dry_run else ""
    print(f"{prefix}ADD '{key}'  →  id {new_id}\n")

    shared_block = (
        f"  - m_Id: {new_id}\n"
        f"    m_Key: {yaml_quote(key)}\n"
        f"    m_Metadata:\n"
        f"      m_Items: []\n"
    )

    locale_entries = {
        "en":      (en,      False),
        "ja":      (ja,      True),
        "zh-hans": (zh_hans, True),
        "zh-hant": (zh_hant, True),
    }

    if not dry_run:
        updated = shared.replace(SHARED_MARKER, shared_block + SHARED_MARKER, 1)
        with open(SHARED_DATA, "w", encoding="utf-8", newline="\n") as f:
            f.write(updated)
    print(f"  {'(dry) ' if dry_run else ''}Wrote {os.path.basename(SHARED_DATA)}")

    for locale, path in LOCALE_FILES.items():
        with open(path, "r", encoding="utf-8") as f:
            content = f.read()
        raw, escape = locale_entries[locale]
        locale_block = (
            f"  - m_Id: {new_id}\n"
            f"    m_Localized: {yaml_quote(raw, escape_unicode=escape)}\n"
            f"    m_Metadata:\n"
            f"      m_Items: []\n"
        )
        if not dry_run:
            updated = content.replace(LOCALE_MARKER, locale_block + LOCALE_MARKER, 1)
            with open(path, "w", encoding="utf-8", newline="\n") as f:
                f.write(updated)
        print(f"  {'(dry) ' if dry_run else ''}Wrote {os.path.basename(path)}")

    print()
    print("UXML binding snippet (paste inside a <Bindings> block):")
    print(uxml_snippet(new_id))


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="Query and add Standard Table localization entries.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    sub = parser.add_subparsers(dest="command", required=True)

    p_query = sub.add_parser("query", help="Look up a key and print its ID and translations.")
    p_query.add_argument("key", help="Exact key string to look up")

    sub.add_parser("list", help="List all keys with their IDs.")

    p_add = sub.add_parser("add", help="Add a new key to Standard Table.")
    p_add.add_argument("--key",      required=True)
    p_add.add_argument("--en",       required=True, help="English value")
    p_add.add_argument("--ja",       required=True, help="Japanese value")
    p_add.add_argument("--zh-hans",  dest="zh_hans", required=True, help="Simplified Chinese")
    p_add.add_argument("--zh-hant",  dest="zh_hant", required=True, help="Traditional Chinese")
    p_add.add_argument("--dry-run",  action="store_true", help="Preview without writing")

    args = parser.parse_args()

    if args.command == "query":
        cmd_query(args.key)
    elif args.command == "list":
        cmd_list()
    elif args.command == "add":
        cmd_add(args.key, args.en, args.ja, args.zh_hans, args.zh_hant, args.dry_run)


if __name__ == "__main__":
    main()
