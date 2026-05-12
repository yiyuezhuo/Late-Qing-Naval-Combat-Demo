#!/usr/bin/env python3
"""
normalize_localization_ids.py — Normalize localization IDs and verify their health.

USAGE
-----
  python Tools/normalize_localization_ids.py              # dry run
  python Tools/normalize_localization_ids.py --apply      # apply normalization
  python Tools/normalize_localization_ids.py status       # show current summary
  python Tools/normalize_localization_ids.py verify       # fail if issues remain

WHAT IT DOES
------------
  - Renumbers all negative IDs in Dynamic Table and Standard Table to -1, -2, -3, ...
  - Updates Standard Table UXML references when their entry IDs change.
  - Reports the current negative-ID range, next free ID, fragmentation, legacy large negatives,
    duplicate IDs inside table assets, locale synchronization status, and Standard Table
    UXML reference health.
"""

import argparse
import glob
import os
import re
import sys

ROOT = os.path.join(os.path.dirname(__file__), "..")
LARGE_NEGATIVE_THRESHOLD = 10 ** 15

TABLES = {
    "dynamic": {
        "shared": "Assets/DynamicStringTableCollection/Dynamic Table Shared Data.asset",
        "locales": [
            "Assets/DynamicStringTableCollection/Dynamic Table_en.asset",
            "Assets/DynamicStringTableCollection/Dynamic Table_ja.asset",
            "Assets/DynamicStringTableCollection/Dynamic Table_zh-Hans.asset",
            "Assets/DynamicStringTableCollection/Dynamic Table_zh-Hant.asset",
        ],
        "uxml_guid": None,
    },
    "standard": {
        "shared": "Assets/StandardStringTableCollection/Standard Table Shared Data.asset",
        "locales": [
            "Assets/StandardStringTableCollection/Standard Table_en.asset",
            "Assets/StandardStringTableCollection/Standard Table_ja.asset",
            "Assets/StandardStringTableCollection/Standard Table_zh-Hans.asset",
            "Assets/StandardStringTableCollection/Standard Table_zh-Hant.asset",
        ],
        "uxml_guid": "7dfd13ea0ff0ef0408a7f015356a0054",
    },
}


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def abs_path(rel: str) -> str:
    return os.path.normpath(os.path.join(ROOT, rel))


def read(path: str) -> str:
    with open(abs_path(path), "r", encoding="utf-8") as f:
        return f.read()


def write(path: str, content: str, apply: bool):
    if apply:
        with open(abs_path(path), "w", encoding="utf-8", newline="\n") as f:
            f.write(content)


def parse_all_ids(content: str) -> list[int]:
    return [int(value) for value in re.findall(r"m_Id: (-?\d+)", content)]


def find_duplicate_ids(ids: list[int]) -> list[int]:
    seen = set()
    duplicates = []
    for entry_id in ids:
        if entry_id in seen and entry_id not in duplicates:
            duplicates.append(entry_id)
        seen.add(entry_id)
    return sorted(duplicates)


def parse_negative_ids(content: str) -> list[int]:
    ids = [entry_id for entry_id in parse_all_ids(content) if entry_id < 0]
    return sorted(set(ids), reverse=True)


def expected_sequential_ids(count: int) -> list[int]:
    return [-(index + 1) for index in range(count)]


def is_sequential(ids: list[int]) -> bool:
    return ids == expected_sequential_ids(len(ids))


def next_free_negative_id(ids: list[int]) -> int:
    return (min(ids) - 1) if ids else -1


def build_mapping(ids: list[int]) -> dict[int, int]:
    return {old: -(index + 1) for index, old in enumerate(ids)}


def apply_mapping_shared(content: str, mapping: dict[int, int]) -> str:
    for old, new in mapping.items():
        content = re.sub(
            rf"(m_Id: ){re.escape(str(old))}(\b)",
            lambda match, new=new: f"{match.group(1)}{new}",
            content,
        )
    return content


def apply_mapping_locale(content: str, mapping: dict[int, int]) -> str:
    return apply_mapping_shared(content, mapping)


def apply_mapping_uxml(content: str, mapping: dict[int, int]) -> str:
    for old, new in mapping.items():
        content = content.replace(f'entry="Id({old})"', f'entry="Id({new})"')
    return content


def find_uxml_files() -> list[str]:
    pattern = os.path.join(abs_path("Assets"), "**", "*.uxml")
    return glob.glob(pattern, recursive=True)


def find_table_uxml_refs(guid: str) -> list[dict]:
    pattern = re.compile(
        rf'<UnityEngine\.Localization\.LocalizedString\b'
        rf'(?=[^>]*\btable="GUID:{re.escape(guid)}")'
        rf'(?=[^>]*\bentry="Id\((-?\d+)\)")'
        rf'[^>]*/?>'
    )
    refs = []
    for path in find_uxml_files():
        with open(path, "r", encoding="utf-8") as f:
            content = f.read()

        for match in pattern.finditer(content):
            refs.append({
                "path": os.path.relpath(path, abs_path(".")),
                "id": int(match.group(1)),
                "line": content.count("\n", 0, match.start()) + 1,
            })
    return refs


def summarize_table(name: str, cfg: dict) -> dict:
    shared_content = read(cfg["shared"])
    shared_all_ids = parse_all_ids(shared_content)
    shared_duplicate_ids = find_duplicate_ids(shared_all_ids)
    negative_ids = parse_negative_ids(shared_content)

    locale_sets = {}
    locale_duplicate_ids = []
    locale_mismatches = []
    for locale_path in cfg["locales"]:
        locale_all_ids = parse_all_ids(read(locale_path))
        duplicate_ids = find_duplicate_ids(locale_all_ids)
        if duplicate_ids:
            locale_duplicate_ids.append({
                "path": locale_path,
                "duplicates": duplicate_ids,
            })

        locale_ids = set(entry_id for entry_id in locale_all_ids if entry_id < 0)
        locale_sets[locale_path] = locale_ids
        shared_set = set(negative_ids)
        if locale_ids != shared_set:
            locale_mismatches.append({
                "path": locale_path,
                "missing": sorted(shared_set - locale_ids, reverse=True),
                "extra": sorted(locale_ids - shared_set, reverse=True),
            })

    uxml_refs = []
    missing_uxml_refs = []
    if cfg["uxml_guid"]:
        uxml_refs = find_table_uxml_refs(cfg["uxml_guid"])
        valid_ids = set(shared_all_ids)
        missing_uxml_refs = [ref for ref in uxml_refs if ref["id"] not in valid_ids]

    large_negative_ids = [entry_id for entry_id in negative_ids if abs(entry_id) >= LARGE_NEGATIVE_THRESHOLD]
    return {
        "name": name,
        "shared_path": cfg["shared"],
        "negative_ids": negative_ids,
        "negative_count": len(negative_ids),
        "all_id_count": len(shared_all_ids),
        "shared_duplicate_ids": shared_duplicate_ids,
        "locale_duplicate_ids": locale_duplicate_ids,
        "sequential": is_sequential(negative_ids),
        "next_free_negative_id": next_free_negative_id(negative_ids),
        "large_negative_ids": large_negative_ids,
        "locale_mismatches": locale_mismatches,
        "uxml_refs": uxml_refs,
        "missing_uxml_refs": missing_uxml_refs,
    }


# ---------------------------------------------------------------------------
# Commands
# ---------------------------------------------------------------------------

def migrate_table(name: str, cfg: dict, apply: bool) -> dict[int, int]:
    print(f"\n{'=' * 60}")
    print(f"  TABLE: {name.upper()}")
    print(f"{'=' * 60}")

    shared_content = read(cfg["shared"])
    ids = parse_negative_ids(shared_content)

    if not ids:
        print("  No negative IDs found. Nothing to do.")
        return {}

    mapping = build_mapping(ids)
    print(f"  {len(mapping)} negative IDs to renumber:")
    for old, new in mapping.items():
        print(f"    {old:>25}  →  {new}")

    new_shared = apply_mapping_shared(shared_content, mapping)
    if new_shared != shared_content:
        print(f"\n  {'WRITE' if apply else 'DRY'}  {cfg['shared']}")
        write(cfg["shared"], new_shared, apply)

    for locale_path in cfg["locales"]:
        content = read(locale_path)
        new_content = apply_mapping_locale(content, mapping)
        if new_content != content:
            print(f"  {'WRITE' if apply else 'DRY'}  {locale_path}")
            write(locale_path, new_content, apply)
        else:
            print(f"  SKIP   {locale_path}  (no matches)")

    return mapping


def migrate_uxml(mapping: dict[int, int], guid: str, apply: bool):
    if not mapping or not guid:
        return

    hits = 0
    for path in find_uxml_files():
        with open(path, "r", encoding="utf-8") as f:
            content = f.read()

        if guid not in content:
            continue
        if not re.search(r'entry="Id\(-?\d+\)"', content):
            continue

        new_content = apply_mapping_uxml(content, mapping)
        if new_content != content:
            rel_path = os.path.relpath(path, abs_path("."))
            print(f"  {'WRITE' if apply else 'DRY'}  {rel_path}")
            if apply:
                with open(path, "w", encoding="utf-8", newline="\n") as f:
                    f.write(new_content)
            hits += 1

    if hits == 0:
        print("  No UXML files needed updating.")


def cmd_normalize(apply: bool):
    if not apply:
        print("DRY RUN -- pass --apply to write changes\n")

    for table_name, cfg in TABLES.items():
        mapping = migrate_table(table_name, cfg, apply)
        if cfg["uxml_guid"]:
            print(f"\n  UXML files ({table_name}):")
            migrate_uxml(mapping, cfg["uxml_guid"], apply)

    if not apply:
        print("\n--- Dry run complete. Run with --apply to write. ---")
    else:
        print("\n--- Done. Localization IDs are now normalized. ---")


def cmd_status():
    for table_name, cfg in TABLES.items():
        summary = summarize_table(table_name, cfg)
        negative_ids = summary["negative_ids"]
        range_text = "none"
        if negative_ids:
            range_text = f"{min(negative_ids)} .. {max(negative_ids)}"

        print(f"\n{table_name.upper()}")
        print("-" * 40)
        print(f"Shared asset: {summary['shared_path']}")
        print(f"Negative IDs: {summary['negative_count']}")
        print(f"Negative range: {range_text}")
        print(f"Sequential: {'yes' if summary['sequential'] else 'no'}")
        print(f"Next free negative ID: {summary['next_free_negative_id']}")
        print(f"Large-magnitude negatives: {len(summary['large_negative_ids'])}")
        print(f"Shared duplicate IDs: {len(summary['shared_duplicate_ids'])}")
        print(f"Locale files with duplicate IDs: {len(summary['locale_duplicate_ids'])}")
        print(f"Locale sync: {'ok' if not summary['locale_mismatches'] else 'mismatch'}")

        if summary["uxml_refs"]:
            print(f"UXML refs: {len(summary['uxml_refs'])}")
            print(f"Missing UXML refs: {len(summary['missing_uxml_refs'])}")


def cmd_verify() -> int:
    issues = []
    for table_name, cfg in TABLES.items():
        summary = summarize_table(table_name, cfg)
        if summary["shared_duplicate_ids"]:
            duplicate_text = ", ".join(str(entry_id) for entry_id in summary["shared_duplicate_ids"])
            issues.append(f"{table_name}: duplicate IDs in {summary['shared_path']}: {duplicate_text}.")
        for duplicate in summary["locale_duplicate_ids"]:
            duplicate_text = ", ".join(str(entry_id) for entry_id in duplicate["duplicates"])
            issues.append(f"{table_name}: duplicate IDs in {duplicate['path']}: {duplicate_text}.")
        if not summary["sequential"]:
            issues.append(f"{table_name}: negative IDs are fragmented; run normalize.")
        if summary["large_negative_ids"]:
            issues.append(
                f"{table_name}: found {len(summary['large_negative_ids'])} large-magnitude negative IDs."
            )
        for mismatch in summary["locale_mismatches"]:
            issues.append(
                f"{table_name}: locale mismatch in {mismatch['path']} "
                f"(missing {len(mismatch['missing'])}, extra {len(mismatch['extra'])})."
            )
        for ref in summary["missing_uxml_refs"]:
            issues.append(
                f"{table_name}: missing UXML reference {ref['id']} at {ref['path']}:{ref['line']}."
            )

    if issues:
        print("VERIFY FAILED")
        for issue in issues:
            print(f"  - {issue}")
        return 1

    print("VERIFY OK")
    return 0


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="Normalize localization IDs or inspect localization ID health.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    parser.add_argument("command", nargs="?", choices=("status", "verify"))
    parser.add_argument("--apply", action="store_true", help="Actually write normalized files")
    args = parser.parse_args()

    if args.command == "status":
        cmd_status()
        return
    if args.command == "verify":
        sys.exit(cmd_verify())

    cmd_normalize(args.apply)


if __name__ == "__main__":
    main()
