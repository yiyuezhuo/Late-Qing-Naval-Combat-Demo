#!/usr/bin/env python3
"""
standard_localization.py — Query, add, batch-ensure, and scan Standard Table localization.

The Standard Table is used for UXML-bound static labels and text
(`LocalizedString property="label"` / `property="text"`).

COMMANDS
--------
  query <key> [--prop label|text]
      Look up a key and print its ID, translations, and a UXML snippet.
      If not found, prints close-match reuse suggestions.

  add --key "..." --en "..." --ja "..." --zh-hans "..." --zh-hant "..."
      Add one key. Prints the assigned ID and a UXML snippet.
      Use --dry-run to preview without writing.

  ensure --file path [--dry-run]
      Batch "query or add". Existing keys are reused; missing keys are created.
      Supports JSON or pipe-delimited text files.

  scan-uxml path
      Scan one UXML file for text/label attributes that are not localized via
      LocalizedString/DataBinding on the same property. Prints reuse suggestions
      and add commands/snippets.

  list
      List all keys in Standard Table with their IDs.

BATCH FILE FORMAT
-----------------
  JSON:
  [
    {
      "key": "Land Battle Losses",
      "en": "Land Battle Losses",
      "ja": "陸上戦損失",
      "zh-hans": "陆地战役损失",
      "zh-hant": "陸地戰役損失",
      "prop": "text"
    }
  ]

  Text:
    key|en|ja|zh-hans|zh-hant
    key|en|ja|zh-hans|zh-hant|text

STANDARD TABLE GUID
-------------------
  7dfd13ea0ff0ef0408a7f015356a0054
"""

import argparse
import difflib
import json
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(__file__), "..")
BASE = os.path.join(ROOT, "Assets", "StandardStringTableCollection")
SHARED_DATA = os.path.join(BASE, "Standard Table Shared Data.asset")

LOCALE_FILES = {
    "en": os.path.join(BASE, "Standard Table_en.asset"),
    "ja": os.path.join(BASE, "Standard Table_ja.asset"),
    "zh-hans": os.path.join(BASE, "Standard Table_zh-Hans.asset"),
    "zh-hant": os.path.join(BASE, "Standard Table_zh-Hant.asset"),
}

TABLE_GUID = "7dfd13ea0ff0ef0408a7f015356a0054"
SHARED_MARKER = "  m_Metadata:\n    m_Items: []\n  m_KeyGenerator:"
LOCALE_MARKER = "  references:\n    version: 2"
VALID_PROPS = {"label", "text"}


# ---------------------------------------------------------------------------
# File helpers
# ---------------------------------------------------------------------------

def read_file(path: str) -> str:
    with open(path, "r", encoding="utf-8") as f:
        return f.read()


def write_file(path: str, content: str):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(content)


# ---------------------------------------------------------------------------
# YAML helpers
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
        "<UnityEngine.Localization.LocalizedString",
        f'    property="{prop}"',
        f'    table="GUID:{TABLE_GUID}"',
        f'    entry="Id({entry_id})" />',
    ]
    return "\n".join(lines)


# ---------------------------------------------------------------------------
# Parsing helpers
# ---------------------------------------------------------------------------

def parse_shared_keys(content: str) -> dict[str, int]:
    result = {}
    for match in re.finditer(r"- m_Id: (-?\d+)\s+m_Key: (.+)", content):
        entry_id = int(match.group(1))
        key = match.group(2).strip()
        if key.startswith("'") and key.endswith("'"):
            key = key[1:-1].replace("''", "'")
        elif key.startswith('"') and key.endswith('"'):
            key = key[1:-1]
        result[key] = entry_id
    return result


def parse_locale_values(content: str) -> dict[int, str]:
    result = {}
    pattern = r"- m_Id: (-?\d+)\s+m_Localized: (.*?)\s+m_Metadata:"
    for match in re.finditer(pattern, content, re.DOTALL):
        entry_id = int(match.group(1))
        value = match.group(2).strip()
        if value.startswith("'") and value.endswith("'"):
            value = value[1:-1].replace("''", "'")
        elif value.startswith('"') and value.endswith('"'):
            value = value[1:-1].encode("raw_unicode_escape").decode("unicode_escape")
        result[entry_id] = value
    return result


def load_state() -> tuple[str, dict[str, int], dict[str, dict[int, str]]]:
    shared = read_file(SHARED_DATA)
    keys = parse_shared_keys(shared)
    locale_values = {
        locale: parse_locale_values(read_file(path))
        for locale, path in LOCALE_FILES.items()
    }
    return shared, keys, locale_values


def next_id(shared_content: str) -> int:
    ids = [int(x) for x in re.findall(r"m_Id: (-\d+)", shared_content)]
    return (min(ids) - 1) if ids else -1


def normalize_prop(prop: str) -> str:
    if prop not in VALID_PROPS:
        raise ValueError(f"Invalid prop '{prop}'. Expected one of: {', '.join(sorted(VALID_PROPS))}.")
    return prop


def print_entry_details(key: str, entry_id: int, locale_values: dict[str, dict[int, str]], prop: str):
    print(f"FOUND: '{key}'  (ID: {entry_id})\n")
    for locale in ("en", "ja", "zh-hans", "zh-hant"):
        print(f"  {locale:8s}: {locale_values[locale].get(entry_id, '<missing>')}")

    print()
    print("UXML binding snippet (paste inside a <Bindings> block):")
    print(uxml_snippet(entry_id, prop))


def find_suggestions(keys: dict[str, int], query: str, limit: int = 5) -> list[tuple[str, int, str]]:
    lowered_query = query.casefold()
    suggestions: list[tuple[str, int, str]] = []
    seen = set()

    def add(candidate: str, reason: str):
        if candidate in seen or candidate == query:
            return
        seen.add(candidate)
        suggestions.append((candidate, keys[candidate], reason))

    contains = sorted(
        (candidate for candidate in keys if lowered_query in candidate.casefold()),
        key=lambda candidate: (abs(len(candidate) - len(query)), candidate.casefold()),
    )
    for candidate in contains:
        add(candidate, "contains")
        if len(suggestions) >= limit:
            return suggestions

    prefix = sorted(
        (candidate for candidate in keys if candidate.casefold().startswith(lowered_query[: max(1, len(lowered_query) // 2)])),
        key=lambda candidate: (abs(len(candidate) - len(query)), candidate.casefold()),
    )
    for candidate in prefix:
        add(candidate, "prefix")
        if len(suggestions) >= limit:
            return suggestions

    close = difflib.get_close_matches(query, list(keys.keys()), n=limit * 2, cutoff=0.45)
    for candidate in close:
        add(candidate, "close")
        if len(suggestions) >= limit:
            return suggestions

    return suggestions


def print_suggestions(keys: dict[str, int], query: str, limit: int = 5):
    suggestions = find_suggestions(keys, query, limit=limit)
    if not suggestions:
        return

    print("Possible reuse candidates:")
    for candidate, entry_id, reason in suggestions:
        print(f"  - {candidate}  (ID: {entry_id}, {reason} match)")


def shell_quote(value: str) -> str:
    return '"' + value.replace('"', '\\"') + '"'


def make_add_command(text: str, prop: str) -> str:
    parts = [
        "python Tools/standard_localization.py add",
        f"--key {shell_quote(text)}",
        f"--en {shell_quote(text)}",
        '--ja "..."',
        '--zh-hans "..."',
        '--zh-hant "..."',
    ]
    if prop != "label":
        parts.append(f"--prop {prop}")
    return " ".join(parts)


def load_batch_entries(path: str) -> list[dict]:
    raw = read_file(path)
    stripped = raw.lstrip()
    entries: list[dict] = []

    if stripped.startswith("["):
        data = json.loads(raw)
        if not isinstance(data, list):
            raise ValueError("Batch JSON must be a list of objects.")
        for index, item in enumerate(data, start=1):
            if not isinstance(item, dict):
                raise ValueError(f"Batch JSON item {index} must be an object.")
            prop = normalize_prop(item.get("prop", "label"))
            entries.append({
                "key": item["key"],
                "en": item["en"],
                "ja": item["ja"],
                "zh-hans": item["zh-hans"],
                "zh-hant": item["zh-hant"],
                "prop": prop,
            })
        return entries

    for line_number, line in enumerate(raw.splitlines(), start=1):
        stripped_line = line.strip()
        if not stripped_line or stripped_line.startswith("#"):
            continue

        parts = [part.strip() for part in line.split("|")]
        if len(parts) not in (5, 6):
            raise ValueError(
                f"Invalid batch line {line_number}: expected "
                "key|en|ja|zh-hans|zh-hant[|prop]"
            )

        prop = normalize_prop(parts[5] if len(parts) == 6 else "label")
        entries.append({
            "key": parts[0],
            "en": parts[1],
            "ja": parts[2],
            "zh-hans": parts[3],
            "zh-hant": parts[4],
            "prop": prop,
        })

    return entries


def ensure_entries(entries: list[dict], dry_run: bool) -> list[dict]:
    shared = read_file(SHARED_DATA)
    locale_contents = {locale: read_file(path) for locale, path in LOCALE_FILES.items()}
    existing = parse_shared_keys(shared)
    next_available_id = next_id(shared)

    shared_blocks: list[str] = []
    locale_blocks = {locale: [] for locale in LOCALE_FILES}
    results: list[dict] = []

    for entry in entries:
        key = entry["key"]
        prop = normalize_prop(entry.get("prop", "label"))

        if key in existing:
            results.append({
                "status": "exists",
                "key": key,
                "id": existing[key],
                "prop": prop,
            })
            continue

        entry_id = next_available_id
        next_available_id -= 1
        existing[key] = entry_id

        shared_blocks.append(
            f"  - m_Id: {entry_id}\n"
            f"    m_Key: {yaml_quote(key)}\n"
            f"    m_Metadata:\n"
            f"      m_Items: []\n"
        )

        locale_payloads = {
            "en": (entry["en"], False),
            "ja": (entry["ja"], True),
            "zh-hans": (entry["zh-hans"], True),
            "zh-hant": (entry["zh-hant"], True),
        }
        for locale, (raw_value, escape_unicode) in locale_payloads.items():
            locale_blocks[locale].append(
                f"  - m_Id: {entry_id}\n"
                f"    m_Localized: {yaml_quote(raw_value, escape_unicode=escape_unicode)}\n"
                f"    m_Metadata:\n"
                f"      m_Items: []\n"
            )

        results.append({
            "status": "added",
            "key": key,
            "id": entry_id,
            "prop": prop,
        })

    if shared_blocks and not dry_run:
        updated_shared = shared.replace(SHARED_MARKER, "".join(shared_blocks) + SHARED_MARKER, 1)
        write_file(SHARED_DATA, updated_shared)
        print(f"Wrote {os.path.basename(SHARED_DATA)}")

        for locale, path in LOCALE_FILES.items():
            updated_locale = locale_contents[locale].replace(
                LOCALE_MARKER,
                "".join(locale_blocks[locale]) + LOCALE_MARKER,
                1,
            )
            write_file(path, updated_locale)
            print(f"Wrote {os.path.basename(path)}")

    elif dry_run and shared_blocks:
        print("DRY RUN — no files written.")

    return results


# ---------------------------------------------------------------------------
# UXML scanning
# ---------------------------------------------------------------------------

def has_binding_for_property(body: str, prop: str) -> bool:
    return (
        f'LocalizedString property="{prop}"' in body or
        f'DataBinding property="{prop}"' in body
    )


def get_line_number(content: str, index: int) -> int:
    return content.count("\n", 0, index) + 1


def extract_attribute(attrs: str, name: str) -> str | None:
    match = re.search(rf'\b{name}="([^"]+)"', attrs)
    return match.group(1) if match else None


def scan_uxml(content: str) -> list[dict]:
    findings: list[dict] = []
    occupied_spans: list[tuple[int, int]] = []

    paired_pattern = re.compile(
        r"<(?P<tag>ui:\w+)(?P<attrs>[^<>]*?\b(?P<prop>text|label)=\"(?P<value>[^\"]+)\"[^<>]*)>"
        r"(?P<body>.*?)"
        r"</(?P=tag)>",
        re.DOTALL,
    )
    self_closing_pattern = re.compile(
        r"<(?P<tag>ui:\w+)(?P<attrs>[^<>]*?\b(?P<prop>text|label)=\"(?P<value>[^\"]+)\"[^<>]*)/>"
    )

    for match in paired_pattern.finditer(content):
        tag = match.group("tag")
        attrs = match.group("attrs")
        prop = match.group("prop")
        value = match.group("value")
        body = match.group("body")
        if has_binding_for_property(body, prop):
            occupied_spans.append((match.start(), match.end()))
            continue

        findings.append({
            "tag": tag,
            "prop": prop,
            "value": value,
            "name": extract_attribute(attrs, "name"),
            "line": get_line_number(content, match.start()),
        })
        occupied_spans.append((match.start(), match.end()))

    def overlaps_existing(start: int, end: int) -> bool:
        return any(not (end <= taken_start or start >= taken_end) for taken_start, taken_end in occupied_spans)

    for match in self_closing_pattern.finditer(content):
        if overlaps_existing(match.start(), match.end()):
            continue

        findings.append({
            "tag": match.group("tag"),
            "prop": match.group("prop"),
            "value": match.group("value"),
            "name": extract_attribute(match.group("attrs"), "name"),
            "line": get_line_number(content, match.start()),
        })

    findings.sort(key=lambda item: item["line"])
    unique = []
    seen = set()
    for finding in findings:
        signature = (finding["line"], finding["tag"], finding["prop"], finding["value"], finding["name"])
        if signature in seen:
            continue
        seen.add(signature)
        unique.append(finding)
    return unique


# ---------------------------------------------------------------------------
# Commands
# ---------------------------------------------------------------------------

def cmd_query(key: str, prop: str):
    _, keys, locale_values = load_state()

    if key not in keys:
        print(f"NOT FOUND: '{key}' is not in Standard Table.")
        print_suggestions(keys, key)
        print("Use the 'add' or 'ensure' command to create it.")
        sys.exit(1)

    print_entry_details(key, keys[key], locale_values, prop)


def cmd_list():
    _, keys, _ = load_state()
    if not keys:
        print("Standard Table is empty.")
        return

    print(f"{'ID':>12}  Key")
    print("-" * 72)
    for key, entry_id in sorted(keys.items(), key=lambda item: item[1]):
        print(f"{entry_id:12d}  {key}")


def cmd_add(key: str, en: str, ja: str, zh_hans: str, zh_hant: str, dry_run: bool, prop: str):
    results = ensure_entries([{
        "key": key,
        "en": en,
        "ja": ja,
        "zh-hans": zh_hans,
        "zh-hant": zh_hant,
        "prop": prop,
    }], dry_run=dry_run)

    result = results[0]
    status_text = "ALREADY EXISTS" if result["status"] == "exists" else ("DRY RUN — ADD" if dry_run else "ADD")
    print(f"{status_text}: '{result['key']}'  (ID: {result['id']})\n")
    print("UXML binding snippet (paste inside a <Bindings> block):")
    print(uxml_snippet(result["id"], result["prop"]))


def cmd_ensure(file_path: str, dry_run: bool):
    entries = load_batch_entries(file_path)
    results = ensure_entries(entries, dry_run=dry_run)

    for result in results:
        status_text = "EXISTS" if result["status"] == "exists" else ("DRY ADD" if dry_run else "ADD")
        print(f"\n{status_text}: '{result['key']}'  (ID: {result['id']})")
        print(uxml_snippet(result["id"], result["prop"]))


def cmd_scan_uxml(path: str):
    full_path = os.path.abspath(path)
    if not os.path.exists(full_path):
        print(f"File not found: {path}")
        sys.exit(1)
    if not full_path.lower().endswith(".uxml"):
        print(f"Expected a .uxml file: {path}")
        sys.exit(1)

    content = read_file(full_path)
    findings = scan_uxml(content)
    _, keys, _ = load_state()

    print(f"SCAN: {os.path.relpath(full_path, ROOT)}")
    print(f"Unlocalized text/label attributes found: {len(findings)}")

    if not findings:
        return

    for finding in findings:
        print()
        name_suffix = f' name="{finding["name"]}"' if finding["name"] else ""
        print(f'L{finding["line"]}: <{finding["tag"]}{name_suffix}> {finding["prop"]}="{finding["value"]}"')

        exact_id = keys.get(finding["value"])
        if exact_id is not None:
            print(f"  Exact key exists: ID {exact_id}")
            print("  Suggested binding:")
            snippet = uxml_snippet(exact_id, finding["prop"]).replace("\n", "\n    ")
            print(f"    {snippet}")
            continue

        print("  No exact Standard Table key found.")
        suggestions = find_suggestions(keys, finding["value"])
        if suggestions:
            print("  Reuse suggestions:")
            for candidate, entry_id, reason in suggestions:
                print(f"    - {candidate}  (ID: {entry_id}, {reason} match)")

        print("  Suggested add command:")
        print(f"    {make_add_command(finding['value'], finding['prop'])}")


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="Work with Standard Table localization entries.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    sub = parser.add_subparsers(dest="command", required=True)

    p_query = sub.add_parser("query", help="Look up a key and print its ID and translations.")
    p_query.add_argument("key", help="Exact key string to look up")
    p_query.add_argument("--prop", default="label", choices=sorted(VALID_PROPS), help="Property name for the UXML snippet")

    sub.add_parser("list", help="List all keys with their IDs.")

    p_add = sub.add_parser("add", help="Add a new key to Standard Table.")
    p_add.add_argument("--key", required=True)
    p_add.add_argument("--en", required=True, help="English value")
    p_add.add_argument("--ja", required=True, help="Japanese value")
    p_add.add_argument("--zh-hans", dest="zh_hans", required=True, help="Simplified Chinese")
    p_add.add_argument("--zh-hant", dest="zh_hant", required=True, help="Traditional Chinese")
    p_add.add_argument("--prop", default="label", choices=sorted(VALID_PROPS), help="Property name for the UXML snippet")
    p_add.add_argument("--dry-run", action="store_true", help="Preview without writing")

    p_ensure = sub.add_parser("ensure", help="Ensure a batch of keys exist in Standard Table.")
    p_ensure.add_argument("--file", required=True, help="JSON or pipe-delimited batch file")
    p_ensure.add_argument("--dry-run", action="store_true", help="Preview without writing")

    p_scan = sub.add_parser("scan-uxml", help="Scan a UXML file for unlocalized text/label attributes.")
    p_scan.add_argument("path", help="Path to the UXML file")

    args = parser.parse_args()

    try:
        if args.command == "query":
            cmd_query(args.key, normalize_prop(args.prop))
        elif args.command == "list":
            cmd_list()
        elif args.command == "add":
            cmd_add(args.key, args.en, args.ja, args.zh_hans, args.zh_hant, args.dry_run, normalize_prop(args.prop))
        elif args.command == "ensure":
            cmd_ensure(args.file, args.dry_run)
        elif args.command == "scan-uxml":
            cmd_scan_uxml(args.path)
    except KeyError as exc:
        print(f"Missing required batch field: {exc}")
        sys.exit(1)
    except ValueError as exc:
        print(exc)
        sys.exit(1)


if __name__ == "__main__":
    main()
