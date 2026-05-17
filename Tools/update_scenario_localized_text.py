#!/usr/bin/env python3
"""
Batch-update localized prose in scenario XML files.

This tool is intentionally JSON-driven so multilingual replacement text is read
from a UTF-8 file instead of being carried through PowerShell command strings.

Batch JSON format:
{
  "entries": [
    {
      "file": "Assets/StreamingAssets/Scenarios/Example.scen.xml",
      "old": "old localized text",
      "new": "new localized text",
      "count": 1
    }
  ]
}

Use --dry-run to preview replacements without writing.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
SCENARIO_ROOT = ROOT / "Assets" / "StreamingAssets" / "Scenarios"


@dataclass(frozen=True)
class FileEncoding:
    name: str
    newline: str


@dataclass(frozen=True)
class Replacement:
    path: Path
    old: str
    new: str
    count: int


def detect_text_format(data: bytes) -> FileEncoding:
    if b"\r\n" in data:
        newline = "\r\n"
    elif b"\r" in data:
        newline = "\r"
    else:
        newline = "\n"

    if data.startswith(b"\xff\xfe") or data.startswith(b"\xfe\xff"):
        return FileEncoding("utf-16", newline)
    if data.startswith(b"\xef\xbb\xbf"):
        return FileEncoding("utf-8-sig", newline)
    return FileEncoding("utf-8", newline)


def read_text_preserving_format(path: Path) -> tuple[str, FileEncoding]:
    data = path.read_bytes()
    text_format = detect_text_format(data)
    try:
        return data.decode(text_format.name), text_format
    except UnicodeDecodeError as exc:
        raise ValueError(f"{path}: could not decode as {text_format.name}: {exc}") from exc


def write_text_preserving_format(path: Path, text: str, text_format: FileEncoding) -> None:
    validate_xml_encoding_declaration(path, text, text_format)
    text = text.replace("\r\n", "\n").replace("\r", "\n").replace("\n", text_format.newline)
    path.write_bytes(text.encode(text_format.name))


def normalize_encoding_name(name: str) -> str:
    return name.lower().replace("_", "-")


def validate_xml_encoding_declaration(path: Path, text: str, text_format: FileEncoding) -> None:
    match = re.search(r"<\?xml[^>]*\bencoding=['\"]([^'\"]+)['\"]", text[:256], re.IGNORECASE)
    if not match:
        return

    declared = normalize_encoding_name(match.group(1))
    actual = "utf-16" if text_format.name == "utf-16" else "utf-8"
    if declared != actual:
        raise ValueError(
            f"{path}: XML declaration says {declared}, but bytes were decoded as {actual}. "
            "Fix the declaration and actual encoding together before writing."
        )


def resolve_scenario_path(raw_path: str) -> Path:
    path = Path(raw_path)
    if not path.is_absolute():
        path = ROOT / path
    path = path.resolve()

    try:
        path.relative_to(SCENARIO_ROOT)
    except ValueError as exc:
        raise ValueError(f"{raw_path}: file must be under {SCENARIO_ROOT}") from exc

    if path.suffix.lower() != ".xml":
        raise ValueError(f"{raw_path}: expected an .xml file")
    if not path.exists():
        raise ValueError(f"{raw_path}: file does not exist")
    return path


def parse_count(raw: Any, default: int = 1) -> int:
    if raw is None:
        return default
    if not isinstance(raw, int) or raw < 1:
        raise ValueError("count must be a positive integer")
    return raw


def load_batch(path: Path) -> list[Replacement]:
    with path.open("r", encoding="utf-8-sig") as file:
        payload = json.load(file)

    entries = payload.get("entries") if isinstance(payload, dict) else payload
    if not isinstance(entries, list):
        raise ValueError("Batch file must contain a JSON list or an object with entries.")

    replacements: list[Replacement] = []
    for index, entry in enumerate(entries, start=1):
        if not isinstance(entry, dict):
            raise ValueError(f"Entry {index}: expected an object.")

        missing = [key for key in ("file", "old", "new") if key not in entry]
        if missing:
            raise ValueError(f"Entry {index}: missing {', '.join(missing)}.")

        old = entry["old"]
        new = entry["new"]
        if not isinstance(old, str) or not isinstance(new, str):
            raise ValueError(f"Entry {index}: old and new must be strings.")
        if old == "":
            raise ValueError(f"Entry {index}: old must not be empty.")

        replacements.append(
            Replacement(
                path=resolve_scenario_path(str(entry["file"])),
                old=old,
                new=new,
                count=parse_count(entry.get("count"), default=1),
            )
        )

    return replacements


def apply_replacements(replacements: list[Replacement], dry_run: bool) -> None:
    by_path: dict[Path, list[Replacement]] = {}
    for replacement in replacements:
        by_path.setdefault(replacement.path, []).append(replacement)

    for path, path_replacements in by_path.items():
        text, text_format = read_text_preserving_format(path)
        original = text

        for replacement in path_replacements:
            actual_count = text.count(replacement.old)
            if actual_count != replacement.count:
                raise ValueError(
                    f"{path}: expected {replacement.count} occurrence(s) of "
                    f"{replacement.old!r}, found {actual_count}."
                )
            text = text.replace(replacement.old, replacement.new, replacement.count)
            print(f"  UPDATE {path.relative_to(ROOT)} ({actual_count} replacement(s))")

        if text != original and not dry_run:
            write_text_preserving_format(path, text, text_format)
            print(f"  Wrote {path.relative_to(ROOT)}")
        elif dry_run:
            print(f"  DRY-RUN {path.relative_to(ROOT)}")


def scan_mojibake(paths: list[Path]) -> int:
    pattern = re.compile(r"(?:[ÃÂãäåæï].|â..|�)")
    total = 0
    for path in paths:
        text, _ = read_text_preserving_format(path)
        for line_number, line in enumerate(text.splitlines(), start=1):
            if pattern.search(line):
                print(f"{path.relative_to(ROOT)}:{line_number}: {line}")
                total += 1
    return total


def cmd_scan_mojibake() -> None:
    paths = sorted(SCENARIO_ROOT.glob("*.xml"))
    total = scan_mojibake(paths)
    if total:
        print(f"Found {total} suspicious line(s).")
        sys.exit(1)
    print("No mojibake markers found in scenario XML.")


def main() -> None:
    parser = argparse.ArgumentParser(description="Batch-update localized scenario XML text.")
    parser.add_argument("--file", help="UTF-8 JSON batch file.")
    parser.add_argument("--dry-run", action="store_true", help="Preview without writing.")
    parser.add_argument(
        "--scan-mojibake",
        action="store_true",
        help="Scan scenario XML with Python UTF-8 decoding for common mojibake markers.",
    )
    args = parser.parse_args()

    try:
        if args.scan_mojibake:
            cmd_scan_mojibake()
            return
        if not args.file:
            parser.error("--file is required unless --scan-mojibake is used")
        replacements = load_batch(Path(args.file))
        apply_replacements(replacements, dry_run=args.dry_run)
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
