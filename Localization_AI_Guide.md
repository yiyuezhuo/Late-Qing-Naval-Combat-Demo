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

Use the helper that matches the target table:

- `Tools\dynamic_localization.py` is for **Dynamic Table query / scan**.
- `Tools\add_localization.py` is for **Dynamic Table add**.
- `Tools\update_dynamic_localization.py` is for **Dynamic Table batch update / mixed add / cleanup**.
- `Tools\standard_localization.py` is for **Standard Table only**.

### Dynamic Table

- Query an existing key when needed:
  - `python Tools\dynamic_localization.py query "My Key"`
- Scan a target C# file for Dynamic Table lookups:
  - `python Tools\dynamic_localization.py scan-cs Assets\...\MyScript.cs`
- Use `python Tools\add_localization.py --key "..." --en "..." --ja "..." --zh-hans "..." --zh-hant "..."`.
- For batch mode, use `--file path\to\keys.json` or `--file path\to\keys.txt`.
- To update existing Dynamic Table values, or to do a mixed batch that updates existing keys and adds missing keys:
  - `python Tools\update_dynamic_localization.py --file path\to\updates.json --add-missing --repair`
  - The JSON file should include `entries` with `key`, `en`, `ja`, `zh-hans`, and `zh-hant`. It may also include `remove` for obsolete keys.
  - Prefer keeping one-off batch JSON files outside `Tools\` or deleting them after use; keep reusable scripts in `Tools\`.
- For enum keys, use `{EnumTypeName}.{EnumMemberName}`.
- If the text is looked up from C# via `Localize(...)` / `Get(...)`, it belongs here.

### Standard Table

- UXML text is **not localized automatically**. A literal `text="..."` / `label="..."` stays literal until you add a `LocalizedString` binding.
- Scan a target UXML file first:
  - `python Tools\standard_localization.py scan-uxml Assets\...\MyDialog.uxml`
- Reuse or add keys:
  - `python Tools\standard_localization.py query "My Label"`
  - `python Tools\standard_localization.py add --key "..." --en "..." --ja "..." --zh-hans "..." --zh-hant "..."`
  - `python Tools\standard_localization.py ensure --file keys.json`
- After adding or reusing a key, paste the printed `LocalizedString` snippet into the target UXML file inside a `<Bindings>` block.
- Use `property="text"` for Button/Label text and `property="label"` for control labels.

For **Standard Table**, treat `--key` as the stable lookup identifier. It can match the English text for simple static labels, but it does not have to.

## 3. Default checklist

1. Decide which table owns the text.
2. If editing C# Dynamic Table lookups, run `python Tools\dynamic_localization.py scan-cs ...` first.
3. If editing UXML, run `python Tools\standard_localization.py scan-uxml ...` first.
4. Add, update, or reuse the key with the matching helper script.
5. If using Standard Table, paste the generated binding snippet into the UXML file.
6. For Dynamic Table batches that remove or add keys, run `python Tools\normalize_localization_ids.py --apply` if verification reports fragmented negative IDs.
7. Run `python Tools\normalize_localization_ids.py verify`.
8. If the UI shows raw key text in-game, first suspect the wrong table or a missing key/binding.

## 4. Verification commands

- Check localization ID health:
  - `python Tools\normalize_localization_ids.py status`
- Verify no localization ID/reference problems remain:
  - `python Tools\normalize_localization_ids.py verify`
- Re-pack fragmented negative IDs when necessary:
  - `python Tools\normalize_localization_ids.py --apply`
- For new entries, let the helper scripts assign the next sequential small negative ID automatically. Do not manually invent large arbitrary negative IDs.

## 5. Only read the reference if needed

Open `Docs\Localization_Reference.md` only when you need one of these:

- exact YAML insertion positions
- quoting / Unicode-escape rules
- detailed tool behavior and examples
- ID normalization details
- troubleshooting unusual localization issues
