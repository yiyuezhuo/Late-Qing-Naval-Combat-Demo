# Localization AI Guide

Use this file as the **default workflow only**. Read `Docs\Localization_Reference.md` **only if needed** for YAML insertion details, quoting/Unicode rules, ID troubleshooting, or unusual edge cases.

## 1. Pick the correct table

| Usage | Table |
|-------|-------|
| C# `Localize("key")` / `ILocalizeService.Get(...)` | **Dynamic Table** |
| C# `LocalizeEnum(enumVal)` / `ILocalizeService.GetEnum(...)` | **Dynamic Table** |
| `LocalizedEnumField` option values | **Dynamic Table** |
| UXML `LocalizedString` binding (`property="label"`, `property="text"`, etc.) | **Standard Table** |

The deciding factor is **how the text is looked up**, not where it appears on screen.

> ⚠️ Putting a `Localize(...)` key into Standard Table causes silent fallback to raw key text.

## 2. Default workflow

### Dynamic Table

- Use `python Tools\add_localization.py --key "..." --en "..." --ja "..." --zh-hans "..." --zh-hant "..."`.
- For batch mode, use `--file path\to\keys.json` or `--file path\to\keys.txt`.
- For enum keys, use `{EnumTypeName}.{EnumMemberName}`.

### Standard Table

- Scan a target UXML file first:
  - `python Tools\standard_localization.py scan-uxml Assets\...\MyDialog.uxml`
- Reuse or add keys:
  - `python Tools\standard_localization.py query "My Label"`
  - `python Tools\standard_localization.py add --key "..." --en "..." --ja "..." --zh-hans "..." --zh-hant "..."`
  - `python Tools\standard_localization.py ensure --file keys.json`
- After adding or reusing a key, paste the printed `LocalizedString` snippet into the target UXML file.

For **Standard Table**, treat `--key` as the stable lookup identifier. It can match the English text for simple static labels, but it does not have to.

## 3. Verification commands

- Check localization ID health:
  - `python Tools\normalize_localization_ids.py status`
- Verify no localization ID/reference problems remain:
  - `python Tools\normalize_localization_ids.py verify`
- Re-pack fragmented negative IDs when necessary:
  - `python Tools\normalize_localization_ids.py --apply`

## 4. Only read the reference if needed

Open `Docs\Localization_Reference.md` only when you need one of these:

- exact YAML insertion positions
- quoting / Unicode-escape rules
- detailed tool behavior and examples
- ID normalization details
- troubleshooting unusual localization issues
