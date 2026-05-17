from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET


def detect_newline(data: bytes) -> str:
    return "\r\n" if b"\r\n" in data else "\n"


def count_legacy_remarks(data: bytes, item_tag: str) -> int:
    root = ET.fromstring(data)
    count = 0
    for item in root.findall(item_tag):
        remark = item.find("remark")
        if remark is not None and remark.find("english") is None:
            count += 1
    return count


def migrate_file(path: Path, item_tag: str) -> int:
    data = path.read_bytes()
    newline = detect_newline(data)
    target_count = count_legacy_remarks(data, item_tag)

    if target_count == 0:
        print(f"No legacy {item_tag} remark nodes found in {path}.")
        return 0

    text = data.decode("utf-8")
    pattern = re.compile(r"(?P<indent>[ \t]*)<remark(?P<attrs>[^>]*)>(?P<body>.*?)</remark>", re.DOTALL)
    converted_count = 0

    def convert(match: re.Match) -> str:
        nonlocal converted_count
        body = match.group("body")
        if re.search(r"<\s*english(?:\s|>)", body):
            return match.group(0)

        converted_count += 1
        indent = match.group("indent")
        attrs = match.group("attrs")
        return f"{indent}<remark{attrs}>{newline}{indent} <english>{body}</english>{newline}{indent}</remark>"

    new_text = pattern.sub(convert, text)

    if converted_count != target_count:
        raise RuntimeError(f"{path}: expected to convert {target_count} remarks, converted {converted_count}.")

    remaining = count_legacy_remarks(new_text.encode("utf-8"), item_tag)
    if remaining:
        raise RuntimeError(f"{path}: {remaining} {item_tag} remark nodes still lack english child elements.")

    path.write_bytes(new_text.encode("utf-8"))
    print(f"Converted {converted_count} {item_tag} remark nodes in {path}.")
    return converted_count


def main() -> int:
    if len(sys.argv) >= 3:
        migrate_file(Path(sys.argv[1]), sys.argv[2])
        return 0

    migrate_file(Path("Assets/StreamingAssets/Scenarios/Leaders.xml"), "Leader")
    migrate_file(Path("Assets/StreamingAssets/Scenarios/NamedShips.xml"), "NamedShip")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
