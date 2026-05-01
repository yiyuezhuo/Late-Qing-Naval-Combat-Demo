#!/usr/bin/env python3
"""
Update Unity Dynamic Table localization entries in batch.

This complements add_localization.py:
- update existing Dynamic Table keys
- optionally add missing keys
- optionally remove obsolete keys
- repair duplicate tail markers left by interrupted/manual table edits

Batch JSON format:
{
  "entries": [
    {
      "key": "Some Key",
      "en": "English",
      "ja": "Japanese",
      "zh-hans": "Simplified Chinese",
      "zh-hant": "Traditional Chinese"
    }
  ],
  "remove": ["Obsolete Key"]
}
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from dataclasses import dataclass

BASE = os.path.join(os.path.dirname(__file__), "..", "Assets", "DynamicStringTableCollection")
SHARED_DATA = os.path.join(BASE, "Dynamic Table Shared Data.asset")

LOCALE_FILES = {
    "en": os.path.join(BASE, "Dynamic Table_en.asset"),
    "ja": os.path.join(BASE, "Dynamic Table_ja.asset"),
    "zh-hans": os.path.join(BASE, "Dynamic Table_zh-Hans.asset"),
    "zh-hant": os.path.join(BASE, "Dynamic Table_zh-Hant.asset"),
}

SHARED_INSERT_MARKER = "  m_Metadata:\n    m_Items: []\n  m_KeyGenerator:"
LOCALE_INSERT_MARKER = "  references:\n    version: 2"
MANUAL_ID_THRESHOLD = -10**15


@dataclass(frozen=True)
class SharedEntry:
    entry_id: int
    key: str


def read_text(path: str) -> str:
    with open(path, "r", encoding="utf-8-sig") as file:
        return file.read()


def write_text(path: str, content: str) -> None:
    with open(path, "w", encoding="utf-8", newline="\n") as file:
        file.write(content)


def yaml_unquote(value: str) -> str:
    value = value.strip()
    if value.startswith("'") and value.endswith("'"):
        return value[1:-1].replace("''", "'")
    if value.startswith('"') and value.endswith('"'):
        return bytes(value[1:-1], "utf-8").decode("unicode_escape")
    return value


def to_unicode_escapes(value: str) -> str:
    return "".join(ch if ord(ch) <= 127 else f"\\u{ord(ch):04X}" for ch in value)


def yaml_quote(value: str, escape_unicode: bool = False) -> str:
    if escape_unicode:
        return '"' + to_unicode_escapes(value).replace('"', '\\"') + '"'

    needs_quoting = (
        value == ""
        or value.strip() != value
        or any(ch in value for ch in "{}:\"'#&*!|>%@`")
    )
    if needs_quoting:
        return "'" + value.replace("'", "''") + "'"
    return value


def parse_shared_entries(content: str) -> dict[str, SharedEntry]:
    entries: dict[str, SharedEntry] = {}
    pattern = re.compile(r"  - m_Id: (-?\d+)\n    m_Key: (.+?)\n    m_Metadata:\n      m_Items: \[\]")
    for match in pattern.finditer(content):
        entry_id = int(match.group(1))
        key = yaml_unquote(match.group(2))
        entries[key] = SharedEntry(entry_id, key)
    return entries


def lowest_manual_id(content: str) -> int:
    ids = [
        int(match.group(1))
        for match in re.finditer(r"m_Id: (-\d+)", content)
        if int(match.group(1)) > MANUAL_ID_THRESHOLD
    ]
    return min(ids) if ids else 0


def normalize_table_tail(content: str, shared: bool) -> str:
    if shared:
        content = re.sub(r"\n:\s*\d+\s*$", "\n", content)
        return content

    content = re.sub(r"\n\n    RefIds: \[\]\s*$", "\n", content)
    content = re.sub(r"\n version: 2\n    RefIds: \[\]\s*$", "\n", content)
    return content


def append_shared_entry(content: str, entry_id: int, key: str) -> str:
    block = (
        f"  - m_Id: {entry_id}\n"
        f"    m_Key: {yaml_quote(key)}\n"
        "    m_Metadata:\n"
        "      m_Items: []\n"
    )
    if SHARED_INSERT_MARKER not in content:
        raise ValueError("Shared Data insert marker not found.")
    return content.replace(SHARED_INSERT_MARKER, block + SHARED_INSERT_MARKER, 1)


def append_locale_entry(content: str, entry_id: int, value: str, locale: str) -> str:
    block = (
        f"  - m_Id: {entry_id}\n"
        f"    m_Localized: {yaml_quote(value, escape_unicode=locale != 'en')}\n"
        "    m_Metadata:\n"
        "      m_Items: []\n"
    )
    if LOCALE_INSERT_MARKER not in content:
        raise ValueError(f"Locale insert marker not found for {locale}.")
    return content.replace(LOCALE_INSERT_MARKER, block + LOCALE_INSERT_MARKER, 1)


def update_locale_value(content: str, entry_id: int, value: str, locale: str) -> tuple[str, bool]:
    pattern = re.compile(
        rf"(  - m_Id: {entry_id}\n    m_Localized: ).*?(\n    m_Metadata:\n      m_Items: \[\])",
        re.DOTALL,
    )
    localized = yaml_quote(value, escape_unicode=locale != "en")
    updated, count = pattern.subn(lambda match: f"{match.group(1)}{localized}{match.group(2)}", content, count=1)
    return updated, count == 1


def remove_entry_block(content: str, entry_id: int, value_field: str) -> tuple[str, bool]:
    pattern = re.compile(
        rf"  - m_Id: {entry_id}\n    {value_field}: .*?\n    m_Metadata:\n      m_Items: \[\]\n?",
        re.DOTALL,
    )
    updated, count = pattern.subn("", content)
    return updated, count > 0


def load_batch(path: str) -> tuple[list[dict[str, str]], list[str], list[int]]:
    with open(path, "r", encoding="utf-8") as file:
        payload = json.load(file)

    if isinstance(payload, list):
        return payload, [], []
    if not isinstance(payload, dict):
        raise ValueError("Batch file must contain a JSON list or object.")

    return payload.get("entries", []), payload.get("remove", []), payload.get("remove_ids", [])


def validate_entry(entry: dict[str, str]) -> None:
    required = ["key", "en", "ja", "zh-hans", "zh-hant"]
    missing = [key for key in required if key not in entry]
    if missing:
        raise ValueError(f"Entry for {entry.get('key', '<unknown>')} is missing: {', '.join(missing)}")


def apply_batch(path: str, add_missing: bool, dry_run: bool, repair: bool) -> None:
    entries, removals, remove_ids = load_batch(path)
    for entry in entries:
        validate_entry(entry)

    shared_content = read_text(SHARED_DATA)
    if repair:
        shared_content = normalize_table_tail(shared_content, shared=True)
    shared_entries = parse_shared_entries(shared_content)
    next_id = lowest_manual_id(shared_content) - 1

    removed_ids: list[int] = list(remove_ids)
    for key in removals:
        shared_entry = shared_entries.get(key)
        if shared_entry is None:
            print(f"  SKIP remove missing key: {key}")
            continue
        shared_content, removed = remove_entry_block(shared_content, shared_entry.entry_id, "m_Key")
        if removed:
            removed_ids.append(shared_entry.entry_id)
            print(f"  REMOVE {key} (id {shared_entry.entry_id})")
            shared_entries.pop(key, None)

    added: list[tuple[int, dict[str, str]]] = []
    updates: list[tuple[int, dict[str, str]]] = []
    for entry in entries:
        key = entry["key"]
        shared_entry = shared_entries.get(key)
        if shared_entry is None:
            if not add_missing:
                raise KeyError(f"Missing key: {key}. Re-run with --add-missing to create it.")
            entry_id = next_id
            next_id -= 1
            shared_content = append_shared_entry(shared_content, entry_id, key)
            shared_entries[key] = SharedEntry(entry_id, key)
            added.append((entry_id, entry))
            print(f"  ADD    {key} (id {entry_id})")
        else:
            updates.append((shared_entry.entry_id, entry))
            print(f"  UPDATE {key} (id {shared_entry.entry_id})")

    if not dry_run:
        write_text(SHARED_DATA, shared_content)

    for locale, file_path in LOCALE_FILES.items():
        content = read_text(file_path)
        if repair:
            content = normalize_table_tail(content, shared=False)

        for entry_id in removed_ids:
            content, _ = remove_entry_block(content, entry_id, "m_Localized")

        for entry_id, entry in updates:
            content, found = update_locale_value(content, entry_id, entry[locale], locale)
            if not found:
                content = append_locale_entry(content, entry_id, entry[locale], locale)

        for entry_id, entry in added:
            content = append_locale_entry(content, entry_id, entry[locale], locale)

        if not dry_run:
            write_text(file_path, content)
        print(f"  {'DRY ' if dry_run else ''}Wrote {os.path.basename(file_path)}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Batch update Dynamic Table localization entries.")
    parser.add_argument("--file", required=True, help="Batch JSON file.")
    parser.add_argument("--add-missing", action="store_true", help="Create missing keys.")
    parser.add_argument("--repair", action="store_true", help="Clean duplicate trailing markers.")
    parser.add_argument("--dry-run", action="store_true", help="Preview without writing.")
    args = parser.parse_args()

    try:
        apply_batch(args.file, add_missing=args.add_missing, dry_run=args.dry_run, repair=args.repair)
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
