from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET


def detect_newline(data: bytes) -> str:
    return "\r\n" if b"\r\n" in data else "\n"


def main() -> int:
    path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("Assets/StreamingAssets/Scenarios/ShipClasses.xml")
    data = path.read_bytes()
    newline = detect_newline(data)

    root = ET.fromstring(data)
    target_count = 0
    for ship_class in root.findall("ShipClass"):
        remark = ship_class.find("remark")
        if remark is not None and len(list(remark)) == 0:
            target_count += 1

    if target_count == 0:
        print("No legacy ShipClass remark nodes found.")
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
        raise RuntimeError(f"Expected to convert {target_count} remarks, converted {converted_count}.")

    new_root = ET.fromstring(new_text.encode("utf-8"))
    missing = 0
    for ship_class in new_root.findall("ShipClass"):
        remark = ship_class.find("remark")
        if remark is not None and remark.find("english") is None:
            missing += 1

    if missing:
        raise RuntimeError(f"{missing} ShipClass remark nodes still lack english child elements.")

    path.write_bytes(new_text.encode("utf-8"))
    print(f"Converted {converted_count} ShipClass remark nodes in {path}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
