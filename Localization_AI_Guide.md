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

## 8. Table Keys

Distributed ID generator

By default, Unity uses a distributed Key Id generator to provide a unique Key Id value specific to the machine that generates it. This means that it is safe for multiple users to work on the same Table. Note that you might need to resolve some merge conflicts, but because Unity never generates the same Key Id twice, resolving these conflicts should be straightforward.

A Key is a 64-bit long data type. It has the following structure:


| **Bits**        | **Name**                                             | **Description**                                                                                                                                                                                                                                                                                                                            |
| --------------- | ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 12 (0-11)       | Sequence Number                                      | A local counter per machine that starts at 0 and increments by 1 for each new ID request that is made during the same millisecond.  <br>The value is limited to 12 bytes, so it can contain 4095 items before the IDs for this millisecond are exhausted and the ID generator must wait until the next millisecond before it can continue. |
| 10 Bits (12-21) | Machine Id                                           | The ID of the machine.  <br>By default, in the Editor, this value is generated from the machine's network interface physical address.  <br>However, you can also set it to a custom value. There is enough space for 1024 machines.                                                                                                        |
| 41 Bits (22-63) | Timestamp                                            | A timestamp using a custom epoch (or start time), which is the time the class was created.  <br>The maximum timestamp that can be represented is 69 years from the custom epoch.  <br>At this point, the Key generator will have exhausted all possible unique Ids.                                                                        |
| 1 Bit (64)      | [Signed Bit](https://en.wikipedia.org/wiki/Sign_bit) | The ID generator does not use the signed bit.  <br>If you want to add custom Id values, use the signed bit and add Key IDs with negative values to avoid conflicts.                                                                                                                                                                        |
