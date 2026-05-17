# Localization Reference

This document contains the **detailed reference** for localization work in this repository. For normal tasks, start with `Localization_AI_Guide.md` and come here only when the quick workflow is not enough.

## 1. Two-table model

This project uses two string table collections:

| Table | Responsibility |
|-------|----------------|
| **Dynamic Table** | Runtime C# lookups — `Localize(...)`, `LocalizeEnum(...)`, `LocalizedEnumField` options |
| **Standard Table** | Static UXML-bound labels and text |

The deciding factor is **how the text is looked up**, not where it appears on screen.

## 2. Routing rule: which table to use

| Usage | Table |
|-------|-------|
| C# `Localize("key")` / `ILocalizeService.Get(...)` | **Dynamic Table** |
| C# `LocalizeEnum(enumVal)` / `ILocalizeService.GetEnum(...)` | **Dynamic Table** |
| `LocalizedEnumField` option values | **Dynamic Table** |
| UXML `LocalizedString` binding (`property="label"`, `property="text"`, etc.) | **Standard Table** |

> ⚠️ Putting a `Localize(...)` key into Standard Table causes silent fallback to raw key text.

## 3. Key naming conventions

**Enum keys (Dynamic Table):**

Format: `{EnumTypeName}.{EnumMemberName}` — e.g. `RangeRingDisplayMode.Circle`, `Country.China`

When you add a new enum displayed by `LocalizedEnumField` or accessed via `LocalizeEnum(...)`, add a key for every member.

**Format strings (Dynamic Table):**

Use `{0}`, `{1}`, … placeholders — e.g. `Azimuth {0} deg, Distance {1} yd`

## 4. UXML label binding (Standard Table)

Localizing a UXML control label is **not automatic**. Add a `LocalizedString` binding explicitly:

```xml
<Bindings>
    <UnityEngine.Localization.LocalizedString
        property="label"
        table="GUID:7dfd13ea0ff0ef0408a7f015356a0054"
        entry="Id(-1)" />
</Bindings>
```

- Standard Table collection GUID: `7dfd13ea0ff0ef0408a7f015356a0054`
- `entry` ID comes from Standard Table Shared Data

After adding a Standard Table entry, update the `entry="Id(...)"` in the corresponding UXML file.

## 5. Asset files to modify

When adding a new localizable key, update:

- **Shared key definition** (one of):
  - `Assets\DynamicStringTableCollection\Dynamic Table Shared Data.asset`
  - `Assets\StandardStringTableCollection\Standard Table Shared Data.asset`
- **Per-locale values** (all four):
  - `*_en.asset`, `*_ja.asset`, `*_zh-Hans.asset`, `*_zh-Hant.asset`

**Prefer `Tools\add_localization.py` for Dynamic Table entries** — it handles insertion position, quoting, and ID assignment automatically.

### Manual insertion positions

New entries must be inserted at specific positions — **not appended to the end of the file**.

**Shared Data assets** (both Dynamic and Standard) — insert immediately before:
```yaml
  m_Metadata:
    m_Items: []
  m_KeyGenerator:
```

**Locale assets** (`_en`, `_ja`, `_zh-Hans`, `_zh-Hant` — both tables) — insert immediately before:
```yaml
  references:
    version: 2
```

## 6. YAML formatting rules

### Quoting

Values containing `{`, `}`, `:`, or `#` **must be quoted** or Unity's YAML parser throws a parse error.

- **English — single-quoted:** `'Azimuth {0} deg'`
  - Escape an internal single quote by doubling: `'It''s done'`
- **Non-English — double-quoted with Unicode escapes:** `"\u65B9\u4F4D{0}\u5EA6"`
  - Do not mix raw CJK/kana characters with double-quoting; always use `\uXXXX` form.

### Encoding for non-English locales

For Japanese / Simplified Chinese / Traditional Chinese, write `m_Localized` as Unicode escape sequences:

- ✅ `"\u30D3\u30E5\u30FC\u30D5\u30A9\u30FC\u30C8\u98A8\u529B\u968E\u7D1A\uFF1A{0}"`
- ❌ `"ビューフォート風力階級：{0}"`

This avoids encoding corruption when files are processed by tools or scripts.

## 7. Recommended implementation checklist

1. Add enum/property in C#.
2. Register enum converter if needed (`RegisterConverters.cs`).
3. Decide which table to use.
4. **Dynamic Table:** run `python Tools\add_localization.py --key "..." --en "..." --ja "..." --zh-hans "..." --zh-hant "..."`.
5. **Standard Table:**
   a. Run `python Tools\standard_localization.py scan-uxml Assets\...\MyDialog.uxml` if you are localizing a UXML file.  
   b. Run `python Tools\standard_localization.py query "My Label"` to check exact reuse.  
   c. If needed, run `python Tools\standard_localization.py add ...` or `ensure --file ...`.  
   d. Paste the printed UXML snippet into the target UXML file inside a `<Bindings>` block.
6. Verify no fallback is happening — raw key text shown in-game means wrong table or missing entry.

For **Standard Table**, treat `--key` as the stable lookup identifier. It can match the English text for simple static labels, but it does not have to.

## 8. Examples

**Dynamic Table — format string:**
- Key: `{0} ({1}) Victory` → en stored as `'{0} ({1}) Victory'` (single-quoted because of `{`)
- Used as: `string.Format(Localize("{0} ({1}) Victory"), countryName, role)`

**Dynamic Table — enum keys:**
- `RangeRingDisplayMode.Circle`, `RangeRingDisplayMode.MergedArcs`, `RangeRingDisplayMode.DistinctArcs`

**Dynamic Table — plain format string:**
- Key: `Azimuth {0} deg, Distance {1} yd`

**Standard Table — UXML label:**
- Key: `Range Ring Display`, bound via `LocalizedString` with `property="label"`

## 9. Key ID scheme

Unity's distributed generator uses bits 0–62 (always non-negative). The sign bit is explicitly reserved for custom IDs — **all manually assigned IDs must be negative**.

This project uses sequential manual IDs: `-1, -2, -3, …`

For **new entries**, use the helper scripts so they assign the next sequential small negative ID automatically.

> ⚠️ Do not manually invent large arbitrary negatives such as `-97000000100001`. Use the next sequential value.

Use `Tools\add_localization.py` or `Tools\standard_localization.py add` to assign the next ID. Use `Tools\normalize_localization_ids.py --apply` to re-pack fragmented manual IDs back into a clean sequential scheme when needed.

## 10. Tools

Python helper scripts live in `Tools\` to reduce manual error when editing the YAML asset files.

### `Tools\dynamic_localization.py` — query and scan Dynamic Table usage

Use for runtime C# lookups that resolve through Dynamic Table.

```bash
python Tools\dynamic_localization.py query "Paused"
python Tools\dynamic_localization.py scan-cs Assets\Scripts\StrategicCombat\StrategicOverlay.cs
```

`query` prints:
- exact key ID
- en / ja / zh-Hans / zh-Hant values
- reuse suggestions when no exact key exists

`scan-cs` reports:
- `Localize("...")` and `ILocalizeService.Get("...")` string lookups
- `LocalizeEnum(...)` and `GetEnum(...)` enum lookups
- exact Dynamic Table matches
- missing keys with ready-to-run `Tools\add_localization.py` commands

For enum expressions that are not simple `EnumType.Member` literals, the script flags them for manual verification instead of guessing.

### `Tools\add_localization.py` — add a new Dynamic Table entry

```bash
python Tools\add_localization.py \
  --key "My New Key" \
  --en "English text" \
  --ja "日本語テキスト" \
  --zh-hans "简体中文" \
  --zh-hant "繁體中文"
```

Options:
- `--dry-run` — preview changes without writing
- `--file path\to\keys.json` — batch mode using a JSON array of entry objects
- `--file path\to\keys.txt` — batch mode; each non-empty non-comment line is `key|en|ja|zh-hans|zh-hant`

The script auto-assigns the next sequential small negative ID, handles YAML quoting, and Unicode-escapes non-ASCII text in the ja/zh-Hans/zh-Hant files.

**Only works for Dynamic Table.** For Standard Table entries, use `Tools\standard_localization.py` and then paste the printed `entry="Id(...)"` binding into the target UXML file.

### `Tools\standard_localization.py` — query, add, ensure, and scan Standard Table entries

Use for UXML-bound static labels. The script prints ready-to-paste UXML binding snippets and can also scan a UXML file for unlocalized `text` / `label` attributes.

**Agent workflow:**
```bash
# Step 1 — check if the label is already localized
python Tools\standard_localization.py query "Range Ring Display"

# → FOUND: prints ID + 4 translations + UXML snippet → paste snippet into UXML file, done.
# → NOT FOUND: prints close-match reuse suggestions → continue to step 2 if needed.

# Step 2 — add the entry
python Tools\standard_localization.py add \
  --key "Range Ring Display" \
  --en "Range Ring Display" \
  --ja "射程環表示" \
  --zh-hans "射程环显示" \
  --zh-hant "射程環顯示"

# → prints assigned ID + UXML snippet → paste snippet into UXML file, done.
```

Other commands:
```bash
python Tools\standard_localization.py list
python Tools\standard_localization.py add --dry-run ...
python Tools\standard_localization.py ensure --file keys.json
python Tools\standard_localization.py scan-uxml Assets\UIDocuments\StrategicCombat\StrategicVictoryStatusDialog.uxml
```

`ensure --file` supports:
- JSON array entries with `key`, `en`, `ja`, `zh-hans`, `zh-hant`, optional `prop`
- Pipe-delimited text lines: `key|en|ja|zh-hans|zh-hant[|prop]`

`scan-uxml` reports:
- controls whose `text` / `label` attribute is not localized through `LocalizedString` or `DataBinding`
- exact Standard Table key reuse opportunities
- near-match suggestions when no exact key exists
- a ready-to-run `add` command for missing keys

The printed UXML snippet looks like:
```xml
<UnityEngine.Localization.LocalizedString
    property="label"
    table="GUID:7dfd13ea0ff0ef0408a7f015356a0054"
    entry="Id(-74)" />
```

Wrap it in a `<Bindings>` block inside the target UXML element. Change `property="label"` to `property="text"` if localizing a Button or Label's text attribute instead.

**Only works for Standard Table.** For Dynamic Table entries, use `Tools\add_localization.py`.

### `Tools\update_standard_localization.py` - update Standard Table entries

Use this JSON-driven helper when changing existing Standard Table translations. It reads multilingual text from a UTF-8 file instead of carrying it through PowerShell command strings.

```bash
python Tools\update_standard_localization.py --file .codex-tmp\standard_updates.json
python Tools\update_standard_localization.py --file .codex-tmp\standard_updates.json --add-missing
python Tools\update_standard_localization.py --file .codex-tmp\standard_updates.json --dry-run
```

Existing-key entries may include only the locale fields being changed:

```json
{
  "entries": [
    {
      "key": "Help Description",
      "ja": "..."
    }
  ]
}
```

When `--add-missing` creates a new key, the entry must include `key`, `en`, `ja`, `zh-hans`, and `zh-hant`.

### `Tools\normalize_localization_ids.py` — normalize and verify localization IDs

Renumbers all manual negative IDs in both tables to a clean `-1, -2, -3, …` sequence and updates `entry="Id(...)"` references in Standard Table UXML files. It can also report current ID health and verify that no issues remain. `verify` also checks localized text for common encoding damage such as replacement characters, question-mark corruption, mojibake markers, suspicious wrapped line breaks, and raw non-ASCII in escaped non-English locale assets.

```bash
python Tools\normalize_localization_ids.py
python Tools\normalize_localization_ids.py --apply
python Tools\normalize_localization_ids.py status
python Tools\normalize_localization_ids.py verify
```

## 11. Notes

- Avoid passing newline characters into `Localize(...)`. In this repository, localized UI text is treated as single-line keys/labels; if you need multi-line presentation, prefer separate keys or UI layout changes instead.
- The deciding factor for which table to use is **how** the text is looked up, not where it appears on screen.
