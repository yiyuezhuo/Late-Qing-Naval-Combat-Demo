#!/usr/bin/env python3
"""
normalize_localization_ids.py — Renumber all manual (negative) localization IDs
in both Dynamic Table and Standard Table to a clean sequential scheme: -1, -2, -3, ...

WHAT IT DOES
------------
  Before:  -97000000100001, -96000000010001, -81000000100002, ...  (fragmented ad-hoc ranges)
  After:   -1, -2, -3, ...  (simple sequential)

FILES UPDATED
-------------
  Dynamic Table:
    - Assets/DynamicStringTableCollection/Dynamic Table Shared Data.asset
    - Assets/DynamicStringTableCollection/Dynamic Table_{en,ja,zh-Hans,zh-Hant}.asset

  Standard Table:
    - Assets/StandardStringTableCollection/Standard Table Shared Data.asset
    - Assets/StandardStringTableCollection/Standard Table_{en,ja,zh-Hans,zh-Hant}.asset
    - All Assets/**/*.uxml files that reference Standard Table negative IDs via entry="Id(-...)"

USAGE
-----
  python Tools/normalize_localization_ids.py              # dry run (safe, prints diff)
  python Tools/normalize_localization_ids.py --apply      # actually write files

SAFETY
------
  - Default is dry-run; nothing is written without --apply.
  - Prints a full mapping of old ID → new ID before applying.
  - All negative IDs are treated as manual/custom IDs and are renumbered.
  - IDs are ordered from least-negative to most-negative so the assignment is
    deterministic and stable across runs (same key always gets the same new ID).
"""

import argparse
import os
import re
import glob

ROOT = os.path.join(os.path.dirname(__file__), "..")

TABLES = {
    "dynamic": {
        "shared":  "Assets/DynamicStringTableCollection/Dynamic Table Shared Data.asset",
        "locales": [
            "Assets/DynamicStringTableCollection/Dynamic Table_en.asset",
            "Assets/DynamicStringTableCollection/Dynamic Table_ja.asset",
            "Assets/DynamicStringTableCollection/Dynamic Table_zh-Hans.asset",
            "Assets/DynamicStringTableCollection/Dynamic Table_zh-Hant.asset",
        ],
        "uxml_guid": None,  # Dynamic Table is only referenced from C#, not UXML
    },
    "standard": {
        "shared":  "Assets/StandardStringTableCollection/Standard Table Shared Data.asset",
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


def get_manual_negative_ids(shared_content: str) -> list[int]:
    """Return all manual negative IDs sorted from least-negative to most-negative."""
    ids = [
        int(x) for x in re.findall(r"m_Id: (-\d+)", shared_content)
    ]
    return sorted(set(ids), reverse=True)  # -1 > -2 > -3 ...  (least negative first)


def build_mapping(ids: list[int]) -> dict[int, int]:
    """Assign -1, -2, -3, ... in order of least-negative to most-negative."""
    return {old: -(i + 1) for i, old in enumerate(ids)}


def apply_mapping_shared(content: str, mapping: dict[int, int]) -> str:
    """Replace m_Id: OLD with m_Id: NEW in SharedData (exact whole-line match)."""
    for old, new in mapping.items():
        content = re.sub(
            rf"(m_Id: ){re.escape(str(old))}(\b)",
            lambda m, new=new: f"{m.group(1)}{new}",
            content
        )
    return content


def apply_mapping_locale(content: str, mapping: dict[int, int]) -> str:
    """Replace m_Id: OLD in locale files."""
    return apply_mapping_shared(content, mapping)


def apply_mapping_uxml(content: str, mapping: dict[int, int]) -> str:
    """Replace entry="Id(OLD)" in UXML files."""
    for old, new in mapping.items():
        content = content.replace(f'entry="Id({old})"', f'entry="Id({new})"')
    return content


def find_uxml_files() -> list[str]:
    pattern = os.path.join(abs_path("Assets"), "**", "*.uxml")
    return glob.glob(pattern, recursive=True)


# ---------------------------------------------------------------------------
# Per-table migration
# ---------------------------------------------------------------------------

def migrate_table(name: str, cfg: dict, apply: bool):
    print(f"\n{'='*60}")
    print(f"  TABLE: {name.upper()}")
    print(f"{'='*60}")

    shared_content = read(cfg["shared"])
    ids = get_manual_negative_ids(shared_content)

    if not ids:
        print("  No manual negative IDs found. Nothing to do.")
        return {}

    mapping = build_mapping(ids)

    print(f"  {len(mapping)} IDs to renumber:")
    for old, new in mapping.items():
        print(f"    {old:>25}  →  {new}")

    # SharedData
    new_shared = apply_mapping_shared(shared_content, mapping)
    if new_shared != shared_content:
        print(f"\n  {'WRITE' if apply else 'DRY'}  {cfg['shared']}")
        write(cfg["shared"], new_shared, apply)

    # Locale files
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

    uxml_files = find_uxml_files()
    hits = 0
    for path in uxml_files:
        with open(path, "r", encoding="utf-8") as f:
            content = f.read()

        # Only process files that reference this table's GUID and have negative IDs
        if guid not in content:
            continue
        if not re.search(r'entry="Id\(-\d+\)"', content):
            continue

        new_content = apply_mapping_uxml(content, mapping)
        if new_content != content:
            rel = os.path.relpath(path, abs_path("."))
            print(f"  {'WRITE' if apply else 'DRY'}  {rel}")
            if apply:
                with open(path, "w", encoding="utf-8", newline="\n") as f:
                    f.write(new_content)
            hits += 1

    if hits == 0:
        print("  No UXML files needed updating.")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--apply", action="store_true",
        help="Actually write files (default is dry-run)")
    args = parser.parse_args()

    if not args.apply:
        print("DRY RUN -- pass --apply to write changes\n")

    for table_name, cfg in TABLES.items():
        mapping = migrate_table(table_name, cfg, args.apply)

        if cfg["uxml_guid"]:
            print(f"\n  UXML files ({table_name}):")
            migrate_uxml(mapping, cfg["uxml_guid"], args.apply)

    if not args.apply:
        print("\n--- Dry run complete. Run with --apply to write. ---")
    else:
        print("\n--- Done. Localization IDs are now normalized. ---")


if __name__ == "__main__":
    main()
