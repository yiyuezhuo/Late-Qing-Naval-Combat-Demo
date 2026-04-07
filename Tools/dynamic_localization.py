#!/usr/bin/env python3
"""
dynamic_localization.py — Query and scan Dynamic Table localization usage.

COMMANDS
--------
  query <key>
      Look up a Dynamic Table key and print its ID and translations.
      If not found, prints close-match reuse suggestions.

  scan-cs path
      Scan one C# file for Dynamic Table lookups such as Localize("..."),
      ILocalizeService.Get("..."), LocalizeEnum(...), and GetEnum(...).
      Prints exact matches, missing keys, and suggested add commands.
"""

import argparse
import difflib
import os
import re
import sys

from add_localization import LOCALE_FILES, SHARED_DATA

ROOT = os.path.join(os.path.dirname(__file__), "..")


def read_file(path: str) -> str:
    with open(path, "r", encoding="utf-8") as f:
        return f.read()


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


def load_state() -> tuple[dict[str, int], dict[str, dict[int, str]]]:
    shared = read_file(SHARED_DATA)
    keys = parse_shared_keys(shared)
    locale_values = {
        locale: parse_locale_values(read_file(path))
        for locale, path in LOCALE_FILES.items()
    }
    return keys, locale_values


def print_entry_details(key: str, entry_id: int, locale_values: dict[str, dict[int, str]]):
    print(f"FOUND: '{key}'  (ID: {entry_id})\n")
    for locale in ("en", "ja", "zh-hans", "zh-hant"):
        print(f"  {locale:8s}: {locale_values[locale].get(entry_id, '<missing>')}")


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


def make_add_command(key: str, en_value: str | None = None) -> str:
    en_value = en_value or key
    return " ".join([
        "python Tools\\add_localization.py",
        f"--key {shell_quote(key)}",
        f"--en {shell_quote(en_value)}",
        '--ja "..."',
        '--zh-hans "..."',
        '--zh-hant "..."',
    ])


def get_line_number(content: str, index: int) -> int:
    return content.count("\n", 0, index) + 1


def normalize_enum_key(expr: str) -> str | None:
    expr = expr.strip()
    if not re.fullmatch(r"[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+", expr):
        return None

    parts = expr.split(".")
    if len(parts) < 2:
        return None
    return f"{parts[-2]}.{parts[-1]}"


def scan_cs(content: str) -> list[dict]:
    patterns = [
        ("string", "Localize", re.compile(r'\bLocalize\(\s*"(?P<value>[^"\r\n]+)"')),
        ("string", "ILocalizeService.Get", re.compile(r'\b(?:ServiceLocator\.)?Get<ILocalizeService>\(\)\.Get\(\s*"(?P<value>[^"\r\n]+)"')),
        ("enum", "LocalizeEnum", re.compile(r'\bLocalizeEnum\(\s*(?P<expr>[^,\r\n\)]+)')),
        ("enum", "ILocalizeService.GetEnum", re.compile(r'\b(?:ServiceLocator\.)?Get<ILocalizeService>\(\)\.GetEnum\(\s*(?P<expr>[^,\r\n\)]+)')),
    ]

    findings = []
    seen = set()

    for finding_type, source, pattern in patterns:
        for match in pattern.finditer(content):
            payload = match.groupdict().get("value") or match.groupdict().get("expr")
            line = get_line_number(content, match.start())
            signature = (finding_type, source, line, payload)
            if signature in seen:
                continue
            seen.add(signature)
            findings.append({
                "type": finding_type,
                "source": source,
                "line": line,
                "payload": payload.strip(),
            })

    findings.sort(key=lambda item: (item["line"], item["source"], item["payload"]))
    return findings


def cmd_query(key: str):
    keys, locale_values = load_state()
    if key not in keys:
        print(f"NOT FOUND: '{key}' is not in Dynamic Table.")
        print_suggestions(keys, key)
        print("Use Tools\\add_localization.py to create it.")
        sys.exit(1)

    print_entry_details(key, keys[key], locale_values)


def cmd_scan_cs(path: str):
    full_path = os.path.abspath(path)
    if not os.path.exists(full_path):
        print(f"File not found: {path}")
        sys.exit(1)
    if not full_path.lower().endswith(".cs"):
        print(f"Expected a .cs file: {path}")
        sys.exit(1)

    content = read_file(full_path)
    findings = scan_cs(content)
    keys, _ = load_state()

    print(f"SCAN: {os.path.relpath(full_path, ROOT)}")
    print(f"Dynamic lookups found: {len(findings)}")

    if not findings:
        return

    for finding in findings:
        print()
        if finding["type"] == "string":
            key = finding["payload"]
            print(f'L{finding["line"]}: {finding["source"]}("{key}")')
            exact_id = keys.get(key)
            if exact_id is not None:
                print(f"  Exact key exists: ID {exact_id}")
                continue

            print("  No exact Dynamic Table key found.")
            print_suggestions(keys, key)
            print("  Suggested add command:")
            print(f"    {make_add_command(key)}")
            continue

        expr = finding["payload"]
        enum_key = normalize_enum_key(expr)
        print(f'L{finding["line"]}: {finding["source"]}({expr})')
        if enum_key is None:
            print("  Could not derive an exact enum key from this expression.")
            print("  Verify that all members of the referenced enum exist in Dynamic Table.")
            continue

        print(f"  Derived enum key: {enum_key}")
        exact_id = keys.get(enum_key)
        if exact_id is not None:
            print(f"  Exact key exists: ID {exact_id}")
            continue

        print("  No exact Dynamic Table key found.")
        print_suggestions(keys, enum_key)
        print("  Suggested add command:")
        print(f"    {make_add_command(enum_key, enum_key.split('.', 1)[1])}")


def main():
    parser = argparse.ArgumentParser(
        description="Query and scan Dynamic Table localization entries.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    sub = parser.add_subparsers(dest="command", required=True)

    p_query = sub.add_parser("query", help="Look up a Dynamic Table key and print its translations.")
    p_query.add_argument("key", help="Exact key string to look up")

    p_scan = sub.add_parser("scan-cs", help="Scan a C# file for Dynamic Table lookups.")
    p_scan.add_argument("path", help="Path to the C# file")

    args = parser.parse_args()

    if args.command == "query":
        cmd_query(args.key)
    elif args.command == "scan-cs":
        cmd_scan_cs(args.path)


if __name__ == "__main__":
    main()
