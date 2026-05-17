# Data Analysis AI Guide

Use this guide when running ad hoc analysis over project data files, especially
XML data under `Assets/StreamingAssets/Scenarios/`.

## 1. Default workflow

1. Read the relevant C# model first so field names, derived properties, and
   formulas are grounded in the game code.
2. Parse source data into explicit rows with one stable entity per row.
3. Report the filtering rules used, such as `isPoorlySupported != true` or
   `displacementTons > 0`.
4. Separate descriptive findings from model-derived guesses.
5. Treat high residuals as review candidates, not automatic errors.
6. Do not write tests for exploratory data analysis unless explicitly asked.

Prefer small Python scripts for repeatable analysis. It is fine to run them
inline for exploration, but keep the parsing and filtering logic clear enough
to reproduce.

## 2. XML encoding checks

Do not assume the XML declaration matches the actual file bytes. Some project
XML may have a declaration such as:

```xml
<?xml version="1.0" encoding="utf-16"?>
```

while the file bytes are actually UTF-8 or ASCII-compatible. Strict XML parsers
trust the declaration and can fail with errors such as:

```text
ParseError: encoding specified in XML declaration is incorrect
```

Before blaming the parser, inspect the first bytes:

```powershell
python -c "from pathlib import Path; print(Path('Assets/StreamingAssets/Scenarios/ShipClasses.xml').read_bytes()[:80])"
```

Useful byte patterns:

- UTF-8/ASCII-compatible XML starts with contiguous bytes like `b'<?xml ...'`.
- UTF-16LE commonly starts with a BOM `b'\xff\xfe'` or interleaved null bytes
  like `b'<\x00?\x00x\x00m\x00l\x00'`.
- UTF-16BE commonly starts with a BOM `b'\xfe\xff'` or the opposite null-byte
  pattern.

## 3. Read-only parsing fallback

For read-only analysis, if the declaration is wrong but the actual bytes are
UTF-8-compatible, read with the actual encoding and replace the declaration in
memory before parsing:

```python
from pathlib import Path
import xml.etree.ElementTree as ET

path = Path("Assets/StreamingAssets/Scenarios/ShipClasses.xml")
text = path.read_text(encoding="utf-8-sig")
text = text.replace('encoding="utf-16"', 'encoding="utf-8"', 1)
root = ET.fromstring(text)
```

This is a read-only workaround. It is appropriate for analysis scripts that do
not write the XML back to disk.

## 4. Write-back rules

When editing scenario XML, follow the repository XML guardrails:

- Prefer Python scripts over PowerShell text replacement or write-back.
- For localized prose exact replacements under
  `Assets/StreamingAssets/Scenarios/`, prefer
  `python Tools\update_scenario_localized_text.py --file updates.json`.
  Put Japanese/Chinese text in the UTF-8 JSON file, not in a PowerShell
  here-string or command argument.
- Do not use `Set-Content` for multilingual XML.
- Do not trust `Get-Content` output when diagnosing mojibake; confirm by reading
  the file with Python using `encoding="utf-8-sig"`.
- Preserve the original encoding and line endings, or deliberately correct the
  encoding declaration and actual file encoding together.
- Verify the diff only changes the intended XML nodes.

If a file's XML declaration and actual encoding disagree, either keep analysis
read-only or fix both consistently. Do not write a file whose declaration says
one encoding while the bytes use another.

## 5. Reporting analysis results

For model-fitting or anomaly checks, include:

- data source path and sample count
- exclusion rules and excluded counts
- target distributions
- simple baselines before complex models
- accuracy or error metrics with the feature set used
- a short list of high-residual candidates with enough fields for review

Avoid presenting inferred rules as historical truth. In this project, many data
points intentionally encode SK5 rules, scenario design choices, or manual
exceptions.
