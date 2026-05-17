#!/usr/bin/env python3
"""
Batch-update Unity Standard Table localization entries.

This tool is JSON-driven so multilingual text is read from a UTF-8 file instead
of being carried through PowerShell command strings.

Batch JSON format:
{
  "entries": [
    {
      "key": "Some Standard Table Key",
      "ja": "Japanese replacement"
    },
    {
      "key": "A new key",
      "en": "English",
      "ja": "Japanese",
      "zh-hans": "Simplified Chinese",
      "zh-hant": "Traditional Chinese"
    }
  ]
}

Use --add-missing to create missing keys. Missing-key entries must include all
four locale values. Existing keys update only the locale fields supplied.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "Assets" / "StandardStringTableCollection"
SHARED_DATA = BASE / "Standard Table Shared Data.asset"

LOCALE_FILES = {
    "en": BASE / "Standard Table_en.asset",
    "ja": BASE / "Standard Table_ja.asset",
    "zh-hans": BASE / "Standard Table_zh-Hans.asset",
    "zh-hant": BASE / "Standard Table_zh-Hant.asset",
}

SHARED_INSERT_MARKER = "  m_Metadata:\n    m_Items: []\n  m_KeyGenerator:"
LOCALE_INSERT_MARKER = "  references:\n    version: 2"
MANUAL_ID_THRESHOLD = -10**15
LOCALES = tuple(LOCALE_FILES.keys())


@dataclass(frozen=True)
class SharedEntry:
    entry_id: int
    key: str


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def write_text(path: Path, content: str) -> None:
    path.write_text(content, encoding="utf-8", newline="\n")


def yaml_unquote(value: str) -> str:
    value = value.strip()
    if value.startswith("'") and value.endswith("'"):
        return value[1:-1].replace("''", "'")
    if value.startswith('"') and value.endswith('"'):
        return bytes(value[1:-1], "utf-8").decode("unicode_escape")
    return value


def yaml_double_escape(value: str, escape_unicode: bool) -> str:
    result: list[str] = []
    for ch in value:
        codepoint = ord(ch)
        if ch == "\\":
            result.append("\\\\")
        elif ch == '"':
            result.append('\\"')
        elif ch == "\n":
            result.append("\\n")
        elif ch == "\r":
            result.append("\\r")
        elif ch == "\t":
            result.append("\\t")
        elif codepoint < 32:
            result.append(f"\\x{codepoint:02X}")
        elif escape_unicode and codepoint > 127:
            if codepoint <= 0xFFFF:
                result.append(f"\\u{codepoint:04X}")
            else:
                result.append(f"\\U{codepoint:08X}")
        else:
            result.append(ch)
    return "".join(result)


def yaml_quote(value: str, escape_unicode: bool = False) -> str:
    if escape_unicode or any(ch in value for ch in "\n\r\t\\\""):
        return '"' + yaml_double_escape(value, escape_unicode=escape_unicode) + '"'

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


def load_batch(path: Path) -> list[dict[str, Any]]:
    with path.open("r", encoding="utf-8-sig") as file:
        payload = json.load(file)

    entries = payload.get("entries") if isinstance(payload, dict) else payload
    if not isinstance(entries, list):
        raise ValueError("Batch file must contain a JSON list or an object with entries.")

    for index, entry in enumerate(entries, start=1):
        if not isinstance(entry, dict):
            raise ValueError(f"Entry {index}: expected an object.")
        if not isinstance(entry.get("key"), str) or not entry["key"]:
            raise ValueError(f"Entry {index}: key must be a non-empty string.")
        supplied_locales = [locale for locale in LOCALES if locale in entry]
        if not supplied_locales:
            raise ValueError(f"Entry {index}: supply at least one locale field.")
        for locale in supplied_locales:
            if not isinstance(entry[locale], str):
                raise ValueError(f"Entry {index}: {locale} must be a string.")

    return entries


def apply_batch(path: Path, add_missing: bool, dry_run: bool) -> None:
    entries = load_batch(path)

    shared_content = read_text(SHARED_DATA)
    shared_entries = parse_shared_entries(shared_content)
    next_id = lowest_manual_id(shared_content) - 1

    added: list[tuple[int, dict[str, Any]]] = []
    updates: list[tuple[int, dict[str, Any], list[str]]] = []

    for entry in entries:
        key = entry["key"]
        shared_entry = shared_entries.get(key)
        supplied_locales = [locale for locale in LOCALES if locale in entry]

        if shared_entry is None:
            if not add_missing:
                raise KeyError(f"Missing key: {key}. Re-run with --add-missing to create it.")
            missing = [locale for locale in LOCALES if locale not in entry]
            if missing:
                raise ValueError(f"Missing-key entry {key!r} is missing: {', '.join(missing)}")

            entry_id = next_id
            next_id -= 1
            shared_content = append_shared_entry(shared_content, entry_id, key)
            shared_entries[key] = SharedEntry(entry_id, key)
            added.append((entry_id, entry))
            print(f"  ADD    {key} (id {entry_id})")
            continue

        updates.append((shared_entry.entry_id, entry, supplied_locales))
        locale_text = ", ".join(supplied_locales)
        print(f"  UPDATE {key} (id {shared_entry.entry_id}; {locale_text})")

    shared_changed = bool(added)
    if shared_changed and not dry_run:
        write_text(SHARED_DATA, shared_content)
    elif shared_changed:
        print(f"  DRY Wrote {SHARED_DATA.name}")

    for locale, file_path in LOCALE_FILES.items():
        content = read_text(file_path)
        original_content = content

        for entry_id, entry, supplied_locales in updates:
            if locale not in supplied_locales:
                continue
            content, found = update_locale_value(content, entry_id, entry[locale], locale)
            if not found:
                content = append_locale_entry(content, entry_id, entry[locale], locale)

        for entry_id, entry in added:
            content = append_locale_entry(content, entry_id, entry[locale], locale)

        if content == original_content:
            print(f"  SKIP  {file_path.name}")
            continue

        if not dry_run:
            write_text(file_path, content)
        print(f"  {'DRY ' if dry_run else ''}Wrote {file_path.name}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Batch update Standard Table localization entries.")
    parser.add_argument("--file", required=True, help="UTF-8 JSON batch file.")
    parser.add_argument("--add-missing", action="store_true", help="Create missing keys.")
    parser.add_argument("--dry-run", action="store_true", help="Preview without writing.")
    args = parser.parse_args()

    try:
        apply_batch(Path(args.file), add_missing=args.add_missing, dry_run=args.dry_run)
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
