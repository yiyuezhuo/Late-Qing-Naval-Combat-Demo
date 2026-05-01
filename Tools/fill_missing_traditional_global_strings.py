#!/usr/bin/env python3
"""Fill missing Chinese Traditional values in scenario GlobalString XML nodes.

The scenario XML files in this project declare UTF-16 but are UTF-8 bytes.
This tool reads and writes UTF-8 while preserving the original declaration,
line endings, and almost all original formatting. It scans XML tag structure
and only inserts or fills direct ``chineseTraditional`` children next to an
existing non-empty ``chineseSimplified`` child.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass, field
from pathlib import Path
import re
import sys
from typing import Iterable

try:
    from opencc import OpenCC
except ImportError as exc:
    raise SystemExit(
        "Missing dependency: opencc. Install with "
        "`python -m pip install opencc-python-reimplemented`."
    ) from exc


REPO_ROOT = Path(__file__).resolve().parents[1]
SCENARIOS_DIR = REPO_ROOT / "Assets" / "StreamingAssets" / "Scenarios"

DEFAULT_STRATEGIC_FILES = [
    SCENARIOS_DIR / "First Sino-Japanese War.xml",
    SCENARIOS_DIR / "Vladivostok Squadron Raiding.xml",
]

TARGET_CHILDREN = {"english", "japanese", "chineseSimplified", "chineseTraditional"}
TAG_RE = re.compile(r"<(/?)([A-Za-z_][\w.-]*)([^<>]*?)(/?)>")


@dataclass
class Child:
    tag: str
    start: int
    end: int
    text_start: int
    text_end: int
    self_closing: bool = False


@dataclass
class Frame:
    tag: str
    start: int
    children: list[Child] = field(default_factory=list)


@dataclass
class Edit:
    start: int
    end: int
    replacement: str
    path: Path
    parent_tag: str
    simplified: str
    traditional: str
    action: str


def default_targets() -> list[Path]:
    tactical = sorted(SCENARIOS_DIR.glob("*.scen.xml"))
    return [
        SCENARIOS_DIR / "ShipClasses.xml",
        SCENARIOS_DIR / "NamedShips.xml",
        *tactical,
        *DEFAULT_STRATEGIC_FILES,
    ]


def read_utf8_text(path: Path) -> str:
    data = path.read_bytes()
    if data.startswith(b"\xef\xbb\xbf"):
        data = data[3:]
    return data.decode("utf-8")


def line_indent_before(text: str, index: int) -> str:
    line_start = text.rfind("\n", 0, index) + 1
    line = text[line_start:index]
    return re.match(r"[ \t]*", line).group(0)


def has_cjk(text: str) -> bool:
    return any("\u3400" <= ch <= "\u9fff" or "\uf900" <= ch <= "\ufaff" for ch in text)


def add_child_to_parent(stack: list[Frame], child: Child) -> None:
    if stack and child.tag in TARGET_CHILDREN:
        stack[-1].children.append(child)


def collect_edits(path: Path, text: str, converter: OpenCC, fill_empty: bool) -> list[Edit]:
    edits: list[Edit] = []
    stack: list[Frame] = []

    for match in TAG_RE.finditer(text):
        closing, tag, attrs, self_closing = match.groups()

        # Skip declarations, comments, and processing instructions. TAG_RE does
        # not usually match them, but this keeps the scanner deliberately narrow.
        if attrs.startswith("?") or attrs.startswith("!"):
            continue

        is_self_closing = bool(self_closing) or attrs.strip().endswith("/")

        if closing:
            if not stack:
                continue

            frame = stack.pop()
            if frame.tag != tag:
                raise ValueError(f"{path}: mismatched XML tags near {tag!r}")

            child = Child(
                tag=tag,
                start=frame.start,
                end=match.end(),
                text_start=text.find(">", frame.start, match.start()) + 1,
                text_end=match.start(),
            )

            maybe_add_global_string_edit(path, text, frame, match.start(), converter, fill_empty, edits)
            add_child_to_parent(stack, child)
        elif is_self_closing:
            child = Child(
                tag=tag,
                start=match.start(),
                end=match.end(),
                text_start=match.end(),
                text_end=match.end(),
                self_closing=True,
            )
            add_child_to_parent(stack, child)
        else:
            stack.append(Frame(tag=tag, start=match.start()))

    if stack:
        raise ValueError(f"{path}: unclosed XML tag {stack[-1].tag!r}")

    return edits


def maybe_add_global_string_edit(
    path: Path,
    text: str,
    frame: Frame,
    close_start: int,
    converter: OpenCC,
    fill_empty: bool,
    edits: list[Edit],
) -> None:
    child_by_tag = {child.tag: child for child in frame.children}
    simplified_child = child_by_tag.get("chineseSimplified")
    if simplified_child is None:
        return

    simplified = text[simplified_child.text_start : simplified_child.text_end]
    if not simplified.strip() or not has_cjk(simplified):
        return

    traditional_child = child_by_tag.get("chineseTraditional")
    if traditional_child is not None:
        existing = text[traditional_child.text_start : traditional_child.text_end]
        if existing.strip() or not fill_empty:
            return

    traditional = converter.convert(simplified)
    if not traditional.strip():
        return

    if traditional_child is None:
        newline = "\r\n" if "\r\n" in text else "\n"
        indent = line_indent_before(text, simplified_child.start)
        insert_text = f"{newline}{indent}<chineseTraditional>{traditional}</chineseTraditional>"
        edits.append(
            Edit(
                start=simplified_child.end,
                end=simplified_child.end,
                replacement=insert_text,
                path=path,
                parent_tag=frame.tag,
                simplified=simplified.strip(),
                traditional=traditional.strip(),
                action="insert",
            )
        )
        return

    indent = line_indent_before(text, traditional_child.start)
    replacement = f"{indent}<chineseTraditional>{traditional}</chineseTraditional>"
    line_start = text.rfind("\n", 0, traditional_child.start) + 1
    edit_start = line_start if text[line_start:traditional_child.start].strip() == "" else traditional_child.start
    edits.append(
        Edit(
            start=edit_start,
            end=traditional_child.end,
            replacement=replacement,
            path=path,
            parent_tag=frame.tag,
            simplified=simplified.strip(),
            traditional=traditional.strip(),
            action="fill-empty",
        )
    )


def apply_edits(text: str, edits: Iterable[Edit]) -> str:
    result = text
    for edit in sorted(edits, key=lambda item: item.start, reverse=True):
        result = result[: edit.start] + edit.replacement + result[edit.end :]
    return result


def validate_xml_parse(path: Path, text: str) -> None:
    import xml.etree.ElementTree as ET

    parse_text = re.sub(r'encoding=["\']utf-16["\']', 'encoding="utf-8"', text, count=1, flags=re.I)
    ET.fromstring(parse_text.encode("utf-8"))


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "paths",
        nargs="*",
        type=Path,
        help="XML files to process. Defaults to ShipClasses, NamedShips, all tactical scenarios, and strategic scenarios.",
    )
    parser.add_argument("--apply", action="store_true", help="Write changes. Without this, only reports planned edits.")
    parser.add_argument(
        "--fill-empty",
        action=argparse.BooleanOptionalAction,
        default=True,
        help="Fill empty chineseTraditional elements as well as missing ones.",
    )
    parser.add_argument("--config", default="s2t", help="OpenCC conversion config, default: s2t.")
    parser.add_argument("--verbose", action="store_true", help="Print each planned value.")
    return parser.parse_args()


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
        sys.stderr.reconfigure(encoding="utf-8")

    args = parse_args()
    converter = OpenCC(args.config)
    targets = [path.resolve() for path in (args.paths or default_targets())]

    all_edits: list[Edit] = []
    changed_files = 0

    for path in targets:
        if not path.exists():
            print(f"missing: {path}", file=sys.stderr)
            return 2

        text = read_utf8_text(path)
        validate_xml_parse(path, text)
        edits = collect_edits(path, text, converter, args.fill_empty)
        all_edits.extend(edits)

        if not edits:
            continue

        changed_files += 1
        print(f"{path.relative_to(REPO_ROOT)}: {len(edits)} GlobalString value(s)")
        if args.verbose:
            for edit in edits:
                print(f"  {edit.action} <{edit.parent_tag}>: {edit.simplified!r} -> {edit.traditional!r}")

        if args.apply:
            updated = apply_edits(text, edits)
            validate_xml_parse(path, updated)
            path.write_bytes(updated.encode("utf-8"))

    action = "updated" if args.apply else "would update"
    print(f"{action} {len(all_edits)} GlobalString value(s) in {changed_files} file(s)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
