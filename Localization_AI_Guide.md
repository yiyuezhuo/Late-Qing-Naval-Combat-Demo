# Localization AI Guide

This document summarizes the localization workflow used in this repository, based on recent implementation and feedback.

## 1. Two-table model in this project

This project uses two string table collections with different responsibilities:

- `Standard Table`: UI labels and static document strings (mostly UXML-bound labels/text).
- `Dynamic Table`: runtime keys, especially enum option texts used by `LocalizedEnumField`.

## 2. How enum localization works

`LocalizedEnumField` localizes enum options by key convention:

- Key format: `{EnumTypeName}.{EnumMemberName}`
- Example:
  - `RangeRingDisplayMode.Circle`
  - `RangeRingDisplayMode.MergedArcs`
  - `RangeRingDisplayMode.DistinctArcs`

So when you add a new enum shown by `LocalizedEnumField`, you must add matching keys into **Dynamic Table Shared Data** and localized values into each locale file.

## 3. How field label localization works

For a control label in UXML (for example, `label="Range Ring Display"`), localization is **not automatic**.  
You must explicitly add a `LocalizedString` binding in UXML:

- `property="label"`
- `table="GUID:..."` (the Standard Table collection GUID already used in the file)
- `entry="Id(...)"` (ID from Standard Table Shared Data)

Without this, the label stays as raw text.

## 4. Asset files to modify

When adding a new localizable key, update all required files:

- Shared key definition:
  - `Assets/StandardStringTableCollection/Standard Table Shared Data.asset`
  - or `Assets/DynamicStringTableCollection/Dynamic Table Shared Data.asset`
- Per-locale values:
  - `*_en.asset`
  - `*_ja.asset`
  - `*_zh-Hans.asset`
  - `*_zh-Hant.asset`

All locale tables must include the same `m_Id`.

## 5. Encoding convention for non-English locale assets

For Japanese / Simplified Chinese / Traditional Chinese in these YAML assets:

- Use Unicode escape form in `m_Localized`, e.g.:
  - `"\u30D3\u30E5\u30FC\u30D5\u30A9\u30FC\u30C8\u98A8\u529B\u968E\u7D1A\uFF1A{0}"`
- Do not leave English source text in non-English locale files when translation is expected.

## 6. Recommended implementation checklist

1. Add enum/property in C#.
2. Register enum converter if needed (`RegisterConverters.cs`).
3. Add/adjust UXML binding (`LocalizedString` for labels).
4. Add key(s) to the correct `* Shared Data.asset`.
5. Add `m_Localized` entries for every locale file with the same `m_Id`.
6. Verify fallback is not happening (no `No translation found` / raw key shown).

## 7. Example from recent change

- New UI label key in Standard Table: `Range Ring Display`
- New enum keys in Dynamic Table:
  - `RangeRingDisplayMode.Circle`
  - `RangeRingDisplayMode.MergedArcs`
  - `RangeRingDisplayMode.DistinctArcs`
- Non-English locale values were written as Unicode escapes, per project convention.

