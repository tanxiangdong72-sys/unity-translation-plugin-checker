# Task 22 Public Project Validation

**Date:** 2026-09-03

**Auditor commit:** `01a2d264c9948c0ab8e722a6ef6aefea2d3a5d13`

**Auditor Unity:** `6000.5.10f1`

## Scope

This task checks whether public Unity projects are realistic validation
targets for Unity Localization Auditor. The first pass is read-only source
inspection. No public project was opened, upgraded, or modified.

## Candidate 1: SeedCalc

**Repository:** `SeedV/SeedCalc`

**Evidence:**

- `Packages/manifest.json` declares `com.unity.localization` version `1.0.5`;
- `ProjectSettings/ProjectVersion.txt` declares Unity `2020.3.30f1c1`;
- The README states that the project supports English (`en`) and Simplified
  Chinese (`zh-CN`);
- The README explicitly states that the project uses Unity Localization;
- The repository contains `Assets/Locales/LocalizedStrings.asset`,
  `LocalizedStrings_en.asset`, and `LocalizedStrings_zh-CN.asset`;
- Addressables groups contain the shared data and both locale tables.

**Relevance:** High. This is the closest candidate to the auditor's MVP:
String Tables, two locales, Addressables, and a conventional Unity project.

**Expected audit surface:** Locale/table completeness, empty translations,
Unicode collection, TMP coverage, Addressables-backed table paths, and
serialized references if present.

## Candidate 2: Team-Capture

**Repository:** `Voltstro-Studios/Team-Capture`

**Evidence:**

- `src/Team-Capture/Packages/manifest.json` declares
  `com.unity.localization` version `1.3.2`;
- `src/Team-Capture/ProjectSettings/ProjectVersion.txt` declares Unity
  `2021.3.19f1`;
- The README lists Localization among the Unity technologies used;
- `Assets/Scripts/AddressablesAddons/CachedLocalizedString.cs` contains a
  serialized `LocalizedString`;
- UI scripts such as `MainMenu.cs` and `PauseMenu.cs` use `LocalizedString`;
- `Assets/Settings/Scenes/MainMenu.asset` contains serialized localized
  references;
- `ProjectSettings/LocalizationSettings.asset` contains a String Table
  reference.

**Relevance:** High for Scene/Prefab reference extraction and invalid
reference checks. The project also uses runtime-generated settings UI, so it
is useful for boundary coverage.

**Expected audit surface:** Serialized LocalizedString references, GUID/name
resolution, Scene and Prefab object paths, String Table references, and
unsupported runtime-generated text cases.

## Candidate 3: Daggerfall Unity

**Repository:** `Interkarma/daggerfall-unity`

**Evidence:**

- `Packages/manifest.json` declares `com.unity.localization` version `1.4.2`;
- `ProjectSettings/ProjectVersion.txt` declares Unity `2019.4.41f2`;
- `Assets/Localization/Settings/Default-Localization-Settings.asset`
  contains Unity Localization settings and a `LocalizedStringDatabase`;
- The repository contains multiple Addressables groups for String Tables;
- `Assets/Scripts/Localization/DaggerfallStringTableImporter.cs` works with
  String Table collections;
- `Assets/Scripts/Game/TextManager.cs` and `TextProvider.cs` resolve localized
  text;
- `StringTablePatcher.cs` can patch table values from CSV files at runtime or
  import time.

**Relevance:** High as a stress and boundary candidate. It uses the target
framework but also has custom patching and legacy compatibility behavior that
may produce cases outside static serialized analysis.

**Expected audit surface:** Multiple collections, Addressables references,
table completeness, custom patching boundaries, and cases that must be
classified as `NotVerified` rather than treated as ordinary serialized
references.

## Compatibility Blocker

The three projects require older Unity Editors than the current validation
environment:

| Project | Required Unity | Current environment |
|---|---|---|
| SeedCalc | `2020.3.30f1c1` | `6000.5.10f1` |
| Team-Capture | `2021.3.19f1` | `6000.5.10f1` |
| Daggerfall Unity | `2019.4.41f2` | `6000.5.10f1` |

Team-Capture also requires a .NET source-generator build and external UPM
registries. SeedCalc requires Git LFS and notes that some release assets are
not included. Daggerfall Unity contains custom import and patching behavior.

Opening these repositories directly in Unity 6 could upgrade serialized
assets or generate project changes. That would violate the read-only
validation requirement and would not be equivalent to testing with each
project's declared editor version.

## Status

**Static candidate validation:** Passed for all three candidates.

**Actual auditor scan:** Not run.

**Overall Task 22 status:** Blocked for environment compatibility, not failed
by an auditor result.

## Required Follow-Up

To complete the actual scan:

1. Obtain isolated copies of all three repositories;
2. Run each in its declared Unity Editor version;
3. Install or resolve the project's declared package dependencies;
4. Add the auditor package without saving unrelated project assets;
5. Record target count, issue count, scan duration, diagnostics, false
   positives, false negatives, and whether each issue is actionable;
6. Repeat the scan after reopening each project to check determinism.

No recall rate, false-positive rate, or real-project performance conclusion is
claimed until those scans are executed.
