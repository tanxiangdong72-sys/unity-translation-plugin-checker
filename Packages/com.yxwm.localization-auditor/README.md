# Unity Localization Auditor — release candidate

Editor-only checks for projects using Unity Localization. This candidate is not yet approved for Asset Store release.

## Quick start
1. Back up or commit your project before installing any editor tool.
2. Install the package and its declared Unity Localization dependency (1.5.12). Allow Unity to finish importing and compiling.
3. Open **Window > Localization Auditor**.
4. Click **Refresh / 刷新目标**, then **Run Audit / 运行审计**.
5. Read the completion status and filter results by severity. Use **Locate / 定位** where an asset/object location is available.

The default discovery scope is scenes and prefabs under Assets. String-table rules also inspect registered String Table Collections; filtering scene/prefab targets does not imply every rule is restricted to those files.

## Rules
- STRING_TABLE_COMPLETENESS: missing locale tables or shared keys.
- EMPTY_TRANSLATION: existing entries with empty/whitespace translations.
- LOCALIZED_STRING_REFERENCE: unresolved or incomplete serialized localized-string references.
- TMP_FONT_COVERAGE: character coverage with explicitly supplied locale/font mappings.

## Important limitations
- Missing locale/font mappings report NotVerified. Do not interpret this as confirmed missing glyphs or complete coverage.
- NotVerified is not a pass. A completed scan can contain errors or unverified checks.
- This tool does not evaluate translation accuracy, grammar, text clipping, gameplay, or arbitrary Console errors.
- Dynamic/runtime-generated references may require separate runtime verification.
- A localization API operation can succeed while returning a missing-translation placeholder; successful game startup is not proof that references are valid.
- Locate can open a Prefab Stage and change selection. Scanning is designed not to save or repair your assets.

## Compatibility and distribution status
The declared minimum is Unity 6000.0. Windows EditMode regression passed on 6000.0.32f1 and 6000.5.10f1: each 179 total, 178 passed, 0 failed, 1 graphics-only test skipped in batch mode. Do not assume other versions/platforms are verified.
This source candidate was tested as a local UPM dependency. Final Asset Store packaging, installation/update/removal checks, interactive font configuration acceptance, and legal/documentation review remain release gates.

## Configure font coverage
1. In the auditor window, click **Create profile / 新建配置** and save the new asset under your project's Assets folder. Alternatively use **Assets > Create > Localization Auditor > Font Profile**.
2. Expand **Locale fonts / 语言字体列表** and add entries. Set **Locale Code** to the exact project Locale identifier (for example `en` or `zh-Hans`). Select the corresponding **TMP Font Asset**, not a TTF/OTF source font.
3. Click **Save profile / 保存配置**, then Refresh and Run Audit. Re-select the saved profile when opening a new window; the profile can be version-controlled with the project.
4. Duplicate codes or fonts without a code block scanning with an explanation. Blank rows are ignored; languages without a font remain NotVerified. Font assets must be saved and independently loadable by asset path; ambiguous sub-assets are rejected.
5. Edit profile entries through the window or Inspector; serialized edits support Unity Undo. Re-run after changing a profile: the displayed report describes the previous scan, not live edits.

Profiles declare which font to check; the tool does not prove that every runtime text object actually uses that font. Scanning does not add glyphs, save fonts or repair translations. Profile creation/saving is an explicit user operation and separate from read-only scanning.

## Samples and removal
Synthetic demonstrations must be explicitly marked as intentional defects. The third-party Metroidvania project and temporary preview scripts are not part of this package.
Remove the installed package through Package Manager. Remove separately imported optional samples only if they are no longer needed. Do not remove Unity Localization if your project uses it.
